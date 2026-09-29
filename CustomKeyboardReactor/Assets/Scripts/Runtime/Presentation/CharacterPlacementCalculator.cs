using UnityEngine;

namespace CustomKeyboardReactor
{
    // 정규화 위치와 화면 아래쪽 중앙 기준점을 변환하는 계산 모듈
    public static class CharacterPlacementCalculator
    {
        // 정규화 위치를 표시 영역이 화면 안에 머무는 기준점으로 변환하는 함수
        public static Vector2 CalculateScreenAnchor(
            Vector2 normalizedPosition,
            Rect viewport,
            Vector2 displayAreaSize)
        {
            Vector2 minimumAnchor = CalculateMinimumAnchor(viewport, displayAreaSize); // 허용 최소 기준점
            Vector2 maximumAnchor = CalculateMaximumAnchor(viewport, displayAreaSize); // 허용 최대 기준점
            return new Vector2(
                Mathf.Lerp(minimumAnchor.x, maximumAnchor.x, Mathf.Clamp01(normalizedPosition.x)),
                Mathf.Lerp(minimumAnchor.y, maximumAnchor.y, Mathf.Clamp01(normalizedPosition.y)));
        }

        // 화면 기준점을 표시 영역이 화면 안에 머무르게 보정하는 함수
        public static Vector2 ClampScreenAnchor(
            Vector2 screenAnchor,
            Rect viewport,
            Vector2 displayAreaSize)
        {
            Vector2 minimumAnchor = CalculateMinimumAnchor(viewport, displayAreaSize); // 허용 최소 기준점
            Vector2 maximumAnchor = CalculateMaximumAnchor(viewport, displayAreaSize); // 허용 최대 기준점
            return new Vector2(
                Mathf.Clamp(screenAnchor.x, minimumAnchor.x, maximumAnchor.x),
                Mathf.Clamp(screenAnchor.y, minimumAnchor.y, maximumAnchor.y));
        }

        // 화면 기준점을 영구 저장용 정규화 위치로 변환하는 함수
        public static Vector2 CalculateNormalizedPosition(
            Vector2 screenAnchor,
            Rect viewport,
            Vector2 displayAreaSize)
        {
            Vector2 clampedAnchor = ClampScreenAnchor( // 화면 안으로 보정된 기준점
                screenAnchor,
                viewport,
                displayAreaSize);
            Vector2 minimumAnchor = CalculateMinimumAnchor(viewport, displayAreaSize); // 허용 최소 기준점
            Vector2 maximumAnchor = CalculateMaximumAnchor(viewport, displayAreaSize); // 허용 최대 기준점
            return new Vector2(
                Mathf.Approximately(minimumAnchor.x, maximumAnchor.x)
                    ? 0.5f
                    : Mathf.InverseLerp(minimumAnchor.x, maximumAnchor.x, clampedAnchor.x),
                Mathf.Approximately(minimumAnchor.y, maximumAnchor.y)
                    ? 0f
                    : Mathf.InverseLerp(minimumAnchor.y, maximumAnchor.y, clampedAnchor.y));
        }

        // 아래쪽 중앙 피벗의 허용 최소 위치를 계산하는 함수
        private static Vector2 CalculateMinimumAnchor(Rect viewport, Vector2 displayAreaSize)
        {
            float width = Mathf.Min(Mathf.Max(0f, displayAreaSize.x), viewport.width); // 화면 이내 표시 너비
            return new Vector2(viewport.xMin + width * 0.5f, viewport.yMin);
        }

        // 아래쪽 중앙 피벗의 허용 최대 위치를 계산하는 함수
        private static Vector2 CalculateMaximumAnchor(Rect viewport, Vector2 displayAreaSize)
        {
            float width = Mathf.Min(Mathf.Max(0f, displayAreaSize.x), viewport.width); // 화면 이내 표시 너비
            float height = Mathf.Min(Mathf.Max(0f, displayAreaSize.y), viewport.height); // 화면 이내 표시 높이
            return new Vector2(viewport.xMax - width * 0.5f, viewport.yMax - height);
        }
    }
}
