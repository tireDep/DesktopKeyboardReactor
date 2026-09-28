using System;
using System.Collections.Generic;

namespace CustomKeyboardReactor
{
    // 애플리케이션 영구 저장 데이터를 보관하는 클래스
    [Serializable]
    public sealed class AppData
    {
        public const int CurrentSchemaVersion = 1; // 현재 저장 스키마 버전

        public int SchemaVersion = CurrentSchemaVersion; // 저장 스키마 버전
        public long TotalInputCount; // 전체 입력 수
        public string ActivePresetId = string.Empty; // 활성 프리셋 ID
        public GlobalSettingsData GlobalSettings = new GlobalSettingsData(); // 공용 설정 데이터
        public List<PresetData> Presets = new List<PresetData>(); // 저장된 프리셋 목록
    }
}
