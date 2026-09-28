using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CustomKeyboardReactor
{
    // 앱 데이터 JSON의 로드와 원자적 저장을 관리하는 클래스
    public sealed class AppDataStore
    {
        private const string DataFileName = "app-data.json"; // 앱 데이터 파일 이름
        private const string TemporaryFileName = "app-data.tmp"; // 임시 데이터 파일 이름
        private const string BackupFileName = "app-data.backup.json"; // 이전 정상 데이터 파일 이름

        private readonly string _dataRootPath; // 앱 데이터 루트 경로
        private readonly string _dataFilePath; // 앱 데이터 파일 경로
        private readonly string _temporaryFilePath; // 임시 데이터 파일 경로
        private readonly string _backupFilePath; // 이전 정상 데이터 파일 경로
        private readonly PresetAssetStore _presetAssetStore; // 프리셋 이미지 저장소
        private AppData _loadedData; // 현재 로드된 앱 데이터

        // 저장 경로와 프리셋 이미지 저장소를 연결하는 생성자
        public AppDataStore(string dataRootPath, PresetAssetStore presetAssetStore)
        {
            if (string.IsNullOrWhiteSpace(dataRootPath))
            {
                throw new ArgumentException("Data root path is required.", nameof(dataRootPath));
            }

            _presetAssetStore = presetAssetStore ??
                                throw new ArgumentNullException(nameof(presetAssetStore));
            _dataRootPath = Path.GetFullPath(dataRootPath);
            _dataFilePath = Path.Combine(_dataRootPath, DataFileName);
            _temporaryFilePath = Path.Combine(_dataRootPath, TemporaryFileName);
            _backupFilePath = Path.Combine(_dataRootPath, BackupFileName);
        }

        // 저장 데이터를 불러오고 첫 실행 또는 손상 상태를 복구하는 함수
        public AppData LoadOrCreate()
        {
            if (_loadedData != null)
            {
                return _loadedData;
            }

            Directory.CreateDirectory(_dataRootPath);
            if (!File.Exists(_dataFilePath))
            {
                _loadedData = CreateDefaultData();
                Save(_loadedData);
                return _loadedData;
            }

            try
            {
                _loadedData = ReadData(_dataFilePath);
                NormalizeStructure(_loadedData);
                return _loadedData;
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch (Exception exception) when (IsCorruptDataException(exception))
            {
                MoveCorruptedPrimaryFile();
                _loadedData = TryReadBackup(out AppData backupData)
                    ? backupData
                    : CreateDefaultData();
                NormalizeStructure(_loadedData);
                Save(_loadedData);
                return _loadedData;
            }
        }

        // 디스크의 최신 앱 데이터를 다시 불러오는 함수
        public AppData LoadLatest()
        {
            _loadedData = null;
            return LoadOrCreate();
        }

        // 앱 데이터를 임시 파일 작성 후 기존 파일과 교체하여 저장하는 함수
        public void Save(AppData appData)
        {
            if (appData == null)
            {
                throw new ArgumentNullException(nameof(appData));
            }

            Directory.CreateDirectory(_dataRootPath);
            NormalizeStructure(appData);
            appData.SchemaVersion = AppData.CurrentSchemaVersion;
            string json = JsonUtility.ToJson(appData, true); // 직렬화된 앱 데이터

            try
            {
                WriteTemporaryFile(json);
                if (File.Exists(_dataFilePath))
                {
                    if (File.Exists(_backupFilePath))
                    {
                        File.Delete(_backupFilePath);
                    }

                    File.Replace(_temporaryFilePath, _dataFilePath, _backupFilePath, true);
                }
                else
                {
                    File.Move(_temporaryFilePath, _dataFilePath);
                }

                _loadedData = appData;
            }
            finally
            {
                if (File.Exists(_temporaryFilePath))
                {
                    File.Delete(_temporaryFilePath);
                }
            }
        }

        // 현재 스키마의 기본 앱 데이터를 생성하는 함수
        private AppData CreateDefaultData()
        {
            PresetData defaultPreset = _presetAssetStore.CreateDefaultPreset(); // 새 기본 프리셋
            AppData appData = new AppData // 새 기본 앱 데이터
            {
                SchemaVersion = AppData.CurrentSchemaVersion,
                TotalInputCount = 0L,
                ActivePresetId = defaultPreset.Id,
                GlobalSettings = new GlobalSettingsData(),
                Presets = new List<PresetData>
                {
                    defaultPreset,
                },
            };
            return appData;
        }

        // JSON 파일을 읽고 지원하는 스키마의 앱 데이터로 변환하는 함수
        private static AppData ReadData(string filePath)
        {
            string json = File.ReadAllText(filePath, Encoding.UTF8); // 저장된 JSON 문자열
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("App data file is empty.");
            }

            SchemaProbe schemaProbe = JsonUtility.FromJson<SchemaProbe>(json); // 저장 스키마 확인 데이터
            if (schemaProbe == null || schemaProbe.SchemaVersion <= 0)
            {
                throw new InvalidDataException("App data schema version is missing.");
            }

            AppData appData = JsonUtility.FromJson<AppData>(json); // 역직렬화된 앱 데이터
            if (appData == null)
            {
                throw new InvalidDataException("App data could not be deserialized.");
            }

            return MigrateToCurrentSchema(appData);
        }

        // 이전 스키마 데이터를 현재 스키마로 마이그레이션하는 함수
        private static AppData MigrateToCurrentSchema(AppData appData)
        {
            switch (appData.SchemaVersion)
            {
                case AppData.CurrentSchemaVersion:
                    return appData;
                default:
                    throw new NotSupportedException(
                        $"App data schema version {appData.SchemaVersion} is not supported.");
            }
        }

        // 앱 데이터의 필수 컬렉션과 기본 참조를 정상화하는 함수
        private void NormalizeStructure(AppData appData)
        {
            appData.TotalInputCount = Math.Max(0L, appData.TotalInputCount);
            appData.GlobalSettings ??= new GlobalSettingsData();
            appData.GlobalSettings.MonitorDeviceId ??= string.Empty;
            appData.GlobalSettings.UiScalePercent = NormalizeScalePercent(
                appData.GlobalSettings.UiScalePercent,
                GlobalSettingsData.DefaultUiScalePercent,
                GlobalSettingsData.MinimumUiScalePercent,
                GlobalSettingsData.MaximumUiScalePercent,
                GlobalSettingsData.UiScaleStepPercent);
            appData.Presets ??= new List<PresetData>();
            appData.Presets.RemoveAll(preset => preset == null);

            foreach (PresetData preset in appData.Presets)
            {
                preset.Id ??= string.Empty;
                preset.Name ??= string.Empty;
                preset.NormalImages ??= new List<ImageAssetData>();
                preset.IdleImages ??= new List<ImageAssetData>();
                preset.HoverTextTemplate ??= string.Empty;
                if (preset.CharacterScalePercent <= 0)
                {
                    preset.CharacterScalePercent = PresetData.DefaultCharacterScalePercent;
                }

                preset.CharacterScalePercent = NormalizeScalePercent(
                    preset.CharacterScalePercent,
                    PresetData.DefaultCharacterScalePercent,
                    PresetData.MinimumCharacterScalePercent,
                    PresetData.MaximumCharacterScalePercent,
                    PresetData.CharacterScaleStepPercent);
            }

            if (appData.Presets.Count == 0)
            {
                appData.Presets.Add(_presetAssetStore.CreateDefaultPreset());
            }

            bool activePresetExists = appData.Presets.Exists( // 활성 프리셋 존재 여부
                preset => preset.Id == appData.ActivePresetId);
            if (!activePresetExists)
            {
                appData.ActivePresetId = appData.Presets[0].Id;
            }
        }

        // 크기 값을 허용 범위의 가장 가까운 변경 단위로 보정하는 함수
        private static int NormalizeScalePercent(
            int scalePercent,
            int defaultPercent,
            int minimumPercent,
            int maximumPercent,
            int stepPercent)
        {
            int value = scalePercent <= 0 ? defaultPercent : scalePercent; // 보정 전 크기 값
            int clampedValue = Mathf.Clamp(value, minimumPercent, maximumPercent); // 범위 보정 크기 값
            return Mathf.RoundToInt(clampedValue / (float)stepPercent) * stepPercent;
        }

        // 완성된 JSON을 임시 파일에 기록하고 디스크에 반영하는 함수
        private void WriteTemporaryFile(string json)
        {
            using (FileStream stream = new FileStream(
                       _temporaryFilePath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }
        }

        // 이전 정상 데이터 파일을 복구 후보로 읽는 함수
        private bool TryReadBackup(out AppData appData)
        {
            appData = null;
            if (!File.Exists(_backupFilePath))
            {
                return false;
            }

            try
            {
                appData = ReadData(_backupFilePath);
                return true;
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch (Exception exception) when (IsCorruptDataException(exception))
            {
                return false;
            }
        }

        // 손상된 주 데이터 파일을 진단 가능한 별도 파일로 이동하는 함수
        private void MoveCorruptedPrimaryFile()
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"); // 손상 감지 시각 문자열
            string corruptedFilePath = Path.Combine( // 손상 데이터 보존 경로
                _dataRootPath,
                $"app-data.corrupt-{timestamp}.json");
            File.Move(_dataFilePath, corruptedFilePath);
        }

        // JSON 내용 손상으로 처리할 수 있는 예외인지 확인하는 함수
        private static bool IsCorruptDataException(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is InvalidDataException;
        }

        // JSON에서 저장 스키마 버전만 확인하는 클래스
        [Serializable]
        private sealed class SchemaProbe
        {
            public int SchemaVersion; // 저장 스키마 버전
        }
    }
}
