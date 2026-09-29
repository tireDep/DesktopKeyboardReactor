using System.Collections.Generic;
using NUnit.Framework;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 반응 상태 전환과 이미지 순환을 검증하는 클래스
    public sealed class ReactorStateMachineTests
    {
        // 생성 직후 첫 일반 이미지가 표시되는지 검증하는 함수
        [Test]
        public void Constructor_StartsReactingWithFirstNormalImage()
        {
            PresetData preset = CreatePreset(3, 1); // 테스트 프리셋
            ReactorStateMachine stateMachine = new ReactorStateMachine(preset, true, 60d); // 반응 상태 머신

            Assert.That(stateMachine.CurrentState, Is.EqualTo(ReactorState.Reacting));
            Assert.That(stateMachine.CurrentNormalImageIndex, Is.Zero);
            Assert.That(stateMachine.CurrentImage, Is.SameAs(preset.NormalImages[0]));
        }

        // 반응 입력마다 일반 이미지가 순환하는지 검증하는 함수
        [Test]
        public void HandleActivity_CyclesNormalImagesAndWraps()
        {
            PresetData preset = CreatePreset(3, 0); // 테스트 프리셋
            ReactorStateMachine stateMachine = new ReactorStateMachine(preset, false, 60d); // 반응 상태 머신

            stateMachine.HandleActivity();
            Assert.That(stateMachine.CurrentNormalImageIndex, Is.EqualTo(1));
            stateMachine.HandleActivity();
            Assert.That(stateMachine.CurrentNormalImageIndex, Is.EqualTo(2));
            stateMachine.HandleActivity();

            Assert.That(stateMachine.CurrentNormalImageIndex, Is.Zero);
        }

        // 일반 이미지가 한 장이면 반응 입력 후에도 화면이 유지되는지 검증하는 함수
        [Test]
        public void HandleActivity_WithOneNormalImage_KeepsCurrentImage()
        {
            PresetData preset = CreatePreset(1, 0); // 테스트 프리셋
            ReactorStateMachine stateMachine = new ReactorStateMachine(preset, false, 60d); // 반응 상태 머신

            bool imageChanged = stateMachine.HandleActivity(); // 이미지 변경 여부

            Assert.That(imageChanged, Is.False);
            Assert.That(stateMachine.CurrentImage, Is.SameAs(preset.NormalImages[0]));
        }

        // 제한 시간이 지나면 대기 이미지로 전환되는지 검증하는 함수
        [Test]
        public void AdvanceTime_AfterTimeout_EntersIdleState()
        {
            PresetData preset = CreatePreset(2, 2); // 테스트 프리셋
            ReactorStateMachine stateMachine = new ReactorStateMachine( // 반응 상태 머신
                preset,
                true,
                60d,
                _ => 1);

            bool beforeTimeoutChanged = stateMachine.AdvanceTime(59.9d); // 제한 전 변경 여부
            bool afterTimeoutChanged = stateMachine.AdvanceTime(0.1d); // 제한 후 변경 여부

            Assert.That(beforeTimeoutChanged, Is.False);
            Assert.That(afterTimeoutChanged, Is.True);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(ReactorState.Idle));
            Assert.That(stateMachine.CurrentIdleImageIndex, Is.EqualTo(1));
            Assert.That(stateMachine.CurrentImage, Is.SameAs(preset.IdleImages[1]));
        }

        // 대기 기능이 꺼졌거나 이미지가 없으면 대기 상태로 전환하지 않는지 검증하는 함수
        [TestCase(false, 1)]
        [TestCase(true, 0)]
        public void AdvanceTime_WhenIdleUnavailable_RemainsReacting(bool idleEnabled, int idleImageCount)
        {
            PresetData preset = CreatePreset(2, idleImageCount); // 테스트 프리셋
            ReactorStateMachine stateMachine = new ReactorStateMachine(preset, idleEnabled, 60d); // 반응 상태 머신

            bool imageChanged = stateMachine.AdvanceTime(120d); // 이미지 변경 여부

            Assert.That(imageChanged, Is.False);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(ReactorState.Reacting));
        }

        // 반응 입력이 대기 진입 타이머를 처음부터 다시 시작하는지 검증하는 함수
        [Test]
        public void HandleActivity_ResetsIdleTimer()
        {
            PresetData preset = CreatePreset(2, 1); // 테스트 프리셋
            ReactorStateMachine stateMachine = new ReactorStateMachine(preset, true, 60d); // 반응 상태 머신
            stateMachine.AdvanceTime(59d);

            stateMachine.HandleActivity();
            bool earlyIdleChange = stateMachine.AdvanceTime(1d); // 초기화 직후 대기 전환 여부
            bool timeoutIdleChange = stateMachine.AdvanceTime(59d); // 새 제한 시간 후 대기 전환 여부

            Assert.That(earlyIdleChange, Is.False);
            Assert.That(timeoutIdleChange, Is.True);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(ReactorState.Idle));
        }

        // 연속 대기 진입에서 직전 대기 이미지를 제외하는지 검증하는 함수
        [Test]
        public void AdvanceTime_WithMultipleIdleImages_ExcludesPreviousImage()
        {
            Queue<int> selectedCandidates = new Queue<int>(new[] { 1, 1 }); // 선택할 후보 인덱스 목록
            PresetData preset = CreatePreset(2, 3); // 테스트 프리셋
            ReactorStateMachine stateMachine = new ReactorStateMachine( // 반응 상태 머신
                preset,
                true,
                60d,
                _ => selectedCandidates.Dequeue());

            stateMachine.AdvanceTime(60d);
            int firstIdleImageIndex = stateMachine.CurrentIdleImageIndex; // 첫 대기 이미지 인덱스
            stateMachine.HandleActivity();
            stateMachine.AdvanceTime(60d);
            int secondIdleImageIndex = stateMachine.CurrentIdleImageIndex; // 둘째 대기 이미지 인덱스

            Assert.That(firstIdleImageIndex, Is.EqualTo(1));
            Assert.That(secondIdleImageIndex, Is.EqualTo(2));
            Assert.That(secondIdleImageIndex, Is.Not.EqualTo(firstIdleImageIndex));
        }

        // 대기 상태의 첫 반응 입력이 첫 일반 이미지로 복귀하는지 검증하는 함수
        [Test]
        public void HandleActivity_FromIdle_ReturnsToFirstNormalImage()
        {
            PresetData preset = CreatePreset(3, 1); // 테스트 프리셋
            ReactorStateMachine stateMachine = new ReactorStateMachine(preset, true, 60d); // 반응 상태 머신
            stateMachine.HandleActivity();
            stateMachine.AdvanceTime(60d);

            bool imageChanged = stateMachine.HandleActivity(); // 이미지 변경 여부

            Assert.That(imageChanged, Is.True);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(ReactorState.Reacting));
            Assert.That(stateMachine.CurrentNormalImageIndex, Is.Zero);
            Assert.That(stateMachine.CurrentImage, Is.SameAs(preset.NormalImages[0]));
        }

        // 설정 상태에서 입력과 대기 시간이 반영되지 않는지 검증하는 함수
        [Test]
        public void Configuring_PausesActivityAndIdleTimerUntilClosed()
        {
            PresetData preset = CreatePreset(2, 1); // 테스트 프리셋
            ReactorStateMachine stateMachine = new ReactorStateMachine(preset, true, 60d); // 반응 상태 머신
            stateMachine.HandleActivity();
            stateMachine.OpenSettings();

            bool activityChanged = stateMachine.HandleActivity(); // 설정 중 입력 변경 여부
            bool timerChanged = stateMachine.AdvanceTime(120d); // 설정 중 타이머 변경 여부

            Assert.That(activityChanged, Is.False);
            Assert.That(timerChanged, Is.False);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(ReactorState.Configuring));

            stateMachine.CloseSettings();
            Assert.That(stateMachine.CurrentState, Is.EqualTo(ReactorState.Reacting));
            Assert.That(stateMachine.CurrentNormalImageIndex, Is.Zero);
        }

        // 대기 상태에서 설정을 열면 현재 표시 이미지를 유지하는지 검증하는 함수
        [Test]
        public void OpenSettings_FromIdle_PreservesDisplayedIdleImage()
        {
            PresetData preset = CreatePreset(2, 1); // 테스트 프리셋
            ReactorStateMachine stateMachine = new ReactorStateMachine(preset, true, 60d); // 반응 상태 머신
            stateMachine.AdvanceTime(60d);
            ImageAssetData idleImage = stateMachine.CurrentImage; // 설정 열기 전 대기 이미지

            stateMachine.OpenSettings();

            Assert.That(stateMachine.CurrentState, Is.EqualTo(ReactorState.Configuring));
            Assert.That(stateMachine.CurrentImage, Is.SameAs(idleImage));
        }

        // 전체 입력 수를 현재 일반 이미지 수로 나눈 환산 루프 수를 검증하는 함수
        [TestCase(0L, 0L)]
        [TestCase(2L, 0L)]
        [TestCase(3L, 1L)]
        [TestCase(8L, 2L)]
        public void CalculateLoopCount_UsesActiveNormalImageCount(long totalInputCount, long expectedLoopCount)
        {
            PresetData preset = CreatePreset(3, 0); // 테스트 프리셋
            ReactorStateMachine stateMachine = new ReactorStateMachine(preset, false, 60d); // 반응 상태 머신

            long loopCount = stateMachine.CalculateLoopCount(totalInputCount); // 환산 루프 수

            Assert.That(loopCount, Is.EqualTo(expectedLoopCount));
        }

        // 지정한 수의 일반 이미지와 대기 이미지를 가진 테스트 프리셋을 생성하는 함수
        private static PresetData CreatePreset(int normalImageCount, int idleImageCount)
        {
            PresetData preset = new PresetData(); // 테스트 프리셋
            for (int index = 0; index < normalImageCount; index++)
            {
                preset.NormalImages.Add(new ImageAssetData
                {
                    Id = $"normal-{index}",
                    RelativePath = $"normal-{index}.png",
                });
            }

            for (int index = 0; index < idleImageCount; index++)
            {
                preset.IdleImages.Add(new ImageAssetData
                {
                    Id = $"idle-{index}",
                    RelativePath = $"idle-{index}.png",
                });
            }

            return preset;
        }
    }
}
