using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 캐릭터 표시 계층과 기준점 구성을 검증하는 클래스
    public sealed class CharacterPresenterTests
    {
        // 캐릭터 이미지가 화면 중앙의 아래쪽 중앙 기준점을 사용하는지 검증하는 함수
        [Test]
        public void Initialize_CreatesBottomCenterCharacterHierarchy()
        {
            GameObject presenterObject = new GameObject("Character Presenter Test"); // 테스트 표시기 오브젝트
            try
            {
                CharacterPresenter presenter = presenterObject.AddComponent<CharacterPresenter>(); // 테스트 캐릭터 표시기
                PresetAssetStore assetStore = new PresetAssetStore(Application.temporaryCachePath); // 테스트 이미지 저장소

                presenter.Initialize(assetStore);

                RectTransform characterRect = presenter.CharacterRect; // 캐릭터 이미지 영역
                RectTransform displayArea = characterRect.parent as RectTransform; // 공통 표시 영역
                Canvas canvas = presenterObject.GetComponentInChildren<Canvas>(); // 캐릭터 캔버스

                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(displayArea, Is.Not.Null);
                Assert.That(displayArea.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(displayArea.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(displayArea.pivot, Is.EqualTo(new Vector2(0.5f, 0f)));
                Assert.That(characterRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
                Assert.That(characterRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0f)));
                Assert.That(characterRect.pivot, Is.EqualTo(new Vector2(0.5f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
            }
        }
    }
}
