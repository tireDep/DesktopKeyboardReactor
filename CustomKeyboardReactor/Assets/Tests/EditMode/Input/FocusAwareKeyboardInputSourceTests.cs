using System.Collections.Generic;
using NUnit.Framework;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 포커스별 키보드 입력 경로 선택을 검증하는 클래스
    public sealed class FocusAwareKeyboardInputSourceTests
    {
        // 활성 포커스 경로만 반환하고 비활성 경로 입력을 버리는지 검증하는 함수
        [Test]
        public void TryDequeue_SelectsFocusedPathAndDiscardsInactiveInputs()
        {
            FakeActivityInputSource globalInputSource = new FakeActivityInputSource(); // 외부 포커스 입력 공급자
            FakeActivityInputSource focusedInputSource = new FakeActivityInputSource(); // 내부 포커스 입력 공급자
            bool isApplicationFocused = true; // 애플리케이션 포커스 여부
            FocusAwareKeyboardInputSource inputSource = new FocusAwareKeyboardInputSource( // 포커스 대응 입력 공급자
                globalInputSource,
                focusedInputSource,
                () => isApplicationFocused);
            inputSource.TryStart();
            globalInputSource.Enqueue(ActivityInputEvent.CreateMouseButton(ActivityInputEvent.MouseButton.Left, 100, 0));
            focusedInputSource.Enqueue(ActivityInputEvent.CreateMouseButton(ActivityInputEvent.MouseButton.Left, 200, 0));

            bool focusedDequeued = inputSource.TryDequeue(out ActivityInputEvent focusedInput); // 내부 포커스 입력 반환 결과
            isApplicationFocused = false;
            bool staleGlobalDequeued = inputSource.TryDequeue(out _); // 이전 외부 포커스 입력 반환 결과
            globalInputSource.Enqueue(ActivityInputEvent.CreateMouseButton(ActivityInputEvent.MouseButton.Left, 300, 0));
            focusedInputSource.Enqueue(ActivityInputEvent.CreateMouseButton(ActivityInputEvent.MouseButton.Left, 400, 0));
            bool globalDequeued = inputSource.TryDequeue(out ActivityInputEvent globalInput); // 외부 포커스 입력 반환 결과
            isApplicationFocused = true;
            bool staleFocusedDequeued = inputSource.TryDequeue(out _); // 이전 내부 포커스 입력 반환 결과

            Assert.That(focusedDequeued, Is.True);
            Assert.That(focusedInput.ScreenX, Is.EqualTo(200));
            Assert.That(staleGlobalDequeued, Is.False);
            Assert.That(globalDequeued, Is.True);
            Assert.That(globalInput.ScreenX, Is.EqualTo(300));
            Assert.That(staleFocusedDequeued, Is.False);
        }

        // 포커스 대응 공급자가 두 하위 공급자의 수명주기를 관리하는지 검증하는 함수
        [Test]
        public void Lifecycle_StartsAndDisposesBothKeyboardInputSources()
        {
            FakeActivityInputSource globalInputSource = new FakeActivityInputSource(); // 외부 포커스 입력 공급자
            FakeActivityInputSource focusedInputSource = new FakeActivityInputSource(); // 내부 포커스 입력 공급자
            FocusAwareKeyboardInputSource inputSource = new FocusAwareKeyboardInputSource( // 포커스 대응 입력 공급자
                globalInputSource,
                focusedInputSource,
                () => false);

            bool started = inputSource.TryStart(); // 키보드 입력 공급자 시작 결과
            inputSource.Dispose();

            Assert.That(started, Is.True);
            Assert.That(globalInputSource.IsRunning, Is.False);
            Assert.That(focusedInputSource.IsRunning, Is.False);
        }

        // 테스트 반응 입력을 보관하는 공급자 클래스
        private sealed class FakeActivityInputSource : IActivityInputSource
        {
            private readonly Queue<ActivityInputEvent> _pendingInputs = new Queue<ActivityInputEvent>(); // 대기 중인 반응 입력 목록

            public bool IsRunning { get; private set; } // 공급자 실행 여부

            // 공급자를 실행 상태로 변경하는 함수
            public bool TryStart()
            {
                IsRunning = true;
                return true;
            }

            // 공급자를 정지 상태로 변경하는 함수
            public void Stop()
            {
                _pendingInputs.Clear();
                IsRunning = false;
            }

            // 다음 반응 입력을 반환하는 함수
            public bool TryDequeue(out ActivityInputEvent inputEvent)
            {
                if (_pendingInputs.Count == 0)
                {
                    inputEvent = default;
                    return false;
                }

                inputEvent = _pendingInputs.Dequeue();
                return true;
            }

            // 테스트 반응 입력을 추가하는 함수
            internal void Enqueue(ActivityInputEvent inputEvent)
            {
                _pendingInputs.Enqueue(inputEvent);
            }

            // 공급자를 정리하는 함수
            public void Dispose()
            {
                Stop();
            }
        }
    }
}
