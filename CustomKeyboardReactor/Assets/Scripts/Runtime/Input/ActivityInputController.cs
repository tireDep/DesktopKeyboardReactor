using System;
using UnityEngine;

namespace CustomKeyboardReactor
{
    // 전역 입력 모듈의 Unity 생명주기를 관리하는 클래스
    [DisallowMultipleComponent]
    public sealed class ActivityInputController : MonoBehaviour
    {
        private const string ControllerObjectName = "Activity Input Controller"; // 컨트롤러 오브젝트 이름

        private ActivityInputCoordinator _coordinator; // 반응 입력 조정기
        private UserSettingsRepository _userSettingsRepository; // 공용 설정 저장소
        private Predicate<ActivityInputEvent> _isMouseInputExcluded; // 자체 마우스 입력 판정 함수
        private bool _keyboardReactionEnabled = true; // 키보드 반응 활성 여부
        private bool _mouseButtonReactionEnabled = true; // 마우스 버튼 반응 활성 여부
        private bool _isConfiguring; // 설정 상태 여부

        public long TotalInputCount => _coordinator?.TotalInputCount ?? 0L; // 전체 입력 수
        public event Action<ActivityInputEvent> ActivityAccepted; // 승인된 반응 입력 전달 이벤트

        public bool KeyboardReactionEnabled // 키보드 반응 활성 여부
        {
            get => _keyboardReactionEnabled;
            set
            {
                _keyboardReactionEnabled = value;
                if (_coordinator != null)
                {
                    _coordinator.KeyboardReactionEnabled = value;
                }
            }
        }

        public bool MouseButtonReactionEnabled // 마우스 버튼 반응 활성 여부
        {
            get => _mouseButtonReactionEnabled;
            set
            {
                _mouseButtonReactionEnabled = value;
                if (_coordinator != null)
                {
                    _coordinator.MouseButtonReactionEnabled = value;
                }
            }
        }

        public bool IsConfiguring // 설정 상태 여부
        {
            get => _isConfiguring;
            set
            {
                _isConfiguring = value;
                if (_coordinator != null)
                {
                    _coordinator.IsConfiguring = value;
                }
            }
        }

        // 자체 마우스 입력 판정 함수를 연결하는 함수
        public void SetMouseInputExclusion(Predicate<ActivityInputEvent> isMouseInputExcluded)
        {
            _isMouseInputExcluded = isMouseInputExcluded;
            _coordinator?.SetMouseInputExclusion(isMouseInputExcluded);
        }

        // Windows 플레이어에서 전역 입력 컨트롤러를 생성하는 함수
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateController()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            ActivityInputController existingController = FindAnyObjectByType<ActivityInputController>(); // 기존 입력 컨트롤러
            if (existingController != null)
            {
                return;
            }

            GameObject controllerObject = new GameObject(ControllerObjectName); // 런타임 입력 컨트롤러 오브젝트
            DontDestroyOnLoad(controllerObject);
            controllerObject.AddComponent<ActivityInputController>();
#endif
        }

        // 전역 입력 공급자와 조정기를 시작하는 함수
        private void OnEnable()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_coordinator == null)
            {
                long initialTotalInputCount = LoadPersistentData(); // 저장된 전체 입력 수
                Win32KeyboardInputSource globalKeyboardInputSource =
                    new Win32KeyboardInputSource(); // 외부 포커스 키보드 입력 공급자
                UnityFocusedKeyboardInputSource focusedKeyboardInputSource =
                    new UnityFocusedKeyboardInputSource(); // 내부 포커스 키보드 입력 공급자
                FocusAwareKeyboardInputSource keyboardInputSource =
                    new FocusAwareKeyboardInputSource( // 포커스 대응 키보드 입력 공급자
                        globalKeyboardInputSource,
                        focusedKeyboardInputSource,
                        () => Application.isFocused);

                _coordinator = new ActivityInputCoordinator(
                    keyboardInputSource,
                    new Win32MouseButtonInputSource(),
                    _isMouseInputExcluded,
                    initialTotalInputCount)
                {
                    KeyboardReactionEnabled = _keyboardReactionEnabled,
                    MouseButtonReactionEnabled = _mouseButtonReactionEnabled,
                    IsConfiguring = _isConfiguring,
                };
                _coordinator.ActivityAccepted += HandleActivityAccepted;
            }

            if (!_coordinator.TryStart())
            {
                Debug.LogError("Failed to start one or more global input hooks.");
            }
#endif
        }

        // 전역 입력 큐를 Unity 메인 스레드에서 처리하는 함수
        private void Update()
        {
            _coordinator?.ProcessPendingInputs();
        }

        // 전역 입력 훅과 조정기를 정리하는 함수
        private void OnDisable()
        {
            TrySaveTotalInputCount();
            TryReleaseCoordinator();
        }

        // 오브젝트 파괴 시 남은 전역 입력 훅 정리를 다시 시도하는 함수
        private void OnDestroy()
        {
            TrySaveTotalInputCount();
            TryReleaseCoordinator();
        }

        // 영구 저장 데이터와 공용 설정을 불러오는 함수
        private long LoadPersistentData()
        {
            try
            {
                string dataRootPath = AppDataPathProvider.PrepareCurrentDataRootPath(); // 앱 데이터 루트 경로
                PresetAssetStore presetAssetStore = new PresetAssetStore( // 프리셋 이미지 저장소
                    dataRootPath);
                AppDataStore appDataStore = new AppDataStore( // 앱 데이터 저장소
                    dataRootPath,
                    presetAssetStore);
                _userSettingsRepository = new UserSettingsRepository(appDataStore);
                GlobalSettingsData settings = _userSettingsRepository.GetSettings(); // 저장된 공용 설정
                _keyboardReactionEnabled = settings.KeyboardReactionEnabled;
                _mouseButtonReactionEnabled = settings.MouseReactionEnabled;
                return _userSettingsRepository.GetTotalInputCount();
            }
            catch (Exception exception)
            {
                _userSettingsRepository = null;
                Debug.LogError($"Failed to load persistent reactor data: {exception.Message}");
                return 0L;
            }
        }

        // 현재 전체 입력 수를 영구 저장 데이터에 반영하는 함수
        private void TrySaveTotalInputCount()
        {
            if (_coordinator == null || _userSettingsRepository == null)
            {
                return;
            }

            try
            {
                _userSettingsRepository.SaveTotalInputCount(_coordinator.TotalInputCount);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to save total input count: {exception.Message}");
            }
        }

        // 조정기가 승인한 반응 입력을 런타임 구독자에게 전달하는 함수
        private void HandleActivityAccepted(ActivityInputEvent inputEvent)
        {
            ActivityAccepted?.Invoke(inputEvent);
        }

        // 전역 입력 훅을 정리하고 성공한 조정기 참조만 해제하는 함수
        private void TryReleaseCoordinator()
        {
            if (_coordinator == null)
            {
                return;
            }

            try
            {
                _coordinator.Dispose();
                _coordinator.ActivityAccepted -= HandleActivityAccepted;
                _coordinator = null;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to release global input hooks: {exception.Message}");
            }
        }
    }
}
