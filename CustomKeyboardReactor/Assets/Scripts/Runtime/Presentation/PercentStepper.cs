using System;

namespace CustomKeyboardReactor
{
    // 백분율 크기를 제품 범위와 변경 단위로 제한하는 클래스
    public sealed class PercentStepper
    {
        private readonly int _minimum; // 최소 백분율
        private readonly int _maximum; // 최대 백분율
        private readonly int _step; // 변경 단위

        public int Value { get; private set; } // 현재 백분율

        // 값과 허용 범위를 설정하는 생성자
        public PercentStepper(int value, int minimum, int maximum, int step)
        {
            if (step <= 0 || minimum > maximum) throw new ArgumentOutOfRangeException(nameof(step));
            _minimum = minimum;
            _maximum = maximum;
            _step = step;
            Set(value);
        }

        // 현재 값에 퍼센트 포인트를 더하는 함수
        public int Change(int delta)
        {
            return Set(Value + delta);
        }

        // 백분율을 가장 가까운 변경 단위로 보정하는 함수
        public int Set(int value)
        {
            Value = Math.Max(_minimum, Math.Min(_maximum,
                (int)Math.Round(value / (double)_step, MidpointRounding.AwayFromZero) * _step));
            return Value;
        }
    }
}
