using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CustomKeyboardReactor
{
    // Win32 저수준 훅으로 전역 마우스 버튼 반응 입력을 수집하는 클래스
    public sealed class Win32MouseButtonInputSource : IActivityInputSource
    {
        private readonly ConcurrentQueue<ActivityInputEvent> _pendingInputs = new ConcurrentQueue<ActivityInputEvent>(); // 대기 중인 마우스 버튼 반응 입력 큐

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int MouseHookType = 14; // 저수준 마우스 훅 종류
        private const uint LeftButtonDownMessage = 0x0201; // 왼쪽 버튼 누름 메시지
        private const uint RightButtonDownMessage = 0x0204; // 오른쪽 버튼 누름 메시지
        private const uint MiddleButtonDownMessage = 0x0207; // 가운데 버튼 누름 메시지
        private const uint ExtraButtonDownMessage = 0x020B; // 확장 버튼 누름 메시지
        private const uint InjectedFlag = 0x00000001; // 주입 입력 플래그
        private const uint LowerIntegrityInjectedFlag = 0x00000002; // 낮은 무결성 주입 플래그

        private static readonly NativeMethods.MouseHookCallback HookCallback = HandleHook; // 마우스 훅 콜백 참조
        private static Win32MouseButtonInputSource _activeSource; // 활성 마우스 입력 공급자

        private IntPtr _hookHandle; // 마우스 훅 핸들
#endif

        public bool IsRunning { get; private set; } // 마우스 훅 실행 여부

        // 전역 마우스 훅을 설치하는 함수
        public bool TryStart()
        {
            if (IsRunning)
            {
                return true;
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_activeSource != null && !ReferenceEquals(_activeSource, this))
            {
                return false;
            }

            IntPtr moduleHandle = NativeMethods.GetModuleHandle(null); // 현재 실행 모듈 핸들
            if (moduleHandle == IntPtr.Zero)
            {
                return false;
            }

            _activeSource = this;
            _hookHandle = NativeMethods.SetWindowsHookEx(
                MouseHookType,
                HookCallback,
                moduleHandle,
                0);
            if (_hookHandle == IntPtr.Zero)
            {
                _activeSource = null;
                return false;
            }

            IsRunning = true;
            return true;
#else
            return false;
#endif
        }

        // 전역 마우스 훅을 해제하는 함수
        public void Stop()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_hookHandle != IntPtr.Zero)
            {
                if (!NativeMethods.UnhookWindowsHookEx(_hookHandle))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                _hookHandle = IntPtr.Zero;
            }

            if (ReferenceEquals(_activeSource, this))
            {
                _activeSource = null;
            }
#endif

            ClearPendingInputs();
            IsRunning = false;
        }

        // 대기 중인 마우스 버튼 반응 입력을 반환하는 함수
        public bool TryDequeue(out ActivityInputEvent inputEvent)
        {
            return _pendingInputs.TryDequeue(out inputEvent);
        }

        // 마우스 훅 자원을 정리하는 함수
        public void Dispose()
        {
            Stop();
        }

        // 대기 중인 마우스 버튼 입력을 비우는 함수
        private void ClearPendingInputs()
        {
            ActivityInputEvent inputEvent; // 제거할 마우스 버튼 반응 입력
            while (_pendingInputs.TryDequeue(out inputEvent))
            {
            }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Win32 마우스 메시지를 반응 입력으로 변환하는 함수
        private static IntPtr HandleHook(int code, IntPtr messagePointer, IntPtr dataPointer)
        {
            Win32MouseButtonInputSource activeSource = _activeSource; // 활성 마우스 입력 공급자
            if (code >= 0 && activeSource != null)
            {
                try
                {
                    activeSource.ProcessMouseMessage(messagePointer, dataPointer);
                }
                catch
                {
                    // 훅 체인 보호
                }
            }

            IntPtr hookHandle = activeSource?._hookHandle ?? IntPtr.Zero; // 다음 훅 전달용 핸들
            return NativeMethods.CallNextHookEx(hookHandle, code, messagePointer, dataPointer);
        }

        // 지원하는 마우스 버튼 누름과 주입 여부를 판정하는 함수
        private void ProcessMouseMessage(IntPtr messagePointer, IntPtr dataPointer)
        {
            uint message = unchecked((uint)messagePointer.ToInt64()); // 마우스 메시지 종류
            bool isSupportedButtonDown = message == LeftButtonDownMessage || // 지원 버튼 누름 여부
                                         message == RightButtonDownMessage ||
                                         message == MiddleButtonDownMessage ||
                                         message == ExtraButtonDownMessage;
            if (!isSupportedButtonDown)
            {
                return;
            }

            NativeMethods.MouseHookData hookData =
                Marshal.PtrToStructure<NativeMethods.MouseHookData>(dataPointer); // 마우스 훅 데이터
            if ((hookData.Flags & (InjectedFlag | LowerIntegrityInjectedFlag)) != 0)
            {
                return;
            }

            _pendingInputs.Enqueue(ActivityInputEvent.CreateMouseButton(
                hookData.Position.X,
                hookData.Position.Y));
        }

        // Win32 마우스 훅 API를 격리하는 클래스
        private static class NativeMethods
        {
            // 저수준 마우스 훅 콜백 형식
            internal delegate IntPtr MouseHookCallback(
                int code,
                IntPtr messagePointer,
                IntPtr dataPointer);

            // Windows 화면 좌표를 보관하는 구조체
            [StructLayout(LayoutKind.Sequential)]
            internal struct Point
            {
                public int X; // 화면 가로 좌표
                public int Y; // 화면 세로 좌표
            }

            // 저수준 마우스 훅 데이터를 보관하는 구조체
            [StructLayout(LayoutKind.Sequential)]
            internal struct MouseHookData
            {
                public Point Position; // 입력 시점 화면 좌표
                public uint MouseData; // 버튼 또는 휠 추가 정보
                public uint Flags; // 마우스 훅 플래그
                public uint Time; // 메시지 발생 시간
                public UIntPtr ExtraInfo; // 추가 입력 정보
            }

            // Windows 훅을 설치하는 함수
            [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
            internal static extern IntPtr SetWindowsHookEx(
                int hookType,
                MouseHookCallback callback,
                IntPtr moduleHandle,
                uint threadId);

            // Windows 훅을 해제하는 함수
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

            // 다음 Windows 훅으로 메시지를 전달하는 함수
            [DllImport("user32.dll")]
            internal static extern IntPtr CallNextHookEx(
                IntPtr hookHandle,
                int code,
                IntPtr messagePointer,
                IntPtr dataPointer);

            // 현재 실행 모듈 핸들을 조회하는 함수
            [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern IntPtr GetModuleHandle(string moduleName);
        }
#endif
    }
}
