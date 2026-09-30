using NUnit.Framework;
using UnityEngine;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 캐릭터 기준점의 정규화 변환과 화면 내부 보정을 검증하는 클래스
    public sealed class CharacterPlacementCalculatorTests
    {
        // 정규화 중앙 위치가 상호작용 가능 범위 중앙으로 변환되는지 검증하는 함수
        [Test]
        public void CalculateScreenAnchor_WithCenteredPosition_ReturnsAvailableCenter()
        {
            Rect viewport = new Rect(0f, 0f, 1920f, 1040f); // 테스트 작업 영역
            Rect visibleBounds = Rect.MinMaxRect(-160f, 0f, 160f, 320f); // 테스트 상호작용 경계

            Vector2 anchor = CharacterPlacementCalculator.CalculateScreenAnchor(
                new Vector2(0.5f, 0.5f),
                viewport,
                visibleBounds);

            Assert.That(anchor.x, Is.EqualTo(960f).Within(0.001f));
            Assert.That(anchor.y, Is.EqualTo(360f).Within(0.001f));
        }

        // 범위 밖 기준점에서 투명 여백을 제외한 상호작용 경계만 화면 안으로 보정하는지 검증하는 함수
        [Test]
        public void ClampScreenAnchor_OutsideViewport_KeepsVisibleBoundsInside()
        {
            Rect viewport = new Rect(0f, 0f, 1000f, 700f); // 테스트 작업 영역
            Rect visibleBounds = Rect.MinMaxRect(-60f, 20f, 80f, 300f); // 비대칭 상호작용 경계

            Vector2 anchor = CharacterPlacementCalculator.ClampScreenAnchor(
                new Vector2(1200f, 900f),
                viewport,
                visibleBounds);

            Assert.That(anchor, Is.EqualTo(new Vector2(920f, 400f)));
        }

        // 화면 기준점이 정규화 위치로 왕복 변환되는지 검증하는 함수
        [Test]
        public void CalculateNormalizedPosition_AfterScreenConversion_RoundTrips()
        {
            Rect viewport = new Rect(-300f, 20f, 1600f, 900f); // 테스트 작업 영역
            Rect visibleBounds = Rect.MinMaxRect(-100f, 30f, 140f, 260f); // 테스트 상호작용 경계
            Vector2 expected = new Vector2(0.25f, 0.8f); // 테스트 정규화 위치
            Vector2 anchor = CharacterPlacementCalculator.CalculateScreenAnchor(
                expected,
                viewport,
                visibleBounds);

            Vector2 actual = CharacterPlacementCalculator.CalculateNormalizedPosition(
                anchor,
                viewport,
                visibleBounds);

            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
        }

        // 알파 임계값 이상인 픽셀만 정규화 상호작용 경계에 포함하는지 검증하는 함수
        [Test]
        public void TryCalculateNormalizedVisibleBounds_WithTransparentMargins_ReturnsOpaqueBounds()
        {
            Color32[] pixels = new Color32[16]; // 테스트 이미지 픽셀 목록
            pixels[0] = new Color32(255, 255, 255, 25);
            pixels[5] = new Color32(255, 255, 255, 26);
            pixels[14] = new Color32(255, 255, 255, 255);

            bool result = CharacterPlacementCalculator.TryCalculateNormalizedVisibleBounds(
                pixels,
                4,
                4,
                PointerHitTester.InteractiveAlphaThreshold,
                out Rect visibleBounds);

            Assert.That(result, Is.True);
            Assert.That(visibleBounds.xMin, Is.EqualTo(-0.25f).Within(0.001f));
            Assert.That(visibleBounds.yMin, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(visibleBounds.xMax, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(visibleBounds.yMax, Is.EqualTo(1f).Within(0.001f));
        }

        // 상호작용 픽셀이 없는 이미지에서 경계 계산 실패를 반환하는지 검증하는 함수
        [Test]
        public void TryCalculateNormalizedVisibleBounds_WithoutInteractivePixel_ReturnsFalse()
        {
            Color32[] pixels = new Color32[4]; // 완전 투명 테스트 픽셀 목록

            bool result = CharacterPlacementCalculator.TryCalculateNormalizedVisibleBounds(
                pixels,
                2,
                2,
                PointerHitTester.InteractiveAlphaThreshold,
                out Rect visibleBounds);

            Assert.That(result, Is.False);
            Assert.That(visibleBounds, Is.EqualTo(default(Rect)));
        }
    }
}
