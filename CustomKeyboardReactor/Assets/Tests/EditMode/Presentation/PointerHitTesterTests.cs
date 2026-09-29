using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 캐릭터 픽셀 알파 기반 상호작용 판정을 검증하는 클래스
    public sealed class PointerHitTesterTests
    {
        // 최소 알파값 픽셀은 상호작용하고 투명 픽셀은 통과하는지 검증하는 함수
        [Test]
        public void IsInteractive_UsesInclusiveAlphaThreshold()
        {
            GameObject canvasObject = new GameObject("Hit Test Canvas", typeof(Canvas)); // 테스트 캔버스
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); // 테스트 텍스처
            try
            {
                Canvas canvas = canvasObject.GetComponent<Canvas>(); // 테스트 화면 캔버스
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                GameObject characterObject = new GameObject("Character", typeof(RectTransform), typeof(RawImage)); // 테스트 캐릭터
                characterObject.transform.SetParent(canvasObject.transform, false);
                RectTransform characterRect = characterObject.GetComponent<RectTransform>(); // 테스트 캐릭터 영역
                characterRect.anchorMin = Vector2.zero;
                characterRect.anchorMax = Vector2.zero;
                characterRect.pivot = Vector2.zero;
                characterRect.anchoredPosition = new Vector2(100f, 100f);
                characterRect.sizeDelta = new Vector2(100f, 100f);

                Color transparent = new Color(1f, 1f, 1f, 0f); // 완전 투명 픽셀
                texture.SetPixels(new[] { transparent, transparent, transparent, transparent });
                texture.SetPixel(0, 0, new Color(1f, 1f, 1f, PointerHitTester.InteractiveAlphaThreshold));
                texture.Apply();
                Canvas.ForceUpdateCanvases();

                PointerHitTester hitTester = new PointerHitTester(); // 테스트 픽셀 판정기

                Assert.That(
                    hitTester.IsInteractive(new Vector2(125f, 125f), characterRect, texture),
                    Is.True);
                Assert.That(
                    hitTester.IsInteractive(new Vector2(175f, 175f), characterRect, texture),
                    Is.False);
                Assert.That(
                    hitTester.IsInteractive(new Vector2(50f, 50f), characterRect, texture),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(canvasObject);
            }
        }
    }
}
