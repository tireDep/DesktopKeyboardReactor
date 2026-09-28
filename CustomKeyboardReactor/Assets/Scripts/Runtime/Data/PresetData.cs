using System;
using System.Collections.Generic;

namespace CustomKeyboardReactor
{
    // 오버레이 캐릭터 프리셋 데이터를 보관하는 클래스
    [Serializable]
    public sealed class PresetData
    {
        public const int DefaultCharacterScalePercent = 100; // 기본 캐릭터 크기
        public const int MinimumCharacterScalePercent = 10; // 최소 캐릭터 크기
        public const int MaximumCharacterScalePercent = 300; // 최대 캐릭터 크기
        public const int CharacterScaleStepPercent = 5; // 캐릭터 크기 변경 단위

        public string Id = string.Empty; // 프리셋 내부 ID
        public string Name = string.Empty; // 프리셋 이름
        public List<ImageAssetData> NormalImages = new List<ImageAssetData>(); // 일반 이미지 목록
        public List<ImageAssetData> IdleImages = new List<ImageAssetData>(); // 대기 이미지 목록
        public string HoverTextTemplate = string.Empty; // 호버 문구
        public int CharacterScalePercent = DefaultCharacterScalePercent; // 캐릭터 크기 백분율
    }
}
