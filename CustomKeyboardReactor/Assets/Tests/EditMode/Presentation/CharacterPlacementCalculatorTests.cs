using NUnit.Framework;
using UnityEngine;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 캐릭터 기준점의 정규화 변환과 화면 내부 보정을 검증하는 클래스
    public sealed class CharacterPlacementCalculatorTests
    {
        // 정규화 중앙 위치가 표시 가능 범위 중앙으로 변환되는지 검증하는 함수
        [Test]
        public void CalculateScreenAnchor_WithCenteredPosition_ReturnsAvailableCenter()
        {
            Rect viewport = new Rect(0f, 0f, 1920f, 1040f); // 테스트 작업 영역
            Vector2 displayAreaSize = new Vector2(320f, 320f); // 테스트 표시 영역 크기

            Vector2 anchor = CharacterPlacementCalculator.CalculateScreenAnchor(
                new Vector2(0.5f, 0.5f),
                viewport,
                displayAreaSize);

            Assert.That(anchor.x, Is.EqualTo(960f).Within(0.001f));
            Assert.That(anchor.y, Is.EqualTo(360f).Within(0.001f));
        }

        // 범위 밖 기준점이 표시 영역 전체가 보이는 위치로 보정되는지 검증하는 함수
        [Test]
        public void ClampScreenAnchor_OutsideViewport_KeepsDisplayAreaInside()
        {
            Rect viewport = new Rect(0f, 0f, 1000f, 700f); // 테스트 작업 영역
            Vector2 displayAreaSize = new Vector2(200f, 300f); // 테스트 표시 영역 크기

            Vector2 anchor = CharacterPlacementCalculator.ClampScreenAnchor(
                new Vector2(1200f, 900f),
                viewport,
                displayAreaSize);

            Assert.That(anchor, Is.EqualTo(new Vector2(900f, 400f)));
        }

        // 화면 기준점이 정규화 위치로 왕복 변환되는지 검증하는 함수
        [Test]
        public void CalculateNormalizedPosition_AfterScreenConversion_RoundTrips()
        {
            Rect viewport = new Rect(-300f, 20f, 1600f, 900f); // 테스트 작업 영역
            Vector2 displayAreaSize = new Vector2(260f, 260f); // 테스트 표시 영역 크기
            Vector2 expected = new Vector2(0.25f, 0.8f); // 테스트 정규화 위치
            Vector2 anchor = CharacterPlacementCalculator.CalculateScreenAnchor(
                expected,
                viewport,
                displayAreaSize);

            Vector2 actual = CharacterPlacementCalculator.CalculateNormalizedPosition(
                anchor,
                viewport,
                displayAreaSize);

            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
        }
    }
}
