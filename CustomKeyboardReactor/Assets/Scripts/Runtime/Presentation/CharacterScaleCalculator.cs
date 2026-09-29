using System;

namespace CustomKeyboardReactor
{
    // 캐릭터 표시 영역의 크기 단계와 화면 제한을 계산하는 클래스
    public static class CharacterScaleCalculator
    {
        public const float BaseDisplayAreaPixels = 320f; // 백 퍼센트 표시 영역 크기
        public const float WorkAreaLimitRatio = 0.9f; // 작업 영역 최대 점유 비율

        // 백분율과 작업 영역을 기준으로 정사각 표시 영역 한 변을 계산하는 함수
        public static float CalculateDisplayAreaSize(
            int scalePercent,
            float workAreaWidth,
            float workAreaHeight)
        {
            if (workAreaWidth <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(workAreaWidth));
            }

            if (workAreaHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(workAreaHeight));
            }

            int normalizedScalePercent = NormalizeScalePercent(scalePercent); // 보정된 캐릭터 크기
            float requestedSize = BaseDisplayAreaPixels * normalizedScalePercent / 100f; // 요청 표시 영역 크기
            float maximumWorkAreaSize = Math.Min(workAreaWidth, workAreaHeight) * WorkAreaLimitRatio; // 작업 영역 제한 크기
            return Math.Min(requestedSize, maximumWorkAreaSize);
        }

        // 현재 캐릭터 크기를 지정한 단계 수만큼 변경하는 함수
        public static int StepScalePercent(int currentScalePercent, int stepCount)
        {
            int normalizedScalePercent = NormalizeScalePercent(currentScalePercent); // 보정된 현재 크기
            long steppedScalePercent = normalizedScalePercent +
                                       (long)stepCount * PresetData.CharacterScaleStepPercent; // 단계 적용 크기
            return (int)Math.Max(
                PresetData.MinimumCharacterScalePercent,
                Math.Min(PresetData.MaximumCharacterScalePercent, steppedScalePercent));
        }

        // 캐릭터 크기를 허용 범위의 가장 가까운 변경 단위로 보정하는 함수
        public static int NormalizeScalePercent(int scalePercent)
        {
            int clampedScalePercent = Math.Max( // 범위 제한 크기
                PresetData.MinimumCharacterScalePercent,
                Math.Min(PresetData.MaximumCharacterScalePercent, scalePercent));
            double scaleStep = clampedScalePercent / (double)PresetData.CharacterScaleStepPercent; // 단계 환산 값
            int roundedStep = (int)Math.Round(scaleStep, MidpointRounding.AwayFromZero); // 반올림된 단계 값
            return roundedStep * PresetData.CharacterScaleStepPercent;
        }
    }
}
