using System;
using System.Collections.Generic;

namespace CustomKeyboardReactor
{
    // 프리셋 초안과 적용, 폐기 및 이미지 순서를 관리하는 클래스
    public sealed class PresetEditorController : IDisposable
    {
        private readonly PresetRepository _repository; // 프리셋 저장소
        private readonly PresetAssetStore _assetStore; // 프리셋 이미지 저장소
        private PresetData _baseline; // 편집 시작 데이터
        private bool _isNew; // 미저장 초안 여부

        public PresetData Draft { get; private set; } // 현재 편집 초안
        public bool IsNew => _isNew; // 미저장 초안 여부
        public bool IsDirty => Draft != null && (_isNew || !AreEqual(Draft, _baseline)); // 저장 필요 여부
        public bool CanApply => Draft != null && !string.IsNullOrWhiteSpace(Draft.Name) &&
                                Draft.NormalImages.Count > 0; // 기본 적용 조건 충족 여부

        // 프리셋과 이미지 저장소를 연결하는 생성자
        public PresetEditorController(PresetRepository repository, PresetAssetStore assetStore)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _assetStore = assetStore ?? throw new ArgumentNullException(nameof(assetStore));
        }

        // 저장된 프리셋을 독립 초안으로 선택하는 함수
        public void Select(string presetId)
        {
            foreach (PresetData preset in _repository.GetAll())
            {
                if (preset.Id != presetId) continue;
                Dispose();
                _baseline = PresetRepository.ClonePreset(preset);
                Draft = preset;
                _isNew = false;
                return;
            }
            throw new KeyNotFoundException("Preset was not found.");
        }

        // 빈 프리셋 초안을 생성하는 함수
        public void Create(string name)
        {
            Dispose();
            Draft = _repository.CreateDraft(name);
            _baseline = PresetRepository.ClonePreset(Draft);
            _isNew = true;
        }

        // 파일을 초안 소유 이미지로 가져오는 함수
        public void Import(string sourcePath, bool idleImage)
        {
            ImageAssetData image = _assetStore.Import(Draft.Id, sourcePath); // 가져온 초안 이미지
            (idleImage ? Draft.IdleImages : Draft.NormalImages).Add(image);
        }

        // 일반 이미지의 순서를 변경하는 함수
        public void MoveNormalImage(int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= Draft.NormalImages.Count ||
                targetIndex < 0 || targetIndex >= Draft.NormalImages.Count)
                throw new ArgumentOutOfRangeException(nameof(targetIndex));
            ImageAssetData image = Draft.NormalImages[sourceIndex]; // 이동할 일반 이미지
            Draft.NormalImages.RemoveAt(sourceIndex);
            Draft.NormalImages.Insert(targetIndex, image);
        }

        // 초안을 저장하고 확정된 이미지 경로로 다시 불러오는 함수
        public PresetData Apply()
        {
            _repository.Apply(Draft);
            Draft = _repository.GetActive();
            _baseline = PresetRepository.ClonePreset(Draft);
            _isNew = false;
            return PresetRepository.ClonePreset(Draft);
        }

        // 초안 파일을 제거하고 편집 시작 데이터로 복원하는 함수
        public void Discard()
        {
            if (Draft == null) return;
            _assetStore.DiscardDraftAssets(Draft.Id);
            Draft = PresetRepository.ClonePreset(_baseline);
        }

        // 프리셋 데이터 변경 여부를 비교하는 함수
        private static bool AreEqual(PresetData left, PresetData right)
        {
            return left.Id == right.Id && left.Name == right.Name &&
                   left.HoverTextTemplate == right.HoverTextTemplate &&
                   left.CharacterScalePercent == right.CharacterScalePercent &&
                   ImagesEqual(left.NormalImages, right.NormalImages) &&
                   ImagesEqual(left.IdleImages, right.IdleImages);
        }

        // 이미지 목록의 순서와 참조를 비교하는 함수
        private static bool ImagesEqual(List<ImageAssetData> left, List<ImageAssetData> right)
        {
            if (left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++) // 비교 이미지 인덱스
            {
                if (left[index].Id != right[index].Id || left[index].RelativePath != right[index].RelativePath)
                    return false;
            }
            return true;
        }

        // 남은 초안 이미지와 편집 참조를 정리하는 함수
        public void Dispose()
        {
            if (Draft != null) _assetStore.DiscardDraftAssets(Draft.Id);
            Draft = null;
            _baseline = null;
            _isNew = false;
        }
    }
}
