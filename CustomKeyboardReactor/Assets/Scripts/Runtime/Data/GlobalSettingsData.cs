using System;
using UnityEngine;

namespace CustomKeyboardReactor
{
    // 공용 설정 값을 보관하는 클래스
    [Serializable]
    public sealed class GlobalSettingsData
    {
        public const int DefaultIdleTimeoutSeconds = 300; // 기본 대기 진입 시간
        public const int MinimumIdleTimeoutSeconds = 60; // 최소 대기 진입 시간
        public const int MaximumIdleTimeoutSeconds = 3600; // 최대 대기 진입 시간
        public const int DefaultUiScalePercent = 100; // 기본 UI 크기
        public const int MinimumUiScalePercent = 50; // 최소 UI 크기
        public const int MaximumUiScalePercent = 200; // 최대 UI 크기
        public const int UiScaleStepPercent = 5; // UI 크기 변경 단위

        public bool KeyboardReactionEnabled = true; // 키보드 반응 활성 여부
        public bool MouseReactionEnabled = true; // 마우스 버튼 반응 활성 여부
        public bool IdleEnabled = true; // 대기 기능 활성 여부
        public int IdleTimeoutSeconds = DefaultIdleTimeoutSeconds; // 대기 진입 시간
        public bool AlwaysOnTop = true; // 항상 위 활성 여부
        public bool PositionLocked; // 위치 잠금 여부
        public string MonitorDeviceId = string.Empty; // 표시 모니터 장치 ID
        public Vector2 NormalizedAnchorPosition = new Vector2(0.5f, 0.5f); // 정규화된 아래쪽 중앙 기준점
        public bool HoverTextPanelEnabled = true; // 호버 텍스트 패널 활성 여부
        public int UiScalePercent = DefaultUiScalePercent; // UI 크기 백분율
    }
}
