using NUnit.Framework;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 캐릭터 표시 크기 단계와 화면 제한을 검증하는 클래스
    public sealed class CharacterScaleCalculatorTests
    {
        // 백 퍼센트가 320픽셀 표시 영역으로 계산되는지 검증하는 함수
        [Test]
        public void CalculateDisplayAreaSize_AtOneHundredPercent_ReturnsBaseSize()
        {
            float displayAreaSize = CharacterScaleCalculator.CalculateDisplayAreaSize( // 표시 영역 크기
                100,
                1920f,
                1080f);

            Assert.That(displayAreaSize, Is.EqualTo(320f));
        }

        // 큰 캐릭터 크기가 작업 영역 짧은 변의 구십 퍼센트로 제한되는지 검증하는 함수
        [Test]
        public void CalculateDisplayAreaSize_WhenRequestedSizeIsTooLarge_UsesWorkAreaLimit()
        {
            float displayAreaSize = CharacterScaleCalculator.CalculateDisplayAreaSize( // 표시 영역 크기
                300,
                800f,
                600f);

            Assert.That(displayAreaSize, Is.EqualTo(540f));
        }

        // 캐릭터 크기가 오 퍼센트 단위와 허용 범위로 보정되는지 검증하는 함수
        [TestCase(7, 10)]
        [TestCase(102, 100)]
        [TestCase(103, 105)]
        [TestCase(304, 300)]
        public void NormalizeScalePercent_UsesSupportedRangeAndStep(int inputPercent, int expectedPercent)
        {
            int normalizedPercent = CharacterScaleCalculator.NormalizeScalePercent(inputPercent); // 보정된 크기

            Assert.That(normalizedPercent, Is.EqualTo(expectedPercent));
        }

        // 크기 단계 변경이 오 퍼센트 단위로 이동하고 경계에서 멈추는지 검증하는 함수
        [TestCase(100, 1, 105)]
        [TestCase(100, -1, 95)]
        [TestCase(300, 1, 300)]
        [TestCase(10, -1, 10)]
        public void StepScalePercent_ChangesByFivePercentWithinRange(
            int currentPercent,
            int stepCount,
            int expectedPercent)
        {
            int steppedPercent = CharacterScaleCalculator.StepScalePercent( // 단계 적용 크기
                currentPercent,
                stepCount);

            Assert.That(steppedPercent, Is.EqualTo(expectedPercent));
        }
    }
}
