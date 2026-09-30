using NUnit.Framework;
using UnityEngine;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 모니터 전환 중 메뉴 위치와 화면 안정 판정을 검증하는 클래스
    public sealed class MonitorTransitionLayoutTests
    {
        // 새 모니터에서도 메뉴가 캐릭터와 같은 상대 위치를 유지하는지 검증하는 함수
        [Test]
        public void CalculateMenuScreenPosition_AfterMonitorChange_PreservesCharacterOffset()
        {
            MonitorTransitionLayout layout = new MonitorTransitionLayout(); // 테스트 모니터 전환 배치
            layout.Begin(
                new Vector2(700f, 500f),
                new Vector2(600f, 300f),
                new RectInt(1920, 0, 1920, 1040));

            Vector2 menuPosition = layout.CalculateMenuScreenPosition( // 전환 후 메뉴 위치
                new Vector2(900f, 400f));

            Assert.That(menuPosition, Is.EqualTo(new Vector2(1000f, 600f)));
        }

        // 반복 전환을 시작하면 화면 안정 상태와 상대 위치를 새 값으로 초기화하는지 검증하는 함수
        [Test]
        public void Begin_AfterCompletedTransition_ResetsLayoutState()
        {
            MonitorTransitionLayout layout = new MonitorTransitionLayout(); // 테스트 모니터 전환 배치
            layout.Begin(
                new Vector2(700f, 500f),
                new Vector2(600f, 300f),
                new RectInt(1920, 0, 1920, 1040));
            layout.IsViewportReady(new Vector2Int(1920, 1040));
            Assert.That(layout.IsViewportReady(new Vector2Int(1920, 1040)), Is.True);

            layout.Begin(
                new Vector2(1000f, 600f),
                new Vector2(900f, 400f),
                new RectInt(0, 0, 1536, 824));

            Assert.That(layout.IsViewportReady(new Vector2Int(1920, 1040)), Is.False);
            Assert.That(layout.IsViewportReady(new Vector2Int(1536, 824)), Is.False);
            Assert.That(layout.IsViewportReady(new Vector2Int(1536, 824)), Is.True);
            Assert.That(
                layout.CalculateMenuScreenPosition(new Vector2(600f, 300f)),
                Is.EqualTo(new Vector2(700f, 500f)));
        }

        // 대상 화면 크기가 연속으로 관찰된 뒤에만 전환 준비가 끝나는지 검증하는 함수
        [Test]
        public void IsViewportReady_DuringResize_WaitsForStableTargetSize()
        {
            MonitorTransitionLayout layout = new MonitorTransitionLayout(); // 테스트 모니터 전환 배치
            layout.Begin(
                new Vector2(700f, 500f),
                new Vector2(600f, 300f),
                new RectInt(1920, 0, 1920, 1040));

            Assert.That(layout.IsViewportReady(new Vector2Int(1536, 824)), Is.False);
            Assert.That(layout.IsViewportReady(new Vector2Int(1920, 1040)), Is.False);
            Assert.That(layout.IsViewportReady(new Vector2Int(1920, 1040)), Is.True);
        }
    }
}
