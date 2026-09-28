using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 앱 데이터 저장과 손상 복구를 검증하는 클래스
    public sealed class AppDataStoreTests
    {
        private string _temporaryDirectory; // 테스트 데이터 루트 경로

        // 각 테스트의 격리된 데이터 폴더를 생성하는 함수
        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "CustomKeyboardReactorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
        }

        // 각 테스트가 만든 데이터 폴더를 정리하는 함수
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }
        }

        // 첫 실행 데이터가 유효한 기본 프리셋과 기본 설정을 포함하는지 검증하는 함수
        [Test]
        public void LoadOrCreate_OnFirstRun_CreatesValidDefaultData()
        {
            PresetAssetStore assetStore = new PresetAssetStore(_temporaryDirectory); // 프리셋 이미지 저장소
            AppDataStore dataStore = new AppDataStore(_temporaryDirectory, assetStore); // 앱 데이터 저장소

            AppData appData = dataStore.LoadOrCreate(); // 첫 실행 앱 데이터

            Assert.That(appData.SchemaVersion, Is.EqualTo(AppData.CurrentSchemaVersion));
            Assert.That(appData.TotalInputCount, Is.Zero);
            Assert.That(appData.Presets, Has.Count.EqualTo(1));
            Assert.That(appData.ActivePresetId, Is.EqualTo(appData.Presets[0].Id));
            Assert.That(appData.Presets[0].Name, Is.EqualTo("defaultPreset"));
            Assert.That(appData.Presets[0].NormalImages, Has.Count.EqualTo(1));
            Assert.That(assetStore.CanLoad(appData.Presets[0].NormalImages[0]), Is.True);
            Assert.That(appData.GlobalSettings.KeyboardReactionEnabled, Is.True);
            Assert.That(appData.GlobalSettings.MouseReactionEnabled, Is.True);
            Assert.That(appData.GlobalSettings.IdleTimeoutSeconds, Is.EqualTo(300));
            Assert.That(File.Exists(Path.Combine(_temporaryDirectory, "app-data.json")), Is.True);
        }

        // 저장 후 새 저장소에서 설정과 전체 입력 수가 복원되는지 검증하는 함수
        [Test]
        public void Save_ThenReload_RestoresPersistedValues()
        {
            PresetAssetStore assetStore = new PresetAssetStore(_temporaryDirectory); // 프리셋 이미지 저장소
            AppDataStore dataStore = new AppDataStore(_temporaryDirectory, assetStore); // 첫 앱 데이터 저장소
            AppData appData = dataStore.LoadOrCreate(); // 수정할 앱 데이터
            appData.TotalInputCount = 9876543210L;
            appData.GlobalSettings.KeyboardReactionEnabled = false;
            appData.GlobalSettings.UiScalePercent = 135;
            dataStore.Save(appData);

            PresetAssetStore reloadedAssetStore = new PresetAssetStore(_temporaryDirectory); // 재실행 이미지 저장소
            AppDataStore reloadedDataStore = new AppDataStore( // 재실행 앱 데이터 저장소
                _temporaryDirectory,
                reloadedAssetStore);
            AppData reloadedData = reloadedDataStore.LoadOrCreate(); // 재실행 후 앱 데이터

            Assert.That(reloadedData.TotalInputCount, Is.EqualTo(9876543210L));
            Assert.That(reloadedData.GlobalSettings.KeyboardReactionEnabled, Is.False);
            Assert.That(reloadedData.GlobalSettings.UiScalePercent, Is.EqualTo(135));
            Assert.That(reloadedData.ActivePresetId, Is.EqualTo(appData.ActivePresetId));
            Assert.That(reloadedAssetStore.CanLoad(reloadedData.Presets[0].NormalImages[0]), Is.True);
        }

        // 주 데이터가 손상되면 이전 정상 데이터로 복구되는지 검증하는 함수
        [Test]
        public void LoadOrCreate_WhenPrimaryDataIsCorrupt_RecoversBackup()
        {
            PresetAssetStore assetStore = new PresetAssetStore(_temporaryDirectory); // 프리셋 이미지 저장소
            AppDataStore dataStore = new AppDataStore(_temporaryDirectory, assetStore); // 앱 데이터 저장소
            AppData appData = dataStore.LoadOrCreate(); // 저장할 앱 데이터
            appData.TotalInputCount = 17L;
            dataStore.Save(appData);
            appData.TotalInputCount = 23L;
            dataStore.Save(appData);
            File.WriteAllText(Path.Combine(_temporaryDirectory, "app-data.json"), "{broken");

            AppDataStore recoveredStore = new AppDataStore( // 복구용 앱 데이터 저장소
                _temporaryDirectory,
                new PresetAssetStore(_temporaryDirectory));
            AppData recoveredData = recoveredStore.LoadOrCreate(); // 복구된 앱 데이터

            Assert.That(recoveredData.TotalInputCount, Is.EqualTo(17L));
            Assert.That(
                Directory.GetFiles(_temporaryDirectory, "app-data.corrupt-*.json").Any(),
                Is.True);
        }

        // 현재보다 새로운 스키마를 손상 데이터로 덮어쓰지 않는지 검증하는 함수
        [Test]
        public void LoadOrCreate_WithFutureSchema_ThrowsWithoutReplacingFile()
        {
            string dataFilePath = Path.Combine(_temporaryDirectory, "app-data.json"); // 미래 스키마 파일 경로
            AppData futureData = new AppData // 미래 스키마 앱 데이터
            {
                SchemaVersion = AppData.CurrentSchemaVersion + 1,
            };
            string futureJson = JsonUtility.ToJson(futureData, true); // 미래 스키마 JSON
            File.WriteAllText(dataFilePath, futureJson);
            AppDataStore dataStore = new AppDataStore( // 앱 데이터 저장소
                _temporaryDirectory,
                new PresetAssetStore(_temporaryDirectory));

            Assert.Throws<NotSupportedException>(() => dataStore.LoadOrCreate());
            Assert.That(File.ReadAllText(dataFilePath), Is.EqualTo(futureJson));
        }
    }
}
