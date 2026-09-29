using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CustomKeyboardReactor.Tests.EditMode
{
    // 저장된 모니터 선택과 주 모니터 대체 규칙을 검증하는 클래스
    public sealed class WindowsMonitorServiceTests
    {
        // 저장된 장치 ID가 있으면 해당 모니터를 선택하는지 검증하는 함수
        [Test]
        public void Resolve_WithSavedDeviceId_ReturnsMatchingMonitor()
        {
            IReadOnlyList<WindowsMonitorService.MonitorWorkArea> monitors = CreateMonitors(); // 테스트 모니터 목록

            WindowsMonitorService.MonitorWorkArea selected = WindowsMonitorService.Resolve(
                "DISPLAY-B",
                monitors);

            Assert.That(selected.DeviceId, Is.EqualTo("DISPLAY-B"));
        }

        // 저장된 모니터가 없으면 주 모니터를 선택하는지 검증하는 함수
        [Test]
        public void Resolve_WithMissingDeviceId_ReturnsPrimaryMonitor()
        {
            IReadOnlyList<WindowsMonitorService.MonitorWorkArea> monitors = CreateMonitors(); // 테스트 모니터 목록

            WindowsMonitorService.MonitorWorkArea selected = WindowsMonitorService.Resolve(
                "REMOVED-DISPLAY",
                monitors);

            Assert.That(selected.DeviceId, Is.EqualTo("DISPLAY-A"));
            Assert.That(selected.IsPrimary, Is.True);
        }

        // 다중 모니터 테스트 데이터를 생성하는 함수
        private static IReadOnlyList<WindowsMonitorService.MonitorWorkArea> CreateMonitors()
        {
            return new[]
            {
                new WindowsMonitorService.MonitorWorkArea(
                    "DISPLAY-A",
                    new RectInt(0, 0, 1920, 1040),
                    true),
                new WindowsMonitorService.MonitorWorkArea(
                    "DISPLAY-B",
                    new RectInt(1920, 0, 2560, 1400),
                    false),
            };
        }
    }
}
