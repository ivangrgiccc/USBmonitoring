using System;
using System.IO;

namespace UsbDeviceMonitor
{
    public class UsbDataTransferMonitor
    {
        private FileSystemWatcher watcher;

        public void MonitorUsbTransfer(string usbDriveLetter)
        {
            if (usbDriveLetter == "Unknown")
            {
                Console.WriteLine("USB uredaj particija nepoznata, monitoranje prekinuto.");
                return;
            }

            watcher = new FileSystemWatcher
            {
                Path = usbDriveLetter,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Created += (s, e) => Console.WriteLine($"Datoteka kreirana: {e.FullPath}");
            watcher.Deleted += (s, e) => Console.WriteLine($"Datoteka deletana: {e.FullPath}");
            watcher.Changed += (s, e) => Console.WriteLine($"Datoteka modificirana: {e.FullPath}");
            watcher.Renamed += (s, e) => Console.WriteLine($"Datoteka ime promijenjeno: {e.OldFullPath} -> {e.FullPath}");

            Console.WriteLine($"Monitoring USB prometa na {usbDriveLetter}. Stisni Enter za stop.");
        }
    }
}