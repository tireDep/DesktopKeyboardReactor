using UnityEngine;

namespace CustomKeyboardReactor
{
    // 화면 좌표가 캐릭터의 상호작용 가능한 픽셀인지 판정하는 모듈
    public sealed class PointerHitTester
    {
        public const float InteractiveAlphaThreshold = 0.1f; // 상호작용 최소 알파값

        // 메뉴 또는 캐릭터 픽셀의 상호작용 가능 여부를 반환하는 함수
        public bool IsInteractive(
            Vector2 screenPosition,
            RectTransform characterRect,
            Texture2D characterTexture,
            RectTransform interactiveUiRect = null)
        {
            if (interactiveUiRect != null &&
                interactiveUiRect.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    interactiveUiRect,
                    screenPosition))
            {
                return true;
            }

            if (characterRect == null ||
                characterTexture == null ||
                !characterRect.gameObject.activeInHierarchy ||
                !RectTransformUtility.RectangleContainsScreenPoint(characterRect, screenPosition))
            {
                return false;
            }

            Vector2 localPoint; // 캐릭터 영역 내부 좌표
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    characterRect,
                    screenPosition,
                    null,
                    out localPoint))
            {
                return false;
            }

            Rect localRect = characterRect.rect; // 캐릭터 로컬 사각형
            float normalizedX = Mathf.InverseLerp(localRect.xMin, localRect.xMax, localPoint.x); // 텍스처 가로 좌표
            float normalizedY = Mathf.InverseLerp(localRect.yMin, localRect.yMax, localPoint.y); // 텍스처 세로 좌표
            int pixelX = Mathf.Clamp( // 텍스처 픽셀 가로 인덱스
                Mathf.FloorToInt(normalizedX * characterTexture.width),
                0,
                characterTexture.width - 1);
            int pixelY = Mathf.Clamp( // 텍스처 픽셀 세로 인덱스
                Mathf.FloorToInt(normalizedY * characterTexture.height),
                0,
                characterTexture.height - 1);

            try
            {
                return characterTexture.GetPixel(pixelX, pixelY).a >= InteractiveAlphaThreshold;
            }
            catch (UnityException)
            {
                return false;
            }
        }
    }
}
