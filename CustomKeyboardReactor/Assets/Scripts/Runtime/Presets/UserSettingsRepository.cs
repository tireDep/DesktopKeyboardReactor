using System;
using UnityEngine;

namespace CustomKeyboardReactor
{
    // 공용 설정과 전체 입력 수의 저장을 관리하는 클래스
    public sealed class UserSettingsRepository
    {
        private readonly AppDataStore _appDataStore; // 앱 데이터 저장소

        // 앱 데이터 저장소를 연결하는 생성자
        public UserSettingsRepository(AppDataStore appDataStore)
        {
            _appDataStore = appDataStore ??
                            throw new ArgumentNullException(nameof(appDataStore));
            _appDataStore.LoadOrCreate();
        }

        // 현재 공용 설정의 복사본을 반환하는 함수
        public GlobalSettingsData GetSettings()
        {
            AppData appData = _appDataStore.LoadLatest(); // 최신 앱 데이터
            return CloneAndNormalizeSettings(appData.GlobalSettings);
        }

        // 공용 설정을 유효 범위로 보정하여 저장하는 함수
        public void SaveSettings(GlobalSettingsData settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            AppData appData = _appDataStore.LoadLatest(); // 최신 앱 데이터
            GlobalSettingsData previousSettings = appData.GlobalSettings; // 변경 전 공용 설정
            appData.GlobalSettings = CloneAndNormalizeSettings(settings);
            try
            {
                _appDataStore.Save(appData);
            }
            catch
            {
                appData.GlobalSettings = previousSettings;
                throw;
            }
        }

        // 저장된 전체 입력 수를 반환하는 함수
        public long GetTotalInputCount()
        {
            return _appDataStore.LoadLatest().TotalInputCount;
        }

        // 전체 입력 수를 저장하는 함수
        public void SaveTotalInputCount(long totalInputCount)
        {
            if (totalInputCount < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalInputCount),
                    "Total input count cannot be negative.");
            }

            AppData appData = _appDataStore.LoadLatest(); // 최신 앱 데이터
            long previousTotalInputCount = appData.TotalInputCount; // 변경 전 전체 입력 수
            appData.TotalInputCount = totalInputCount;
            try
            {
                _appDataStore.Save(appData);
            }
            catch
            {
                appData.TotalInputCount = previousTotalInputCount;
                throw;
            }
        }

        // 공용 설정을 복사하고 제품 허용 범위로 보정하는 함수
        private static GlobalSettingsData CloneAndNormalizeSettings(GlobalSettingsData source)
        {
            source ??= new GlobalSettingsData();
            return new GlobalSettingsData
            {
                KeyboardReactionEnabled = source.KeyboardReactionEnabled,
                MouseReactionEnabled = source.MouseReactionEnabled,
                IdleEnabled = source.IdleEnabled,
                IdleTimeoutSeconds = Mathf.Clamp(
                    source.IdleTimeoutSeconds,
                    GlobalSettingsData.MinimumIdleTimeoutSeconds,
                    GlobalSettingsData.MaximumIdleTimeoutSeconds),
                AlwaysOnTop = source.AlwaysOnTop,
                PositionLocked = source.PositionLocked,
                MonitorDeviceId = source.MonitorDeviceId ?? string.Empty,
                NormalizedAnchorPosition = new Vector2(
                    Mathf.Clamp01(source.NormalizedAnchorPosition.x),
                    Mathf.Clamp01(source.NormalizedAnchorPosition.y)),
                HoverTextPanelEnabled = source.HoverTextPanelEnabled,
                UiScalePercent = NormalizeUiScalePercent(
                    source.UiScalePercent,
                    GlobalSettingsData.MinimumUiScalePercent,
                    GlobalSettingsData.MaximumUiScalePercent),
            };
        }

        // UI 크기를 허용 범위의 가장 가까운 5퍼센트 단위로 보정하는 함수
        private static int NormalizeUiScalePercent(
            int uiScalePercent,
            int minimumPercent,
            int maximumPercent)
        {
            int clampedValue = Mathf.Clamp(uiScalePercent, minimumPercent, maximumPercent); // 범위 보정 UI 크기
            return Mathf.RoundToInt(
                clampedValue / (float)GlobalSettingsData.UiScaleStepPercent) *
                GlobalSettingsData.UiScaleStepPercent;
        }
    }
}
