using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CustomKeyboardReactor
{
    // Win32 오버레이 창 속성을 관리하는 클래스
    public sealed class OverlayWindowService : IDisposable
    {
        private const long PopupStyle = 0x80000000L; // 팝업 창 스타일
        private const long WindowedFrameStyle = 0x00CF0000L; // 일반 창 프레임 스타일
        private const long LayeredStyle = 0x00080000L; // 레이어드 창 스타일
        private const long TransparentStyle = 0x00000020L; // 클릭 통과 창 스타일

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int WindowStyleIndex = -16; // 기본 창 스타일 인덱스
        private const int ExtendedWindowStyleIndex = -20; // 확장 창 스타일 인덱스
        private const long TopMostStyle = 0x00000008L; // 항상 위 확장 창 스타일
        private const uint NoSizeFlag = 0x0001; // 크기 유지 플래그
        private const uint NoMoveFlag = 0x0002; // 위치 유지 플래그
        private const uint NoActivateFlag = 0x0010; // 비활성 유지 플래그
        private const uint FrameChangedFlag = 0x0020; // 창 프레임 갱신 플래그
        private const uint ShowWindowFlag = 0x0040; // 창 표시 플래그

        private static readonly IntPtr TopMostWindow = new IntPtr(-1); // 항상 위 창 위치
        private static readonly IntPtr NotTopMostWindow = new IntPtr(-2); // 일반 창 위치

        private IntPtr _windowHandle; // Unity 플레이어 창 핸들
        private long _originalStyle; // 초기 기본 창 스타일
        private long _originalExtendedStyle; // 초기 확장 창 스타일
        private bool _hasOriginalStyles; // 초기 스타일 보관 여부
#endif

        public bool IsInitialized { get; private set; } // 창 초기화 완료 여부
        public bool IsAlwaysOnTop { get; private set; } // 항상 위 적용 상태
        public bool IsClickThrough { get; private set; } // 클릭 통과 적용 상태

        // 기존 비트를 보존하며 보더리스 스타일을 조합하는 함수
        public static long ComposeBorderlessStyle(long currentStyle)
        {
            return (currentStyle & ~WindowedFrameStyle) | PopupStyle;
        }

        // 기존 비트를 보존하며 클릭 통과 스타일을 조합하는 함수
        public static long ComposeClickThroughStyle(long currentStyle, bool clickThrough)
        {
            long composedStyle = currentStyle | LayeredStyle; // 레이어드가 보장된 확장 스타일
            return clickThrough
                ? composedStyle | TransparentStyle
                : composedStyle & ~TransparentStyle;
        }

        // 플레이어 창을 찾고 오버레이 속성을 초기화하는 함수
        public bool TryInitialize(bool alwaysOnTop, bool clickThrough)
        {
            if (IsInitialized)
            {
                return true;
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            IntPtr windowHandle; // 현재 프로세스 주 창 핸들
            using (Process currentProcess = Process.GetCurrentProcess()) // 현재 Unity 프로세스
            {
                windowHandle = currentProcess.MainWindowHandle;
            }

            if (windowHandle == IntPtr.Zero)
            {
                return false;
            }

            if (_hasOriginalStyles)
            {
                if (!TryRestoreOriginalProperties())
                {
                    return false;
                }

                ResetNativeState();
            }

            _windowHandle = windowHandle;
            bool initialized = TryCaptureOriginalStyles() && // 초기 창 스타일 획득 결과
                               ApplyTransparency() &&
                               ApplyBorderlessStyle() &&
                               ApplyAlwaysOnTop(alwaysOnTop, true) &&
                               ApplyClickThrough(clickThrough, true);
            if (!initialized)
            {
                bool restored = !_hasOriginalStyles || TryRestoreOriginalProperties(); // 부분 초기화 복원 결과
                if (restored)
                {
                    ResetNativeState();
                }

                IsAlwaysOnTop = false;
                IsClickThrough = false;
                return false;
            }
#else
            ApplyAlwaysOnTop(alwaysOnTop, true);
            ApplyClickThrough(clickThrough, true);
#endif

            IsInitialized = true;
            return true;
        }

        // 항상 위 상태를 변경하는 함수
        public bool SetAlwaysOnTop(bool alwaysOnTop)
        {
            return IsInitialized && ApplyAlwaysOnTop(alwaysOnTop, false);
        }

        // 클릭 통과 상태를 변경하는 함수
        public bool SetClickThrough(bool clickThrough)
        {
            return IsInitialized && ApplyClickThrough(clickThrough, false);
        }

        // 현재 커서의 창 클라이언트 좌표를 조회하는 함수
        public bool TryGetCursorClientPosition(out int x, out int y)
        {
            x = 0;
            y = 0;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            NativeMethods.Point point; // 현재 커서 화면 좌표
            if (!IsInitialized || _windowHandle == IntPtr.Zero || !NativeMethods.GetCursorPos(out point))
            {
                return false;
            }

            if (!NativeMethods.ScreenToClient(_windowHandle, ref point))
            {
                return false;
            }

            x = point.X;
            y = point.Y;
            return true;
#else
            return false;
#endif
        }

        // 현재 오버레이 창 속성을 다시 적용하는 함수
        public bool ReapplyWindowProperties()
        {
            if (!IsInitialized)
            {
                return false;
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            bool transparencyApplied = ApplyTransparency(); // DWM 투명 프레임 재적용 결과
            bool borderlessApplied = ApplyBorderlessStyle(); // 보더리스 스타일 재적용 결과
            bool alwaysOnTopApplied = ApplyAlwaysOnTop(IsAlwaysOnTop, true); // 항상 위 상태 재적용 결과
            bool clickThroughApplied = ApplyClickThrough(IsClickThrough, true); // 클릭 통과 상태 재적용 결과
            return transparencyApplied && borderlessApplied && alwaysOnTopApplied && clickThroughApplied;
#else
            return true;
#endif
        }

        // 초기 창 스타일을 복원하고 자원을 정리하는 함수
        public bool TryDispose()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            bool restored = !_hasOriginalStyles || // 초기 창 속성 복원 결과
                            _windowHandle == IntPtr.Zero ||
                            TryRestoreOriginalProperties();
            if (!restored)
            {
                return false;
            }

            ResetNativeState();
#endif

            IsInitialized = false;
            IsAlwaysOnTop = false;
            IsClickThrough = false;
            return true;
        }

        // 초기 창 스타일 복원 실패를 예외로 알리며 자원을 정리하는 함수
        public void Dispose()
        {
            if (!TryDispose())
            {
                throw new InvalidOperationException("Failed to restore the original window properties.");
            }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // 초기 창 스타일을 보관하는 함수
        private bool TryCaptureOriginalStyles()
        {
            if (_hasOriginalStyles)
            {
                return true;
            }

            IntPtr originalStyle; // 초기 기본 창 스타일
            IntPtr originalExtendedStyle; // 초기 확장 창 스타일
            bool styleRead = NativeMethods.TryGetWindowLongPtr( // 기본 창 스타일 조회 결과
                _windowHandle,
                WindowStyleIndex,
                out originalStyle);
            bool extendedStyleRead = NativeMethods.TryGetWindowLongPtr( // 확장 창 스타일 조회 결과
                _windowHandle,
                ExtendedWindowStyleIndex,
                out originalExtendedStyle);
            if (!styleRead || !extendedStyleRead)
            {
                return false;
            }

            _originalStyle = originalStyle.ToInt64();
            _originalExtendedStyle = originalExtendedStyle.ToInt64();
            _hasOriginalStyles = true;
            return true;
        }

        // DWM 투명 프레임을 전체 클라이언트 영역에 적용하는 함수
        private bool ApplyTransparency()
        {
            NativeMethods.Margins margins = new NativeMethods.Margins // 전체 영역 투명 프레임 여백
            {
                LeftWidth = -1,
            };

            return NativeMethods.DwmExtendFrameIntoClientArea(_windowHandle, ref margins) == 0;
        }

        // DWM 투명 프레임을 제거하는 함수
        private bool RemoveTransparency()
        {
            NativeMethods.Margins margins = new NativeMethods.Margins(); // 기본 DWM 프레임 여백
            return NativeMethods.DwmExtendFrameIntoClientArea(_windowHandle, ref margins) == 0;
        }

        // 보더리스 스타일을 창에 적용하는 함수
        private bool ApplyBorderlessStyle()
        {
            IntPtr currentStylePointer; // 현재 기본 창 스타일 포인터
            if (!NativeMethods.TryGetWindowLongPtr(_windowHandle, WindowStyleIndex, out currentStylePointer))
            {
                return false;
            }

            long borderlessStyle = ComposeBorderlessStyle(currentStylePointer.ToInt64()); // 보더리스 기본 창 스타일
            bool styleApplied = NativeMethods.TrySetWindowLongPtr( // 기본 창 스타일 적용 결과
                _windowHandle,
                WindowStyleIndex,
                new IntPtr(borderlessStyle));
            bool frameUpdated = NativeMethods.SetWindowPos( // 창 프레임 갱신 결과
                _windowHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                NoSizeFlag | NoMoveFlag | NoActivateFlag | ShowWindowFlag | FrameChangedFlag);
            return styleApplied && frameUpdated;
        }

        // 초기 창 속성을 가능한 범위에서 모두 복원하는 함수
        private bool TryRestoreOriginalProperties()
        {
            bool transparencyRemoved = RemoveTransparency(); // DWM 투명 프레임 제거 결과
            bool styleRestored = NativeMethods.TrySetWindowLongPtr( // 기본 창 스타일 복원 결과
                _windowHandle,
                WindowStyleIndex,
                new IntPtr(_originalStyle));
            bool extendedStyleRestored = NativeMethods.TrySetWindowLongPtr( // 확장 창 스타일 복원 결과
                _windowHandle,
                ExtendedWindowStyleIndex,
                new IntPtr(_originalExtendedStyle));
            IntPtr originalWindowPosition = (_originalExtendedStyle & TopMostStyle) != 0 // 초기 항상 위 창 위치
                ? TopMostWindow
                : NotTopMostWindow;
            bool positionRestored = NativeMethods.SetWindowPos( // 창 위치와 프레임 복원 결과
                _windowHandle,
                originalWindowPosition,
                0,
                0,
                0,
                0,
                NoSizeFlag | NoMoveFlag | NoActivateFlag | FrameChangedFlag);

            return transparencyRemoved && styleRestored && extendedStyleRestored && positionRestored;
        }

        // Win32 창 상태를 초기화하는 함수
        private void ResetNativeState()
        {
            _windowHandle = IntPtr.Zero;
            _originalStyle = 0L;
            _originalExtendedStyle = 0L;
            _hasOriginalStyles = false;
        }
#endif

        // 항상 위 상태를 필요할 때만 적용하는 함수
        private bool ApplyAlwaysOnTop(bool alwaysOnTop, bool forceReapply)
        {
            if (!forceReapply && IsAlwaysOnTop == alwaysOnTop)
            {
                return true;
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_windowHandle == IntPtr.Zero)
            {
                return false;
            }

            IntPtr windowPosition = alwaysOnTop ? TopMostWindow : NotTopMostWindow; // 적용할 창 위치
            if (!NativeMethods.SetWindowPos(
                    _windowHandle,
                    windowPosition,
                    0,
                    0,
                    0,
                    0,
                    NoSizeFlag | NoMoveFlag | NoActivateFlag | ShowWindowFlag))
            {
                return false;
            }
#endif

            IsAlwaysOnTop = alwaysOnTop;
            return true;
        }

        // 클릭 통과 상태를 필요할 때만 적용하는 함수
        private bool ApplyClickThrough(bool clickThrough, bool forceReapply)
        {
            if (!forceReapply && IsClickThrough == clickThrough)
            {
                return true;
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            IntPtr currentStylePointer; // 현재 확장 창 스타일 포인터
            if (_windowHandle == IntPtr.Zero ||
                !NativeMethods.TryGetWindowLongPtr(
                    _windowHandle,
                    ExtendedWindowStyleIndex,
                    out currentStylePointer))
            {
                return false;
            }

            long clickThroughStyle = ComposeClickThroughStyle( // 클릭 통과 확장 창 스타일
                currentStylePointer.ToInt64(),
                clickThrough);
            if (!NativeMethods.TrySetWindowLongPtr(
                    _windowHandle,
                    ExtendedWindowStyleIndex,
                    new IntPtr(clickThroughStyle)))
            {
                return false;
            }
#endif

            IsClickThrough = clickThrough;
            return true;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Win32 창 제어 API를 격리하는 클래스
        private static class NativeMethods
        {
            // DWM 프레임 여백을 보관하는 구조체
            [StructLayout(LayoutKind.Sequential)]
            internal struct Margins
            {
                public int LeftWidth; // 왼쪽 프레임 너비
                public int RightWidth; // 오른쪽 프레임 너비
                public int TopHeight; // 위쪽 프레임 높이
                public int BottomHeight; // 아래쪽 프레임 높이
            }

            // Windows 화면 또는 클라이언트 좌표를 보관하는 구조체
            [StructLayout(LayoutKind.Sequential)]
            internal struct Point
            {
                public int X; // 가로 좌표
                public int Y; // 세로 좌표
            }

            // 창 스타일을 오류 정보와 함께 조회하는 함수
            internal static bool TryGetWindowLongPtr(IntPtr windowHandle, int index, out IntPtr value)
            {
                SetLastError(0);
                value = GetWindowLongPtr(windowHandle, index);
                return value != IntPtr.Zero || Marshal.GetLastWin32Error() == 0;
            }

            // 창 스타일을 오류 정보와 함께 변경하는 함수
            internal static bool TrySetWindowLongPtr(IntPtr windowHandle, int index, IntPtr newValue)
            {
                SetLastError(0);
                IntPtr previousValue = SetWindowLongPtr(windowHandle, index, newValue); // 변경 전 창 스타일
                return previousValue != IntPtr.Zero || Marshal.GetLastWin32Error() == 0;
            }

            // 포인터 크기에 맞는 창 스타일을 조회하는 함수
            private static IntPtr GetWindowLongPtr(IntPtr windowHandle, int index)
            {
                return IntPtr.Size == 8
                    ? GetWindowLongPtr64(windowHandle, index)
                    : new IntPtr(GetWindowLong32(windowHandle, index));
            }

            // 포인터 크기에 맞는 창 스타일을 변경하는 함수
            private static IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr newValue)
            {
                if (IntPtr.Size == 8)
                {
                    return SetWindowLongPtr64(windowHandle, index, newValue);
                }

                int newValue32 = unchecked((int)newValue.ToInt64()); // 32비트 창 스타일 값
                return new IntPtr(SetWindowLong32(windowHandle, index, newValue32));
            }

            // DWM 프레임을 클라이언트 영역으로 확장하는 함수
            [DllImport("dwmapi.dll", SetLastError = true)]
            internal static extern int DwmExtendFrameIntoClientArea(IntPtr windowHandle, ref Margins margins);

            // 현재 커서의 Windows 화면 좌표를 조회하는 함수
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetCursorPos(out Point point);

            // Windows 화면 좌표를 창 클라이언트 좌표로 변환하는 함수
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool ScreenToClient(IntPtr windowHandle, ref Point point);

            // 32비트 창 스타일을 조회하는 함수
            [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
            private static extern int GetWindowLong32(IntPtr windowHandle, int index);

            // 64비트 창 스타일을 조회하는 함수
            [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
            private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

            // 32비트 창 스타일을 변경하는 함수
            [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
            private static extern int SetWindowLong32(IntPtr windowHandle, int index, int newValue);

            // 64비트 창 스타일을 변경하는 함수
            [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
            private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr newValue);

            // 창 위치와 표시 순서를 변경하는 함수
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetWindowPos(
                IntPtr windowHandle,
                IntPtr insertAfter,
                int x,
                int y,
                int width,
                int height,
                uint flags);

            // 마지막 Win32 오류 값을 설정하는 함수
            [DllImport("kernel32.dll", EntryPoint = "SetLastError", ExactSpelling = true)]
            private static extern void SetLastError(uint errorCode);
        }
#endif
    }
}
