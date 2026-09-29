namespace CustomKeyboardReactor
{
    // 입력 내용을 보관하지 않는 반응 입력 이벤트 구조체
    public readonly struct ActivityInputEvent
    {
        // 지원하는 마우스 버튼 종류
        public enum MouseButton
        {
            None,
            Left,
            Right,
            Middle,
            X1,
            X2,
        }

        // 입력 종류와 화면 좌표를 보관하는 생성자
        private ActivityInputEvent(
            MouseButton button,
            bool hasScreenPosition,
            int screenX,
            int screenY)
        {
            Button = button;
            HasScreenPosition = hasScreenPosition;
            ScreenX = screenX;
            ScreenY = screenY;
        }

        public MouseButton Button { get; } // 마우스 버튼 종류
        public bool HasScreenPosition { get; } // 화면 좌표 포함 여부
        public int ScreenX { get; } // 입력 시점 화면 가로 좌표
        public int ScreenY { get; } // 입력 시점 화면 세로 좌표

        // 화면 좌표가 없는 키보드 반응 입력을 생성하는 함수
        public static ActivityInputEvent CreateKeyboard()
        {
            return new ActivityInputEvent(MouseButton.None, false, 0, 0);
        }


        // 화면 좌표가 있는 마우스 버튼 반응 입력을 생성하는 함수
        public static ActivityInputEvent CreateMouseButton(
            MouseButton button,
            int screenX,
            int screenY)
        {
            return new ActivityInputEvent(button, true, screenX, screenY);
        }
    }
}
