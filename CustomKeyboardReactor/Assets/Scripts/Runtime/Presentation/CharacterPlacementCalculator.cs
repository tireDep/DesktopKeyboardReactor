using System;
using UnityEngine;

namespace CustomKeyboardReactor
{
    // 정규화 위치와 화면 아래쪽 중앙 기준점을 변환하는 계산 모듈
    public static class CharacterPlacementCalculator
    {
        // 정규화 위치를 상호작용 경계가 화면 안에 머무는 기준점으로 변환하는 함수
        public static Vector2 CalculateScreenAnchor(
            Vector2 normalizedPosition,
            Rect viewport,
            Rect visibleBounds)
        {
            CalculateAnchorRange(
                viewport,
                visibleBounds,
                out Vector2 minimumAnchor,
                out Vector2 maximumAnchor);
            return new Vector2(
                Mathf.Lerp(minimumAnchor.x, maximumAnchor.x, Mathf.Clamp01(normalizedPosition.x)),
                Mathf.Lerp(minimumAnchor.y, maximumAnchor.y, Mathf.Clamp01(normalizedPosition.y)));
        }

        // 화면 기준점을 상호작용 경계가 화면 안에 머무르게 보정하는 함수
        public static Vector2 ClampScreenAnchor(
            Vector2 screenAnchor,
            Rect viewport,
            Rect visibleBounds)
        {
            CalculateAnchorRange(
                viewport,
                visibleBounds,
                out Vector2 minimumAnchor,
                out Vector2 maximumAnchor);
            return new Vector2(
                Mathf.Clamp(screenAnchor.x, minimumAnchor.x, maximumAnchor.x),
                Mathf.Clamp(screenAnchor.y, minimumAnchor.y, maximumAnchor.y));
        }

        // 화면 기준점을 영구 저장용 정규화 위치로 변환하는 함수
        public static Vector2 CalculateNormalizedPosition(
            Vector2 screenAnchor,
            Rect viewport,
            Rect visibleBounds)
        {
            Vector2 clampedAnchor = ClampScreenAnchor( // 화면 안으로 보정된 기준점
                screenAnchor,
                viewport,
                visibleBounds);
            CalculateAnchorRange(
                viewport,
                visibleBounds,
                out Vector2 minimumAnchor,
                out Vector2 maximumAnchor);
            return new Vector2(
                Mathf.Approximately(minimumAnchor.x, maximumAnchor.x)
                    ? 0.5f
                    : Mathf.InverseLerp(minimumAnchor.x, maximumAnchor.x, clampedAnchor.x),
                Mathf.Approximately(minimumAnchor.y, maximumAnchor.y)
                    ? 0f
                    : Mathf.InverseLerp(minimumAnchor.y, maximumAnchor.y, clampedAnchor.y));
        }

        // 이미지 픽셀에서 공통 표시 영역 기준 상호작용 경계를 계산하는 함수
        public static bool TryCalculateNormalizedVisibleBounds(
            Color32[] pixels,
            int width,
            int height,
            float alphaThreshold,
            out Rect normalizedVisibleBounds)
        {
            normalizedVisibleBounds = default;
            if (pixels == null)
            {
                throw new ArgumentNullException(nameof(pixels));
            }

            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            int expectedPixelCount = checked(width * height); // 예상 픽셀 수
            if (pixels.Length != expectedPixelCount)
            {
                throw new ArgumentException("Pixel count does not match the image size.", nameof(pixels));
            }

            float minimumAlpha = Mathf.Clamp01(alphaThreshold) * byte.MaxValue; // 상호작용 최소 알파값
            int minimumX = width; // 상호작용 픽셀 최소 가로 좌표
            int minimumY = height; // 상호작용 픽셀 최소 세로 좌표
            int maximumX = -1; // 상호작용 픽셀 최대 가로 좌표
            int maximumY = -1; // 상호작용 픽셀 최대 세로 좌표
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = y * width + x; // 현재 픽셀 인덱스
                    if (pixels[pixelIndex].a < minimumAlpha)
                    {
                        continue;
                    }

                    minimumX = Mathf.Min(minimumX, x);
                    minimumY = Mathf.Min(minimumY, y);
                    maximumX = Mathf.Max(maximumX, x);
                    maximumY = Mathf.Max(maximumY, y);
                }
            }

            if (maximumX < 0 || maximumY < 0)
            {
                return false;
            }

            float fitScale = Mathf.Min(1f / width, 1f / height); // 공통 표시 영역 맞춤 배율
            float fittedWidth = width * fitScale; // 맞춤 이미지 너비
            normalizedVisibleBounds = Rect.MinMaxRect(
                -fittedWidth * 0.5f + minimumX * fitScale,
                minimumY * fitScale,
                -fittedWidth * 0.5f + (maximumX + 1) * fitScale,
                (maximumY + 1) * fitScale);
            return true;
        }

        // 화면과 상호작용 경계로 허용 기준점 범위를 계산하는 함수
        private static void CalculateAnchorRange(
            Rect viewport,
            Rect visibleBounds,
            out Vector2 minimumAnchor,
            out Vector2 maximumAnchor)
        {
            minimumAnchor = new Vector2(
                viewport.xMin - visibleBounds.xMin,
                viewport.yMin - visibleBounds.yMin);
            maximumAnchor = new Vector2(
                viewport.xMax - visibleBounds.xMax,
                viewport.yMax - visibleBounds.yMax);

            if (minimumAnchor.x > maximumAnchor.x)
            {
                float centeredX = viewport.center.x - visibleBounds.center.x; // 가로 중앙 기준점
                minimumAnchor.x = centeredX;
                maximumAnchor.x = centeredX;
            }

            if (minimumAnchor.y > maximumAnchor.y)
            {
                float centeredY = viewport.center.y - visibleBounds.center.y; // 세로 중앙 기준점
                minimumAnchor.y = centeredY;
                maximumAnchor.y = centeredY;
            }
        }
    }
}
