using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CustomKeyboardReactor
{
    // Windows 모니터의 장치 ID와 작업 영역을 제공하는 인프라 모듈
    public sealed class WindowsMonitorService
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const uint PrimaryMonitorFlag = 0x00000001; // 주 모니터 플래그
#endif

        // 모니터 장치 ID와 작업 영역을 보관하는 값 형식
        public readonly struct MonitorWorkArea
        {
            // 모니터 정보를 생성하는 생성자
            public MonitorWorkArea(string deviceId, RectInt workArea, bool isPrimary)
            {
                DeviceId = deviceId ?? string.Empty;
                WorkArea = workArea;
                IsPrimary = isPrimary;
            }

            public string DeviceId { get; } // 모니터 장치 ID
            public RectInt WorkArea { get; } // Windows 작업 영역
            public bool IsPrimary { get; } // 주 모니터 여부
        }

        // 현재 연결된 모니터 작업 영역 목록을 반환하는 함수
        public IReadOnlyList<MonitorWorkArea> GetMonitors()
        {
            List<MonitorWorkArea> monitors = new List<MonitorWorkArea>(); // 조회된 모니터 목록
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            NativeMethods.EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                (monitorHandle, deviceContext, monitorRect, userData) =>
                {
                    NativeMethods.MonitorInfo monitorInfo = new NativeMethods.MonitorInfo // 모니터 정보 버퍼
                    {
                        Size = Marshal.SizeOf<NativeMethods.MonitorInfo>(),
                    };
                    if (!NativeMethods.GetMonitorInfo(monitorHandle, ref monitorInfo))
                    {
                        return true;
                    }

                    int width = monitorInfo.Work.Right - monitorInfo.Work.Left; // 작업 영역 너비
                    int height = monitorInfo.Work.Bottom - monitorInfo.Work.Top; // 작업 영역 높이
                    monitors.Add(new MonitorWorkArea(
                        monitorInfo.DeviceName,
                        new RectInt(monitorInfo.Work.Left, monitorInfo.Work.Top, width, height),
                        (monitorInfo.Flags & PrimaryMonitorFlag) != 0));
                    return true;
                },
                IntPtr.Zero);
#else
            monitors.Add(new MonitorWorkArea(
                "Primary",
                new RectInt(0, 0, Math.Max(1, Screen.width), Math.Max(1, Screen.height)),
                true));
#endif
            return monitors;
        }

        // 저장된 장치 ID에 대응하거나 주 모니터인 작업 영역을 반환하는 함수
        public MonitorWorkArea Resolve(string deviceId)
        {
            return Resolve(deviceId, GetMonitors());
        }

        // 주어진 목록에서 저장 장치 ID와 대체 주 모니터를 선택하는 함수
        public static MonitorWorkArea Resolve(
            string deviceId,
            IReadOnlyList<MonitorWorkArea> monitors)
        {
            if (monitors == null || monitors.Count == 0)
            {
                throw new InvalidOperationException("No display monitor is available.");
            }

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                for (int index = 0; index < monitors.Count; index++)
                {
                    if (string.Equals(
                            monitors[index].DeviceId,
                            deviceId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return monitors[index];
                    }
                }
            }

            for (int index = 0; index < monitors.Count; index++)
            {
                if (monitors[index].IsPrimary)
                {
                    return monitors[index];
                }
            }

            return monitors[0];
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Win32 모니터 조회 API를 격리하는 클래스
        private static class NativeMethods
        {
            // Win32 사각형 값
            [StructLayout(LayoutKind.Sequential)]
            internal struct Rect
            {
                public int Left; // 왼쪽 좌표
                public int Top; // 위쪽 좌표
                public int Right; // 오른쪽 좌표
                public int Bottom; // 아래쪽 좌표
            }

            // Win32 확장 모니터 정보
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct MonitorInfo
            {
                public int Size; // 구조체 크기
                public Rect Monitor; // 전체 모니터 영역
                public Rect Work; // 작업 영역
                public uint Flags; // 모니터 플래그

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
                public string DeviceName; // 모니터 장치 이름
            }

            // 모니터 열거 콜백 형식
            internal delegate bool MonitorEnumerationCallback(
                IntPtr monitorHandle,
                IntPtr deviceContext,
                IntPtr monitorRect,
                IntPtr userData);

            // 연결된 모니터를 열거하는 함수
            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool EnumDisplayMonitors(
                IntPtr deviceContext,
                IntPtr clipRect,
                MonitorEnumerationCallback callback,
                IntPtr userData);

            // 모니터의 장치 이름과 작업 영역을 조회하는 함수
            [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetMonitorInfo(
                IntPtr monitorHandle,
                ref MonitorInfo monitorInfo);
        }
#endif
    }
}
