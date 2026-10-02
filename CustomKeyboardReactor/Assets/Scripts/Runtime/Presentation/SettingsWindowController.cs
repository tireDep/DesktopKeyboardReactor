using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CustomKeyboardReactor
{
    // 설정 화면과 프리셋 초안의 사용자 흐름을 연결하는 클래스
    public sealed class SettingsWindowController : MonoBehaviour
    {
        private const float DesignWidth = 800f; // 설정 화면 기준 너비
        private const float DesignHeight = 680f; // 설정 화면 기준 높이
        private static readonly Color SurfaceColor = new Color(0.96f, 0.97f, 0.97f); // 화면 배경색
        private static readonly Color InkColor = new Color(0.12f, 0.16f, 0.18f); // 기본 문구 색상
        private static readonly Color AccentColor = new Color(0.13f, 0.48f, 0.39f); // 주요 명령 색상
        private ReactorController _reactor; // 런타임 반응 컨트롤러
        private PresetRepository _repository; // 프리셋 저장소
        private PresetAssetStore _assetStore; // 이미지 저장소
        private UserSettingsRepository _settingsRepository; // 공용 설정 저장소
        private PresetEditorController _editor; // 초안 편집 컨트롤러
        private GlobalSettingsData _settings; // 공용 설정
        private Canvas _canvas; // 설정 전용 캔버스
        private RectTransform _panel; // 설정 패널 영역
        private RectTransform _content; // 설정 내용 영역
        private TMP_FontAsset _font; // 한국어 동적 글꼴
        private TextMeshProUGUI _status; // 편집 상태 및 오류 문구
        private TextMeshProUGUI _nameError; // 프리셋 이름 검증 문구
        private Button _applyButton; // 프리셋 적용 버튼
        private GraphicRaycaster _raycaster; // 설정 및 팝업 포인터 판정기
        private PointerEventData _pointerEvent; // 재사용 포인터 이벤트
        private readonly List<RaycastResult> _pointerHits = new List<RaycastResult>(); // 포인터 UI 판정 결과
        private GameObject _dialog; // 현재 확인 대화상자
        private RawImage _preview; // 초안 이미지 미리보기
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(); // UI 텍스처 캐시
        private bool _showGlobalSettings; // 공용 설정 탭 선택 여부
        private bool _allowQuit; // 종료 확인 완료 여부
        private int _screenWidth; // 마지막 화면 너비
        private int _screenHeight; // 마지막 화면 높이

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf; // 설정 화면 표시 여부
        public RectTransform PanelRect => _panel; // 설정 상호작용 영역

        // 종료 확인 이벤트를 구독하는 함수
        private void OnEnable()
        {
            Application.wantsToQuit += HandleWantsToQuit;
        }

        // 종료 확인 이벤트를 해제하는 함수
        private void OnDisable()
        {
            Application.wantsToQuit -= HandleWantsToQuit;
        }

        // 저장 계층과 런타임 컨트롤러를 연결하는 함수
        public void Initialize(ReactorController reactor, PresetRepository repository,
            PresetAssetStore assetStore, UserSettingsRepository settingsRepository)
        {
            _reactor = reactor;
            _repository = repository;
            _assetStore = assetStore;
            _settingsRepository = settingsRepository;
            _editor = new PresetEditorController(repository, assetStore);
        }

        // 설정 화면을 열고 활성 프리셋 초안을 불러오는 함수
        public void Show()
        {
            if (IsOpen) return;
            EnsureHierarchy();
            _settings = _settingsRepository.GetSettings();
            _editor.Select(_repository.GetActive().Id);
            _panel.gameObject.SetActive(true);
            _reactor.OpenSettings();
            ApplyLayout();
            RebuildContent();
        }

        // UI 배율과 화면 변경을 설정 패널에 반영하는 함수
        private void Update()
        {
            if (!IsOpen) return;
            if (_screenWidth != Screen.width || _screenHeight != Screen.height) ApplyLayout();
            _applyButton.interactable = _editor.CanApply && _dialog == null;
        }

        // 설정 화면 안의 포인터인지 판정하는 함수
        public bool ContainsScreenPoint(Vector2 point)
        {
            if (!IsOpen) return false;
            if (RectTransformUtility.RectangleContainsScreenPoint(_panel, point)) return true;
            _pointerHits.Clear();
            _pointerEvent.position = point;
            _raycaster.Raycast(_pointerEvent, _pointerHits);
            return _pointerHits.Count > 0;
        }

        // 변경 확인 후 설정 화면을 닫는 함수
        public void RequestClose()
        {
            ResolveChanges(CloseWindow);
        }

        // 변경 확인 후 앱 종료를 요청하는 함수
        public void RequestExit()
        {
            ResolveChanges(() =>
            {
                _allowQuit = true;
                Application.Quit();
            });
        }

        // 운영체제 종료 요청에 편집 변경 확인을 연결하는 함수
        private bool HandleWantsToQuit()
        {
            if (_allowQuit || !IsOpen || !_editor.IsDirty) return true;
            RequestExit();
            return false;
        }

        // 적용과 버리기, 취소 선택으로 요청한 동작을 결정하는 함수
        private void ResolveChanges(Action continuation)
        {
            if (_dialog != null) return;
            if (!_editor.IsDirty)
            {
                Guard(continuation);
                return;
            }
            ShowDialog("변경 사항을 적용할까요?",
                ("적용", () => { ApplyDraft(); continuation(); }),
                ("버리기", () => { DiscardDraft(); continuation(); }),
                ("취소", () => RebuildContent(false)));
        }

        // 초안을 적용하고 열린 설정 상태를 유지하는 함수
        private void ApplyDraft()
        {
            if (!_editor.CanApply)
                throw new InvalidOperationException("프리셋 이름과 일반 이미지 1장 이상이 필요합니다.");
            _reactor.ApplyPreset(_editor.Apply());
            _reactor.OpenSettings();
            RebuildContent();
        }

        // 편집 시작 데이터와 활성 캐릭터 표시를 복원하는 함수
        private void DiscardDraft()
        {
            _editor.Discard();
            if (_editor.IsNew) _editor.Select(_repository.GetActive().Id);
            _reactor.RestoreActivePreview();
            RebuildContent(false);
        }

        // 편집 자원을 정리하고 설정 진입 전 상태로 돌아가는 함수
        private void CloseWindow()
        {
            _reactor.RestoreActivePreview();
            ReleaseTextures();
            _editor.Dispose();
            _panel.gameObject.SetActive(false);
            _reactor.CloseSettings();
        }

        // 저장 실패나 검증 실패를 설정 화면에 표시하는 함수
        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception)
            {
                _status.text = "작업을 완료하지 못했습니다. 이름, 이미지 파일과 저장 폴더의 쓰기 권한을 확인하세요.";
                _status.color = new Color(0.65f, 0.15f, 0.22f);
            }
        }

        // 설정 캔버스와 제목, 탭, 스크롤 및 하단 명령을 생성하는 함수
        private void EnsureHierarchy()
        {
            if (_canvas != null) return;
            _font = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular") ??
                TMP_FontAsset.CreateFontAsset("Arial", "Regular") ??
                throw new InvalidOperationException("설정 화면 글꼴을 불러올 수 없습니다.");
            _font.hideFlags = HideFlags.DontSave;
            GameObject canvasObject = new GameObject("Settings Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); // 설정 캔버스 오브젝트
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 300;
            if (EventSystem.current == null)
            {
                GameObject eventObject = new GameObject("Settings Event System",
                    typeof(EventSystem), typeof(InputSystemUIInputModule)); // UI 입력 오브젝트
                eventObject.transform.SetParent(transform, false);
                eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            _pointerEvent = new PointerEventData(EventSystem.current);
            _panel = CreateRect("Settings Panel", canvasObject.transform);
            _panel.gameObject.AddComponent<Image>().color = SurfaceColor;
            VerticalLayoutGroup layout = _panel.gameObject.AddComponent<VerticalLayoutGroup>(); // 설정 외곽 배치
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            RectTransform header = Row(_panel, 38); // 제목 행
            Label(header, "CustomKeyboardReactor", 24, 0);
            CreateButton(header, "×", RequestClose, 38);
            RectTransform tabs = Row(_panel, 38); // 설정 탭 행
            CreateButton(tabs, "프리셋", () => { _showGlobalSettings = false; RebuildContent(); }, 120);
            CreateButton(tabs, "공용 설정", () => { _showGlobalSettings = true; RebuildContent(false); }, 120);
            RectTransform scrollRoot = CreateRect("Settings Scroll", _panel); // 설정 스크롤 영역
            scrollRoot.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); // 설정 스크롤 컨트롤러
            RectTransform viewport = CreateRect("Viewport", scrollRoot); // 내용 표시 영역
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-14, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = SurfaceColor;
            _content = Column(viewport, "Settings Content");
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.sizeDelta = new Vector2(-12, 0);
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.verticalScrollbar = CreateScrollbar(scrollRoot, true);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            _status = Label(_panel, "", 14, 44);
            RectTransform footer = Row(_panel, 38); // 하단 명령 행
            _applyButton = CreateButton(footer, "적용", ApplyDraft, 0, true);
            CreateButton(footer, "버리기", DiscardDraft);
            CreateButton(footer, "닫기", RequestClose);
            CreateButton(footer, "종료", RequestExit);
            _panel.gameObject.SetActive(false);
        }

        // 현재 UI 배율에서 화면 안의 패널 크기를 계산하는 함수
        private void ApplyLayout()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            float scale = _settings.UiScalePercent / 100f; // 사용자 UI 배율
            _canvas.GetComponent<CanvasScaler>().scaleFactor = scale;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(Mathf.Min(DesignWidth, Screen.width / scale - 24),
                Mathf.Min(DesignHeight, Screen.height / scale - 24));
        }

        // 선택 탭의 내용과 초안 상태를 갱신하는 함수
        private void RebuildContent(bool previewDraft = true)
        {
            foreach (Transform child in _content.Cast<Transform>().ToArray())
            {
                child.gameObject.SetActive(false);
                ReleaseObject(child.gameObject);
            }
            ReleaseTextures();
            _nameError = null;
            if (_showGlobalSettings) BuildGlobalSettings();
            else BuildPresetEditor();
            _status.color = InkColor;
            _status.text = _editor.IsDirty ? "적용하지 않은 변경 사항" : "활성 프리셋: " + _repository.GetActive().Name;
            if (!_showGlobalSettings) PreviewDraft(null, previewDraft);
        }

        // 프리셋 선택과 편집 항목을 생성하는 함수
        private void BuildPresetEditor()
        {
            Label(_content, "프리셋 목록", 17, 28);
            RectTransform list = Row(_content, 38); // 프리셋 선택 행
            IReadOnlyList<PresetData> presets = _repository.GetAll(); // 저장 프리셋 목록
            CreateDropdown(list, presets.Select(item => item.Name).ToList(),
                Math.Max(0, presets.ToList().FindIndex(item => item.Id == _editor.Draft.Id)),
                index => ResolveChanges(() => { _editor.Select(presets[index].Id); RebuildContent(); }));
            RectTransform commands = Row(_content, 38); // 프리셋 관리 명령 행
            CreateButton(commands, "+ 새 프리셋", () => ResolveChanges(() =>
            {
                _editor.Create("새 프리셋");
                RebuildContent();
            }));
            CreateButton(commands, "복제", () => ResolveChanges(() =>
            {
                PresetData duplicate = _repository.Duplicate(_editor.Draft.Id,
                    _editor.Draft.Name + " 복사"); // 독립 복제 프리셋
                _editor.Select(duplicate.Id);
                RebuildContent();
            })).interactable = !_editor.IsNew;
            CreateButton(commands, "삭제", ConfirmDelete);
            Label(_content, "프리셋 이름", 15, 24);
            CreateInput(_content, _editor.Draft.Name, false, value =>
            {
                _editor.Draft.Name = value;
                RefreshDirtyStatus();
            });
            _nameError = Label(_content, "프리셋 이름을 입력하세요.", 13, 24);
            _nameError.color = new Color(0.65f, 0.15f, 0.22f);
            _nameError.gameObject.SetActive(string.IsNullOrWhiteSpace(_editor.Draft.Name));
            RectTransform previewRow = Row(_content, 122); // 미리보기 및 크기 행
            RectTransform previewRect = CreateRect("Draft Preview", previewRow); // 미리보기 영역
            SetSize(previewRect, 122, 122);
            previewRect.gameObject.AddComponent<Image>().color = new Color(0.86f, 0.89f, 0.9f);
            RectTransform imageRect = CreateRect("Preview Image", previewRect); // 미리보기 이미지 영역
            Stretch(imageRect, 8);
            _preview = imageRect.gameObject.AddComponent<RawImage>();
            _preview.raycastTarget = false;
            _preview.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            Label(previewRow, "미리보기", 15, 0);
            RectTransform scaleColumn = Column(_content, "Character Scale"); // 캐릭터 크기 영역
            Label(scaleColumn, "캐릭터 크기", 15, 30);
            CreateStepper(scaleColumn, _editor.Draft.CharacterScalePercent,
                PresetData.MinimumCharacterScalePercent, PresetData.MaximumCharacterScalePercent,
                PresetData.CharacterScaleStepPercent, value =>
                {
                    _editor.Draft.CharacterScalePercent = value;
                    PreviewDraft();
                    RefreshDirtyStatus();
                }, true);
            BuildImages(false);
            BuildImages(true);
            Label(_content, "호버 문구", 17, 28);
            CreateInput(_content, _editor.Draft.HoverTextTemplate, true, value =>
            {
                _editor.Draft.HoverTextTemplate = value;
                RefreshDirtyStatus();
            });
            RectTransform variables = Row(_content, 34); // 호버 문구 변수 명령 행
            CreateButton(variables, "{TOTAL_INPUT_COUNT}", () => AppendVariable("{TOTAL_INPUT_COUNT}"));
            CreateButton(variables, "{LOOP_COUNT}", () => AppendVariable("{LOOP_COUNT}"));
            Label(_content, "전체 입력 수 · 활성 프리셋 기준 환산 루프 수", 13, 26);
        }

        // 호버 문구에 지원 변수를 추가하는 함수
        private void AppendVariable(string variable)
        {
            _editor.Draft.HoverTextTemplate += variable;
            RebuildContent();
        }

        // 변경 여부와 필수 필드 오류를 표시하는 함수
        private void RefreshDirtyStatus()
        {
            if (_nameError != null) _nameError.gameObject.SetActive(string.IsNullOrWhiteSpace(_editor.Draft.Name));
            _status.text = string.IsNullOrWhiteSpace(_editor.Draft.Name) ? "프리셋 이름을 입력하세요." :
                _editor.Draft.NormalImages.Count == 0 ? "일반 이미지를 1장 이상 추가하세요." :
                _editor.IsDirty ? "적용하지 않은 변경 사항" : "변경 사항 없음";
            _status.color = _editor.CanApply ? InkColor : new Color(0.65f, 0.15f, 0.22f);
        }

        // 선택 초안을 캐릭터 및 설정 미리보기에 표시하는 함수
        private void PreviewDraft(ImageAssetData selectedImage = null, bool updateReactor = true)
        {
            ImageAssetData image = selectedImage ?? _editor.Draft.NormalImages.FirstOrDefault(); // 미리보기 이미지
            if (updateReactor) _reactor.PreviewPreset(_editor.Draft, image);
            if (_preview == null) return;
            _preview.enabled = image != null;
            if (image == null) return;
            Texture2D texture = LoadUiTexture(image); // 미리보기 텍스처
            _preview.texture = texture;
            _preview.GetComponent<AspectRatioFitter>().aspectRatio = texture.width / (float)texture.height;
        }

        // 프리셋 삭제 전 확인을 표시하는 함수
        private void ConfirmDelete()
        {
            ShowDialog("이 프리셋과 소유 이미지를 삭제할까요?", ("삭제", () =>
            {
                string activeId = _repository.GetActive().Id; // 삭제 전 활성 프리셋 ID
                string selectedId = _editor.Draft.Id; // 삭제할 초안 ID
                if (!_editor.IsNew) _repository.Delete(selectedId);
                _editor.Dispose();
                PresetData active = _repository.GetActive(); // 삭제 후 활성 프리셋
                if (active.Id != activeId) { _reactor.ApplyPreset(active); _reactor.OpenSettings(); }
                _editor.Select(active.Id);
                RebuildContent();
            }), ("취소", () => { }));
        }

        // 일반 및 대기 이미지의 썸네일 목록을 생성하는 함수
        private void BuildImages(bool idle)
        {
            List<ImageAssetData> images = idle ? _editor.Draft.IdleImages : _editor.Draft.NormalImages; // 편집 이미지 목록
            RectTransform heading = Row(_content, 34); // 이미지 목록 제목 행
            Label(heading, idle ? $"대기 이미지 · {images.Count}" : $"일반 이미지 · {images.Count}", 17, 0);
            CreateButton(heading, "+ 이미지", () => ImportImages(idle), 100);
            RectTransform strip = CreateRect(idle ? "Idle Images" : "Normal Images", _content); // 썸네일 스크롤 영역
            SetSize(strip, 0, 164);
            ScrollRect scroll = strip.gameObject.AddComponent<ScrollRect>(); // 썸네일 스크롤 컨트롤러
            RectTransform viewport = CreateRect("Viewport", strip); // 썸네일 표시 영역
            Stretch(viewport);
            viewport.offsetMin = new Vector2(0, 14);
            viewport.gameObject.AddComponent<Image>().color = new Color(0.90f, 0.92f, 0.93f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform imageContent = Row(viewport, 144); // 썸네일 내용 영역
            imageContent.anchorMin = imageContent.anchorMax = new Vector2(0, 1);
            imageContent.pivot = new Vector2(0, 1);
            imageContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = imageContent;
            scroll.vertical = false;
            scroll.horizontalScrollbar = CreateScrollbar(strip, false);
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            List<RectTransform> tiles = new List<RectTransform>(); // 드래그 대상 썸네일 목록
            for (int index = 0; index < images.Count; index++) // 썸네일 인덱스
            {
                int imageIndex = index; // 콜백 이미지 인덱스
                ImageAssetData image = images[index]; // 썸네일 이미지 참조
                RectTransform tile = Column(imageContent, $"Image {index + 1}"); // 썸네일 영역
                SetSize(tile, 124, 144);
                tiles.Add(tile);
                RectTransform thumbnail = CreateRect("Thumbnail", tile); // 이미지 표시 영역
                SetSize(thumbnail, 124, 76);
                Image thumbnailBackground = thumbnail.gameObject.AddComponent<Image>(); // 썸네일 배경
                thumbnailBackground.color = new Color(0.84f, 0.87f, 0.88f);
                RectTransform picture = CreateRect("Picture", thumbnail); // 비율 유지 이미지 영역
                Stretch(picture);
                RawImage raw = picture.gameObject.AddComponent<RawImage>(); // 썸네일 표시기
                Texture2D texture = LoadUiTexture(image); // 썸네일 텍스처
                raw.texture = texture;
                picture.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                picture.GetComponent<AspectRatioFitter>().aspectRatio = texture.width / (float)texture.height;
                thumbnail.gameObject.AddComponent<Button>().onClick.AddListener(() => Guard(() => PreviewDraft(image)));
                Label(tile, $"{index + 1} · {image.OriginalWidth}×{image.OriginalHeight}", 12, 20);
                RectTransform actions = Row(tile, 28); // 썸네일 명령 행
                if (!idle)
                {
                    CreateButton(actions, "←", () => MoveImage(imageIndex, imageIndex - 1), 28).interactable = index > 0;
                    CreateButton(actions, "→", () => MoveImage(imageIndex, imageIndex + 1), 28).interactable = index < images.Count - 1;
                    EventTrigger trigger = thumbnail.gameObject.AddComponent<EventTrigger>(); // 썸네일 드래그 이벤트
                    AddTrigger(trigger, EventTriggerType.BeginDrag, data => raw.color = new Color(0.5f, 0.85f, 0.7f));
                    AddTrigger(trigger, EventTriggerType.Drag, data => { });
                    AddTrigger(trigger, EventTriggerType.EndDrag, data =>
                    {
                        raw.color = Color.white;
                        PointerEventData pointer = (PointerEventData)data; // 드롭 포인터 좌표
                        for (int target = 0; target < tiles.Count; target++) // 드롭 대상 인덱스
                        {
                            if (!RectTransformUtility.RectangleContainsScreenPoint(tiles[target], pointer.position)) continue;
                            Guard(() => MoveImage(imageIndex, target));
                            break;
                        }
                    });
                }
                CreateButton(actions, "×", () =>
                {
                    images.RemoveAt(imageIndex);
                    RebuildContent();
                    RefreshDirtyStatus();
                }, 28);
            }
            if (images.Count == 0)
                Label(imageContent, idle ? "대기 이미지 없음" : "일반 이미지가 필요합니다.", 14, 100);
            if (HasUnevenImages())
                Label(_content, "이미지 크기 또는 비율 차이가 큽니다. 전환 시 캐릭터 위치가 달라 보일 수 있습니다.", 13, 44);
        }

        // 이미지 가져오기 후 목록과 미리보기를 갱신하는 함수
        private void ImportImages(bool idle)
        {
            string[] paths = WindowsImageFileDialog.SelectImages(); // 선택 이미지 경로 목록
            int failed = 0; // 가져오기 실패 수
            foreach (string path in paths)
            {
                try { _editor.Import(path, idle); }
                catch (Exception) { failed++; }
            }
            RebuildContent();
            RefreshDirtyStatus();
            if (failed > 0) _status.text = $"{failed}개 파일을 가져오지 못했습니다. PNG/JPG/JPEG 파일과 저장 권한을 확인하세요.";
        }

        // 일반 이미지의 순서와 미리보기를 갱신하는 함수
        private void MoveImage(int source, int target)
        {
            _editor.MoveNormalImage(source, target);
            RebuildContent();
        }

        // 큰 해상도 및 비율 차이를 판정하는 함수
        private bool HasUnevenImages()
        {
            List<ImageAssetData> images = _editor.Draft.NormalImages.Concat(_editor.Draft.IdleImages).ToList(); // 전체 초안 이미지 목록
            if (images.Count < 2) return false;
            float minSide = images.Min(item => Math.Max(item.OriginalWidth, item.OriginalHeight)); // 최소 긴 변 크기
            float maxSide = images.Max(item => Math.Max(item.OriginalWidth, item.OriginalHeight)); // 최대 긴 변 크기
            float minRatio = images.Min(item => item.OriginalWidth / (float)Math.Max(1, item.OriginalHeight)); // 최소 가로세로 비율
            float maxRatio = images.Max(item => item.OriginalWidth / (float)Math.Max(1, item.OriginalHeight)); // 최대 가로세로 비율
            return maxSide > minSide * 1.5f || maxRatio > minRatio * 1.5f;
        }

        // 공용 설정과 초기화 명령을 생성하는 함수
        private void BuildGlobalSettings()
        {
            Label(_content, "입력과 대기", 18, 30);
            CreateToggle(_content, "키보드 반응", _settings.KeyboardReactionEnabled,
                value => SaveSetting(() => _settings.KeyboardReactionEnabled = value));
            CreateToggle(_content, "마우스 버튼 반응", _settings.MouseReactionEnabled,
                value => SaveSetting(() => _settings.MouseReactionEnabled = value));
            CreateToggle(_content, "대기 기능", _settings.IdleEnabled,
                value => SaveSetting(() => _settings.IdleEnabled = value));
            RectTransform idleRow = Row(_content, 38); // 대기 시간 설정 행
            Label(idleRow, "대기 진입 시간", 15, 0);
            CreateButton(idleRow, "−", () => ChangeIdleMinutes(-1), 38);
            Label(idleRow, $"{_settings.IdleTimeoutSeconds / 60}분", 15, 0);
            CreateButton(idleRow, "+", () => ChangeIdleMinutes(1), 38);
            Label(_content, "화면과 표시", 18, 30);
            CreateToggle(_content, "항상 위", _settings.AlwaysOnTop,
                value => SaveSetting(() => _settings.AlwaysOnTop = value));
            CreateToggle(_content, "위치 잠금", _settings.PositionLocked,
                value => SaveSetting(() => _settings.PositionLocked = value));
            CreateToggle(_content, "호버 텍스트 패널", _settings.HoverTextPanelEnabled,
                value => SaveSetting(() => _settings.HoverTextPanelEnabled = value));
            Label(_content, "표시 모니터", 15, 24);
            IReadOnlyList<WindowsMonitorService.MonitorWorkArea> monitors =
                new WindowsMonitorService().GetMonitors(); // 표시 모니터 목록
            if (monitors.Count > 0)
                CreateDropdown(_content, monitors.Select((item, index) =>
                    $"모니터 {index + 1} · {item.WorkArea.width}×{item.WorkArea.height}").ToList(),
                    Math.Max(0, monitors.ToList().FindIndex(item => item.DeviceId == _settings.MonitorDeviceId)),
                    index => SaveSetting(() => _settings.MonitorDeviceId = monitors[index].DeviceId));
            Label(_content, "UI 크기", 15, 28);
            CreateStepper(_content, _settings.UiScalePercent,
                GlobalSettingsData.MinimumUiScalePercent, GlobalSettingsData.MaximumUiScalePercent,
                GlobalSettingsData.UiScaleStepPercent,
                value => SaveSetting(() => _settings.UiScalePercent = value), false);
            Label(_content, "데이터", 18, 30);
            Label(_content, $"전체 입력 수: {_reactor.TotalInputCount:N0}", 15, 30);
            RectTransform resets = Row(_content, 38); // 데이터 초기화 명령 행
            CreateButton(resets, "카운트 초기화", () => ShowDialog("전체 입력 수를 0으로 초기화할까요?",
                ("초기화", () => { _reactor.ResetInputCount(); RebuildContent(false); }),
                ("취소", () => { })));
            CreateButton(resets, "전체 데이터 초기화", () => ShowDialog("프리셋, 이미지, 설정과 전체 입력 수를 모두 초기화할까요?",
                ("전체 초기화", () =>
                {
                    _reactor.ResetAllData();
                    _editor.Dispose();
                    _editor.Select(_repository.GetActive().Id);
                    _settings = _settingsRepository.GetSettings();
                    ApplyLayout();
                    RebuildContent();
                }), ("취소", () => { })));
        }

        // 공용 설정 변경을 저장 및 런타임에 반영하는 함수
        private void SaveSetting(Action change)
        {
            GlobalSettingsData previous = _settingsRepository.GetSettings(); // 변경 전 공용 설정
            try
            {
                _settings = _settingsRepository.GetSettings();
                change();
                _settingsRepository.SaveSettings(_settings);
                _settings = _settingsRepository.GetSettings();
                _reactor.UpdateGlobalSettings(_settings);
                ApplyLayout();
            }
            catch
            {
                _settings = previous;
                RebuildContent(false);
                throw;
            }
        }

        // 대기 시간을 제품 범위의 분 단위로 변경하는 함수
        private void ChangeIdleMinutes(int delta)
        {
            SaveSetting(() => _settings.IdleTimeoutSeconds = Mathf.Clamp(
                _settings.IdleTimeoutSeconds + delta * 60,
                GlobalSettingsData.MinimumIdleTimeoutSeconds, GlobalSettingsData.MaximumIdleTimeoutSeconds));
            RebuildContent(false);
        }

        // 독립 제한값을 사용하는 동일 모양의 크기 스테퍼를 생성하는 함수
        private void CreateStepper(Transform parent, int value, int minimum, int maximum,
            int step, Action<int> changed, bool character)
        {
            PercentStepper model = new PercentStepper(value, minimum, maximum, step); // 백분율 계산기
            RectTransform row = Row(parent, 44); // 스테퍼 행
            TextMeshProUGUI current = null; // 현재 값 문구
            Action refresh = () => // 스테퍼 문구 갱신 함수
            {
                current.text = character ? $"{model.Value}% · {Mathf.RoundToInt(CharacterScaleCalculator.CalculateDisplayAreaSize(model.Value, Screen.width, Screen.height))}px" :
                    $"{model.Value}%";
            };
            foreach (int delta in new[] { -10, -5 }) // 감소 퍼센트 포인트
                CreateButton(row, delta.ToString(), () => { changed(model.Change(delta)); refresh(); }, 32);
            current = Label(row, "", 13, 0);
            current.alignment = TextAlignmentOptions.Center;
            foreach (int delta in new[] { 5, 10 }) // 증가 퍼센트 포인트
                CreateButton(row, "+" + delta, () => { changed(model.Change(delta)); refresh(); }, 32);
            Button reset = CreateButton(row, "초기화", () => { changed(model.Set(100)); refresh(); }, 56); // 크기 초기화 버튼
            EventTrigger tooltip = reset.gameObject.AddComponent<EventTrigger>(); // 초기화 설명 이벤트
            AddTrigger(tooltip, EventTriggerType.PointerEnter, data => _status.text = "크기 초기화 · 100%");
            AddTrigger(tooltip, EventTriggerType.PointerExit, data => RefreshDirtyStatus());
            refresh();
        }

        // 확인 대화상자와 선택 명령을 생성하는 함수
        private void ShowDialog(string message, params (string label, Action action)[] choices)
        {
            if (_dialog != null) return;
            RectTransform overlay = CreateRect("Confirmation Dialog", _panel); // 대화상자 차단 영역
            overlay.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(overlay, -20);
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.35f);
            _dialog = overlay.gameObject;
            RectTransform box = Column(overlay, "Dialog Content"); // 확인 내용 영역
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(Mathf.Min(480, _panel.sizeDelta.x - 40), 190);
            box.gameObject.AddComponent<Image>().color = SurfaceColor;
            box.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(16, 16, 18, 18);
            Label(box, message, 17, 96);
            RectTransform row = Row(box, 40); // 확인 선택 명령 행
            foreach ((string label, Action action) choice in choices)
                CreateButton(row, choice.label, () =>
                {
                    _dialog.SetActive(false);
                    ReleaseObject(_dialog);
                    _dialog = null;
                    choice.action();
                }, 0, choice.label == "적용");
        }

        // 설정 화면의 텍스트 입력 필드를 생성하는 함수
        private TMP_InputField CreateInput(Transform parent, string value, bool multiline, Action<string> changed)
        {
            RectTransform rect = CreateRect(multiline ? "Hover Text Input" : "Preset Name Input", parent); // 입력 필드 영역
            SetSize(rect, 0, multiline ? 90 : 38);
            Image background = rect.gameObject.AddComponent<Image>(); // 입력 배경
            background.color = Color.white;
            TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>(); // 문구 입력기
            RectTransform viewport = CreateRect("Text Viewport", rect); // 입력 문구 표시 영역
            Stretch(viewport, 8);
            viewport.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI text = Label(viewport, "", 15, 0); // 입력 문구 표시기
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.TopLeft;
            TextMeshProUGUI placeholder = Label(viewport, multiline ? "호버 문구" : "프리셋 이름", 15, 0); // 입력 안내 문구
            Stretch(placeholder.rectTransform);
            placeholder.color = new Color(0.5f, 0.53f, 0.55f);
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = background;
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            input.SetTextWithoutNotify(value);
            input.onValueChanged.AddListener(next => Guard(() => changed(next)));
            return input;
        }

        // 항목 목록과 팝업을 갖춘 선택 메뉴를 생성하는 함수
        private TMP_Dropdown CreateDropdown(Transform parent, List<string> options, int selected, Action<int> changed)
        {
            RectTransform rect = CreateRect("Selection", parent); // 선택 메뉴 영역
            SetSize(rect, 0, 38);
            Image background = rect.gameObject.AddComponent<Image>(); // 선택 메뉴 배경
            background.color = Color.white;
            TMP_Dropdown dropdown = rect.gameObject.AddComponent<TMP_Dropdown>(); // 항목 선택기
            TextMeshProUGUI caption = Label(rect, "", 15, 0); // 선택된 항목 문구
            Stretch(caption.rectTransform, 8);
            caption.rectTransform.offsetMax = new Vector2(-32, -8);
            TextMeshProUGUI arrow = Label(rect, "▼", 12, 0); // 선택 목록 펼치기 표시
            arrow.rectTransform.anchorMin = new Vector2(1, 0);
            arrow.rectTransform.anchorMax = Vector2.one;
            arrow.rectTransform.sizeDelta = new Vector2(24, 0);
            arrow.rectTransform.anchoredPosition = new Vector2(-12, 0);
            arrow.alignment = TextAlignmentOptions.Center;
            dropdown.targetGraphic = background;
            dropdown.captionText = caption;
            RectTransform template = CreateRect("Template", rect); // 선택 목록 팝업
            template.anchorMin = new Vector2(0, 0);
            template.anchorMax = new Vector2(1, 0);
            template.pivot = new Vector2(0.5f, 1);
            template.anchoredPosition = new Vector2(0, -2);
            template.sizeDelta = new Vector2(0, 170);
            template.gameObject.AddComponent<Image>().color = Color.white;
            ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>(); // 선택 목록 스크롤
            RectTransform viewport = CreateRect("Viewport", template); // 선택 목록 표시 영역
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect("Content", viewport); // 선택 목록 내용 영역
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 38);
            RectTransform item = CreateRect("Item", content); // 선택 항목 영역
            Stretch(item);
            Image itemBackground = item.gameObject.AddComponent<Image>(); // 선택 항목 배경
            itemBackground.color = new Color(0.88f, 0.93f, 0.91f);
            Toggle toggle = item.gameObject.AddComponent<Toggle>(); // 선택 항목 토글
            toggle.targetGraphic = itemBackground;
            toggle.graphic = itemBackground;
            TextMeshProUGUI itemText = Label(item, "", 15, 0); // 선택 항목 문구
            Stretch(itemText.rectTransform, 8);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            dropdown.template = template;
            dropdown.itemText = itemText;
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(selected);
            template.gameObject.SetActive(false);
            dropdown.onValueChanged.AddListener(index => Guard(() => changed(index)));
            return dropdown;
        }

        // 체크박스와 설정 이름을 생성하는 함수
        private void CreateToggle(Transform parent, string title, bool value, Action<bool> changed)
        {
            RectTransform row = Row(parent, 36); // 체크박스 설정 행
            RectTransform square = CreateRect(title, row); // 체크박스 영역
            SetSize(square, 24, 24);
            Image background = square.gameObject.AddComponent<Image>(); // 체크박스 배경
            background.color = new Color(0.75f, 0.80f, 0.79f);
            Toggle toggle = square.gameObject.AddComponent<Toggle>(); // 설정 체크박스
            RectTransform check = CreateRect("Check", square); // 체크 표시 영역
            Stretch(check, 4);
            Image mark = check.gameObject.AddComponent<Image>(); // 체크 표시
            mark.color = AccentColor;
            toggle.targetGraphic = background;
            toggle.graphic = mark;
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(next => Guard(() => changed(next)));
            Label(row, title, 15, 0);
        }

        // 명령 버튼과 문구를 생성하는 함수
        private Button CreateButton(Transform parent, string title, Action action, float width = 0, bool primary = false)
        {
            RectTransform rect = CreateRect(title, parent); // 명령 버튼 영역
            SetSize(rect, width, 36);
            Image image = rect.gameObject.AddComponent<Image>(); // 버튼 배경
            image.color = primary ? AccentColor : new Color(0.85f, 0.89f, 0.88f);
            Button button = rect.gameObject.AddComponent<Button>(); // 명령 버튼
            button.targetGraphic = image;
            TextMeshProUGUI text = Label(rect, title, 14, 0); // 버튼 문구
            Stretch(text.rectTransform, 3);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10;
            text.fontSizeMax = 14;
            text.color = primary ? Color.white : InkColor;
            button.onClick.AddListener(() => Guard(action));
            return button;
        }

        // 기본 TMP 문구를 생성하는 함수
        private TextMeshProUGUI Label(Transform parent, string title, int fontSize, float height)
        {
            RectTransform rect = CreateRect("Label", parent); // 문구 영역
            SetSize(rect, 0, height);
            if (height == 0) rect.GetComponent<LayoutElement>().preferredHeight = -1;
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>(); // 문구 표시기
            text.font = _font;
            text.fontSize = fontSize;
            text.color = InkColor;
            text.text = title;
            text.richText = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        // UI 영역 오브젝트를 생성하는 함수
        private static RectTransform CreateRect(string title, Transform parent)
        {
            GameObject item = new GameObject(title, typeof(RectTransform)); // UI 영역 오브젝트
            item.transform.SetParent(parent, false);
            return item.GetComponent<RectTransform>();
        }

        // 세로 및 가로 목록의 스크롤 표시기를 생성하는 함수
        private static Scrollbar CreateScrollbar(RectTransform parent, bool vertical)
        {
            RectTransform rect = CreateRect("Scrollbar", parent); // 스크롤바 영역
            rect.anchorMin = vertical ? new Vector2(1, 0) : Vector2.zero;
            rect.anchorMax = vertical ? Vector2.one : new Vector2(1, 0);
            rect.sizeDelta = vertical ? new Vector2(8, 0) : new Vector2(0, 8);
            rect.anchoredPosition = vertical ? new Vector2(-4, 0) : new Vector2(0, 4);
            rect.gameObject.AddComponent<Image>().color = new Color(0.85f, 0.89f, 0.88f);
            Scrollbar scrollbar = rect.gameObject.AddComponent<Scrollbar>(); // 스크롤 위치 선택기
            RectTransform handle = CreateRect("Handle", rect); // 스크롤 손잡이 영역
            Stretch(handle);
            Image image = handle.gameObject.AddComponent<Image>(); // 스크롤 손잡이
            image.color = AccentColor;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = image;
            scrollbar.direction = vertical ? Scrollbar.Direction.BottomToTop : Scrollbar.Direction.LeftToRight;
            return scrollbar;
        }

        // UI 영역에 배치 크기를 지정하는 함수
        private static void SetSize(RectTransform rect, float width, float height)
        {
            LayoutElement size = rect.gameObject.GetComponent<LayoutElement>() ??
                rect.gameObject.AddComponent<LayoutElement>(); // UI 크기 제한
            size.preferredWidth = width;
            size.preferredHeight = height;
            size.flexibleWidth = width == 0 ? 1 : 0;
            size.minWidth = width;
            rect.sizeDelta = new Vector2(width, height);
        }

        // UI 영역을 부모 영역 안에 펼치는 함수
        private static void Stretch(RectTransform rect, float padding = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        // 일정 높이의 가로 배치를 생성하는 함수
        private static RectTransform Row(Transform parent, float height)
        {
            RectTransform row = CreateRect("Row", parent); // 가로 배치 영역
            SetSize(row, 0, height);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); // 가로 배치기
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        // 내용 높이에 맞는 세로 배치를 생성하는 함수
        private static RectTransform Column(Transform parent, string title)
        {
            RectTransform column = CreateRect(title, parent); // 세로 배치 영역
            VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>(); // 세로 배치기
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return column;
        }

        // UI 포인터 이벤트 콜백을 등록하는 함수
        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> action)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type }; // 포인터 이벤트 항목
            entry.callback.AddListener(data => action(data));
            trigger.triggers.Add(entry);
        }

        // UI에서 로드한 이미지 텍스처를 정리하는 함수
        private void ReleaseTextures()
        {
            _preview = null;
            foreach (Texture2D texture in _textures.Values) ReleaseObject(texture);
            _textures.Clear();
        }

        // UI 이미지별로 텍스처를 한 번 로드하는 함수
        private Texture2D LoadUiTexture(ImageAssetData image)
        {
            if (!_textures.TryGetValue(image.RelativePath, out Texture2D texture)) // 캐시 텍스처
            {
                texture = _assetStore.LoadTexture(image);
                _textures.Add(image.RelativePath, texture);
            }
            return texture;
        }

        // 종료 확인 이벤트와 편집 자원을 정리하는 함수
        private void OnDestroy()
        {
            Application.wantsToQuit -= HandleWantsToQuit;
            _editor?.Dispose();
            ReleaseTextures();
            if (_font != null)
            {
                foreach (Texture2D atlas in _font.atlasTextures) ReleaseObject(atlas);
                ReleaseObject(_font.material);
                ReleaseObject(_font);
            }
        }

        // 실행 모드에 맞춰 UI 자원을 해제하는 함수
        private static void ReleaseObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
