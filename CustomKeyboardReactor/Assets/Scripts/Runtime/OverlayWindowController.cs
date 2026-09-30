using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CustomKeyboardReactor
{
    // 오버레이 창 서비스의 Unity 생명주기를 관리하는 클래스
    [DisallowMultipleComponent]
    public sealed class OverlayWindowController : MonoBehaviour
    {
        private const string ControllerObjectName = "Overlay Window Controller"; // 컨트롤러 오브젝트 이름

        [SerializeField] private bool _alwaysOnTop = true; // 항상 위 초기 상태
        [SerializeField] private bool _initialClickThrough = true; // 클릭 통과 초기 상태
        [SerializeField, Min(0.01f)] private float _initializationRetrySeconds = 0.1f; // 창 초기화 재시도 간격
        [SerializeField] private bool _showVerificationMarker; // 임시 검증 이미지 표시 여부
        [SerializeField] private Vector2 _verificationMarkerSize = new Vector2(2.5f, 2.5f); // 임시 검증 이미지 크기
        [SerializeField] private Color _verificationMarkerColor = new Color(0.1f, 0.75f, 0.95f, 1f); // 임시 검증 이미지 색상

        private OverlayWindowService _windowService; // 오버레이 창 서비스
        private OverlayVerificationMarker _verificationMarker; // 임시 검증 이미지 표시기
        private readonly List<DisplayInfo> _displayLayout = new List<DisplayInfo>(); // Unity 디스플레이 목록
        private Func<Vector2, bool> _pointerInteractionProbe; // 포인터 상호작용 판정 함수
        private Coroutine _initializationCoroutine; // 창 초기화 코루틴
        private Coroutine _reapplyCoroutine; // 창 속성 재적용 코루틴

        public bool IsInitialized => _windowService != null && _windowService.IsInitialized; // 창 초기화 완료 여부
        public event Action DisplaysUpdated; // 디스플레이 구성 변경 이벤트

        // 씬 로드 후 오버레이 컨트롤러를 생성하는 함수
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateController()
        {
            OverlayWindowController existingController = FindAnyObjectByType<OverlayWindowController>(); // 기존 오버레이 컨트롤러
            if (existingController != null)
            {
                return;
            }

            GameObject controllerObject = new GameObject(ControllerObjectName); // 런타임 컨트롤러 오브젝트
            DontDestroyOnLoad(controllerObject);
            controllerObject.AddComponent<OverlayWindowController>();
        }

        // 창 서비스와 임시 검증 이미지를 준비하는 함수
        private void OnEnable()
        {
            OverlayPerformanceSettings.Apply();
            _windowService = new OverlayWindowService();
            Display.onDisplaysUpdated += HandleDisplaysUpdated;

            if (_showVerificationMarker)
            {
                _verificationMarker = new OverlayVerificationMarker(
                    transform,
                    Camera.main,
                    _verificationMarkerSize,
                    _verificationMarkerColor);
            }

            _initializationCoroutine = StartCoroutine(InitializeWindow());
        }

        // 커서 위치에 맞춰 클릭 통과 상태를 갱신하는 함수
        private void Update()
        {
            if (!TryGetCursorClientPosition(out int cursorX, out int cursorY))
            {
                return;
            }

            Vector2 screenPosition = ConvertClientToUnityScreenPoint(cursorX, cursorY); // Unity 화면 좌표
            bool isInteractive = _pointerInteractionProbe?.Invoke(screenPosition) ?? false; // 런타임 상호작용 여부
            if (_verificationMarker != null)
            {
                isInteractive |= _verificationMarker.ContainsClientPoint(cursorX, cursorY);
            }

            _windowService.SetClickThrough(!isInteractive);
        }

        // 포인터 상호작용 영역 판정 함수를 연결하는 함수
        public void SetPointerInteractionProbe(Func<Vector2, bool> pointerInteractionProbe)
        {
            _pointerInteractionProbe = pointerInteractionProbe;
        }

        // 현재 커서의 Unity 화면 좌표를 반환하는 함수
        public bool TryGetCursorUnityScreenPosition(out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;
            if (!TryGetCursorClientPosition(out int cursorX, out int cursorY))
            {
                return false;
            }

            screenPosition = ConvertClientToUnityScreenPoint(cursorX, cursorY);
            return true;
        }

        // Windows 화면 좌표를 Unity 화면 좌표로 변환하는 함수
        public bool TryConvertScreenToUnityPoint(
            int screenX,
            int screenY,
            out Vector2 unityScreenPosition)
        {
            unityScreenPosition = Vector2.zero;
            if (_windowService == null ||
                !_windowService.TryScreenToClientPosition(
                    screenX,
                    screenY,
                    out int clientX,
                    out int clientY))
            {
                return false;
            }

            unityScreenPosition = ConvertClientToUnityScreenPoint(clientX, clientY);
            return true;
        }

        // 오버레이 창을 지정한 Windows 작업 영역으로 이동하는 함수
        public bool SetWindowBounds(RectInt workArea)
        {
            return _windowService != null &&
                   _windowService.SetWindowBounds(
                       workArea.x,
                       workArea.y,
                       workArea.width,
                       workArea.height);
        }

        // Unity 렌더링 경로를 통해 주 창을 대상 디스플레이로 이동하는 함수
        public AsyncOperation MoveMainWindowToDisplay(int displayIndex)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _displayLayout.Clear();
            Screen.GetDisplayLayout(_displayLayout);
            if (displayIndex < 0 || displayIndex >= _displayLayout.Count)
            {
                return null;
            }

            DisplayInfo targetDisplay = _displayLayout[displayIndex]; // 대상 Unity 디스플레이
            Vector2Int targetPosition = targetDisplay.workArea.position; // 대상 작업 영역 상대 위치
            return Screen.MoveMainWindowTo(targetDisplay, targetPosition);
#else
            return null;
#endif
        }

        // 항상 위 설정을 현재 창에 적용하는 함수
        public bool SetAlwaysOnTop(bool alwaysOnTop)
        {
            _alwaysOnTop = alwaysOnTop;
            return _windowService != null && _windowService.SetAlwaysOnTop(alwaysOnTop);
        }

        // 현재 오버레이 창 속성 재적용을 요청하는 함수
        public void RefreshWindowProperties()
        {
            RequestWindowPropertyReapply();
        }

        // 포커스 복귀 시 창 속성을 다시 적용하는 함수
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                RequestWindowPropertyReapply();
            }
        }

        // 디스플레이 변경 시 창 속성과 캐릭터 위치 갱신을 요청하는 함수
        private void HandleDisplaysUpdated()
        {
            RequestWindowPropertyReapply();
            DisplaysUpdated?.Invoke();
        }

        // 창 핸들이 준비될 때까지 초기화를 재시도하는 함수
        private IEnumerator InitializeWindow()
        {
            WaitForSecondsRealtime retryDelay = new WaitForSecondsRealtime(_initializationRetrySeconds); // 초기화 재시도 대기
            while (_windowService != null && !_windowService.TryInitialize(_alwaysOnTop, _initialClickThrough))
            {
                yield return retryDelay;
            }

            _initializationCoroutine = null;
        }

        // 현재 커서의 창 클라이언트 좌표를 반환하는 함수
        private bool TryGetCursorClientPosition(out int cursorX, out int cursorY)
        {
            cursorX = 0;
            cursorY = 0;
            return _windowService != null &&
                   _windowService.IsInitialized &&
                   _windowService.TryGetCursorClientPosition(out cursorX, out cursorY);
        }

        // 왼쪽 위 기준 클라이언트 좌표를 왼쪽 아래 기준 Unity 좌표로 변환하는 함수
        private static Vector2 ConvertClientToUnityScreenPoint(int clientX, int clientY)
        {
            return new Vector2(clientX, Screen.height - 1 - clientY);
        }

        // 창 속성 재적용을 요청하는 함수
        private void RequestWindowPropertyReapply()
        {
            if (_windowService == null || !_windowService.IsInitialized || _reapplyCoroutine != null)
            {
                return;
            }

            if (!_windowService.ReapplyWindowProperties())
            {
                _reapplyCoroutine = StartCoroutine(ReapplyWindowProperties());
            }
        }

        // 창 속성이 적용될 때까지 일정 간격으로 재시도하는 함수
        private IEnumerator ReapplyWindowProperties()
        {
            WaitForSecondsRealtime retryDelay = new WaitForSecondsRealtime(_initializationRetrySeconds); // 재적용 재시도 대기
            bool propertiesApplied = false; // 창 속성 재적용 완료 여부
            while (_windowService != null && _windowService.IsInitialized && !propertiesApplied)
            {
                yield return retryDelay;
                propertiesApplied = _windowService.ReapplyWindowProperties();
            }

            _reapplyCoroutine = null;
        }

        // 창 서비스와 임시 검증 이미지 자원을 정리하는 함수
        private void OnDisable()
        {
            Display.onDisplaysUpdated -= HandleDisplaysUpdated;
            _pointerInteractionProbe = null;
            DisplaysUpdated = null;

            if (_initializationCoroutine != null)
            {
                StopCoroutine(_initializationCoroutine);
                _initializationCoroutine = null;
            }

            if (_reapplyCoroutine != null)
            {
                StopCoroutine(_reapplyCoroutine);
                _reapplyCoroutine = null;
            }

            if (_windowService != null && !_windowService.TryDispose())
            {
                Debug.LogError("Failed to restore the original overlay window properties.");
            }

            _windowService = null;

            _verificationMarker?.Dispose();
            _verificationMarker = null;
        }
    }
}
