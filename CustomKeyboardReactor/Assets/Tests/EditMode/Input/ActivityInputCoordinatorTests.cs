using System.Collections.Generic;
using NUnit.Framework;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 반응 입력 병합과 집계를 검증하는 클래스
    public sealed class ActivityInputCoordinatorTests
    {
        // 키보드와 마우스 반응 입력이 전체 입력 수에 합산되는지 검증하는 함수
        [Test]
        public void ProcessPendingInputs_CountsKeyboardAndMouseActivity()
        {
            FakeActivityInputSource keyboardInputSource = new FakeActivityInputSource(); // 키보드 반응 입력 공급자
            FakeActivityInputSource mouseButtonInputSource = new FakeActivityInputSource(); // 마우스 버튼 반응 입력 공급자
            keyboardInputSource.Enqueue(ActivityInputEvent.CreateKeyboard());
            mouseButtonInputSource.Enqueue(ActivityInputEvent.CreateMouseButton(ActivityInputEvent.MouseButton.Left, 120, 240));
            ActivityInputCoordinator coordinator = new ActivityInputCoordinator( // 반응 입력 조정기
                keyboardInputSource,
                mouseButtonInputSource);

            int processedInputCount = coordinator.ProcessPendingInputs(); // 처리된 반응 입력 수

            Assert.That(processedInputCount, Is.EqualTo(2));
            Assert.That(coordinator.TotalInputCount, Is.EqualTo(2L));
        }

        // 저장된 전체 입력 수에서 새로운 반응 입력을 이어서 집계하는지 검증하는 함수
        [Test]
        public void ProcessPendingInputs_WithInitialCount_ContinuesPersistedCount()
        {
            FakeActivityInputSource keyboardInputSource = new FakeActivityInputSource(); // 키보드 반응 입력 공급자
            FakeActivityInputSource mouseButtonInputSource = new FakeActivityInputSource(); // 마우스 버튼 반응 입력 공급자
            keyboardInputSource.Enqueue(ActivityInputEvent.CreateKeyboard());
            ActivityInputCoordinator coordinator = new ActivityInputCoordinator( // 반응 입력 조정기
                keyboardInputSource,
                mouseButtonInputSource,
                initialTotalInputCount: 41L);

            coordinator.ProcessPendingInputs();

            Assert.That(coordinator.TotalInputCount, Is.EqualTo(42L));
        }

        // 설정 상태에서 받은 입력이 이후에도 집계되지 않는지 검증하는 함수
        [Test]
        public void ProcessPendingInputs_WhenConfiguring_DiscardsQueuedActivity()
        {
            FakeActivityInputSource keyboardInputSource = new FakeActivityInputSource(); // 키보드 반응 입력 공급자
            FakeActivityInputSource mouseButtonInputSource = new FakeActivityInputSource(); // 마우스 버튼 반응 입력 공급자
            keyboardInputSource.Enqueue(ActivityInputEvent.CreateKeyboard());
            mouseButtonInputSource.Enqueue(ActivityInputEvent.CreateMouseButton(ActivityInputEvent.MouseButton.Left, 320, 180));
            ActivityInputCoordinator coordinator = new ActivityInputCoordinator( // 반응 입력 조정기
                keyboardInputSource,
                mouseButtonInputSource)
            {
                IsConfiguring = true,
            };

            int configuringInputCount = coordinator.ProcessPendingInputs(); // 설정 상태 처리 입력 수
            coordinator.IsConfiguring = false;
            int resumedInputCount = coordinator.ProcessPendingInputs(); // 설정 종료 후 처리 입력 수

            Assert.That(configuringInputCount, Is.Zero);
            Assert.That(resumedInputCount, Is.Zero);
            Assert.That(coordinator.TotalInputCount, Is.Zero);
        }

        // 비활성화된 장치 입력이 재활성화 후에도 집계되지 않는지 검증하는 함수
        [Test]
        public void ProcessPendingInputs_WhenDeviceReactionsAreDisabled_DiscardsQueuedActivity()
        {
            FakeActivityInputSource keyboardInputSource = new FakeActivityInputSource(); // 키보드 반응 입력 공급자
            FakeActivityInputSource mouseButtonInputSource = new FakeActivityInputSource(); // 마우스 버튼 반응 입력 공급자
            keyboardInputSource.Enqueue(ActivityInputEvent.CreateKeyboard());
            mouseButtonInputSource.Enqueue(ActivityInputEvent.CreateMouseButton(ActivityInputEvent.MouseButton.Left, 640, 360));
            ActivityInputCoordinator coordinator = new ActivityInputCoordinator( // 반응 입력 조정기
                keyboardInputSource,
                mouseButtonInputSource)
            {
                KeyboardReactionEnabled = false,
                MouseButtonReactionEnabled = false,
            };

            int disabledInputCount = coordinator.ProcessPendingInputs(); // 장치 비활성 상태 처리 입력 수
            coordinator.KeyboardReactionEnabled = true;
            coordinator.MouseButtonReactionEnabled = true;
            int enabledInputCount = coordinator.ProcessPendingInputs(); // 장치 재활성 상태 처리 입력 수

            Assert.That(disabledInputCount, Is.Zero);
            Assert.That(enabledInputCount, Is.Zero);
            Assert.That(coordinator.TotalInputCount, Is.Zero);
        }

        // 자체 UI 상호작용으로 판정된 마우스 입력이 제외되는지 검증하는 함수
        [Test]
        public void ProcessPendingInputs_WhenMouseActivityIsSelfInteraction_ExcludesActivity()
        {
            FakeActivityInputSource keyboardInputSource = new FakeActivityInputSource(); // 키보드 반응 입력 공급자
            FakeActivityInputSource mouseButtonInputSource = new FakeActivityInputSource(); // 마우스 버튼 반응 입력 공급자
            mouseButtonInputSource.Enqueue(ActivityInputEvent.CreateMouseButton(ActivityInputEvent.MouseButton.Left, 100, 200));
            mouseButtonInputSource.Enqueue(ActivityInputEvent.CreateMouseButton(ActivityInputEvent.MouseButton.Left, 300, 400));
            ActivityInputCoordinator coordinator = new ActivityInputCoordinator( // 반응 입력 조정기
                keyboardInputSource,
                mouseButtonInputSource);
            coordinator.SetMouseInputExclusion(inputEvent => inputEvent.ScreenX == 100);

            int processedInputCount = coordinator.ProcessPendingInputs(); // 처리된 반응 입력 수

            Assert.That(processedInputCount, Is.EqualTo(1));
            Assert.That(coordinator.TotalInputCount, Is.EqualTo(1L));
        }

        // 유효한 반응 입력이 좌표 정보와 함께 전달되는지 검증하는 함수
        [Test]
        public void ProcessPendingInputs_DeliversAcceptedActivity()
        {
            FakeActivityInputSource keyboardInputSource = new FakeActivityInputSource(); // 키보드 반응 입력 공급자
            FakeActivityInputSource mouseButtonInputSource = new FakeActivityInputSource(); // 마우스 버튼 반응 입력 공급자
            keyboardInputSource.Enqueue(ActivityInputEvent.CreateKeyboard());
            mouseButtonInputSource.Enqueue(ActivityInputEvent.CreateMouseButton(
                ActivityInputEvent.MouseButton.Right,
                512,
                288));
            ActivityInputCoordinator coordinator = new ActivityInputCoordinator( // 반응 입력 조정기
                keyboardInputSource,
                mouseButtonInputSource);
            List<ActivityInputEvent> acceptedInputs = new List<ActivityInputEvent>(); // 전달된 반응 입력 목록
            coordinator.ActivityAccepted += acceptedInputs.Add;

            coordinator.ProcessPendingInputs();

            Assert.That(acceptedInputs, Has.Count.EqualTo(2));
            Assert.That(acceptedInputs[0].HasScreenPosition, Is.False);
            Assert.That(acceptedInputs[1].HasScreenPosition, Is.True);
            Assert.That(acceptedInputs[1].Button, Is.EqualTo(ActivityInputEvent.MouseButton.Right));
            Assert.That(acceptedInputs[1].ScreenX, Is.EqualTo(512));
            Assert.That(acceptedInputs[1].ScreenY, Is.EqualTo(288));
        }

        // 조정기 시작과 정리가 두 입력 공급자의 수명주기를 관리하는지 검증하는 함수
        [Test]
        public void Lifecycle_StartsAndDisposesBothInputSources()
        {
            FakeActivityInputSource keyboardInputSource = new FakeActivityInputSource(); // 키보드 반응 입력 공급자
            FakeActivityInputSource mouseButtonInputSource = new FakeActivityInputSource(); // 마우스 버튼 반응 입력 공급자
            ActivityInputCoordinator coordinator = new ActivityInputCoordinator( // 반응 입력 조정기
                keyboardInputSource,
                mouseButtonInputSource);

            bool started = coordinator.TryStart(); // 입력 공급자 시작 결과
            coordinator.Dispose();

            Assert.That(started, Is.True);
            Assert.That(keyboardInputSource.IsRunning, Is.False);
            Assert.That(mouseButtonInputSource.IsRunning, Is.False);
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
            public void Enqueue(ActivityInputEvent inputEvent)
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
