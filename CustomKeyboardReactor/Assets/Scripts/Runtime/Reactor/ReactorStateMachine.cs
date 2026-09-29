using System;
using System.Collections.Generic;

namespace CustomKeyboardReactor
{
    // 반응, 대기, 설정 상태와 이미지 전환을 관리하는 클래스
    public sealed class ReactorStateMachine
    {
        private PresetData _activePreset; // 활성 프리셋
        private readonly Func<int, int> _selectIdleCandidateIndex; // 대기 이미지 후보 선택 함수
        private bool _idleEnabled; // 대기 기능 활성 여부
        private double _idleTimeoutSeconds; // 대기 진입 시간
        private double _reactingElapsedSeconds; // 마지막 반응 이후 경과 시간
        private int _currentNormalImageIndex; // 현재 일반 이미지 인덱스
        private int _currentIdleImageIndex = -1; // 현재 대기 이미지 인덱스
        private int _previousIdleImageIndex = -1; // 직전 대기 이미지 인덱스

        // 활성 프리셋과 대기 설정을 연결하는 생성자
        public ReactorStateMachine(
            PresetData activePreset,
            bool idleEnabled,
            double idleTimeoutSeconds,
            Func<int, int> selectIdleCandidateIndex = null)
        {
            ValidatePreset(activePreset);
            ValidateIdleTimeout(idleTimeoutSeconds);

            Random random = new Random(); // 기본 무작위 선택기
            _activePreset = activePreset;
            _idleEnabled = idleEnabled;
            _idleTimeoutSeconds = idleTimeoutSeconds;
            _selectIdleCandidateIndex = selectIdleCandidateIndex ?? random.Next;
            CurrentState = ReactorState.Reacting;
        }

        public ReactorState CurrentState { get; private set; } // 현재 반응 상태
        public PresetData ActivePreset => _activePreset; // 활성 프리셋
        public int CurrentNormalImageIndex => _currentNormalImageIndex; // 현재 일반 이미지 인덱스
        public int CurrentIdleImageIndex => _currentIdleImageIndex; // 현재 대기 이미지 인덱스
        public ImageAssetData CurrentImage => _currentIdleImageIndex >= 0
            ? _activePreset.IdleImages[_currentIdleImageIndex]
            : _activePreset.NormalImages[_currentNormalImageIndex]; // 현재 표시 이미지

        // 유효한 반응 입력을 현재 상태와 이미지 순서에 반영하는 함수
        public bool HandleActivity()
        {
            if (CurrentState == ReactorState.Configuring)
            {
                return false;
            }

            _reactingElapsedSeconds = 0d;
            if (CurrentState == ReactorState.Idle)
            {
                CurrentState = ReactorState.Reacting;
                _currentIdleImageIndex = -1;
                _currentNormalImageIndex = 0;
                return true;
            }

            int nextImageIndex = (_currentNormalImageIndex + 1) % _activePreset.NormalImages.Count; // 다음 일반 이미지 인덱스
            bool imageChanged = nextImageIndex != _currentNormalImageIndex; // 표시 이미지 변경 여부
            _currentNormalImageIndex = nextImageIndex;
            return imageChanged;
        }

        // 반응 상태의 경과 시간을 누적하고 대기 전환 여부를 반환하는 함수
        public bool AdvanceTime(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            if (CurrentState != ReactorState.Reacting ||
                !_idleEnabled ||
                _activePreset.IdleImages.Count == 0)
            {
                return false;
            }

            _reactingElapsedSeconds += elapsedSeconds;
            if (_reactingElapsedSeconds < _idleTimeoutSeconds)
            {
                return false;
            }

            CurrentState = ReactorState.Idle;
            _reactingElapsedSeconds = 0d;
            _currentIdleImageIndex = SelectNextIdleImageIndex();
            _previousIdleImageIndex = _currentIdleImageIndex;
            return true;
        }

        // 설정 상태로 전환하여 반응과 대기 타이머를 정지하는 함수
        public void OpenSettings()
        {
            CurrentState = ReactorState.Configuring;
        }

