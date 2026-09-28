using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 프리셋 이미지 가져오기와 소유권 관리를 검증하는 클래스
    public sealed class PresetAssetStoreTests
    {
        private string _temporaryDirectory; // 테스트 데이터 루트 경로
        private PresetAssetStore _assetStore; // 테스트 프리셋 이미지 저장소

        // 각 테스트의 격리된 이미지 저장소를 생성하는 함수
        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "CustomKeyboardReactorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
            _assetStore = new PresetAssetStore(_temporaryDirectory);
        }

        // 각 테스트가 만든 이미지 폴더를 정리하는 함수
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }
        }

        // 지원 이미지 형식을 가져와 읽기 가능한 텍스처로 불러오는지 검증하는 함수
        [TestCase(".png")]
        [TestCase(".jpg")]
        [TestCase(".jpeg")]
        public void Import_SupportedImage_CopiesAndLoadsReadableTexture(string extension)
        {
            string sourcePath = CreateSourceImage(extension); // 가져올 원본 이미지 경로
            string presetId = Guid.NewGuid().ToString("N"); // 이미지 소유 프리셋 ID

            ImageAssetData image = _assetStore.Import(presetId, sourcePath); // 가져온 이미지 데이터
            Texture2D texture = _assetStore.LoadTexture(image); // 불러온 런타임 텍스처

            try
            {
            Assert.That(image.RelativePath, Does.StartWith($"drafts/{presetId}/images/"));
                Assert.That(image.RelativePath, Does.EndWith(extension));
                Assert.That(image.OriginalWidth, Is.EqualTo(3));
                Assert.That(image.OriginalHeight, Is.EqualTo(2));
                Assert.That(texture.width, Is.EqualTo(3));
                Assert.That(texture.height, Is.EqualTo(2));
                Assert.That(texture.isReadable, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        // 프리셋 복제 이미지가 원본 삭제 후에도 독립적으로 유지되는지 검증하는 함수
        [Test]
        public void DuplicatePresetAssets_ThenDeleteSource_KeepsTargetImages()
        {
            PresetData sourcePreset = _assetStore.CreateDefaultPreset(); // 원본 기본 프리셋
            PresetData targetPreset = new PresetData // 복제 대상 프리셋
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = "duplicatePreset",
            };

            _assetStore.DuplicatePresetAssets(sourcePreset, targetPreset);
            _assetStore.DeletePresetAssets(sourcePreset.Id);

            Assert.That(targetPreset.NormalImages, Has.Count.EqualTo(1));
            Assert.That(
                targetPreset.NormalImages[0].RelativePath,
                Is.Not.EqualTo(sourcePreset.NormalImages[0].RelativePath));
            Assert.That(_assetStore.CanLoad(sourcePreset.NormalImages[0]), Is.False);
            Assert.That(_assetStore.CanLoad(targetPreset.NormalImages[0]), Is.True);
        }

        // 지원하지 않는 확장자의 파일을 가져오지 않는지 검증하는 함수
        [Test]
        public void Import_UnsupportedExtension_Throws()
        {
            string sourcePath = Path.Combine(_temporaryDirectory, "source.gif"); // 미지원 이미지 경로
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3 });

            Assert.Throws<NotSupportedException>(
                () => _assetStore.Import(Guid.NewGuid().ToString("N"), sourcePath));
        }

        // 버린 편집 초안의 임시 이미지를 제거하는지 검증하는 함수
        [Test]
        public void DiscardDraftAssets_ImportedImage_RemovesTemporaryCopy()
        {
            string sourcePath = CreateSourceImage(".png"); // 가져올 원본 이미지 경로
            string presetId = Guid.NewGuid().ToString("N"); // 이미지 소유 프리셋 ID
            ImageAssetData image = _assetStore.Import(presetId, sourcePath); // 초안 이미지 데이터

            _assetStore.DiscardDraftAssets(presetId);

            Assert.That(_assetStore.CanLoad(image), Is.False);
        }

        // 테스트용 정적 이미지 파일을 생성하는 함수
        private string CreateSourceImage(string extension)
        {
            Texture2D texture = new Texture2D(3, 2, TextureFormat.RGBA32, false); // 테스트 원본 텍스처
            texture.SetPixels(new[]
            {
                Color.red,
                Color.green,
                Color.blue,
                Color.white,
                Color.black,
                Color.clear,
            });
            texture.Apply(false, false);
            byte[] imageBytes = extension == ".png" // 인코딩된 테스트 이미지 데이터
                ? ImageConversion.EncodeToPNG(texture)
                : ImageConversion.EncodeToJPG(texture);
            Object.DestroyImmediate(texture);

            string sourcePath = Path.Combine(_temporaryDirectory, "source" + extension); // 테스트 원본 이미지 경로
            File.WriteAllBytes(sourcePath, imageBytes);
            return sourcePath;
        }
    }
}
