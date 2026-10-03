using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 격리된 데이터로 설정 화면의 실제 명령과 표시를 검증하는 클래스
    public sealed class SettingsWindowControllerTests
    {
        private string _root; // 테스트 데이터 경로
        private GameObject _runtime; // 격리된 런타임 오브젝트
        private SettingsWindowController _window; // 테스트 설정 화면
        private PresetRepository _repository; // 테스트 프리셋 저장소
        private ReactorStateMachine _machine; // 테스트 반응 상태 머신

        // 실제 저장 계층과 비활성 런타임 어댑터를 연결하는 함수
        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "CustomKeyboardReactorTests", Guid.NewGuid().ToString("N"));
            PresetAssetStore assets = new PresetAssetStore(_root); // 테스트 이미지 저장소
            AppDataStore data = new AppDataStore(_root, assets); // 테스트 앱 데이터 저장소
            _repository = new PresetRepository(data, assets);
            UserSettingsRepository settingsRepository = new UserSettingsRepository(data); // 테스트 설정 저장소
            GlobalSettingsData settings = settingsRepository.GetSettings(); // 테스트 공용 설정
            PresetData active = _repository.GetActive(); // 초기 활성 프리셋
            _machine = new ReactorStateMachine(active, true, 60);
            _runtime = new GameObject("Settings Test Runtime");
            _runtime.SetActive(false);
            ReactorController reactor = _runtime.AddComponent<ReactorController>(); // 테스트 런타임 연결기
            reactor.enabled = false;
            CharacterPresenter presenter = _runtime.AddComponent<CharacterPresenter>(); // 테스트 캐릭터 표시기
            presenter.Initialize(assets);
            presenter.SetActivePreset(active);
            CharacterInteractionController interaction = _runtime.AddComponent<CharacterInteractionController>(); // 테스트 상호작용 연결기
            interaction.enabled = false;
            interaction.Initialize(presenter, settings, settingsRepository, reactor.OpenSettings, reactor.CloseSettings);
            SetField(reactor, "_presetAssetStore", assets);
            SetField(reactor, "_appDataStore", data);
            SetField(reactor, "_presetRepository", _repository);
            SetField(reactor, "_userSettingsRepository", settingsRepository);
            SetField(reactor, "_stateMachine", _machine);
            SetField(reactor, "_characterPresenter", presenter);
            SetField(reactor, "_characterInteractionController", interaction);
            SetField(reactor, "_activePreset", active);
            SetField(reactor, "_globalSettings", settings);
            _window = _runtime.AddComponent<SettingsWindowController>();
            _window.Initialize(reactor, _repository, assets, settingsRepository);
            interaction.SetSettingsWindow(_window);
            _runtime.SetActive(true);
        }

        // 테스트 오브젝트와 저장 파일을 정리하는 함수
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_runtime);
            Directory.Delete(_root, true);
        }

        // 프리셋 선택이 초안 미리보기만 바꾸는지 검증하는 함수
        [Test]
        public void SelectOtherPreset_DoesNotChangeActivePreset()
        {
            string originalId = _repository.GetActive().Id; // 기존 활성 프리셋 ID
            _repository.Duplicate(originalId, "other");
            _window.Show();
            _window.GetComponentInChildren<TMP_Dropdown>().value = 1;
            Assert.That(_repository.GetActive().Id, Is.EqualTo(originalId));
            Assert.That(NameInput().text, Is.EqualTo("other"));
            Assert.That(_machine.CurrentState, Is.EqualTo(ReactorState.Configuring));
            _window.RequestClose();
            Assert.That(_machine.CurrentState, Is.EqualTo(ReactorState.Reacting));
        }

        // 닫기 취소가 편집 화면과 초안을 유지하는지 검증하는 함수
        [Test]
        public void DirtyCloseThenCancel_KeepsOpenDraftWithoutSaving()
        {
            string originalName = _repository.GetActive().Name; // 기존 저장 이름
            _window.Show();
            NameInput().text = "edited";
            _window.RequestClose();
            Click("취소");
            Assert.That(_window.IsOpen, Is.True);
            Assert.That(NameInput().text, Is.EqualTo("edited"));
            Assert.That(_repository.GetActive().Name, Is.EqualTo(originalName));
            Assert.That(_machine.CurrentState, Is.EqualTo(ReactorState.Configuring));
        }

        // 닫기 버리기가 데이터와 반응 상태를 복원하는지 검증하는 함수
        [Test]
        public void DirtyCloseThenDiscard_KeepsStoredPresetAndCloses()
        {
            string originalName = _repository.GetActive().Name; // 기존 저장 이름
            _window.Show();
            NameInput().text = "edited";
            _window.RequestClose();
            Click("버리기");
            Assert.That(_window.IsOpen, Is.False);
            Assert.That(_repository.GetActive().Name, Is.EqualTo(originalName));
            Assert.That(_machine.CurrentState, Is.EqualTo(ReactorState.Reacting));
        }

        // 적용 후에도 열린 설정 입력은 제외되고 닫을 때 반응으로 돌아가는지 검증하는 함수
        [Test]
        public void ApplyThenClose_SavesDraftAndKeepsConfiguringUntilClosed()
        {
            _window.Show();
            NameInput().text = "edited";
            Click("적용");
            Assert.That(_repository.GetActive().Name, Is.EqualTo("edited"));
            Assert.That(_window.IsOpen, Is.True);
            Assert.That(_machine.CurrentState, Is.EqualTo(ReactorState.Configuring));
            Assert.That(_machine.HandleActivity(), Is.False);
            _window.RequestClose();
            Assert.That(_machine.CurrentState, Is.EqualTo(ReactorState.Reacting));
            Assert.That(_machine.CurrentNormalImageIndex, Is.Zero);
        }

        // 연속 공용 설정 조작이 대기 상태에 즉시 반영되는지 검증하는 함수
        [Test]
        public void ChangeKeyboardThenDisableIdle_ClosesSettingsToReacting()
        {
            _machine.ActivePreset.IdleImages.Add(_machine.ActivePreset.NormalImages[0]);
            _machine.AdvanceTime(60);
            _window.Show();
            Click("공용 설정");
            _window.GetComponentsInChildren<Toggle>().Single(item => item.name == "키보드 반응").isOn = false;
            _window.GetComponentsInChildren<Toggle>().Single(item => item.name == "대기 기능").isOn = false;
            _window.RequestClose();
            Assert.That(_machine.CurrentState, Is.EqualTo(ReactorState.Reacting));
            Assert.That(_machine.CurrentIdleImageIndex, Is.EqualTo(-1));
        }

        // 한 장짜리 프리셋의 입력과 카운트 초기화가 호버 문구를 갱신하는지 검증하는 함수
        [Test]
        public void SingleNormalImageActivityAndCountReset_RefreshHoverText()
        {
            ReactorController reactor = _runtime.GetComponent<ReactorController>(); // 테스트 런타임 연결기
            PresetData active = _machine.ActivePreset; // 테스트 활성 프리셋
            active.NormalImages.RemoveRange(1, active.NormalImages.Count - 1);
            active.HoverTextTemplate = "{TOTAL_INPUT_COUNT} / {LOOP_COUNT}";
            using (HoverTextPanel panel = new HoverTextPanel(_runtime.transform)) // 테스트 호버 패널
            {
                SetField(reactor, "_hoverTextPanel", panel);
                UserSettingsRepository settings = new UserSettingsRepository(
                    new AppDataStore(_root, new PresetAssetStore(_root))); // 테스트 카운트 저장소
                settings.SaveTotalInputCount(12);
                typeof(ReactorController).GetMethod("HandleActivityAccepted", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(reactor, new object[] { default(ActivityInputEvent) });
                panel.UpdateHover(true, new Rect(400, 200, 100, 100), new Rect(0, 0, 1280, 720));
                Canvas.ForceUpdateCanvases();
                TextMeshProUGUI label = panel.PanelRect.GetComponentInChildren<TextMeshProUGUI>(); // 호버 문구 표시기
                label.ForceMeshUpdate();
                Assert.That(label.GetParsedText(), Is.EqualTo("12 / 12"));
                reactor.ResetInputCount();
                panel.UpdateHover(true, new Rect(400, 200, 100, 100), new Rect(0, 0, 1280, 720));
                label.ForceMeshUpdate();
                Assert.That(label.GetParsedText(), Is.EqualTo("0 / 0"));
                Assert.That(_machine.CurrentNormalImageIndex, Is.Zero);
                SetField(reactor, "_hoverTextPanel", null);
            }
        }

        // 활성 프리셋 변경이 호버 문구와 환산 루프 수를 갱신하는지 검증하는 함수
        [Test]
        public void ApplyPreset_RefreshesHoverTextWithActiveNormalImageCount()
        {
            ReactorController reactor = _runtime.GetComponent<ReactorController>(); // 테스트 런타임 연결기
            PresetData active = _machine.ActivePreset; // 테스트 활성 프리셋
            active.HoverTextTemplate = "변경 {LOOP_COUNT}";
            active.NormalImages.Add(active.NormalImages[0]);
            using (HoverTextPanel panel = new HoverTextPanel(_runtime.transform)) // 테스트 호버 패널
            {
                SetField(reactor, "_hoverTextPanel", panel);
                UserSettingsRepository settings = new UserSettingsRepository(
                    new AppDataStore(_root, new PresetAssetStore(_root))); // 테스트 카운트 저장소
                settings.SaveTotalInputCount(active.NormalImages.Count * 3);
                reactor.ApplyPreset(active);
                panel.UpdateHover(true, new Rect(400, 200, 100, 100), new Rect(0, 0, 1280, 720));
                Canvas.ForceUpdateCanvases();
                TextMeshProUGUI label = panel.PanelRect.GetComponentInChildren<TextMeshProUGUI>(); // 호버 문구 표시기
                label.ForceMeshUpdate();
                Assert.That(label.GetParsedText(), Is.EqualTo("변경 3"));
                SetField(reactor, "_hoverTextPanel", null);
            }
        }

        // 한국어 글꼴과 UI 배치를 렌더링해 검증 이미지를 생성하는 함수
        [TestCase(800, 680, false)]
        [TestCase(320, 560, false)]
        [TestCase(800, 680, true)]
        public void RenderSettings_HasVisibleTextAndNonblankPixels(int width, int height, bool global)
        {
            _window.Show();
            if (global) Click("공용 설정");
            _window.PanelRect.sizeDelta = new Vector2(width, height);
            GameObject cameraObject = new GameObject("Settings Test Camera", typeof(Camera)); // 검증 카메라 오브젝트
            Camera camera = cameraObject.GetComponent<Camera>(); // 검증 카메라
            RenderTexture target = new RenderTexture(1280, 720, 24); // 검증 렌더링 대상
            Texture2D pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false); // 검증 이미지
            RenderTexture previous = RenderTexture.active; // 이전 렌더링 대상
            try
            {
                Canvas canvas = _window.GetComponentsInChildren<Canvas>().Single(item => item.name == "Settings Canvas"); // 설정 캔버스
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.2f, 0.22f, 0.24f);
                camera.targetTexture = target;
                LayoutRebuilder.ForceRebuildLayoutImmediate(_window.PanelRect);
                Canvas.ForceUpdateCanvases();
                TextMeshProUGUI heading = _window.GetComponentsInChildren<TextMeshProUGUI>()
                    .First(item => item.text == "CustomKeyboardReactor"); // 제목 문구
                heading.ForceMeshUpdate();
                Assert.That(heading.rectTransform.rect.height, Is.GreaterThan(15));
                Assert.That(heading.textInfo.characterCount, Is.GreaterThan(0));
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                int lightPixels = pixels.GetPixels32().Count(pixel => pixel.r > 200 && pixel.g > 200 && pixel.b > 200); // 밝은 UI 픽셀 수
                Assert.That(lightPixels, Is.GreaterThan(10000));
                string results = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults")); // 검증 이미지 폴더
                Directory.CreateDirectory(results);
                File.WriteAllBytes(Path.Combine(results, $"SettingsUi-{width}-{(global ? "Global" : "Preset")}.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        // 현재 이름 입력 필드를 반환하는 함수
        private TMP_InputField NameInput()
        {
            return _window.GetComponentsInChildren<TMP_InputField>().Single(item => item.name == "Preset Name Input");
        }

        // 가장 앞에 표시된 같은 이름의 명령을 실행하는 함수
        private void Click(string title)
        {
            _window.GetComponentsInChildren<Button>().Last(item => item.name == title).onClick.Invoke();
        }

        // 테스트 런타임에 격리된 의존성을 주입하는 함수
        private static void SetField(ReactorController reactor, string name, object value)
        {
            typeof(ReactorController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reactor, value);
        }
    }
}
