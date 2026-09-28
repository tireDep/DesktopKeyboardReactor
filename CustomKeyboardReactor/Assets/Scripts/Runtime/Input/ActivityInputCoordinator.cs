using System;

namespace CustomKeyboardReactor
{
    // 장치별 반응 입력을 병합하고 전체 입력 수를 집계하는 클래스
    public sealed class ActivityInputCoordinator : IDisposable
    {
        private readonly IActivityInputSource _keyboardInputSource; // 키보드 반응 입력 공급자
        private readonly IActivityInputSource _mouseButtonInputSource; // 마우스 버튼 반응 입력 공급자
        private Predicate<ActivityInputEvent> _isMouseInputExcluded; // 자체 마우스 입력 판정 함수

        // 장치별 입력 공급자와 마우스 제외 판정을 연결하는 생성자
        public ActivityInputCoordinator(
            IActivityInputSource keyboardInputSource,
            IActivityInputSource mouseButtonInputSource,
            Predicate<ActivityInputEvent> isMouseInputExcluded = null,
            long initialTotalInputCount = 0L)
        {
            _keyboardInputSource = keyboardInputSource ??
                                   throw new ArgumentNullException(nameof(keyboardInputSource));
            _mouseButtonInputSource = mouseButtonInputSource ??
                                      throw new ArgumentNullException(nameof(mouseButtonInputSource));
            if (initialTotalInputCount < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialTotalInputCount),
                    "Initial total input count cannot be negative.");
            }

            _isMouseInputExcluded = isMouseInputExcluded;
            TotalInputCount = initialTotalInputCount;
        }

        public long TotalInputCount { get; private set; } // 전체 입력 수
        public bool KeyboardReactionEnabled { get; set; } = true; // 키보드 반응 활성 여부
        public bool MouseButtonReactionEnabled { get; set; } = true; // 마우스 버튼 반응 활성 여부
        public bool IsConfiguring { get; set; } // 설정 상태 여부

        public event Action<ActivityInputEvent> ActivityAccepted; // 유효한 반응 입력 전달 이벤트

        // 자체 마우스 입력 판정 함수를 변경하는 함수
        public void SetMouseInputExclusion(Predicate<ActivityInputEvent> isMouseInputExcluded)
        {
            _isMouseInputExcluded = isMouseInputExcluded;
        }

        // 두 입력 공급자의 수집을 시작하는 함수
        public bool TryStart()
        {
            bool keyboardStarted = _keyboardInputSource.TryStart(); // 키보드 공급자 시작 결과
            bool mouseButtonStarted = _mouseButtonInputSource.TryStart(); // 마우스 버튼 공급자 시작 결과
            return keyboardStarted && mouseButtonStarted;
        }

        // 대기 중인 장치 입력을 처리하고 집계된 입력 수를 반환하는 함수
        public int ProcessPendingInputs()
        {
            int processedInputCount = 0; // 이번 호출에서 처리한 반응 입력 수
            processedInputCount += ProcessSource(
                _keyboardInputSource,
                !IsConfiguring && KeyboardReactionEnabled,
                null);
            processedInputCount += ProcessSource(
                _mouseButtonInputSource,
                !IsConfiguring && MouseButtonReactionEnabled,
                _isMouseInputExcluded);
            return processedInputCount;
        }

        // 두 입력 공급자를 정리하는 함수
        public void Dispose()
        {
            try
            {
                _keyboardInputSource.Dispose();
            }
            finally
            {
                _mouseButtonInputSource.Dispose();
            }
        }

        // 한 공급자의 대기 입력을 전체 입력 수에 반영하는 함수
        private int ProcessSource(
            IActivityInputSource inputSource,
            bool shouldCount,
            Predicate<ActivityInputEvent> isInputExcluded)
        {
            int processedInputCount = 0; // 공급자에서 처리한 반응 입력 수
            ActivityInputEvent inputEvent; // 공급자가 반환한 반응 입력
            while (inputSource.TryDequeue(out inputEvent))
            {
                if (!shouldCount || (isInputExcluded != null && isInputExcluded(inputEvent)))
                {
                    continue;
                }

                TotalInputCount++;
                processedInputCount++;
                ActivityAccepted?.Invoke(inputEvent);
            }

            return processedInputCount;
        }
    }
}
