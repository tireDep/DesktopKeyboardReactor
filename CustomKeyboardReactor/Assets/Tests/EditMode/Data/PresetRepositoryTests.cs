using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 프리셋과 공용 설정 Repository 동작을 검증하는 클래스
    public sealed class PresetRepositoryTests
    {
        private string _temporaryDirectory; // 테스트 데이터 루트 경로
        private PresetAssetStore _assetStore; // 테스트 프리셋 이미지 저장소
        private AppDataStore _dataStore; // 테스트 앱 데이터 저장소
        private PresetRepository _presetRepository; // 테스트 프리셋 Repository
        private UserSettingsRepository _settingsRepository; // 테스트 공용 설정 Repository

        // 각 테스트의 Repository와 격리된 데이터 폴더를 생성하는 함수
        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "CustomKeyboardReactorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
            _assetStore = new PresetAssetStore(_temporaryDirectory);
            _dataStore = new AppDataStore(_temporaryDirectory, _assetStore);
            _presetRepository = new PresetRepository(_dataStore, _assetStore);
            _settingsRepository = new UserSettingsRepository(_dataStore);
        }

        // 각 테스트가 만든 Repository 데이터를 정리하는 함수
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }
        }

        // 프리셋과 설정 및 전체 입력 수가 새 Repository에서 복원되는지 검증하는 함수
        [Test]
        public void SaveAndReload_RestoresActivePresetSettingsImagesAndCount()
        {
            PresetData draft = _presetRepository.CreateDraft("userPreset"); // 적용할 프리셋 초안
            string sourcePath = CreateSourceImage(); // 가져올 이미지 경로
            draft.NormalImages.Add(_assetStore.Import(draft.Id, sourcePath));
            draft.HoverTextTemplate = "{TOTAL_INPUT_COUNT}";
            draft.CharacterScalePercent = 125;
            _presetRepository.Apply(draft);
            GlobalSettingsData settings = _settingsRepository.GetSettings(); // 변경할 공용 설정
            settings.KeyboardReactionEnabled = false;
            settings.IdleTimeoutSeconds = 720;
            settings.UiScalePercent = 145;
            _settingsRepository.SaveSettings(settings);
            _settingsRepository.SaveTotalInputCount(123456789L);

            PresetAssetStore reloadedAssetStore = new PresetAssetStore(_temporaryDirectory); // 재실행 이미지 저장소
            AppDataStore reloadedDataStore = new AppDataStore( // 재실행 앱 데이터 저장소
                _temporaryDirectory,
                reloadedAssetStore);
            PresetRepository reloadedPresetRepository = new PresetRepository( // 재실행 프리셋 Repository
                reloadedDataStore,
                reloadedAssetStore);
            UserSettingsRepository reloadedSettingsRepository = new UserSettingsRepository( // 재실행 설정 Repository
                reloadedDataStore);
            PresetData activePreset = reloadedPresetRepository.GetActive(); // 복원된 활성 프리셋
            GlobalSettingsData reloadedSettings = reloadedSettingsRepository.GetSettings(); // 복원된 공용 설정

            Assert.That(activePreset.Id, Is.EqualTo(draft.Id));
            Assert.That(activePreset.Name, Is.EqualTo("userPreset"));
            Assert.That(activePreset.NormalImages, Has.Count.EqualTo(1));
            Assert.That(reloadedAssetStore.CanLoad(activePreset.NormalImages[0]), Is.True);
            Assert.That(reloadedSettings.KeyboardReactionEnabled, Is.False);
            Assert.That(reloadedSettings.IdleTimeoutSeconds, Is.EqualTo(720));
            Assert.That(reloadedSettings.UiScalePercent, Is.EqualTo(145));
            Assert.That(reloadedSettingsRepository.GetTotalInputCount(), Is.EqualTo(123456789L));
        }

        // 프리셋 복제가 이미지 파일을 독립적으로 복사하는지 검증하는 함수
        [Test]
        public void Duplicate_ThenDeleteSource_KeepsDuplicatedPresetUsable()
        {
            PresetData sourcePreset = _presetRepository.GetActive(); // 복제할 활성 프리셋

            PresetData duplicate = _presetRepository.Duplicate( // 복제된 프리셋
                sourcePreset.Id,
                "duplicatePreset");
            _presetRepository.Delete(sourcePreset.Id);

            Assert.That(_presetRepository.GetAll(), Has.Count.EqualTo(1));
            Assert.That(_assetStore.CanLoad(sourcePreset.NormalImages[0]), Is.False);
            Assert.That(_assetStore.CanLoad(duplicate.NormalImages[0]), Is.True);
        }

        // 마지막 프리셋 삭제 시 새 기본 프리셋을 생성하는지 검증하는 함수
        [Test]
        public void Delete_LastPreset_CreatesEditableDefaultPreset()
        {
            PresetData originalPreset = _presetRepository.GetActive(); // 삭제할 마지막 프리셋

            _presetRepository.Delete(originalPreset.Id);

            PresetData replacementPreset = _presetRepository.GetActive(); // 새 기본 프리셋
            Assert.That(replacementPreset.Id, Is.Not.EqualTo(originalPreset.Id));
            Assert.That(replacementPreset.Name, Is.EqualTo("defaultPreset"));
            Assert.That(replacementPreset.NormalImages, Has.Count.EqualTo(1));
            Assert.That(_assetStore.CanLoad(replacementPreset.NormalImages[0]), Is.True);
        }

        // 일반 이미지가 없는 프리셋 적용을 차단하는지 검증하는 함수
        [Test]
        public void Apply_WithoutNormalImage_Throws()
        {
            PresetData draft = _presetRepository.CreateDraft("emptyPreset"); // 일반 이미지 없는 초안

            Assert.Throws<InvalidDataException>(() => _presetRepository.Apply(draft));
        }

        // 공용 설정 값이 제품 허용 범위로 보정되는지 검증하는 함수
        [Test]
        public void SaveSettings_OutOfRangeValues_ClampsToProductLimits()
        {
            GlobalSettingsData settings = _settingsRepository.GetSettings(); // 범위를 벗어날 공용 설정
            settings.IdleTimeoutSeconds = 1;
            settings.UiScalePercent = 999;
            settings.NormalizedAnchorPosition = new Vector2(-2f, 3f);

            _settingsRepository.SaveSettings(settings);
            GlobalSettingsData savedSettings = _settingsRepository.GetSettings(); // 보정된 공용 설정

            Assert.That(
                savedSettings.IdleTimeoutSeconds,
                Is.EqualTo(GlobalSettingsData.MinimumIdleTimeoutSeconds));
            Assert.That(
                savedSettings.UiScalePercent,
                Is.EqualTo(GlobalSettingsData.MaximumUiScalePercent));
            Assert.That(savedSettings.NormalizedAnchorPosition, Is.EqualTo(new Vector2(0f, 1f)));
        }

        // 가져온 초안 이미지를 적용할 때 프리셋 소유 폴더로 확정하는지 검증하는 함수
        [Test]
        public void Apply_ImportedDraftImage_FinalizesOwnedImage()
        {
            PresetData draft = _presetRepository.CreateDraft("draftPreset"); // 적용할 프리셋 초안
            ImageAssetData draftImage = _assetStore.Import(draft.Id, CreateSourceImage()); // 가져온 초안 이미지
            string draftRelativePath = draftImage.RelativePath; // 적용 전 초안 이미지 경로
            draft.NormalImages.Add(draftImage);

            _presetRepository.Apply(draft);
            PresetData activePreset = _presetRepository.GetActive(); // 적용된 활성 프리셋

            Assert.That(activePreset.NormalImages[0].RelativePath, Does.StartWith($"presets/{draft.Id}/images/"));
            Assert.That(activePreset.NormalImages[0].RelativePath, Is.Not.EqualTo(draftRelativePath));
            Assert.That(_assetStore.CanLoad(draftImage), Is.False);
            Assert.That(_assetStore.CanLoadOwnedImage(draft.Id, activePreset.NormalImages[0]), Is.True);
        }

        // 다른 프리셋이 소유한 이미지 참조를 적용하지 않는지 검증하는 함수
        [Test]
        public void Apply_ImageOwnedByAnotherPreset_Throws()
        {
            PresetData sourcePreset = _presetRepository.GetActive(); // 이미지 소유 원본 프리셋
            PresetData draft = _presetRepository.CreateDraft("foreignImagePreset"); // 잘못된 이미지 참조 초안
            draft.NormalImages.Add(sourcePreset.NormalImages[0]);

            Assert.Throws<InvalidDataException>(() => _presetRepository.Apply(draft));
        }

        // 캐릭터 크기가 5퍼센트 단위가 아니면 적용하지 않는지 검증하는 함수
        [Test]
        public void Apply_CharacterScaleOutsideFivePercentSteps_Throws()
        {
            PresetData draft = _presetRepository.CreateDraft("invalidScalePreset"); // 잘못된 크기 프리셋 초안
            draft.NormalImages.Add(_assetStore.Import(draft.Id, CreateSourceImage()));
            draft.CharacterScalePercent = 126;

            Assert.Throws<ArgumentOutOfRangeException>(() => _presetRepository.Apply(draft));
        }

        // UI 크기를 가장 가까운 5퍼센트 단위로 저장하는지 검증하는 함수
        [Test]
        public void SaveSettings_UiScaleOutsideFivePercentSteps_NormalizesValue()
        {
            GlobalSettingsData settings = _settingsRepository.GetSettings(); // 변경할 공용 설정
            settings.UiScalePercent = 137;

            _settingsRepository.SaveSettings(settings);

            Assert.That(_settingsRepository.GetSettings().UiScalePercent, Is.EqualTo(135));
        }

        // 오래된 Repository의 카운트 저장이 최신 프리셋을 덮어쓰지 않는지 검증하는 함수
        [Test]
        public void SaveTotalInputCount_FromStaleRepository_PreservesLatestPresetChanges()
        {
            AppDataStore staleDataStore = new AppDataStore( // 오래된 상태를 보관할 앱 데이터 저장소
                _temporaryDirectory,
                new PresetAssetStore(_temporaryDirectory));
            UserSettingsRepository staleSettingsRepository = new UserSettingsRepository( // 오래된 공용 설정 Repository
                staleDataStore);
            staleSettingsRepository.GetSettings();
            PresetData draft = _presetRepository.CreateDraft("latestPreset"); // 나중에 저장할 프리셋 초안
            draft.NormalImages.Add(_assetStore.Import(draft.Id, CreateSourceImage()));
            _presetRepository.Apply(draft);

            staleSettingsRepository.SaveTotalInputCount(77L);

            AppDataStore reloadedDataStore = new AppDataStore( // 결과 확인 앱 데이터 저장소
                _temporaryDirectory,
                new PresetAssetStore(_temporaryDirectory));
            AppData reloadedData = reloadedDataStore.LoadOrCreate(); // 다시 불러온 앱 데이터
            Assert.That(reloadedData.TotalInputCount, Is.EqualTo(77L));
            Assert.That(reloadedData.ActivePresetId, Is.EqualTo(draft.Id));
            Assert.That(reloadedData.Presets.Exists(preset => preset.Id == draft.Id), Is.True);
        }

        // 프리셋에 사용할 테스트 PNG 파일을 생성하는 함수
        private string CreateSourceImage()
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); // 테스트 원본 텍스처
            texture.SetPixels(new[]
            {
                Color.red,
                Color.green,
                Color.blue,
                Color.white,
            });
            texture.Apply(false, false);
            byte[] imageBytes = ImageConversion.EncodeToPNG(texture); // 테스트 PNG 데이터
            Object.DestroyImmediate(texture);

            string sourcePath = Path.Combine(_temporaryDirectory, "import-source.png"); // 테스트 PNG 경로
            File.WriteAllBytes(sourcePath, imageBytes);
            return sourcePath;
        }
    }
}
