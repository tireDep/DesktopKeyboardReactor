using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CustomKeyboardReactor
{
    // 캐릭터 픽셀 상호작용과 위치 저장, 우클릭 명령을 조정하는 클래스
    [DisallowMultipleComponent]
    public sealed class CharacterInteractionController : MonoBehaviour
    {
        private const int MonitorTransitionMaximumFrames = 60; // 모니터 전환 최대 대기 프레임 수
        private const int WindowBoundsReapplyFrame = 3; // 창 영역 재적용 프레임

        private readonly PointerHitTester _pointerHitTester = new PointerHitTester(); // 픽셀 상호작용 판정기
        private readonly WindowsMonitorService _monitorService = new WindowsMonitorService(); // 모니터 작업 영역 서비스
        private readonly MonitorTransitionLayout _monitorTransitionLayout = new MonitorTransitionLayout(); // 모니터 전환 배치 계산기

        private CharacterPresenter _presenter; // 캐릭터 표시기
        private GlobalSettingsData _settings; // 공용 설정
        private UserSettingsRepository _settingsRepository; // 공용 설정 저장소
        private OverlayWindowController _overlayWindowController; // 오버레이 창 컨트롤러
        private OverlayContextMenuController _contextMenu; // 우클릭 메뉴
        private Action _openSettings; // 설정 상태 진입 함수
        private Action _closeSettings; // 설정 상태 종료 함수
        private Coroutine _monitorTransitionCoroutine; // 모니터 전환 처리 코루틴
        private bool _monitorApplied; // 모니터 작업 영역 적용 여부
        private bool _isDragging; // 캐릭터 드래그 진행 여부
        private Vector2 _dragAnchorOffset; // 포인터와 캐릭터 기준점 간격

        // 상호작용에 필요한 표시기와 저장 계층을 연결하는 함수
        public void Initialize(
            CharacterPresenter presenter,
            GlobalSettingsData settings,
            UserSettingsRepository settingsRepository,
            Action openSettings,
            Action closeSettings)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settingsRepository = settingsRepository ??
                                  throw new ArgumentNullException(nameof(settingsRepository));
            _openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
            _closeSettings = closeSettings ?? throw new ArgumentNullException(nameof(closeSettings));

            _contextMenu?.Dispose();
            _contextMenu = new OverlayContextMenuController(transform);
            _presenter.SetNormalizedAnchorPosition(_settings.NormalizedAnchorPosition);
            TryConnectOverlayWindow();
        }

        // 포인터 입력과 모니터 준비 상태를 매 프레임 처리하는 함수
        private void Update()
        {
            if (_presenter == null || _settings == null)
            {
                return;
            }

            TryConnectOverlayWindow();
            if (_monitorTransitionCoroutine == null && !_monitorApplied)
            {
                StartSelectedMonitorTransition();
            }

            Mouse mouse = Mouse.current; // 현재 Unity 마우스 장치
            if (mouse == null || !TryGetPointerPosition(out Vector2 pointerPosition))
            {
                return;
            }

            if (_contextMenu != null && _contextMenu.IsOpen)
            {
                if (mouse.leftButton.wasPressedThisFrame &&
                    _contextMenu.TryGetCommand(
                        pointerPosition,
                        out OverlayContextMenuController.Command command))
                {
                    ExecuteMenuCommand(command);
                }

                return;
            }

            if (mouse.rightButton.wasPressedThisFrame && IsCharacterInteractive(pointerPosition))
            {
                ShowContextMenu(pointerPosition);
                return;
            }

            if (!_settings.PositionLocked &&
                mouse.leftButton.wasPressedThisFrame &&
                IsCharacterInteractive(pointerPosition))
            {
                _isDragging = true;
                _dragAnchorOffset = _presenter.ScreenAnchorPosition - pointerPosition;
            }

            if (!_isDragging)
            {
                return;
            }

            if (mouse.leftButton.isPressed)
            {
                _presenter.SetScreenAnchorPosition(pointerPosition + _dragAnchorOffset);
                return;
            }

            _isDragging = false;
            _settings.NormalizedAnchorPosition = _presenter.NormalizedAnchorPosition;
            SaveSettings();
        }

        // 전역 입력이 캐릭터 또는 메뉴 자체 조작인지 판정하는 함수
        public bool ShouldExcludeMouseInput(ActivityInputEvent inputEvent)
        {
            bool supportedButton = inputEvent.Button == ActivityInputEvent.MouseButton.Left ||
                                   inputEvent.Button == ActivityInputEvent.MouseButton.Right; // 자체 조작 버튼 여부
            if (!supportedButton ||
                !inputEvent.HasScreenPosition ||
                _overlayWindowController == null ||
                !_overlayWindowController.TryConvertScreenToUnityPoint(
                    inputEvent.ScreenX,
                    inputEvent.ScreenY,
                    out Vector2 screenPosition))
            {
                return false;
            }

            return IsPointerInteractive(screenPosition);
        }

        // 오버레이 컨트롤러를 찾고 상호작용 판정 함수를 연결하는 함수
        private void TryConnectOverlayWindow()
        {
            if (_overlayWindowController != null)
            {
                return;
            }

            OverlayWindowController controller = FindAnyObjectByType<OverlayWindowController>(); // 검색된 오버레이 컨트롤러
            if (controller == null)
            {
                return;
            }

            _overlayWindowController = controller;
            _overlayWindowController.SetPointerInteractionProbe(IsPointerInteractive);
            _overlayWindowController.DisplaysUpdated += HandleDisplaysUpdated;
        }

        // 저장 장치 또는 주 모니터의 작업 영역을 오버레이 창에 적용하는 함수
        private void TryApplySelectedMonitor()
        {
            if (_monitorApplied ||
                _overlayWindowController == null ||
                !_overlayWindowController.IsInitialized)
            {
                return;
            }

            IReadOnlyList<WindowsMonitorService.MonitorWorkArea> monitors =
                _monitorService.GetMonitors(); // 현재 모니터 목록
            if (monitors.Count == 0)
            {
                return;
            }

            WindowsMonitorService.MonitorWorkArea selectedMonitor =
                WindowsMonitorService.Resolve( // 저장 장치 또는 주 모니터
                    _settings.MonitorDeviceId,
                    monitors);
            if (!_overlayWindowController.SetWindowBounds(selectedMonitor.WorkArea))
            {
                return;
            }

            bool monitorChanged = !string.Equals( // 저장 장치 보정 여부
                _settings.MonitorDeviceId,
                selectedMonitor.DeviceId,
                StringComparison.OrdinalIgnoreCase);
            _settings.MonitorDeviceId = selectedMonitor.DeviceId;
            _overlayWindowController.SetAlwaysOnTop(_settings.AlwaysOnTop);
            _presenter.SetNormalizedAnchorPosition(_settings.NormalizedAnchorPosition);
            _monitorApplied = true;
            if (monitorChanged)
            {
                SaveSettings();
            }
        }

        // 저장된 모니터로 Unity 주 창 이동과 작업 영역 적용을 시작하는 함수
        private void StartSelectedMonitorTransition()
        {
            if (_overlayWindowController == null ||
                !_overlayWindowController.IsInitialized)
            {
                return;
            }

            IReadOnlyList<WindowsMonitorService.MonitorWorkArea> monitors =
                _monitorService.GetMonitors(); // 현재 모니터 목록
            if (monitors.Count == 0)
            {
                return;
            }

            WindowsMonitorService.MonitorWorkArea selectedMonitor =
                WindowsMonitorService.Resolve( // 저장 장치 또는 주 모니터
                    _settings.MonitorDeviceId,
                    monitors);
            bool monitorChanged = !string.Equals( // 저장 장치 보정 여부
                _settings.MonitorDeviceId,
                selectedMonitor.DeviceId,
                StringComparison.OrdinalIgnoreCase);
            _settings.MonitorDeviceId = selectedMonitor.DeviceId;
            int monitorIndex = FindCurrentMonitorIndex(monitors); // 적용할 모니터 인덱스
            _monitorTransitionLayout.Begin(
                _contextMenu.ScreenPosition,
                _presenter.ScreenAnchorPosition,
                selectedMonitor.WorkArea);
            _monitorTransitionCoroutine = StartCoroutine(ApplyMonitorTransition(
                selectedMonitor.WorkArea,
                monitorIndex,
                monitors.Count,
                false,
                selectedMonitor.DeviceId,
                monitorIndex));

            if (monitorChanged)
            {
                SaveSettings();
            }
        }

        // 우클릭 메뉴를 열고 반응 입력을 설정 상태로 전환하는 함수
        private void ShowContextMenu(Vector2 pointerPosition)
        {
            IReadOnlyList<WindowsMonitorService.MonitorWorkArea> monitors =
                _monitorService.GetMonitors(); // 메뉴 표시용 모니터 목록
            int monitorIndex = FindCurrentMonitorIndex(monitors); // 현재 모니터 인덱스
            _contextMenu.Show(
                pointerPosition,
                _settings.PositionLocked,
                monitorIndex,
                monitors.Count);
            _openSettings();
        }

        // 선택한 우클릭 메뉴 명령을 실행하는 함수
        private void ExecuteMenuCommand(OverlayContextMenuController.Command command)
        {
            switch (command)
            {
                case OverlayContextMenuController.Command.TogglePositionLock:
                {
                    _settings.PositionLocked = !_settings.PositionLocked;
                    IReadOnlyList<WindowsMonitorService.MonitorWorkArea> monitors =
                        _monitorService.GetMonitors(); // 메뉴 갱신용 모니터 목록
                    _contextMenu.Refresh(
                        _settings.PositionLocked,
                        FindCurrentMonitorIndex(monitors),
                        monitors.Count);
                    SaveSettings();
                    break;
                }
                case OverlayContextMenuController.Command.SelectNextMonitor:
                    SelectNextMonitor();
                    break;
                case OverlayContextMenuController.Command.Close:
                    CloseContextMenu();
                    break;
                case OverlayContextMenuController.Command.Exit:
                    Application.Quit();
                    break;
            }
        }

        // 다음 연결 모니터를 선택하여 창과 저장 설정에 적용하는 함수
        private void SelectNextMonitor()
        {
            IReadOnlyList<WindowsMonitorService.MonitorWorkArea> monitors =
                _monitorService.GetMonitors(); // 전환 가능한 모니터 목록
            if (monitors.Count == 0)
            {
                return;
            }

            int currentIndex = FindCurrentMonitorIndex(monitors); // 현재 모니터 인덱스
            int nextIndex = (currentIndex + 1) % monitors.Count; // 다음 모니터 인덱스
            bool restoreMenu = _contextMenu.IsOpen; // 전환 후 메뉴 복원 여부
            string fallbackMonitorDeviceId = monitors[currentIndex].DeviceId; // 전환 실패 시 복구 모니터 ID
            WindowsMonitorService.MonitorWorkArea nextMonitor = monitors[nextIndex]; // 다음 모니터 작업 영역
            // 모니터 전환 전 캐릭터 기준 메뉴 상대 위치 보관
            _monitorTransitionLayout.Begin(
                _contextMenu.ScreenPosition,
                _presenter.ScreenAnchorPosition,
                nextMonitor.WorkArea);
            _settings.MonitorDeviceId = nextMonitor.DeviceId;

            if (_monitorTransitionCoroutine != null)
            {
                StopCoroutine(_monitorTransitionCoroutine);
            }

            _monitorTransitionCoroutine = StartCoroutine(ApplyMonitorTransition(
                nextMonitor.WorkArea,
                nextIndex,
                monitors.Count,
                restoreMenu,
                fallbackMonitorDeviceId,
                currentIndex));
            SaveSettings();
        }

        // 새 모니터의 화면 크기가 안정된 뒤 캐릭터와 메뉴를 다시 배치하는 함수
        private IEnumerator ApplyMonitorTransition(
            RectInt targetWorkArea,
            int monitorIndex,
            int monitorCount,
            bool restoreMenu,
            string fallbackMonitorDeviceId,
            int fallbackMonitorIndex)
        {
            _contextMenu.Hide();
            _monitorApplied = false;

            // Unity 디스플레이와 렌더링 상태 동기화
            AsyncOperation moveOperation =
                _overlayWindowController.MoveMainWindowToDisplay(monitorIndex); // Unity 창 이동 작업
            int moveFrameCount = 0; // Unity 창 이동 대기 프레임 수
            while (moveOperation != null &&
                   !moveOperation.isDone &&
                   moveFrameCount < MonitorTransitionMaximumFrames)
            {
                moveFrameCount++;
                yield return null;
            }

            int applyFrameCount = 0; // 창 영역 적용 대기 프레임 수
            while (!_monitorApplied && applyFrameCount < MonitorTransitionMaximumFrames)
            {
                TryApplySelectedMonitor();
                if (_monitorApplied)
                {
                    break;
                }

                applyFrameCount++;
                yield return null;
            }

            if (!_monitorApplied)
            {
                _settings.MonitorDeviceId = fallbackMonitorDeviceId;
                TryApplySelectedMonitor();
                Canvas.ForceUpdateCanvases();
                if (restoreMenu)
                {
                    _contextMenu.Show(
                        _monitorTransitionLayout.CalculateMenuScreenPosition(
                            _presenter.ScreenAnchorPosition),
                        _settings.PositionLocked,
                        fallbackMonitorIndex,
                        monitorCount);
                }

                SaveSettings();
                _monitorTransitionCoroutine = null;
                yield break;
            }

            // 대상 화면 크기 연속 일치 대기
            int resizeFrameCount = 0; // 화면 크기 안정 대기 프레임 수
            bool viewportReady = false; // 대상 화면 크기 안정 여부
            while (_monitorApplied &&
                   !viewportReady &&
                   resizeFrameCount < MonitorTransitionMaximumFrames)
            {
                yield return null;
                resizeFrameCount++;
                if (resizeFrameCount == WindowBoundsReapplyFrame)
                {
                    _overlayWindowController.SetWindowBounds(targetWorkArea);
                }

                viewportReady = _monitorTransitionLayout.IsViewportReady(
                    new Vector2Int(Screen.width, Screen.height));
            }

            // 안정된 Canvas 기준 캐릭터와 창 속성 재적용
            Canvas.ForceUpdateCanvases();
            _presenter.SetNormalizedAnchorPosition(_settings.NormalizedAnchorPosition);
            Canvas.ForceUpdateCanvases();
            _overlayWindowController.RefreshWindowProperties();
            _monitorApplied = true;
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (restoreMenu)
            {
                _contextMenu.Show(
                    _monitorTransitionLayout.CalculateMenuScreenPosition(
                        _presenter.ScreenAnchorPosition),
                    _settings.PositionLocked,
                    monitorIndex,
                    monitorCount);
            }

            _monitorTransitionCoroutine = null;
        }

        // 현재 저장된 장치 ID의 모니터 인덱스를 반환하는 함수
        private int FindCurrentMonitorIndex(
            IReadOnlyList<WindowsMonitorService.MonitorWorkArea> monitors)
        {
            for (int index = 0; index < monitors.Count; index++)
            {
                if (string.Equals(
                        monitors[index].DeviceId,
                        _settings.MonitorDeviceId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return 0;
        }

        // 현재 포인터의 Unity 화면 좌표를 반환하는 함수
        private bool TryGetPointerPosition(out Vector2 pointerPosition)
        {
            if (_overlayWindowController != null &&
                _overlayWindowController.TryGetCursorUnityScreenPosition(out pointerPosition))
            {
                return true;
            }

#if UNITY_EDITOR
            pointerPosition = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            return Mouse.current != null;
#else
            pointerPosition = Vector2.zero;
            return false;
#endif
        }

        // 포인터가 캐릭터의 불투명 픽셀인지 반환하는 함수
        private bool IsCharacterInteractive(Vector2 screenPosition)
        {
            return _pointerHitTester.IsInteractive(
                screenPosition,
                _presenter.CharacterRect,
                _presenter.CurrentTexture);
        }

        // 포인터가 캐릭터 또는 열린 메뉴의 상호작용 영역인지 반환하는 함수
        private bool IsPointerInteractive(Vector2 screenPosition)
        {
            return _pointerHitTester.IsInteractive(
                screenPosition,
                _presenter?.CharacterRect,
                _presenter?.CurrentTexture,
                _contextMenu?.PanelRect);
        }

        // 우클릭 메뉴와 설정 상태를 닫는 함수
        private void CloseContextMenu()
        {
            _contextMenu?.Hide();
            _closeSettings();
        }

        // 현재 공용 설정을 저장하고 실패를 로그로 알리는 함수
        private void SaveSettings()
        {
            try
            {
                _settingsRepository.SaveSettings(_settings);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to save character interaction settings: {exception.Message}");
            }
        }

        // 디스플레이 구성 변경 시 저장 모니터를 다시 적용하도록 표시하는 함수
        private void HandleDisplaysUpdated()
        {
            _monitorApplied = false;
        }

        // 오버레이 연결과 메뉴 자원을 정리하는 함수
        private void OnDisable()
        {
            if (_monitorTransitionCoroutine != null)
            {
                StopCoroutine(_monitorTransitionCoroutine);
                _monitorTransitionCoroutine = null;
            }

            if (_overlayWindowController != null)
            {
                _overlayWindowController.SetPointerInteractionProbe(null);
                _overlayWindowController.DisplaysUpdated -= HandleDisplaysUpdated;
                _overlayWindowController = null;
            }

            _contextMenu?.Dispose();
            _contextMenu = null;
            _isDragging = false;
        }
    }
}
