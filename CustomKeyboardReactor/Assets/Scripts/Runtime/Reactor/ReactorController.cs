using System;
using System.Collections;
using UnityEngine;

namespace CustomKeyboardReactor
{
    // 저장된 프리셋과 입력, 상태 머신, 캐릭터 표시를 연결하는 클래스
    [DisallowMultipleComponent]
    public sealed class ReactorController : MonoBehaviour
    {
        private const string ControllerObjectName = "Reactor Controller"; // 컨트롤러 오브젝트 이름
        private const float RuntimePollIntervalSeconds = 0.1f; // 런타임 상태 확인 간격

        private PresetAssetStore _presetAssetStore; // 프리셋 이미지 저장소
        private PresetRepository _presetRepository; // 프리셋 저장소
        private UserSettingsRepository _userSettingsRepository; // 공용 설정 저장소
        private ActivityInputController _activityInputController; // 반응 입력 컨트롤러
        private ReactorStateMachine _stateMachine; // 반응 상태 머신
        private CharacterPresenter _characterPresenter; // 캐릭터 표시기
        private CharacterInteractionController _characterInteractionController; // 캐릭터 상호작용 컨트롤러
        private PresetData _activePreset; // 활성 프리셋
        private GlobalSettingsData _globalSettings; // 공용 설정
        private Coroutine _runtimeCoroutine; // 런타임 상태 처리 코루틴

        public ReactorState CurrentState => _stateMachine?.CurrentState ?? ReactorState.Reacting; // 현재 반응 상태

        // 씬 로드 후 반응 컨트롤러를 생성하는 함수
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateController()
        {
            ReactorController existingController = FindAnyObjectByType<ReactorController>(); // 기존 반응 컨트롤러
            if (existingController != null)
            {
                return;
            }

            GameObject controllerObject = new GameObject(ControllerObjectName); // 런타임 반응 컨트롤러 오브젝트
            DontDestroyOnLoad(controllerObject);
            controllerObject.AddComponent<ReactorController>();
        }

        // 저장 데이터와 캐릭터 표시를 준비하는 함수
        private void OnEnable()
        {
            try
            {
                InitializeReactor();
                TryConnectInputController();
                _runtimeCoroutine = StartCoroutine(ProcessRuntimeState());
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to initialize reactor: {exception.Message}");
                enabled = false;
            }
        }

        // 설정 화면 진입 상태를 입력 계층과 상태 머신에 반영하는 함수
        public void OpenSettings()
        {
            _stateMachine?.OpenSettings();
            if (_activityInputController != null)
            {
                _activityInputController.IsConfiguring = true;
            }
        }

        // 설정 화면을 닫고 현재 프리셋의 첫 일반 이미지로 복귀하는 함수
        public void CloseSettings()
        {
            if (_stateMachine == null)
            {
                return;
            }

            _stateMachine.CloseSettings();
            if (_activityInputController != null)
            {
                _activityInputController.IsConfiguring = false;
            }

            PresentCurrentImage();
        }

        // 새 프리셋을 저장 상태와 런타임 표시에 적용하는 함수
        public void ApplyPreset(PresetData activePreset)
        {
            if (activePreset == null)
            {
                throw new ArgumentNullException(nameof(activePreset));
            }

            _activePreset = activePreset;
            _stateMachine.ApplyPreset(activePreset);
            _characterPresenter.ClearCache();
            _characterPresenter.SetActivePreset(_activePreset);
            if (_activityInputController != null)
            {
                _activityInputController.IsConfiguring = false;
            }

            PresentCurrentImage();
        }

        // 저장소와 상태 머신, 캐릭터 표시기를 생성하는 함수
        private void InitializeReactor()
        {
            if (_stateMachine != null)
            {
                return;
            }

            string dataRootPath = AppDataPathProvider.PrepareCurrentDataRootPath(); // 앱 데이터 루트 경로
            _presetAssetStore = new PresetAssetStore(dataRootPath);
            AppDataStore appDataStore = new AppDataStore( // 앱 데이터 저장소
                dataRootPath,
                _presetAssetStore);
            _presetRepository = new PresetRepository(appDataStore, _presetAssetStore);
            _userSettingsRepository = new UserSettingsRepository(appDataStore);
            _activePreset = _presetRepository.GetActive();
            _globalSettings = _userSettingsRepository.GetSettings();
            _stateMachine = new ReactorStateMachine(
                _activePreset,
                _globalSettings.IdleEnabled,
                _globalSettings.IdleTimeoutSeconds);

            _characterPresenter = gameObject.GetComponent<CharacterPresenter>() ??
                                  gameObject.AddComponent<CharacterPresenter>();
            _characterPresenter.Initialize(_presetAssetStore);
            _characterPresenter.SetActivePreset(_activePreset);
            _characterInteractionController = gameObject.GetComponent<CharacterInteractionController>() ??
                                              gameObject.AddComponent<CharacterInteractionController>();
            _characterInteractionController.Initialize(
                _characterPresenter,
                _globalSettings,
                _userSettingsRepository,
                OpenSettings,
                CloseSettings);
            PresentCurrentImage();
        }

        // 입력 컨트롤러가 준비되면 승인 입력 이벤트를 연결하는 함수
        private void TryConnectInputController()
        {
            if (_activityInputController != null)
            {
                return;
            }

            ActivityInputController inputController = FindAnyObjectByType<ActivityInputController>(); // 검색된 입력 컨트롤러
            if (inputController == null)
            {
                return;
            }

            _activityInputController = inputController;
            _activityInputController.ActivityAccepted += HandleActivityAccepted;
            _activityInputController.SetMouseInputExclusion(
                _characterInteractionController.ShouldExcludeMouseInput);
            _activityInputController.IsConfiguring = _stateMachine.CurrentState == ReactorState.Configuring;
        }

        // 대기 타이머와 지연 생성된 입력 컨트롤러를 일정 간격으로 처리하는 함수
        private IEnumerator ProcessRuntimeState()
        {
            WaitForSecondsRealtime pollDelay = new WaitForSecondsRealtime(RuntimePollIntervalSeconds); // 런타임 확인 대기
            double previousTime = Time.realtimeSinceStartupAsDouble; // 이전 확인 시각
            while (true)
            {
                yield return pollDelay;
                TryConnectInputController();

                double currentTime = Time.realtimeSinceStartupAsDouble; // 현재 확인 시각
                double elapsedSeconds = currentTime - previousTime; // 확인 사이 경과 시간
                previousTime = currentTime;
                if (_stateMachine.AdvanceTime(elapsedSeconds))
                {
                    PresentCurrentImage();
                }
            }
        }

        // 승인된 반응 입력을 상태 머신에 전달하는 함수
        private void HandleActivityAccepted(ActivityInputEvent inputEvent)
        {
            if (_stateMachine.HandleActivity())
            {
                PresentCurrentImage();
            }
        }

        // 상태 머신의 현재 이미지를 캐릭터 표시기에 반영하는 함수
        private void PresentCurrentImage()
        {
            _characterPresenter.Present(
                _stateMachine.CurrentImage,
                _activePreset.CharacterScalePercent);
        }

        // 입력 이벤트와 런타임 코루틴 연결을 정리하는 함수
        private void OnDisable()
        {
            if (_runtimeCoroutine != null)
            {
                StopCoroutine(_runtimeCoroutine);
                _runtimeCoroutine = null;
            }

            if (_activityInputController != null)
            {
                _activityInputController.SetMouseInputExclusion(null);
                _activityInputController.ActivityAccepted -= HandleActivityAccepted;
                _activityInputController = null;
            }
        }
    }
}
