using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomKeyboardReactor
{
    // 클릭 통과 전환을 표시하는 클래스
    internal sealed class OverlayVerificationMarker : IDisposable
    {
        private const string MarkerObjectName = "Stage 1 Verification Marker"; // 검증 이미지 오브젝트 이름

        private readonly Camera _camera; // 화면 좌표 변환 카메라
        private readonly GameObject _markerObject; // 임시 검증 이미지 오브젝트
        private readonly Texture2D _markerTexture; // 임시 검증 이미지 텍스처
        private readonly Sprite _markerSprite; // 임시 검증 이미지 스프라이트
        private readonly SpriteRenderer _markerRenderer; // 임시 검증 이미지 렌더러

        // 중앙에 불투명한 임시 검증 이미지를 생성하는 함수
        public OverlayVerificationMarker(Transform parent, Camera camera, Vector2 size, Color color)
        {
            _camera = camera;
            _markerTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) // 임시 검증 이미지 텍스처
            {
                name = MarkerObjectName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            _markerTexture.SetPixel(0, 0, Color.white);
            _markerTexture.Apply(false, false);

            _markerSprite = Sprite.Create( // 임시 검증 이미지 스프라이트
                _markerTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            _markerSprite.name = MarkerObjectName;
            _markerSprite.hideFlags = HideFlags.DontSave;

            _markerObject = new GameObject(MarkerObjectName) // 임시 검증 이미지 오브젝트
            {
                hideFlags = HideFlags.DontSave,
            };
            _markerObject.transform.SetParent(parent, false);
            _markerObject.transform.localScale = new Vector3(size.x, size.y, 1f);

            _markerRenderer = _markerObject.AddComponent<SpriteRenderer>();
            _markerRenderer.sprite = _markerSprite;
            _markerRenderer.color = color;
            _markerRenderer.sortingOrder = 100;
        }

        // Windows 클라이언트 좌표가 검증 이미지 안인지 확인하는 함수
        public bool ContainsClientPoint(int clientX, int clientY)
        {
            if (_camera == null)
            {
                return false;
            }

            Bounds worldBounds = _markerRenderer.bounds; // 검증 이미지 월드 경계
            Vector3 firstScreenCorner = _camera.WorldToScreenPoint(worldBounds.min); // 첫 화면 경계 좌표
            Vector3 secondScreenCorner = _camera.WorldToScreenPoint(worldBounds.max); // 둘째 화면 경계 좌표
            float minimumX = Mathf.Min(firstScreenCorner.x, secondScreenCorner.x); // 화면 최소 가로 좌표
            float maximumX = Mathf.Max(firstScreenCorner.x, secondScreenCorner.x); // 화면 최대 가로 좌표
            float minimumY = Mathf.Min(firstScreenCorner.y, secondScreenCorner.y); // 화면 최소 세로 좌표
            float maximumY = Mathf.Max(firstScreenCorner.y, secondScreenCorner.y); // 화면 최대 세로 좌표
            Vector2 unityScreenPoint = new Vector2(clientX, Screen.height - clientY); // Unity 화면 좌표
            Rect screenBounds = Rect.MinMaxRect(minimumX, minimumY, maximumX, maximumY); // 검증 이미지 화면 경계

            return screenBounds.Contains(unityScreenPoint);
        }

        // 임시 검증 이미지 자원을 정리하는 함수
        public void Dispose()
        {
            Object.Destroy(_markerObject);
            Object.Destroy(_markerSprite);
            Object.Destroy(_markerTexture);
        }
    }
}
