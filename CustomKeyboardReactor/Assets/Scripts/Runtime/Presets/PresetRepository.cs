using System;
using System.Collections.Generic;
using System.IO;

namespace CustomKeyboardReactor
{
    // 프리셋 조회와 적용, 복제, 삭제를 관리하는 클래스
    public sealed class PresetRepository
    {
        private readonly AppDataStore _appDataStore; // 앱 데이터 저장소
        private readonly PresetAssetStore _presetAssetStore; // 프리셋 이미지 저장소

        // 앱 데이터와 프리셋 이미지 저장소를 연결하는 생성자
        public PresetRepository(
            AppDataStore appDataStore,
            PresetAssetStore presetAssetStore)
        {
            _appDataStore = appDataStore ??
                            throw new ArgumentNullException(nameof(appDataStore));
            _presetAssetStore = presetAssetStore ??
                                throw new ArgumentNullException(nameof(presetAssetStore));
            _appDataStore.LoadOrCreate();
        }

        // 저장된 모든 프리셋의 복사본을 반환하는 함수
        public IReadOnlyList<PresetData> GetAll()
        {
            AppData appData = _appDataStore.LoadLatest(); // 최신 앱 데이터
            List<PresetData> presets = new List<PresetData>(appData.Presets.Count); // 반환할 프리셋 목록
            foreach (PresetData preset in appData.Presets)
            {
                presets.Add(ClonePreset(preset));
            }

            return presets.AsReadOnly();
        }

        // 활성 프리셋의 복사본을 반환하는 함수
        public PresetData GetActive()
        {
            AppData appData = _appDataStore.LoadLatest(); // 최신 앱 데이터
            PresetData activePreset = FindPreset(appData, appData.ActivePresetId); // 현재 활성 프리셋
            return ClonePreset(activePreset);
        }

        // 아직 저장되지 않은 빈 프리셋 초안을 생성하는 함수
        public PresetData CreateDraft(string name)
        {
            return new PresetData
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name?.Trim() ?? string.Empty,
            };
        }

        // 유효한 프리셋을 저장하고 활성 프리셋으로 적용하는 함수
        public void Apply(PresetData preset)
        {
            PresetData storedPreset = ClonePreset(preset); // 저장할 프리셋 복사본
            using (PresetAssetStore.DraftAssetCommit draftCommit =
                   _presetAssetStore.PrepareDraftAssets(storedPreset))
            {
                ValidatePreset(storedPreset);
                AppData appData = _appDataStore.LoadLatest(); // 최신 앱 데이터
                int existingIndex = appData.Presets.FindIndex( // 기존 프리셋 인덱스
                    item => item.Id == storedPreset.Id);
                string previousActivePresetId = appData.ActivePresetId; // 변경 전 활성 프리셋 ID
                PresetData previousPreset = existingIndex >= 0 // 변경 전 프리셋 데이터
                    ? appData.Presets[existingIndex]
                    : null;

                if (existingIndex >= 0)
                {
                    appData.Presets[existingIndex] = storedPreset;
                }
                else
                {
                    appData.Presets.Add(storedPreset);
                }

                appData.ActivePresetId = storedPreset.Id;
                try
                {
                    _appDataStore.Save(appData);
                    draftCommit.Complete();
                }
                catch
                {
                    appData.ActivePresetId = previousActivePresetId;
                    if (existingIndex >= 0)
                    {
                        appData.Presets[existingIndex] = previousPreset;
                    }
                    else
                    {
                        appData.Presets.Remove(storedPreset);
                    }

                    throw;
                }
            }
        }

        // 프리셋 설정과 소유 이미지를 독립적으로 복제하는 함수
        public PresetData Duplicate(string presetId, string duplicateName)
        {
            AppData appData = _appDataStore.LoadLatest(); // 최신 앱 데이터
            PresetData sourcePreset = FindPreset(appData, presetId); // 복제할 원본 프리셋
            PresetData duplicatedPreset = new PresetData // 새 복제 프리셋
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = duplicateName?.Trim() ?? string.Empty,
                HoverTextTemplate = sourcePreset.HoverTextTemplate,
                CharacterScalePercent = sourcePreset.CharacterScalePercent,
            };
            ValidatePresetName(duplicatedPreset.Name);
            _presetAssetStore.DuplicatePresetAssets(sourcePreset, duplicatedPreset);

