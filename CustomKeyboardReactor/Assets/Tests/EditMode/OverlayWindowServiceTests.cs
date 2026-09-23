using NUnit.Framework;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 오버레이 창 스타일 조합을 검증하는 클래스
    public sealed class OverlayWindowServiceTests
    {
        // 보더리스 스타일이 관련 비트만 변경하는지 검증하는 함수
        [Test]
        public void ComposeBorderlessStyle_PreservesUnrelatedBitsAndReplacesWindowFrame()
        {
            const long currentStyle = 0x14CF0000L; // 기존 창 스타일
            const long expectedStyle = 0x94000000L; // 예상 보더리스 스타일

            long actualStyle = OverlayWindowService.ComposeBorderlessStyle(currentStyle); // 계산된 보더리스 스타일

            Assert.That(actualStyle, Is.EqualTo(expectedStyle));
        }

        // 클릭 통과 스타일이 레이어드와 투명 비트만 변경하는지 검증하는 함수
        [Test]
        public void ComposeClickThroughStyle_TogglesTransparentBitAndKeepsLayeredBit()
        {
            const long currentStyle = 0x00000008L; // 기존 확장 창 스타일
            const long expectedEnabledStyle = 0x00080028L; // 클릭 통과 활성 스타일
            const long expectedDisabledStyle = 0x00080008L; // 클릭 통과 비활성 스타일

            long enabledStyle = OverlayWindowService.ComposeClickThroughStyle(currentStyle, true); // 클릭 통과 활성 스타일
            long disabledStyle = OverlayWindowService.ComposeClickThroughStyle(enabledStyle, false); // 클릭 통과 비활성 스타일

            Assert.That(enabledStyle, Is.EqualTo(expectedEnabledStyle));
            Assert.That(disabledStyle, Is.EqualTo(expectedDisabledStyle));
        }
    }
}
