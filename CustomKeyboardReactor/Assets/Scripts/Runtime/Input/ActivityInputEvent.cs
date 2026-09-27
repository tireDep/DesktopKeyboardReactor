namespace CustomKeyboardReactor
{
    // 입력 내용을 보관하지 않는 반응 입력 이벤트 구조체
    public readonly struct ActivityInputEvent
    {
        // 화면 좌표 포함 여부와 좌표를 보관하는 생성자
        private ActivityInputEvent(bool hasScreenPosition, int screenX, int screenY)
        {
            HasScreenPosition = hasScreenPosition;
            ScreenX = screenX;
            ScreenY = screenY;
        }

        public bool HasScreenPosition { get; } // 화면 좌표 포함 여부
        public int ScreenX { get; } // 입력 시점 화면 가로 좌표
        public int ScreenY { get; } // 입력 시점 화면 세로 좌표

        // 화면 좌표가 없는 키보드 반응 입력을 생성하는 함수
        public static ActivityInputEvent CreateKeyboard()
        {
            return new ActivityInputEvent(false, 0, 0);
        }

        // 화면 좌표가 있는 마우스 버튼 반응 입력을 생성하는 함수
        public static ActivityInputEvent CreateMouseButton(int screenX, int screenY)
        {
            return new ActivityInputEvent(true, screenX, screenY);
        }
    }
}