        // 설정을 닫고 첫 일반 이미지의 반응 상태로 복귀하는 함수
        public void CloseSettings()
        {
            ResetToFirstNormalImage();
        }

        // 새 활성 프리셋을 적용하고 첫 일반 이미지로 초기화하는 함수
        public void ApplyPreset(PresetData activePreset)
        {
            ValidatePreset(activePreset);
            _activePreset = activePreset;
            _previousIdleImageIndex = -1;
            ResetToFirstNormalImage();
        }

        // 공용 대기 설정을 변경하는 함수
        public void SetIdleSettings(bool idleEnabled, double idleTimeoutSeconds)
        {
            ValidateIdleTimeout(idleTimeoutSeconds);
            _idleEnabled = idleEnabled;
            _idleTimeoutSeconds = idleTimeoutSeconds;
            _reactingElapsedSeconds = 0d;

            if (!idleEnabled && CurrentState == ReactorState.Idle)
            {
                ResetToFirstNormalImage();
            }
        }

        // 현재 활성 프리셋 기준 환산 루프 수를 계산하는 함수
        public long CalculateLoopCount(long totalInputCount)
        {
            if (totalInputCount < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(totalInputCount));
            }

            return totalInputCount / _activePreset.NormalImages.Count;
        }

        // 직전 이미지가 제외된 다음 대기 이미지 인덱스를 선택하는 함수
        private int SelectNextIdleImageIndex()
        {
            int idleImageCount = _activePreset.IdleImages.Count; // 대기 이미지 수
            if (idleImageCount == 1)
            {
                return 0;
            }

            bool hasPreviousImage = _previousIdleImageIndex >= 0 &&
                                    _previousIdleImageIndex < idleImageCount; // 직전 대기 이미지 존재 여부
            int candidateCount = hasPreviousImage ? idleImageCount - 1 : idleImageCount; // 선택 가능한 후보 수
            int candidateIndex = _selectIdleCandidateIndex(candidateCount); // 선택된 후보 인덱스
            if (candidateIndex < 0 || candidateIndex >= candidateCount)
            {
                throw new InvalidOperationException("Idle image selector returned an invalid index.");
            }

            if (hasPreviousImage && candidateIndex >= _previousIdleImageIndex)
            {
                candidateIndex++;
            }

            return candidateIndex;
        }

        // 반응 상태와 첫 일반 이미지로 런타임 위치를 초기화하는 함수
        private void ResetToFirstNormalImage()
        {
            CurrentState = ReactorState.Reacting;
            _reactingElapsedSeconds = 0d;
            _currentNormalImageIndex = 0;
            _currentIdleImageIndex = -1;
        }

        // 상태 머신에 필요한 프리셋 이미지 구성을 검증하는 함수
        private static void ValidatePreset(PresetData preset)
        {
            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            if (preset.NormalImages == null || preset.NormalImages.Count == 0)
            {
                throw new ArgumentException("A reactor preset requires at least one normal image.", nameof(preset));
            }

            ValidateImages(preset.NormalImages, nameof(preset));
            if (preset.IdleImages != null)
            {
                ValidateImages(preset.IdleImages, nameof(preset));
            }
            else
            {
                preset.IdleImages = new List<ImageAssetData>();
            }
        }

        // 상태 머신에 전달된 이미지 목록의 빈 항목을 검증하는 함수
        private static void ValidateImages(IReadOnlyList<ImageAssetData> images, string parameterName)
        {
            for (int index = 0; index < images.Count; index++)
            {
                if (images[index] == null)
                {
                    throw new ArgumentException("A reactor preset cannot contain a null image.", parameterName);
                }
            }
        }

        // 대기 진입 시간이 유효한 양수인지 검증하는 함수
        private static void ValidateIdleTimeout(double idleTimeoutSeconds)
        {
            if (double.IsNaN(idleTimeoutSeconds) ||
                double.IsInfinity(idleTimeoutSeconds) ||
                idleTimeoutSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(idleTimeoutSeconds));
            }
        }
    }
}
