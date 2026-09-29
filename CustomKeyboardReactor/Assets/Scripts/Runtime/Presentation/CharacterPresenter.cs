using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor
{
    // 일반 및 대기 이미지를 공통 캐릭터 표시 영역에 표시하는 클래스
    [DisallowMultipleComponent]
    public sealed class CharacterPresenter : MonoBehaviour
    {
        private const string DisplayAreaObjectName = "Character Display Area"; // 표시 영역 오브젝트 이름
        private const string CharacterImageObjectName = "Character Image"; // 캐릭터 이미지 오브젝트 이름
        private const string CanvasObjectName = "Character Canvas"; // 캐릭터 캔버스 오브젝트 이름
        private const int CanvasSortingOrder = 100; // 캐릭터 캔버스 정렬 순서

        private readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>(); // 런타임 텍스처 캐시
        private PresetAssetStore _presetAssetStore; // 프리셋 이미지 저장소
        private Canvas _canvas; // 캐릭터 캔버스
        private RectTransform _displayArea; // 공통 캐릭터 표시 영역
        private RectTransform _characterRect; // 캐릭터 이미지 영역
        private RawImage _characterImage; // 캐릭터 이미지 표시기
        private int _currentScalePercent = PresetData.DefaultCharacterScalePercent; // 현재 캐릭터 크기
        private Vector2 _normalizedAnchorPosition = new Vector2(0.5f, 0.5f); // 정규화된 아래쪽 중앙 기준점
        private int _lastScreenWidth; // 마지막 화면 너비
        private int _lastScreenHeight; // 마지막 화면 높이

        public Texture2D CurrentTexture => _characterImage != null
            ? _characterImage.texture as Texture2D
            : null; // 현재 표시 텍스처
        public RectTransform CharacterRect => _characterRect; // 현재 캐릭터 이미지 영역
        public RectTransform DisplayAreaRect => _displayArea; // 공통 캐릭터 표시 영역
        public Vector2 NormalizedAnchorPosition => _normalizedAnchorPosition; // 현재 정규화 기준점
        public Vector2 ScreenAnchorPosition => _displayArea == null
            ? Vector2.zero
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) +
              _displayArea.anchoredPosition; // 현재 화면 기준점

        // 캔버스 렌더링 전 화면 크기 변경 감지를 시작하는 함수
        private void OnEnable()
        {
            Canvas.willRenderCanvases += HandleWillRenderCanvases;
        }

        // 캔버스 렌더링 전 화면 크기 변경 감지를 중지하는 함수
        private void OnDisable()
        {
            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        }

        // 프리셋 이미지 저장소와 런타임 표시 계층을 준비하는 함수
        public void Initialize(PresetAssetStore presetAssetStore)
        {
            _presetAssetStore = presetAssetStore ??
                                throw new ArgumentNullException(nameof(presetAssetStore));
            EnsureDisplayHierarchy();
        }

        // 지정한 프리셋 이미지를 현재 캐릭터 크기로 표시하는 함수
        public void Present(ImageAssetData image, int scalePercent)
        {
            if (_presetAssetStore == null)
            {
                throw new InvalidOperationException("Character presenter is not initialized.");
            }

            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            EnsureDisplayHierarchy();
            _currentScalePercent = CharacterScaleCalculator.NormalizeScalePercent(scalePercent);
            _characterImage.texture = GetOrLoadTexture(image);
            _characterImage.enabled = true;
            RefreshLayout();
        }

        // 저장된 정규화 기준점을 현재 화면에 적용하는 함수
        public void SetNormalizedAnchorPosition(Vector2 normalizedPosition)
        {
            _normalizedAnchorPosition = new Vector2(
                Mathf.Clamp01(normalizedPosition.x),
                Mathf.Clamp01(normalizedPosition.y));
            ApplyAnchorPosition();
        }

        // 드래그 화면 좌표를 보정하고 정규화 기준점으로 반영하는 함수
        public void SetScreenAnchorPosition(Vector2 screenAnchorPosition)
        {
            if (_displayArea == null)
            {
                return;
            }

            Rect viewport = new Rect(0f, 0f, Screen.width, Screen.height); // 현재 창의 표시 영역
            _normalizedAnchorPosition = CharacterPlacementCalculator.CalculateNormalizedPosition(
                screenAnchorPosition,
                viewport,
                _displayArea.sizeDelta);
            ApplyAnchorPosition();
        }

        // 프리셋 변경 시 이전 런타임 텍스처 캐시를 정리하는 함수
        public void ClearCache()
        {
            if (_characterImage != null)
            {
                _characterImage.texture = null;
                _characterImage.enabled = false;
            }

            foreach (Texture2D texture in _textureCache.Values)
            {
                DestroyTexture(texture);
            }

            _textureCache.Clear();
        }

        // 캐릭터 캔버스와 아래쪽 중앙 기준 표시 영역을 생성하는 함수
        private void EnsureDisplayHierarchy()
        {
            if (_canvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject( // 캐릭터 캔버스 오브젝트
                CanvasObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>(); // 캐릭터 캔버스 크기 조정기
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasScaler.scaleFactor = 1f;

            GameObject displayAreaObject = new GameObject( // 공통 표시 영역 오브젝트
                DisplayAreaObjectName,
                typeof(RectTransform));
            displayAreaObject.transform.SetParent(_canvas.transform, false);
            _displayArea = displayAreaObject.GetComponent<RectTransform>();
            _displayArea.anchorMin = new Vector2(0.5f, 0.5f);
            _displayArea.anchorMax = new Vector2(0.5f, 0.5f);
            _displayArea.pivot = new Vector2(0.5f, 0f);
            _displayArea.anchoredPosition = Vector2.zero;

            GameObject characterImageObject = new GameObject( // 캐릭터 이미지 오브젝트
                CharacterImageObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            characterImageObject.transform.SetParent(_displayArea, false);
            _characterRect = characterImageObject.GetComponent<RectTransform>();
            _characterRect.anchorMin = new Vector2(0.5f, 0f);
            _characterRect.anchorMax = new Vector2(0.5f, 0f);
            _characterRect.pivot = new Vector2(0.5f, 0f);
            _characterRect.anchoredPosition = Vector2.zero;

            _characterImage = characterImageObject.GetComponent<RawImage>();
            _characterImage.raycastTarget = false;
            _characterImage.enabled = false;
        }

        // 현재 이미지와 화면 크기에 맞춰 표시 영역과 이미지 크기를 갱신하는 함수
        private void RefreshLayout()
        {
            if (_displayArea == null || _characterRect == null || CurrentTexture == null)
            {
                return;
            }

            float displayAreaSize = CharacterScaleCalculator.CalculateDisplayAreaSize( // 제한된 표시 영역 크기
                _currentScalePercent,
                Screen.width,
                Screen.height);
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _displayArea.sizeDelta = new Vector2(displayAreaSize, displayAreaSize);

            Texture2D texture = CurrentTexture; // 현재 표시 텍스처
            float fitScale = Math.Min( // 비율 유지 맞춤 배율
                displayAreaSize / texture.width,
                displayAreaSize / texture.height);
            _characterRect.sizeDelta = new Vector2(
                texture.width * fitScale,
                texture.height * fitScale);
            ApplyAnchorPosition();
        }

        // 정규화 기준점을 현재 화면 크기의 캔버스 위치로 적용하는 함수
        private void ApplyAnchorPosition()
        {
            if (_displayArea == null)
            {
                return;
            }

            Rect viewport = new Rect(0f, 0f, Screen.width, Screen.height); // 현재 창의 표시 영역
            Vector2 screenAnchor = CharacterPlacementCalculator.CalculateScreenAnchor( // 보정된 화면 기준점
                _normalizedAnchorPosition,
                viewport,
                _displayArea.sizeDelta);
            _displayArea.anchoredPosition = screenAnchor -
                                            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        // 캔버스 렌더링 전 화면 크기가 달라졌으면 표시 영역을 갱신하는 함수
        private void HandleWillRenderCanvases()
        {
            if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height)
            {
                RefreshLayout();
            }
        }

        // 이미지 데이터에 해당하는 런타임 텍스처를 캐시에서 반환하는 함수
        private Texture2D GetOrLoadTexture(ImageAssetData image)
        {
            string cacheKey = image.RelativePath; // 텍스처 캐시 키
            if (_textureCache.TryGetValue(cacheKey, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D texture = _presetAssetStore.LoadTexture(image); // 새로 로드한 런타임 텍스처
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            _textureCache.Add(cacheKey, texture);
            return texture;
        }

        // 프레젠터 파괴 시 생성한 런타임 텍스처를 정리하는 함수
        private void OnDestroy()
        {
            ClearCache();
        }

        // 실행 환경에 맞게 런타임 텍스처를 정리하는 함수
        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(texture);
            }
            else
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
