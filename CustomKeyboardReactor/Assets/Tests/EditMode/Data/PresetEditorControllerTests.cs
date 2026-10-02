using System;
using System.IO;
using NUnit.Framework;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 프리셋 초안의 소유권과 저장 및 상태 연결을 검증하는 클래스
    public sealed class PresetEditorControllerTests
    {
        private string _root; // 격리된 데이터 경로
        private PresetAssetStore _assets; // 테스트 이미지 저장소
        private AppDataStore _data; // 테스트 앱 데이터 저장소
        private PresetRepository _presets; // 테스트 프리셋 저장소
        private PresetEditorController _editor; // 테스트 초안 편집기
        private string _source; // 외부 이미지 경로

        // 격리된 저장소와 외부 이미지를 준비하는 함수
        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "CustomKeyboardReactorTests", Guid.NewGuid().ToString("N"));
            _assets = new PresetAssetStore(_root);
            _data = new AppDataStore(_root, _assets);
            _presets = new PresetRepository(_data, _assets);
            _editor = new PresetEditorController(_presets, _assets);
            PresetData active = _presets.GetActive(); // 초기 활성 프리셋
            _source = Path.Combine(_root, "external.png");
            File.Copy(Path.Combine(_root, active.NormalImages[0].RelativePath), _source);
            _editor.Select(active.Id);
        }

        // 초안과 테스트 파일을 정리하는 함수
        [TearDown]
        public void TearDown()
        {
            _editor.Dispose();
            Directory.Delete(_root, true);
        }

        // 선택 및 편집이 활성 프리셋을 변경하지 않는지 검증하는 함수
        [Test]
        public void SelectAndEdit_KeepsActivePresetAndStoredData()
        {
            PresetData original = _presets.GetActive(); // 기존 활성 프리셋
            PresetData other = _presets.Duplicate(original.Id, "other"); // 선택할 다른 프리셋
            _editor.Select(other.Id);
            _editor.Draft.Name = "edited";
            Assert.That(_editor.IsDirty, Is.True);
            Assert.That(_presets.GetActive().Id, Is.EqualTo(original.Id));
            Assert.That(_presets.GetAll()[1].Name, Is.EqualTo("other"));
        }

        // 값을 원래대로 바꾸면 변경 없음으로 판정하는지 검증하는 함수
        [Test]
        public void EditThenRestoreOriginalValue_IsNotDirty()
        {
            string originalName = _editor.Draft.Name; // 편집 전 이름
            _editor.Draft.Name = "changed";
            _editor.Draft.Name = originalName;
            Assert.That(_editor.IsDirty, Is.False);
        }

        // 버리기가 가져온 임시 파일만 제거하는지 검증하는 함수
        [Test]
        public void Discard_RemovesDraftAssetsAndKeepsOwnedImages()
        {
            ImageAssetData owned = _editor.Draft.NormalImages[0]; // 기존 소유 이미지
            _editor.Import(_source, false);
            ImageAssetData imported = _editor.Draft.NormalImages[1]; // 가져온 임시 이미지
            _editor.Draft.Name = "changed";
            _editor.Discard();
            Assert.That(_editor.IsDirty, Is.False);
            Assert.That(_editor.Draft.NormalImages.Count, Is.EqualTo(1));
            Assert.That(_assets.CanLoad(imported), Is.False);
            Assert.That(_assets.CanLoad(owned), Is.True);
            Assert.That(File.Exists(_source), Is.True);
        }

        // 새 프리셋 적용이 순서와 대기 이미지 및 독립 복사 파일을 복원하는지 검증하는 함수
        [Test]
        public void ApplyNewPreset_ReloadsOrderedImagesWithoutOriginalFiles()
        {
            _editor.Create("user");
            _editor.Import(_source, false);
            _editor.Import(_source, false);
            _editor.Import(_source, true);
            string secondId = _editor.Draft.NormalImages[1].Id; // 앞으로 이동할 일반 이미지 ID
            _editor.MoveNormalImage(1, 0);
            _editor.Draft.HoverTextTemplate = "{TOTAL_INPUT_COUNT} · {LOOP_COUNT}";
            _editor.Draft.CharacterScalePercent = 145;
            PresetData applied = _editor.Apply(); // 확정된 프리셋
            File.Delete(_source);
            PresetRepository reloaded = new PresetRepository(new AppDataStore(_root, _assets), _assets); // 재실행 저장소
            PresetData active = reloaded.GetActive(); // 재실행 활성 프리셋
            Assert.That(active.Id, Is.EqualTo(applied.Id));
            Assert.That(active.NormalImages[0].Id, Is.EqualTo(secondId));
            Assert.That(active.CharacterScalePercent, Is.EqualTo(145));
            Assert.That(active.IdleImages.Count, Is.EqualTo(1));
            Assert.That(_assets.CanLoad(active.NormalImages[0]), Is.True);
            Assert.That(_assets.CanLoad(active.IdleImages[0]), Is.True);
            Assert.That(_editor.IsDirty, Is.False);
            Assert.That(Directory.Exists(Path.Combine(_root, "drafts", active.Id)), Is.False);
        }

        // 유효하지 않은 적용이 기존 활성 프리셋과 초안을 보존하는지 검증하는 함수
        [Test]
        public void ApplyWithoutNormalImages_KeepsDraftAndActivePreset()
        {
            string activeId = _presets.GetActive().Id; // 기존 활성 프리셋 ID
            _editor.Create("empty");
            _editor.Import(_source, true);
            ImageAssetData imported = _editor.Draft.IdleImages[0]; // 유지할 임시 이미지
            Assert.That(_editor.CanApply, Is.False);
            Assert.Throws<InvalidDataException>(() => _editor.Apply());
            Assert.That(_presets.GetActive().Id, Is.EqualTo(activeId));
            Assert.That(_assets.CanLoad(imported), Is.True);
            Assert.That(_editor.IsDirty, Is.True);
        }

        // 저장 실패가 가져온 초안을 유지하는지 검증하는 함수
        [Test]
        public void ApplyWhenStorageFails_KeepsDraftForRetry()
        {
            _editor.Import(_source, false);
            ImageAssetData imported = _editor.Draft.NormalImages[1]; // 재시도할 초안 이미지
            Directory.CreateDirectory(Path.Combine(_root, "app-data.tmp"));
            Assert.Throws<UnauthorizedAccessException>(() => _editor.Apply());
            Assert.That(_assets.CanLoad(imported), Is.True);
            Assert.That(_editor.IsDirty, Is.True);
            Directory.Delete(Path.Combine(_root, "app-data.tmp"));
            Assert.That(_editor.Apply().NormalImages.Count, Is.EqualTo(2));
        }

        // 설정에서 초안을 버리면 기존 상태와 이미지가 유지되는지 검증하는 함수
        [TestCase(false)]
        [TestCase(true)]
        public void DiscardThenCloseSettings_RestoresPreviousStateAndImage(bool idle)
        {
            _editor.Import(_source, false);
            _editor.Import(_source, true);
            PresetData active = _editor.Apply(); // 다중 이미지 활성 프리셋
            ReactorStateMachine machine = new ReactorStateMachine(active, true, 60); // 테스트 상태 머신
            machine.HandleActivity();
            if (idle) machine.AdvanceTime(60);
            ReactorState previousState = machine.CurrentState; // 설정 진입 전 상태
            string previousImage = machine.CurrentImage.Id; // 설정 진입 전 이미지
            machine.OpenSettings();
            _editor.Draft.CharacterScalePercent = 180;
            _editor.Discard();
            Assert.That(machine.HandleActivity(), Is.False);
            Assert.That(machine.AdvanceTime(600), Is.False);
            machine.CloseSettings();
            Assert.That(machine.CurrentState, Is.EqualTo(previousState));
            Assert.That(machine.CurrentImage.Id, Is.EqualTo(previousImage));
        }

        // 설정에서 적용하면 첫 이미지로 돌아가고 설정 입력이 제외되는지 검증하는 함수
        [Test]
        public void ApplyWhileSettingsOpen_ResumesAtFirstNormalImageAfterClose()
        {
            _editor.Import(_source, false);
            PresetData active = _editor.Apply(); // 다중 이미지 활성 프리셋
            ReactorStateMachine machine = new ReactorStateMachine(active, true, 60); // 테스트 상태 머신
            machine.HandleActivity();
            machine.OpenSettings();
            _editor.MoveNormalImage(1, 0);
            machine.ApplyPreset(_editor.Apply());
            machine.OpenSettings();
            Assert.That(machine.HandleActivity(), Is.False);
            machine.CloseSettings();
            Assert.That(machine.CurrentState, Is.EqualTo(ReactorState.Reacting));
            Assert.That(machine.CurrentNormalImageIndex, Is.Zero);
            Assert.That(machine.CurrentImage.Id, Is.EqualTo(_presets.GetActive().NormalImages[0].Id));
        }

        // 전체 초기화가 설정과 카운트 및 소유 이미지를 기본값으로 바꾸는지 검증하는 함수
        [Test]
        public void ResetAllData_ReplacesPresetsSettingsCountAndOwnedImages()
        {
            PresetData previous = _presets.GetActive(); // 초기화할 프리셋
            UserSettingsRepository settings = new UserSettingsRepository(_data); // 테스트 설정 저장소
            GlobalSettingsData changed = settings.GetSettings(); // 변경할 공용 설정
            changed.UiScalePercent = 150;
            settings.SaveSettings(changed);
            settings.SaveTotalInputCount(50);
            PresetData orphanDraft = _presets.CreateDraft("orphan"); // 이전 실행에서 남은 미저장 초안
            ImageAssetData orphanImage = _assets.Import(orphanDraft.Id, _source); // 이전 실행의 임시 이미지
            _data.ResetAllData();
            PresetData replacement = _presets.GetActive(); // 기본 원본에서 생성된 새 프리셋
            Assert.That(replacement.Id, Is.Not.EqualTo(previous.Id));
            Assert.That(_assets.CanLoad(previous.NormalImages[0]), Is.False);
            Assert.That(_assets.CanLoad(orphanImage), Is.False);
            Assert.That(_assets.CanLoad(replacement.NormalImages[0]), Is.True);
            Assert.That(settings.GetTotalInputCount(), Is.Zero);
            Assert.That(settings.GetSettings().UiScalePercent, Is.EqualTo(100));
            Assert.That(File.Exists(_source), Is.True);
        }

        // 대기 상태에서 기능을 끄면 설정 종료 후 반응 상태로 복원되는지 검증하는 함수
        [Test]
        public void DisableIdleWhileConfiguring_ClosesToReactingFirstImage()
        {
            _editor.Import(_source, true);
            ReactorStateMachine machine = new ReactorStateMachine(_editor.Apply(), true, 60); // 테스트 상태 머신
            machine.AdvanceTime(60);
            machine.OpenSettings();
            machine.SetIdleSettings(false, 60);
            Assert.That(machine.CurrentState, Is.EqualTo(ReactorState.Configuring));
            machine.CloseSettings();
            Assert.That(machine.CurrentState, Is.EqualTo(ReactorState.Reacting));
            Assert.That(machine.CurrentIdleImageIndex, Is.EqualTo(-1));
        }

        // 캐릭터와 UI 스테퍼가 각각의 범위와 단위를 지키는지 검증하는 함수
        [TestCase(10, 300, 298, 10, 300)]
        [TestCase(10, 300, 10, -10, 10)]
        [TestCase(50, 200, 50, -5, 50)]
        [TestCase(50, 200, 195, 10, 200)]
        [TestCase(50, 200, 101, 5, 105)]
        public void PercentStepper_ClampsAndNormalizes(int minimum, int maximum, int value, int delta, int expected)
        {
            PercentStepper stepper = new PercentStepper(value, minimum, maximum, 5); // 테스트 크기 계산기
            Assert.That(stepper.Change(delta), Is.EqualTo(expected));
            Assert.That(stepper.Set(100), Is.EqualTo(100));
        }
    }
}