            try
            {
                ValidatePreset(duplicatedPreset);
                appData.Presets.Add(duplicatedPreset);
                _appDataStore.Save(appData);
                return ClonePreset(duplicatedPreset);
            }
            catch
            {
                appData.Presets.Remove(duplicatedPreset);
                _presetAssetStore.DeletePresetAssets(duplicatedPreset.Id);
                throw;
            }
        }

        // 프리셋을 삭제하고 마지막 프리셋이면 새 기본 프리셋을 생성하는 함수
        public void Delete(string presetId)
        {
            AppData appData = _appDataStore.LoadLatest(); // 최신 앱 데이터
            PresetData preset = FindPreset(appData, presetId); // 삭제할 프리셋
            int presetIndex = appData.Presets.IndexOf(preset); // 삭제할 프리셋 인덱스
            string previousActivePresetId = appData.ActivePresetId; // 삭제 전 활성 프리셋 ID
            PresetData replacementPreset = null; // 마지막 삭제 시 대체 기본 프리셋

            appData.Presets.RemoveAt(presetIndex);
            if (appData.Presets.Count == 0)
            {
                replacementPreset = _presetAssetStore.CreateDefaultPreset();
                appData.Presets.Add(replacementPreset);
            }

            if (appData.ActivePresetId == presetId)
            {
                appData.ActivePresetId = appData.Presets[0].Id;
            }

            try
            {
                _appDataStore.Save(appData);
            }
            catch
            {
                if (replacementPreset != null)
                {
                    appData.Presets.Remove(replacementPreset);
                    _presetAssetStore.DeletePresetAssets(replacementPreset.Id);
                }

                appData.Presets.Insert(presetIndex, preset);
                appData.ActivePresetId = previousActivePresetId;
                throw;
            }

            _presetAssetStore.DeletePresetAssets(presetId);
        }

        // 저장된 프리셋을 활성 프리셋으로 변경하는 함수
        public void SetActive(string presetId)
        {
            AppData appData = _appDataStore.LoadLatest(); // 최신 앱 데이터
            FindPreset(appData, presetId);
            string previousActivePresetId = appData.ActivePresetId; // 변경 전 활성 프리셋 ID
            appData.ActivePresetId = presetId;
            try
            {
                _appDataStore.Save(appData);
            }
            catch
            {
                appData.ActivePresetId = previousActivePresetId;
                throw;
            }
        }

        // 프리셋이 적용 가능한 데이터와 이미지를 갖는지 검증하는 함수
        private void ValidatePreset(PresetData preset)
        {
            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            PresetAssetStore.ValidatePresetId(preset.Id);
            ValidatePresetName(preset.Name);
            if (preset.CharacterScalePercent < PresetData.MinimumCharacterScalePercent ||
                preset.CharacterScalePercent > PresetData.MaximumCharacterScalePercent ||
                preset.CharacterScalePercent % PresetData.CharacterScaleStepPercent != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(preset),
                    "Character scale must be inside the supported range in five percent steps.");
            }

            if (preset.NormalImages == null || preset.NormalImages.Count == 0)
            {
                throw new InvalidDataException("A preset requires at least one normal image.");
            }

            ValidateImages(preset.Id, preset.NormalImages);
            ValidateImages(preset.Id, preset.IdleImages);
        }

        // 이미지 목록의 소유권과 디코딩 가능 여부를 검증하는 함수
        private void ValidateImages(string presetId, IReadOnlyList<ImageAssetData> images)
        {
            if (images == null)
            {
                return;
            }

            foreach (ImageAssetData image in images)
            {
                if (image == null ||
                    string.IsNullOrWhiteSpace(image.Id) ||
                    string.IsNullOrWhiteSpace(image.RelativePath) ||
                    !_presetAssetStore.CanLoadOwnedImage(presetId, image))
                {
                    throw new InvalidDataException("Preset contains an unreadable image.");
                }
            }
        }

        // 내부 ID로 저장된 프리셋을 찾는 함수
        private static PresetData FindPreset(AppData appData, string presetId)
        {
            PresetData preset = appData.Presets.Find(item => item.Id == presetId); // 검색된 프리셋
            if (preset == null)
            {
                throw new KeyNotFoundException($"Preset '{presetId}' was not found.");
            }

            return preset;
        }

        // 프리셋 데이터와 이미지 참조를 깊은 복사하는 함수
        private static PresetData ClonePreset(PresetData source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            PresetData clone = new PresetData // 복사된 프리셋 데이터
            {
                Id = source.Id,
                Name = source.Name,
                HoverTextTemplate = source.HoverTextTemplate,
                CharacterScalePercent = source.CharacterScalePercent,
                NormalImages = CloneImages(source.NormalImages),
                IdleImages = CloneImages(source.IdleImages),
            };
            return clone;
        }

        // 이미지 참조 목록을 깊은 복사하는 함수
        private static List<ImageAssetData> CloneImages(IReadOnlyList<ImageAssetData> source)
        {
            List<ImageAssetData> images = new List<ImageAssetData>(); // 복사된 이미지 참조 목록
            if (source == null)
            {
                return images;
            }

            foreach (ImageAssetData image in source)
            {
                if (image == null)
                {
                    images.Add(null);
                    continue;
                }

                images.Add(new ImageAssetData
                {
                    Id = image.Id,
                    RelativePath = image.RelativePath,
                    OriginalWidth = image.OriginalWidth,
                    OriginalHeight = image.OriginalHeight,
                });
            }

            return images;
        }

        // 프리셋 이름이 비어 있지 않은지 검증하는 함수
        private static void ValidatePresetName(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
            {
                throw new ArgumentException("Preset name is required.", nameof(presetName));
            }
        }
    }
}
