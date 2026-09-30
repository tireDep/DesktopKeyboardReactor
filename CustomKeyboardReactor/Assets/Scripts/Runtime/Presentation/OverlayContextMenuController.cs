using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor
{
    // 캐릭터 우클릭 명령을 표시하고 화면 좌표로 선택하는 메뉴 모듈
    public sealed class OverlayContextMenuController : IDisposable
    {
        private const string CanvasObjectName = "Character Context Menu Canvas"; // 메뉴 캔버스 이름
        private const float MenuWidth = 220f; // 메뉴 너비
        private const float RowHeight = 34f; // 메뉴 행 높이
        private const int CanvasSortingOrder = 200; // 메뉴 캔버스 정렬 순서

        private readonly List<RectTransform> _rowRects = new List<RectTransform>(); // 메뉴 행 영역 목록
        private readonly List<Text> _rowLabels = new List<Text>(); // 메뉴 행 문구 목록
        private GameObject _canvasObject; // 메뉴 캔버스 오브젝트
        private RectTransform _panelRect; // 메뉴 패널 영역
        private Font _menuFont; // 메뉴 동적 글꼴
        private bool _ownsMenuFont; // 메뉴 글꼴 소유 여부

        // 메뉴에서 실행 가능한 명령
        public enum Command
        {
            None,
            TogglePositionLock,
            SelectNextMonitor,
            Close,
            Exit,
        }

        public bool IsOpen => _panelRect != null && _panelRect.gameObject.activeSelf; // 메뉴 표시 여부
        public RectTransform PanelRect => _panelRect; // 메뉴 패널 영역
        public Vector2 ScreenPosition => _panelRect != null
            ? _panelRect.anchoredPosition
            : Vector2.zero; // 메뉴 화면 위치

        // 메뉴 계층을 생성하는 생성자
        public OverlayContextMenuController(Transform parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            CreateHierarchy(parent);
        }

        // 현재 상태 문구를 갱신하고 포인터 위치에 메뉴를 표시하는 함수
        public void Show(
            Vector2 screenPosition,
            bool positionLocked,
            int monitorIndex,
            int monitorCount)
        {
            Refresh(positionLocked, monitorIndex, monitorCount);
            _panelRect.gameObject.SetActive(true);

            SetScreenPosition(screenPosition);
        }

        // 화면 경계 안으로 보정하여 메뉴 위치를 변경하는 함수
        public void SetScreenPosition(Vector2 screenPosition)
        {
            float menuHeight = RowHeight * _rowRects.Count; // 전체 메뉴 높이
            float x = Mathf.Clamp(screenPosition.x, 0f, Mathf.Max(0f, Screen.width - MenuWidth)); // 보정 메뉴 가로 위치
            float y = Mathf.Clamp(screenPosition.y, menuHeight, Screen.height); // 보정 메뉴 세로 위치
            _panelRect.anchoredPosition = new Vector2(x, y);
        }

        // 현재 설정에 맞춰 메뉴 문구를 갱신하는 함수
        public void Refresh(bool positionLocked, int monitorIndex, int monitorCount)
        {
            _rowLabels[0].text = positionLocked ? "위치 잠금: 켬" : "위치 잠금: 끔";
            _rowLabels[1].text = monitorCount > 1
                ? $"모니터: {monitorIndex + 1}/{monitorCount}"
                : "모니터: 1/1";
            _rowLabels[2].text = "닫기";
            _rowLabels[3].text = "종료";
        }

        // 메뉴를 숨기는 함수
        public void Hide()
        {
            if (_panelRect != null)
            {
                _panelRect.gameObject.SetActive(false);
            }
        }

        // 화면 좌표가 메뉴 내부인지 반환하는 함수
        public bool ContainsScreenPoint(Vector2 screenPosition)
        {
            return IsOpen &&
                   RectTransformUtility.RectangleContainsScreenPoint(
                       _panelRect,
                       screenPosition);
        }

        // 화면 좌표에 대응하는 메뉴 명령을 반환하는 함수
        public bool TryGetCommand(Vector2 screenPosition, out Command command)
        {
            command = Command.None;
            if (!IsOpen)
            {
                return false;
            }

            for (int index = 0; index < _rowRects.Count; index++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        _rowRects[index],
                        screenPosition))
                {
                    command = (Command)(index + 1);
                    return true;
                }
            }

            return false;
        }

        // 메뉴 캔버스와 네 개 명령 행을 생성하는 함수
        private void CreateHierarchy(Transform parent)
        {
            _canvasObject = new GameObject(
                CanvasObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            _canvasObject.transform.SetParent(parent, false);

            Canvas canvas = _canvasObject.GetComponent<Canvas>(); // 메뉴 캔버스
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>(); // 메뉴 크기 조정기
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            GameObject panelObject = new GameObject(
                "Menu Panel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panelObject.transform.SetParent(_canvasObject.transform, false);
            _panelRect = panelObject.GetComponent<RectTransform>();
            _panelRect.anchorMin = Vector2.zero;
            _panelRect.anchorMax = Vector2.zero;
            _panelRect.pivot = new Vector2(0f, 1f);
            _panelRect.sizeDelta = new Vector2(MenuWidth, RowHeight * 4f);
            panelObject.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.96f);

            _menuFont = Font.CreateDynamicFontFromOSFont( // 한국어 우선 시스템 글꼴
                new[] { "Malgun Gothic", "Arial" },
                15);
            _ownsMenuFont = _menuFont != null;
            _menuFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int index = 0; index < 4; index++)
            {
                CreateRow(index, _menuFont);
            }

            Hide();
        }

        // 지정한 인덱스의 메뉴 행을 생성하는 함수
        private void CreateRow(int index, Font font)
        {
            GameObject rowObject = new GameObject(
                $"Menu Row {index + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            rowObject.transform.SetParent(_panelRect, false);
            RectTransform rowRect = rowObject.GetComponent<RectTransform>(); // 메뉴 행 영역
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -RowHeight * index);
            rowRect.sizeDelta = new Vector2(0f, RowHeight);
            rowObject.GetComponent<Image>().color = index % 2 == 0
                ? new Color(1f, 1f, 1f, 0.035f)
                : Color.clear;

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            labelObject.transform.SetParent(rowRect, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>(); // 메뉴 문구 영역
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);

            Text label = labelObject.GetComponent<Text>(); // 메뉴 문구 표시기
            label.font = font;
            label.fontSize = 15;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.raycastTarget = false;
            _rowRects.Add(rowRect);
            _rowLabels.Add(label);
        }

        // 생성한 메뉴 오브젝트를 정리하는 함수
        public void Dispose()
        {
            if (_canvasObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(_canvasObject);
            }
            else
            {
                Object.DestroyImmediate(_canvasObject);
            }

            _canvasObject = null;
            _panelRect = null;
            _rowRects.Clear();
            _rowLabels.Clear();

            if (_ownsMenuFont && _menuFont != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_menuFont);
                }
                else
                {
                    Object.DestroyImmediate(_menuFont);
                }
            }

            _menuFont = null;
            _ownsMenuFont = false;
        }
    }
}
