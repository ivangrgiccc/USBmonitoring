using System;
using System.IO;
using System.Threading;
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
        public void Test_DeviceDisconnectedEvent()
        {
            var monitor = new UsbDeviceMonitor();
            string receivedMessage = null;

            // Povezivanje događaja
            monitor.UsbDeviceDisconnected += (message) =>
            {
                receivedMessage = message;
            };

            // Simuliraj odspajanje uređaja
            monitor.StartMonitoring();

            // Provjera da li je događaj pozvan s jednim argumentom
            Assert.NotNull(receivedMessage);
            Assert.Contains("USB uredaj odspojen u", receivedMessage);
        }

        [Fact]
        public void Test_GenerateReport()
        {
            var monitor = new UsbDeviceMonitor();
            string message = "Test message";

            // Generiranje izvještaja
            monitor.GenerateReport(message);

            // Provjera je li izvještaj generiran
            string reportContent = File.ReadAllText("usb_device_report.txt");
            Assert.Contains(message, reportContent);
        }

        [Fact]
        public void Test_GenerateReport_MultipleMessages()
        {
            var monitor = new UsbDeviceMonitor();
            string message1 = "Test message 1";
            string message2 = "Test message 2";

            // Generiranje izvještaja
            monitor.GenerateReport(message1);
            monitor.GenerateReport(message2);

            // Provjera je li izvještaj generiran
            string reportContent = File.ReadAllText("usb_device_report.txt");
            Assert.Contains(message1, reportContent);
            Assert.Contains(message2, reportContent);
        }

        [Fact]
        public void Test_PrintUsbDriveTree()
        {
            var monitor = new UsbDeviceMonitor();
            string tempPath = Path.GetTempPath();
            string testDir = Path.Combine(tempPath, "TestDir");

            // Kreiranje testnog direktorija i fajlova
            Directory.CreateDirectory(testDir);
            File.WriteAllText(Path.Combine(testDir, "testfile.txt"), "Test content");

            // Testiranje ispisa strukture direktorija
            using (var sw = new StringWriter())
            {
                Console.SetOut(sw);
                monitor.PrintUsbDriveTree(testDir);

                var result = sw.ToString();
                Assert.Contains("testfile.txt", result);
            }

            // Čišćenje
            Directory.Delete(testDir, true);
        }

        [Fact]
        public void Test_ShouldProcessEvent()
        {
            var monitor = new UsbDeviceMonitor();
            string filePath = "testfile.txt";

            // Prvi put bi trebao vratiti true
            Assert.True(monitor.ShouldProcessEvent(filePath));

            // Drugi put bi trebao vratiti false zbog cooldown-a
            Assert.False(monitor.ShouldProcessEvent(filePath));

            // Čekanje više od cooldown vremena
            Thread.Sleep(600); // Cooldown je 500 ms
            Assert.True(monitor.ShouldProcessEvent(filePath));
        }

        [Fact]
        public void Test_CleanupOldEvents()
        {
            var monitor = new UsbDeviceMonitor();
            string filePath = "testfile.txt";

            // Dodavanje starog događaja
            monitor.ShouldProcessEvent(filePath);
            Thread.Sleep(600); // Čekanje više od cooldown vremena

            // Čišćenje starih događaja
            monitor.CleanupOldEvents();

            // Provjera da li je događaj uklonjen
            Assert.False(monitor.ShouldProcessEvent(filePath));
        }
    }
}