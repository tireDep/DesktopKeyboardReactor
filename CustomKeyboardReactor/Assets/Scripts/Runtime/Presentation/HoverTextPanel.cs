using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor
{
    // 호버 문구와 카운트를 측정하여 화면 안에 표시하는 클래스
    public sealed class HoverTextPanel : IDisposable
    {
        private const float BaseFontSize = 18f; // 기본 글자 크기
        private const float MinimumFontRatio = 0.7f; // 최소 글자 크기 비율
        private const float MaximumWidth = 360f; // 기준 최대 패널 너비
        private const float MaximumHeightRatio = 0.4f; // 작업 영역 최대 높이 비율
        private const float CharacterGap = 16f; // 캐릭터와 패널 기준 간격
        private const float HorizontalPadding = 12f; // 기준 좌우 여백
        private const float VerticalPadding = 10f; // 기준 상하 여백
        private const int BackgroundSize = 16; // 배경 이미지 크기

        private readonly GameObject _canvasObject; // 호버 전용 캔버스 오브젝트
        private readonly Canvas _canvas; // 호버 전용 캔버스
        private readonly CanvasScaler _scaler; // 호버 UI 배율 조정기
        private readonly RectTransform _panelRect; // 패널 표시 영역
        private readonly TextMeshProUGUI _label; // 호버 문구 표시기
        private readonly TMP_FontAsset _font; // 한국어 동적 글꼴
        private readonly Texture2D _backgroundTexture; // 고정 배경 이미지
        private readonly Sprite _backgroundSprite; // 9-slice 배경 스프라이트
        private string _resolvedText = string.Empty; // 변수 치환 문구
        private string _protectedText = string.Empty; // 영어 단어 보호 문구
        private float _uiScale = 1f; // 공용 UI 배율
        private bool _enabled = true; // 호버 표시 활성 여부
        private bool _layoutDirty = true; // 텍스트 측정 갱신 여부
        private Vector2 _lastWorkAreaSize; // 마지막 작업 영역 크기
        private float _lastMaximumHeight; // 마지막 화면 기준 높이 제한
        private bool _disposed; // 리소스 해제 여부

        public bool IsVisible => _panelRect != null && _panelRect.gameObject.activeSelf; // 패널 표시 여부
        public RectTransform PanelRect => _panelRect; // 패널 표시 영역

        // 전용 캔버스와 고정 배경 및 한국어 글꼴을 생성하는 생성자
        public HoverTextPanel(Transform parent)
        {
            _font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular") ??
                    TMP_FontAsset.CreateFontAsset("Arial", "Regular") ??
                    throw new InvalidOperationException("호버 패널 글꼴을 불러올 수 없습니다.");
            _font.hideFlags = HideFlags.DontSave;
            _canvasObject = new GameObject("Hover Text Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler));
            _canvasObject.transform.SetParent(parent, false);
            _canvas = _canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 150;
            _scaler = _canvasObject.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            GameObject panelObject = new GameObject("Hover Text Panel", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image)); // 패널 오브젝트
            panelObject.transform.SetParent(_canvasObject.transform, false);
            _panelRect = panelObject.GetComponent<RectTransform>();
            _panelRect.anchorMin = Vector2.zero;
            _panelRect.anchorMax = Vector2.zero;
            _panelRect.pivot = Vector2.zero;
            _backgroundTexture = CreateBackgroundTexture();
            _backgroundSprite = Sprite.Create(_backgroundTexture,
                new Rect(0, 0, BackgroundSize, BackgroundSize), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(2, 2, 2, 2));
            Image background = panelObject.GetComponent<Image>(); // 패널 배경 표시기
            background.sprite = _backgroundSprite;
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;

            GameObject textObject = new GameObject("Hover Text", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI)); // 호버 문구 오브젝트
            textObject.transform.SetParent(_panelRect, false);
            _label = textObject.GetComponent<TextMeshProUGUI>();
            _label.font = _font;
            _label.fontSize = BaseFontSize;
            _label.enableAutoSizing = false;
            _label.richText = true;
            _label.parseCtrlCharacters = false;
            _label.textWrappingMode = TextWrappingModes.Normal;
            _label.alignment = TextAlignmentOptions.TopLeft;
            _label.color = new Color32(34, 39, 44, 255);
            _label.raycastTarget = false;
            _label.rectTransform.anchorMin = Vector2.zero;
            _label.rectTransform.anchorMax = Vector2.one;
            Hide();
        }

        // 활성 프리셋 기준 카운트 변수를 치환하는 함수
        public void SetContent(PresetData preset, long totalInputCount)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (totalInputCount < 0) throw new ArgumentOutOfRangeException(nameof(totalInputCount));
            int normalImageCount = preset.NormalImages.Count; // 활성 일반 이미지 수
            if (normalImageCount == 0) throw new ArgumentException("일반 이미지가 필요합니다.", nameof(preset));
            string resolvedText = (preset.HoverTextTemplate ?? string.Empty)
                .Replace("{TOTAL_INPUT_COUNT}", totalInputCount.ToString(CultureInfo.InvariantCulture))
                .Replace("{LOOP_COUNT}", (totalInputCount / normalImageCount).ToString(CultureInfo.InvariantCulture)); // 변수 치환 결과
            if (_resolvedText == resolvedText) return;
            _resolvedText = resolvedText;
            _protectedText = ProtectEnglishWords(resolvedText);
            _layoutDirty = true;
            if (resolvedText.Length == 0) Hide();
        }

        // 공용 표시 옵션과 캐릭터와 독립적인 UI 배율을 적용하는 함수
        public void SetSettings(GlobalSettingsData settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            float uiScale = settings.UiScalePercent / 100f; // 변경할 공용 UI 배율
            if (_uiScale != uiScale)
            {
                _uiScale = uiScale;
                _scaler.scaleFactor = uiScale;
                _canvas.scaleFactor = uiScale;
                _layoutDirty = true;
            }
            _enabled = settings.HoverTextPanelEnabled;
            if (!_enabled) Hide();
        }

        // 캐릭터 픽셀 호버와 공통 표시 영역 기준 패널 배치를 갱신하는 함수
        public void UpdateHover(bool characterHovered, Rect displayArea, Rect workArea)
        {
            if (_disposed) return;
            if (!characterHovered || !_enabled || _resolvedText.Length == 0 ||
                workArea.width <= 0 || workArea.height <= 0)
            {
                Hide();
                return;
            }
            _panelRect.gameObject.SetActive(true);
            float gap = CharacterGap * _uiScale; // 화면 기준 캐릭터 간격
            float aboveSpace = Mathf.Max(0, workArea.yMax - displayArea.yMax - gap); // 캐릭터 위 여유 공간
            float belowSpace = Mathf.Max(0, displayArea.yMin - workArea.yMin - gap); // 캐릭터 아래 여유 공간
            float maximumHeight = Mathf.Min(workArea.height * MaximumHeightRatio,
                Mathf.Max(aboveSpace, belowSpace)); // 겹침 없는 화면 기준 최대 높이
            if (maximumHeight <= 0)
            {
                Hide();
                return;
            }
            if (_lastWorkAreaSize != workArea.size)
            {
                _lastWorkAreaSize = workArea.size;
                _layoutDirty = true;
            }
            if (_panelRect.sizeDelta.y * _uiScale > maximumHeight ||
                (_label.isTextTruncated && maximumHeight > _lastMaximumHeight)) _layoutDirty = true;
            if (_layoutDirty) MeasureText(workArea, maximumHeight);
            _lastMaximumHeight = maximumHeight;

            Vector2 panelSize = _panelRect.sizeDelta * _uiScale; // 화면 기준 패널 크기
            float x = Mathf.Clamp(displayArea.center.x - panelSize.x * 0.5f,
                workArea.xMin, Mathf.Max(workArea.xMin, workArea.xMax - panelSize.x)); // 보정된 가로 위치
            float y = displayArea.yMax + gap; // 캐릭터 위 패널 위치
            if (y + panelSize.y > workArea.yMax) y = displayArea.yMin - gap - panelSize.y;
            y = Mathf.Clamp(y, workArea.yMin, Mathf.Max(workArea.yMin, workArea.yMax - panelSize.y));
            _panelRect.anchoredPosition = new Vector2(x, y) / _uiScale;
        }

        // 포인터 상호작용을 변경하지 않고 패널을 숨기는 함수
        public void Hide()
        {
            if (_panelRect != null) _panelRect.gameObject.SetActive(false);
        }

        // TMP 측정으로 너비 확장과 글자 축소 및 말줄임표를 적용하는 함수
        private void MeasureText(Rect workArea, float maximumScreenHeight)
        {
            float maximumWidth = Mathf.Min(MaximumWidth, workArea.width / _uiScale); // 제한된 UI 기준 너비
            float maximumHeight = maximumScreenHeight / _uiScale; // 제한된 UI 기준 높이
            float horizontalPadding = Mathf.Min(HorizontalPadding, maximumWidth * 0.25f); // 실제 좌우 여백
            float verticalPadding = Mathf.Min(VerticalPadding, maximumHeight * 0.25f); // 실제 상하 여백
            _label.fontSize = BaseFontSize;
            _label.overflowMode = TextOverflowModes.Overflow;
            Vector2 naturalSize = _label.GetPreferredValues(_protectedText, float.PositiveInfinity, float.PositiveInfinity); // 기본 문구 크기
            float width = Mathf.Min(maximumWidth, Mathf.Max(1f, naturalSize.x + horizontalPadding * 2)); // 실제 패널 너비
            float contentWidth = Mathf.Max(1f, width - horizontalPadding * 2); // 문구 표시 너비
            float contentHeight = Mathf.Max(1f, maximumHeight - verticalPadding * 2); // 문구 최대 높이
            float minimumFontSize = BaseFontSize * MinimumFontRatio; // 최소 글자 크기
            float longestEnglishWordWidth = MeasureLongestEnglishWord(); // 기본 크기 최장 영문 단어 너비
            string displayText = _protectedText; // 최종 표시 문구
            Vector2 preferred = _label.GetPreferredValues(displayText, contentWidth, float.PositiveInfinity); // 줄바꿈 문구 크기
            while ((preferred.x > contentWidth + 0.1f || preferred.y > contentHeight + 0.1f ||
                    longestEnglishWordWidth * _label.fontSize / BaseFontSize > contentWidth + 0.1f) &&
                   _label.fontSize > minimumFontSize)
            {
                _label.fontSize = Mathf.Max(minimumFontSize, _label.fontSize - 0.5f);
                preferred = _label.GetPreferredValues(displayText, contentWidth, float.PositiveInfinity);
            }
            if (_label.fontSize <= minimumFontSize &&
                longestEnglishWordWidth * minimumFontSize / BaseFontSize > contentWidth + 0.1f)
            {
                displayText = EscapeLiteral(_resolvedText);
                preferred = _label.GetPreferredValues(displayText, contentWidth, float.PositiveInfinity);
            }
            float height = Mathf.Min(maximumHeight, preferred.y + verticalPadding * 2); // 실제 패널 높이
            _panelRect.sizeDelta = new Vector2(width, height);
            _label.rectTransform.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            _label.rectTransform.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            _label.text = displayText;
            _label.overflowMode = TextOverflowModes.Ellipsis;
            _label.ForceMeshUpdate();
            _layoutDirty = false;
        }

        // TMP의 단어 내부 줄바꿈 없이 가장 긴 영문 단어 너비를 측정하는 함수
        private float MeasureLongestEnglishWord()
        {
            float width = 0f; // 최장 영문 단어 너비
            int index = 0; // 현재 문구 인덱스
            while (index < _resolvedText.Length)
            {
                int start = index; // 영문 단어 시작 위치
                while (index < _resolvedText.Length && _resolvedText[index] >= '!' && _resolvedText[index] <= '~') index++;
                if (index == start)
                {
                    index++;
                    continue;
                }
                string word = EscapeLiteral(_resolvedText.Substring(start, index - start)); // 문자 그대로의 영문 단어
                width = Mathf.Max(width, _label.GetPreferredValues(word,
                    float.PositiveInfinity, float.PositiveInfinity).x);
            }
            return width;
        }

        // 영어 단어의 내부 줄바꿈을 제한하고 사용자 태그를 문자로 보존하는 함수
        private static string ProtectEnglishWords(string text)
        {
            StringBuilder result = new StringBuilder(text.Length); // 단어 보호 문구 버퍼
            int index = 0; // 현재 문구 인덱스
            while (index < text.Length)
            {
                int start = index; // 현재 영문 단어 시작 위치
                while (index < text.Length && text[index] >= '!' && text[index] <= '~') index++;
                if (index > start)
                {
                    result.Append("<nobr>");
                    result.Append(EscapeLiteral(text.Substring(start, index - start)));
                    result.Append("</nobr>");
                }
                else
                {
                    result.Append(text[index++]);
                }
            }
            return result.ToString();
        }

        // TMP 태그 시작 문자를 해석되지 않는 문자로 변환하는 함수
        private static string EscapeLiteral(string text)
        {
            return text.Replace("<", "<noparse><</noparse>");
        }

        // 테두리가 있는 고정 사각형 배경 이미지를 생성하는 함수
        private static Texture2D CreateBackgroundTexture()
        {
            Texture2D texture = new Texture2D(BackgroundSize, BackgroundSize, TextureFormat.RGBA32, false); // 배경 텍스처
            texture.name = "Hover Text Panel Background";
            texture.hideFlags = HideFlags.DontSave;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[BackgroundSize * BackgroundSize]; // 배경 픽셀 목록
            for (int y = 0; y < BackgroundSize; y++) // 배경 픽셀 세로 인덱스
            {
                for (int x = 0; x < BackgroundSize; x++) // 배경 픽셀 가로 인덱스
                {
                    bool border = x == 0 || y == 0 || x == BackgroundSize - 1 || y == BackgroundSize - 1; // 테두리 픽셀 여부
                    pixels[y * BackgroundSize + x] = border
                        ? new Color32(137, 151, 157, 255)
                        : new Color32(245, 248, 249, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // 패널이 소유한 캔버스와 런타임 이미지 및 글꼴을 정리하는 함수
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Hide();
            DestroyOwnedObject(_canvasObject);
            if (_font != null)
            {
                DestroyOwnedObject(_font.material);
                foreach (Texture2D atlas in _font.atlasTextures) // 동적 글꼴 아틀라스 이미지
                    DestroyOwnedObject(atlas);
                DestroyOwnedObject(_font);
            }
            DestroyOwnedObject(_backgroundSprite);
            DestroyOwnedObject(_backgroundTexture);
        }

        // 실행 환경에 맞게 소유한 Unity 오브젝트를 해제하는 함수
        private static void DestroyOwnedObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
