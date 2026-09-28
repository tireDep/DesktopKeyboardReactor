using System;

namespace CustomKeyboardReactor
{
    // 애플리케이션 포커스에 맞춰 키보드 입력 경로를 선택하는 클래스
    public sealed class FocusAwareKeyboardInputSource : IActivityInputSource
    {
        private readonly IActivityInputSource _globalInputSource; // 외부 포커스 키보드 입력 공급자
        private readonly IActivityInputSource _focusedInputSource; // 내부 포커스 키보드 입력 공급자
        private readonly Func<bool> _isApplicationFocused; // 애플리케이션 포커스 판정 함수

        // 두 키보드 입력 공급자와 포커스 판정을 연결하는 생성자
        public FocusAwareKeyboardInputSource(
            IActivityInputSource globalInputSource,
            IActivityInputSource focusedInputSource,
            Func<bool> isApplicationFocused)
        {
            _globalInputSource = globalInputSource ??
                                 throw new ArgumentNullException(nameof(globalInputSource));
            _focusedInputSource = focusedInputSource ??
                                  throw new ArgumentNullException(nameof(focusedInputSource));
            _isApplicationFocused = isApplicationFocused ??
                                    throw new ArgumentNullException(nameof(isApplicationFocused));
        }

        public bool IsRunning { get; private set; } // 키보드 입력 공급자 실행 여부

        // 두 키보드 입력 공급자를 시작하는 함수
        public bool TryStart()
        {
            if (IsRunning)
            {
                return true;
            }

            if (!_globalInputSource.TryStart())
            {
                return false;
            }

            if (!_focusedInputSource.TryStart())
            {
                _globalInputSource.Stop();
                return false;
            }

            IsRunning = true;
            return true;
        }

        // 두 키보드 입력 공급자를 정지하는 함수
        public void Stop()
        {
            try
            {
                _globalInputSource.Stop();
            }
            finally
            {
                _focusedInputSource.Stop();
                IsRunning = false;
            }
        }

        // 현재 포커스에 맞는 다음 키보드 반응 입력을 반환하는 함수
        public bool TryDequeue(out ActivityInputEvent inputEvent)
        {
            bool isApplicationFocused = _isApplicationFocused(); // 현재 애플리케이션 포커스 여부
            if (isApplicationFocused)
            {
                DiscardPendingInputs(_globalInputSource);
                return _focusedInputSource.TryDequeue(out inputEvent);
            }

            DiscardPendingInputs(_focusedInputSource);
            return _globalInputSource.TryDequeue(out inputEvent);
        }

        // 두 키보드 입력 공급자 자원을 정리하는 함수
        public void Dispose()
        {
            Stop();
        }

        // 비활성 입력 경로의 대기 입력을 비우는 함수
        private static void DiscardPendingInputs(IActivityInputSource inputSource)
        {
            ActivityInputEvent inputEvent; // 버릴 키보드 반응 입력
            while (inputSource.TryDequeue(out inputEvent))
            {
            }
        }
    }
}
