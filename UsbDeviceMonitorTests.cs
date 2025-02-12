using System;
using Xunit;

namespace UsbDeviceMonitor.Tests
{
    public class UsbDeviceMonitorTests
    {
        [Fact]
        public void Test_DeviceConnectedEvent()
        {
            var monitor = new UsbDeviceMonitor();
            string receivedMessage = null;

            // Povezivanje događaja - sada očekuje samo jedan argument (string)
            monitor.UsbDeviceConnected += (message) =>
            {
                receivedMessage = message;
            };

            // Simuliraj povezivanje uređaja
            monitor.StartMonitoring();

            // Provjera da li je događaj pozvan s jednim argumentom
            Assert.NotNull(receivedMessage);
            Assert.Contains("USB uredaj spojen u", receivedMessage);
        }

        [Fact]
        public void Test_GenerateReport()
        {
            var monitor = new UsbDeviceMonitor();
            string message = "Test message";

            // Generiranje izvještaja
            monitor.GenerateReport(message);

            // Provjera je li izvještaj generiran
            string reportContent = System.IO.File.ReadAllText("usb_device_report.txt");
            Assert.Contains(message, reportContent);
        }
    }
}