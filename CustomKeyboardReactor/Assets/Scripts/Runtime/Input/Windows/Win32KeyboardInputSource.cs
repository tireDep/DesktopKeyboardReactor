using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace CustomKeyboardReactor
{
    // Win32 저수준 훅으로 전역 키보드 반응 입력을 수집하는 클래스
    public sealed class Win32KeyboardInputSource : IActivityInputSource
    {
        private readonly ConcurrentQueue<ActivityInputEvent> _pendingInputs = new ConcurrentQueue<ActivityInputEvent>(); // 대기 중인 키보드 반응 입력 큐

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int KeyboardHookType = 13; // 저수준 키보드 훅 종류
        private const uint KeyDownMessage = 0x0100; // 키 누름 메시지
        private const uint KeyUpMessage = 0x0101; // 키 해제 메시지
        private const uint SystemKeyDownMessage = 0x0104; // 시스템 키 누름 메시지
        private const uint SystemKeyUpMessage = 0x0105; // 시스템 키 해제 메시지
        private const uint ExtendedKeyFlag = 0x00000001; // 확장 키 플래그
        private const uint LowerIntegrityInjectedFlag = 0x00000002; // 낮은 무결성 주입 플래그
        private const uint InjectedFlag = 0x00000010; // 주입 입력 플래그
        private const uint ExtendedKeyIdentifierFlag = 0x00010000; // 확장 스캔 코드 구분 플래그
        private const uint QuitMessage = 0x0012; // 훅 스레드 종료 메시지
        private const uint NoRemoveMessage = 0x0000; // 메시지 큐 생성 플래그

        private static readonly NativeMethods.KeyboardHookCallback HookCallback = HandleHook; // 키보드 훅 콜백 참조
        private static Win32KeyboardInputSource _activeSource; // 활성 키보드 입력 공급자

        private readonly HashSet<uint> _pressedKeyIdentifiers = new HashSet<uint>(); // 현재 눌린 스캔 코드 목록
        private IntPtr _hookHandle; // 키보드 훅 핸들
        private ManualResetEventSlim _hookStartupCompleted; // 훅 스레드 시작 완료 신호
        private Thread _hookThread; // 키보드 훅 메시지 스레드
        private Exception _hookThreadException; // 훅 스레드 예외
        private uint _hookThreadId; // 키보드 훅 스레드 식별자
        private bool _hookStartupSucceeded; // 키보드 훅 시작 성공 여부

#endif

        public bool IsRunning { get; private set; } // 키보드 훅 실행 여부

        // 전역 키보드 훅을 설치하는 함수
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

            _activeSource = this;
            _hookStartupSucceeded = false;
            _hookThreadException = null;
            _hookStartupCompleted = new ManualResetEventSlim(false);
            _hookThread = new Thread(RunHookThread)
            {
                IsBackground = true,
                Name = "CustomKeyboardReactor Keyboard Hook",
            };
            _hookThread.Start();
            _hookStartupCompleted.Wait();

            if (!_hookStartupSucceeded)
            {
                _hookThread.Join();
                ReleaseHookThreadState();
                _activeSource = null;
                return false;
            }

            return true;
#else
            return false;
#endif
        }

        // 전역 키보드 훅을 해제하는 함수
        public void Stop()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Thread hookThread = _hookThread; // 종료할 키보드 훅 스레드
            if (hookThread != null)
            {
                if (hookThread.IsAlive && !NativeMethods.PostThreadMessage(
                        _hookThreadId,
                        QuitMessage,
                        UIntPtr.Zero,
                        IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                hookThread.Join();
            }

            Exception hookThreadException = _hookThreadException; // 종료 중 발생한 훅 스레드 예외
            ReleaseHookThreadState();
            _pressedKeyIdentifiers.Clear();
            if (ReferenceEquals(_activeSource, this))
            {
                _activeSource = null;
            }

            if (hookThreadException != null)
            {
                throw hookThreadException;
            }
#endif

            ClearPendingInputs();
            IsRunning = false;
        }

        // 대기 중인 키보드 반응 입력을 반환하는 함수
        public bool TryDequeue(out ActivityInputEvent inputEvent)
        {
            return _pendingInputs.TryDequeue(out inputEvent);
        }

        // 키보드 훅 자원을 정리하는 함수
        public void Dispose()
        {
            Stop();
        }

        // 대기 중인 키보드 입력을 비우는 함수
        private void ClearPendingInputs()
        {
            ActivityInputEvent inputEvent; // 제거할 키보드 반응 입력
            while (_pendingInputs.TryDequeue(out inputEvent))
            {
            }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // 전용 메시지 루프에서 키보드 훅을 실행하는 함수
        private void RunHookThread()
        {
            bool startupSignaled = false; // 훅 시작 결과 전달 여부
            try
            {
                _hookThreadId = NativeMethods.GetCurrentThreadId();
                NativeMethods.MessageData messageData; // 훅 스레드 메시지 데이터
                NativeMethods.PeekMessage(
                    out messageData,
                    IntPtr.Zero,
                    0,
                    0,
                    NoRemoveMessage);

                IntPtr moduleHandle = NativeMethods.GetModuleHandle(null); // 현재 실행 모듈 핸들
                if (moduleHandle == IntPtr.Zero)
                {
                    _hookThreadException = new Win32Exception(Marshal.GetLastWin32Error());
                    return;
                }

                _hookHandle = NativeMethods.SetWindowsHookEx(
                    KeyboardHookType,
                    HookCallback,
                    moduleHandle,
                    0);
                if (_hookHandle == IntPtr.Zero)
                {
                    _hookThreadException = new Win32Exception(Marshal.GetLastWin32Error());
                    return;
                }

                _hookStartupSucceeded = true;
                IsRunning = true;
                _hookStartupCompleted.Set();
                startupSignaled = true;

                while (true)
                {
                    int messageResult = NativeMethods.GetMessage( // 메시지 조회 결과
                        out messageData,
                        IntPtr.Zero,
                        0,
                        0);
                    if (messageResult > 0)
                    {
                        continue;
                    }

                    if (messageResult < 0)
                    {
                        _hookThreadException = new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    break;
                }
            }
            catch (Exception exception)
            {
                _hookThreadException = exception;
            }
            finally
            {
                if (!startupSignaled)
                {
                    _hookStartupCompleted.Set();
                }

                if (_hookHandle != IntPtr.Zero)
                {
                    if (!NativeMethods.UnhookWindowsHookEx(_hookHandle) && _hookThreadException == null)
                    {
                        _hookThreadException = new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    _hookHandle = IntPtr.Zero;
                }

                IsRunning = false;
            }
        }

        // 종료된 키보드 훅 스레드 상태를 정리하는 함수
        private void ReleaseHookThreadState()
        {
            _hookStartupCompleted?.Dispose();
            _hookStartupCompleted = null;
            _hookThread = null;
            _hookThreadId = 0;
            _hookStartupSucceeded = false;
            _hookThreadException = null;
        }

        // Win32 키보드 메시지를 반응 입력으로 변환하는 함수
        private static IntPtr HandleHook(int code, IntPtr messagePointer, IntPtr dataPointer)
        {
            Win32KeyboardInputSource activeSource = _activeSource; // 활성 키보드 입력 공급자
            if (code >= 0 && activeSource != null)
            {
                try
                {
                    activeSource.ProcessKeyboardMessage(messagePointer, dataPointer);
                }
                catch
                {
                    // 훅 체인 보호
                }
            }

            IntPtr hookHandle = activeSource?._hookHandle ?? IntPtr.Zero; // 다음 훅 전달용 핸들
            return NativeMethods.CallNextHookEx(hookHandle, code, messagePointer, dataPointer);
        }

        // 키 누름과 해제 메시지에서 반복 및 주입 입력을 제거하는 함수
        private void ProcessKeyboardMessage(IntPtr messagePointer, IntPtr dataPointer)
        {
            uint message = unchecked((uint)messagePointer.ToInt64()); // 키보드 메시지 종류
            bool isKeyDown = message == KeyDownMessage || message == SystemKeyDownMessage; // 키 누름 여부
            bool isKeyUp = message == KeyUpMessage || message == SystemKeyUpMessage; // 키 해제 여부
            if (!isKeyDown && !isKeyUp)
            {
                return;
            }

            NativeMethods.KeyboardHookData hookData =
                Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(dataPointer); // 키보드 훅 데이터
            if ((hookData.Flags & (InjectedFlag | LowerIntegrityInjectedFlag)) != 0)
            {
                return;
            }

            uint keyIdentifier = (hookData.Flags & ExtendedKeyFlag) != 0 // 반복 판정용 스캔 코드
                ? hookData.ScanCode | ExtendedKeyIdentifierFlag
                : hookData.ScanCode;
            if (isKeyUp)
            {
                _pressedKeyIdentifiers.Remove(keyIdentifier);
                return;
            }

            if (!_pressedKeyIdentifiers.Add(keyIdentifier))
            {
                return;
            }

            _pendingInputs.Enqueue(ActivityInputEvent.CreateKeyboard());
        }

        // Win32 키보드 훅 API를 격리하는 클래스
        private static class NativeMethods
        {
            // 저수준 키보드 훅 콜백 형식
            internal delegate IntPtr KeyboardHookCallback(
                int code,
                IntPtr messagePointer,
                IntPtr dataPointer);

            // 저수준 키보드 훅 데이터를 보관하는 구조체
            [StructLayout(LayoutKind.Sequential)]
            internal struct KeyboardHookData
            {
                public uint VirtualKeyCode; // 가상 키 코드
                public uint ScanCode; // 물리 스캔 코드
                public uint Flags; // 키보드 훅 플래그
                public uint Time; // 메시지 발생 시간
                public UIntPtr ExtraInfo; // 추가 입력 정보
            }

            // Win32 메시지 루프 데이터를 보관하는 구조체
            [StructLayout(LayoutKind.Sequential)]
            internal struct MessageData
            {
                public IntPtr WindowHandle; // 대상 창 핸들
                public uint Message; // 메시지 종류
                public UIntPtr WParam; // 메시지 부가 값
                public IntPtr LParam; // 메시지 데이터 포인터
                public uint Time; // 메시지 발생 시간
                public PointData Point; // 메시지 발생 좌표
                public uint Private; // 시스템 전용 값
            }

            // Win32 메시지 좌표를 보관하는 구조체
            [StructLayout(LayoutKind.Sequential)]
            internal struct PointData
            {
                public int X; // 가로 좌표
                public int Y; // 세로 좌표
            }

            // Windows 훅을 설치하는 함수
            [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
            internal static extern IntPtr SetWindowsHookEx(
                int hookType,
                KeyboardHookCallback callback,
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

            // 현재 스레드의 메시지 큐를 준비하는 함수
            [DllImport("user32.dll", EntryPoint = "PeekMessageW")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool PeekMessage(
                out MessageData messageData,
                IntPtr windowHandle,
                uint minimumMessage,
                uint maximumMessage,
                uint removeMessage);

            // 훅 스레드에서 다음 메시지를 기다리는 함수
            [DllImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
            internal static extern int GetMessage(
                out MessageData messageData,
                IntPtr windowHandle,
                uint minimumMessage,
                uint maximumMessage);

            // 훅 스레드에 종료 메시지를 보내는 함수
            [DllImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool PostThreadMessage(
                uint threadId,
                uint message,
                UIntPtr wParam,
                IntPtr lParam);

            // 현재 실행 모듈 핸들을 조회하는 함수
            [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern IntPtr GetModuleHandle(string moduleName);

            // 현재 Win32 스레드 식별자를 조회하는 함수
            [DllImport("kernel32.dll")]
            internal static extern uint GetCurrentThreadId();
        }
#endif
    }
}
