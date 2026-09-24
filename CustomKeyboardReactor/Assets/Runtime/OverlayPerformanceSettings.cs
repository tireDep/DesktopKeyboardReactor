using UnityEngine;

namespace CustomKeyboardReactor
{
    // 오버레이 렌더링 성능 기준을 적용하는 클래스
    public static class OverlayPerformanceSettings
    {
        public const int TargetFrameRate = 60; // 목표 프레임 속도

        // 백그라운드 렌더링 프레임 제한을 적용하는 함수
        public static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
