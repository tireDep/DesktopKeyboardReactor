using UnityEngine;

namespace CustomKeyboardReactor
{
    // 모니터 전환 전후의 메뉴 위치와 화면 안정 상태를 계산하는 클래스
    public sealed class MonitorTransitionLayout
    {
        private const int RequiredStableViewportFrames = 2; // 필요한 연속 화면 일치 프레임 수

        private Vector2 _menuAnchorOffset; // 캐릭터 기준 메뉴 위치 간격
        private Vector2Int _targetViewportSize; // 전환 대상 화면 크기
        private int _matchingViewportFrameCount; // 연속 화면 일치 프레임 수

        // 전환 전 상대 위치와 대상 작업 영역을 보관하는 함수
        public void Begin(
            Vector2 menuScreenPosition,
            Vector2 characterScreenAnchor,
            RectInt targetWorkArea)
        {
            _menuAnchorOffset = menuScreenPosition - characterScreenAnchor;
            _targetViewportSize = new Vector2Int(
                targetWorkArea.width,
                targetWorkArea.height);
            _matchingViewportFrameCount = 0;
        }

        // 현재 화면 크기가 대상 크기로 연속 안정되었는지 반환하는 함수
        public bool IsViewportReady(Vector2Int viewportSize)
        {
            if (viewportSize == _targetViewportSize)
            {
                _matchingViewportFrameCount++;
            }
            else
            {
                _matchingViewportFrameCount = 0;
            }

            return _matchingViewportFrameCount >= RequiredStableViewportFrames;
        }

        // 전환 후 캐릭터 기준 상대 위치를 적용한 메뉴 화면 위치를 반환하는 함수
        public Vector2 CalculateMenuScreenPosition(Vector2 characterScreenAnchor)
        {
            return characterScreenAnchor + _menuAnchorOffset;
        }
    }
}
