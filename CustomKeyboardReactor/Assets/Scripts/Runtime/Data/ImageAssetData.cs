using System;

namespace CustomKeyboardReactor
{
    // 프리셋 소유 이미지 정보를 보관하는 클래스
    [Serializable]
    public sealed class ImageAssetData
    {
        public string Id = string.Empty; // 이미지 내부 ID
        public string RelativePath = string.Empty; // 앱 데이터 루트 기준 상대 경로
        public int OriginalWidth; // 이미지 원본 너비
        public int OriginalHeight; // 이미지 원본 높이
    }
}
