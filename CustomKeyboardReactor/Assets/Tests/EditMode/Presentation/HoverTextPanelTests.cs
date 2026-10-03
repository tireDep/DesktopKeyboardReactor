using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 호버 문구와 화면 배치를 실제 TMP 렌더링으로 검증하는 클래스
    public sealed class HoverTextPanelTests
    {
        private GameObject _root; // 격리된 UI 오브젝트
        private HoverTextPanel _panel; // 테스트 호버 패널
        private PresetData _preset; // 테스트 활성 프리셋
        private static readonly Rect WorkArea = new Rect(0, 0, 1280, 720); // 테스트 모니터 작업 영역
        private static readonly Rect DisplayArea = new Rect(480, 220, 320, 320); // 공통 캐릭터 표시 영역

        // 파일 저장 없이 패널과 프리셋을 준비하는 함수
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Hover Panel Test");
            _panel = new HoverTextPanel(_root.transform);
            _preset = new PresetData { HoverTextTemplate = "안녕하세요" };
            _preset.NormalImages.Add(new ImageAssetData());
            _preset.NormalImages.Add(new ImageAssetData());
        }

        // 패널이 소유한 이미지와 글꼴을 정리하는 함수
        [TearDown]
        public void TearDown()
        {
            _panel.Dispose();
            Object.DestroyImmediate(_root);
        }

        // 큰 카운트와 활성 일반 이미지 수를 치환하는 함수
        [Test]
        public void HoverText_UsesTotalInputCountAndActivePresetLoopCount()
        {
            _preset.HoverTextTemplate = "{TOTAL_INPUT_COUNT} / {LOOP_COUNT} / {UNKNOWN}";
            Show(long.MaxValue);
            Assert.That(Label().GetParsedText(), Is.EqualTo("9223372036854775807 / 4611686018427387903 / {UNKNOWN}"));
            _preset.NormalImages.RemoveAt(1);
            Show(5);
            Assert.That(Label().GetParsedText(), Is.EqualTo("5 / 5 / {UNKNOWN}"));
            Show(0);
            Assert.That(Label().GetParsedText(), Is.EqualTo("0 / 0 / {UNKNOWN}"));
        }

        // 지원하지 않는 사용자 TMP 태그가 문자 그대로 보이는지 검증하는 함수
        [Test]
        public void HoverText_PreservesLiteralMarkupAndControlCharacters()
        {
            _preset.HoverTextTemplate = "<b>문구</b> </noparse><size=1000> abc\\n";
            Show();
            Assert.That(Label().GetParsedText(), Is.EqualTo(_preset.HoverTextTemplate));
        }

        // 캐릭터 호버와 표시 옵션 및 빈 문구를 검증하는 함수
        [Test]
        public void HoverText_RequiresCharacterHoverEnabledOptionAndNonemptyTemplate()
        {
            _panel.SetContent(_preset, 0);
            _panel.UpdateHover(false, DisplayArea, WorkArea);
            Assert.That(_panel.IsVisible, Is.False);
            Show();
            Assert.That(_panel.IsVisible, Is.True);
            _panel.SetSettings(new GlobalSettingsData { HoverTextPanelEnabled = false });
            _panel.UpdateHover(true, DisplayArea, WorkArea);
            Assert.That(_panel.IsVisible, Is.False);
            _panel.SetSettings(new GlobalSettingsData());
            Show();
            Assert.That(_panel.IsVisible, Is.True);
            _preset.HoverTextTemplate = string.Empty;
            Show();
            Assert.That(_panel.IsVisible, Is.False);
        }

        // 9-slice 배경과 문구가 포인터 상호작용에서 제외되는지 검증하는 함수
        [Test]
        public void HoverText_DoesNotBlockPointerAndUsesSlicedBackground()
        {
            Show();
            Image background = _panel.PanelRect.GetComponent<Image>(); // 테스트 패널 배경
            Assert.That(background.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(background.sprite.border, Is.EqualTo(new Vector4(2, 2, 2, 2)));
            Assert.That(_root.GetComponentsInChildren<Graphic>().All(item => !item.raycastTarget), Is.True);
            Assert.That(_root.GetComponentInChildren<GraphicRaycaster>(), Is.Null);
        }

        // 짧은 문구가 기본 크기와 공통 표시 영역 기준 간격을 유지하는지 검증하는 함수
        [Test]
        public void ShortHoverText_KeepsBaseFontAndCommonDisplayAreaAnchor()
        {
            Show();
            Assert.That(Label().fontSize, Is.EqualTo(18));
            Assert.That(_panel.PanelRect.anchoredPosition.y, Is.EqualTo(DisplayArea.yMax + 16));
            Assert.That(_panel.PanelRect.sizeDelta.x, Is.LessThan(360));
            Vector2 originalPosition = _panel.PanelRect.anchoredPosition; // 이전 패널 위치
            _panel.UpdateHover(true, DisplayArea, WorkArea);
            Assert.That(_panel.PanelRect.anchoredPosition, Is.EqualTo(originalPosition));
        }

        // 영어 단어는 축소로 맞출 수 있으면 내부에서 나누지 않는지 검증하는 함수
        [Test]
        public void EnglishWord_ShrinksBeforeCharacterWrapping()
        {
            _preset.HoverTextTemplate = new string('W', 25);
            Show();
            Assert.That(Label().fontSize, Is.LessThan(18));
            Assert.That(Label().fontSize, Is.GreaterThanOrEqualTo(12.6f - 0.001f));
            Assert.That(Label().textInfo.lineCount, Is.EqualTo(1));
            Assert.That(Label().isTextTruncated, Is.False);
        }

        // 최소 크기에서도 넘치는 영문 단어를 문자 단위로 줄바꿈하는지 검증하는 함수
        [Test]
        public void LongEnglishWord_WrapsCharactersOnlyAtMinimumFontSize()
        {
            _preset.HoverTextTemplate = new string('W', 120);
            Show();
            Assert.That(Label().fontSize, Is.EqualTo(12.6f).Within(0.001f));
            Assert.That(Label().text, Does.Not.Contain("<nobr>"));
            Assert.That(Label().textInfo.lineCount, Is.GreaterThan(1));
            Assert.That(_panel.PanelRect.sizeDelta.y, Is.LessThanOrEqualTo(720 * 0.4f));
        }

        // 긴 한국어 문구가 높이 제한과 최소 글자 크기 및 말줄임표를 사용하는지 검증하는 함수
        [Test]
        public void LongKoreanText_UsesHeightLimitMinimumFontAndEllipsis()
        {
            _preset.HoverTextTemplate = string.Concat(Enumerable.Repeat("긴한국어문구를표시합니다 ", 200));
            Show();
            Assert.That(_panel.PanelRect.sizeDelta.x, Is.LessThanOrEqualTo(360));
            Assert.That(_panel.PanelRect.sizeDelta.y, Is.LessThanOrEqualTo(720 * 0.4f));
            Assert.That(Label().fontSize, Is.EqualTo(12.6f).Within(0.001f));
            Assert.That(Label().isTextTruncated, Is.True);
            Assert.That(Label().textInfo.characterInfo.Take(Label().textInfo.characterCount)
                .Any(item => item.character == '\u2026'), Is.True);
        }

        // UI 배율별 화면 모서리와 위 공간 부족 배치를 검증하는 함수
        [TestCase(50, 0, 0)]
        [TestCase(100, 1180, 0)]
        [TestCase(200, 0, 620)]
        [TestCase(200, 1180, 620)]
        public void HoverText_StaysInsideWorkAreaAndMovesBelowAtTop(int percent, float x, float y)
        {
            _panel.SetSettings(new GlobalSettingsData { UiScalePercent = percent });
            _panel.SetContent(_preset, 0);
            Rect displayArea = new Rect(x, y, 100, 100); // 화면 모서리 캐릭터 영역
            _panel.UpdateHover(true, displayArea, WorkArea);
            float scale = percent / 100f; // 검증 UI 배율
            Rect bounds = new Rect(_panel.PanelRect.anchoredPosition * scale,
                _panel.PanelRect.sizeDelta * scale); // 화면 기준 패널 경계
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(WorkArea.xMin));
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(WorkArea.xMax + 0.01f));
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(WorkArea.yMin));
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(WorkArea.yMax + 0.01f));
            if (y > 0) Assert.That(bounds.yMax, Is.EqualTo(displayArea.yMin - 16 * scale).Within(0.01f));
        }

        // 긴 패널이 위아래 여유 공간에 맞춰 캐릭터와 겹치지 않는지 검증하는 함수
        [TestCase(100)]
        [TestCase(200)]
        public void LongHoverText_DoesNotOverlapCommonCharacterDisplayArea(int percent)
        {
            _preset.HoverTextTemplate = string.Concat(Enumerable.Repeat("긴 한국어 문구 ", 200));
            _panel.SetSettings(new GlobalSettingsData { UiScalePercent = percent });
            Show();
            float scale = percent / 100f; // 검증 UI 배율
            Rect bounds = new Rect(_panel.PanelRect.anchoredPosition * scale,
                _panel.PanelRect.sizeDelta * scale); // 화면 기준 패널 경계
            Assert.That(bounds.yMax <= DisplayArea.yMin - 16 * scale + 0.01f ||
                        bounds.yMin >= DisplayArea.yMax + 16 * scale - 0.01f, Is.True);
            Assert.That(Label().isTextTruncated, Is.True);
        }

        // 작업 영역 변경이 열린 패널의 크기와 배치를 다시 측정하는지 검증하는 함수
        [Test]
        public void HoverText_ReflowsWhenWorkAreaBecomesNarrow()
        {
            _preset.HoverTextTemplate = "A longer English sentence with several words and 한국어 문구";
            Show();
            _panel.SetSettings(new GlobalSettingsData { UiScalePercent = 200 });
            _panel.UpdateHover(true, new Rect(100, 10, 100, 100), new Rect(0, 0, 320, 240));
            Assert.That(_panel.PanelRect.sizeDelta.x * 2, Is.LessThanOrEqualTo(320));
            Assert.That(_panel.PanelRect.sizeDelta.y * 2, Is.LessThanOrEqualTo(240 * 0.4f));
            Assert.That(Label().fontSize, Is.GreaterThanOrEqualTo(12.6f - 0.001f));
        }

        // 배경과 한국어 및 영어 글자를 렌더링해 검증 이미지를 생성하는 함수
        [TestCase(50, "한국어 {TOTAL_INPUT_COUNT} / {LOOP_COUNT}")]
        [TestCase(100, "A longer English sentence with several words and 한국어 문구")]
        [TestCase(200, "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW")]
        public void RenderHoverText_HasBackgroundAndVisibleGlyphs(int percent, string template)
        {
            _preset.HoverTextTemplate = template;
            _panel.SetSettings(new GlobalSettingsData { UiScalePercent = percent });
            Show(9223372036854775807L);
            GameObject cameraObject = new GameObject("Hover Test Camera", typeof(Camera)); // 검증 카메라 오브젝트
            Camera camera = cameraObject.GetComponent<Camera>(); // 검증 카메라
            RenderTexture target = new RenderTexture(1280, 720, 24); // 검증 렌더링 대상
            Texture2D pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false); // 검증 이미지
            RenderTexture previous = RenderTexture.active; // 이전 렌더링 대상
            try
            {
                Canvas canvas = _root.GetComponentInChildren<Canvas>(); // 호버 캔버스
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.2f, 0.22f, 0.24f);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                Label().ForceMeshUpdate();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                string results = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults")); // 검증 이미지 폴더
                Directory.CreateDirectory(results);
                File.WriteAllBytes(Path.Combine(results, $"HoverText-{percent}.png"), pixels.EncodeToPNG());
                Assert.That(pixels.GetPixels32().Count(pixel => pixel.r > 230 && pixel.g > 230 && pixel.b > 230), Is.GreaterThan(100));
                Assert.That(pixels.GetPixels32().Count(pixel => pixel.r < 110 && pixel.g < 115 && pixel.b < 120), Is.GreaterThan(10));
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

        // 카운트 반영 후 공통 표시 영역 위에 패널을 표시하는 함수
        private void Show(long count = 0)
        {
            _panel.SetContent(_preset, count);
            _panel.UpdateHover(true, DisplayArea, WorkArea);
            Canvas.ForceUpdateCanvases();
            Label().ForceMeshUpdate();
        }

        // 패널의 실제 TMP 표시기를 반환하는 함수
        private TextMeshProUGUI Label()
        {
            return _panel.PanelRect.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
