using System;
using System.IO;
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

        // 일반 이미지와 대기 이미지의 상호작용 경계를 합쳐 적용하는지 검증하는 함수
        [Test]
        public void SetActivePreset_WithNormalAndIdleImages_CombinesVisibleBounds()
        {
            string temporaryDirectory = Path.Combine( // 테스트 데이터 폴더 경로
                Application.temporaryCachePath,
                $"CharacterPresenterTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryDirectory);
            GameObject presenterObject = new GameObject("Character Presenter Bounds Test"); // 테스트 표시기 오브젝트
            try
            {
                PresetAssetStore assetStore = new PresetAssetStore(temporaryDirectory); // 테스트 이미지 저장소
                PresetData preset = new PresetData // 테스트 활성 프리셋
                {
                    Id = "bounds-preset",
                    Name = "boundsPreset",
                };
                string normalSourcePath = Path.Combine(temporaryDirectory, "normal.png"); // 일반 이미지 원본 경로
                string idleSourcePath = Path.Combine(temporaryDirectory, "idle.png"); // 대기 이미지 원본 경로
                CreateTestImage(normalSourcePath, 1, 0);
                CreateTestImage(idleSourcePath, 3, 2);
                preset.NormalImages.Add(assetStore.Import(preset.Id, normalSourcePath));
                preset.IdleImages.Add(assetStore.Import(preset.Id, idleSourcePath));

                CharacterPresenter presenter = presenterObject.AddComponent<CharacterPresenter>(); // 테스트 캐릭터 표시기
                presenter.Initialize(assetStore);
                presenter.SetActivePreset(preset);

                Rect visibleBounds = presenter.NormalizedVisibleBounds; // 계산된 프리셋 상호작용 경계
                Assert.That(visibleBounds.xMin, Is.EqualTo(-0.25f).Within(0.001f));
                Assert.That(visibleBounds.yMin, Is.EqualTo(0f).Within(0.001f));
                Assert.That(visibleBounds.xMax, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(visibleBounds.yMax, Is.EqualTo(0.75f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, true);
                }
            }
        }

        // 지정한 픽셀만 불투명한 테스트 PNG를 생성하는 함수
        private static void CreateTestImage(string path, int opaqueX, int opaqueY)
        {
            const int imageSize = 4; // 테스트 이미지 한 변 크기
            Texture2D texture = new Texture2D( // 테스트 이미지 텍스처
                imageSize,
                imageSize,
                TextureFormat.RGBA32,
                false);
            try
            {
                Color32[] pixels = new Color32[imageSize * imageSize]; // 테스트 이미지 픽셀 목록
                pixels[opaqueY * imageSize + opaqueX] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
