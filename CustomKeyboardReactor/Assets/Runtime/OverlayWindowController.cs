using System.Collections;
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
        [SerializeField] private bool _showVerificationMarker = true; // 임시 검증 이미지 표시 여부
        [SerializeField] private Vector2 _verificationMarkerSize = new Vector2(2.5f, 2.5f); // 임시 검증 이미지 크기
        [SerializeField] private Color _verificationMarkerColor = new Color(0.1f, 0.75f, 0.95f, 1f); // 임시 검증 이미지 색상

        private OverlayWindowService _windowService; // 오버레이 창 서비스
        private OverlayVerificationMarker _verificationMarker; // 임시 검증 이미지 표시기
        private Coroutine _initializationCoroutine; // 창 초기화 코루틴
        private Coroutine _reapplyCoroutine; // 창 속성 재적용 코루틴

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
            if (_windowService == null || !_windowService.IsInitialized || _verificationMarker == null)
            {
                return;
            }

            int cursorX; // 커서 클라이언트 가로 좌표
            int cursorY; // 커서 클라이언트 세로 좌표
            bool cursorPositionRead = _windowService.TryGetCursorClientPosition( // 커서 클라이언트 좌표 조회 결과
                out cursorX,
                out cursorY);
            if (!cursorPositionRead)
            {
                return;
            }

            bool clickThrough = !_verificationMarker.ContainsClientPoint(cursorX, cursorY); // 적용할 클릭 통과 상태
            _windowService.SetClickThrough(clickThrough);
        }

        // 포커스 복귀 시 창 속성을 다시 적용하는 함수
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                RequestWindowPropertyReapply();
            }
        }

        // 디스플레이 변경 시 창 속성을 다시 적용하는 함수
        private void HandleDisplaysUpdated()
        {
            RequestWindowPropertyReapply();
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
