using System;
using System.IO;

namespace UsbFileCopyMonitor
{
    public class UsbFileCopyMonitor
    {
        private FileSystemWatcher usbWatcher; // Praćenje promjena na USB uređaju
        private FileSystemWatcher pcWatcher;  // Praćenje promjena na racunalu

        public void StartMonitoring(string usbDriveLetter, string pcDirectoryPath)
        {
            // Inicijalizacija USB FileSystemWatcher
            usbWatcher = new FileSystemWatcher
            {
                Path = usbDriveLetter,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            usbWatcher.Created += OnUsbFileCreated;
            usbWatcher.Changed += OnUsbFileChanged;
            usbWatcher.Renamed += OnUsbFileRenamed;

            Console.WriteLine($"Započeto praćenje USB particije: {usbDriveLetter}");

            // Inicijalizacija PC FileSystemWatcher
            pcWatcher = new FileSystemWatcher
            {
                Path = pcDirectoryPath,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            pcWatcher.Created += OnPcFileCreated;
            pcWatcher.Changed += OnPcFileChanged;
            pcWatcher.Renamed += OnPcFileRenamed;

            Console.WriteLine($"Započeto praćenje direktorija na racunalu: {pcDirectoryPath}");
        }

        // Event handleri za USB promjene
        private void OnUsbFileCreated(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"Datoteka kreirana na USB-u: {e.FullPath}");
        }

        private void OnUsbFileChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"Datoteka promijenjena na USB-u: {e.FullPath}");
        }

        private void OnUsbFileRenamed(object sender, RenamedEventArgs e)
        {
            Console.WriteLine($"Datoteka preimenovana na USB-u: {e.OldFullPath} -> {e.FullPath}");
        }

        // Event handleri za PC promjene
        private void OnPcFileCreated(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"Datoteka kreirana na racunalu: {e.FullPath}");
        }

        private void OnPcFileChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"Datoteka promijenjena na racunalu: {e.FullPath}");
        }

        private void OnPcFileRenamed(object sender, RenamedEventArgs e)
        {
            Console.WriteLine($"Datoteka preimenovana na racunalu: {e.OldFullPath} -> {e.FullPath}");
        }

        public void StopMonitoring()
        {
            usbWatcher?.Dispose();
            pcWatcher?.Dispose();
            Console.WriteLine("Praćenje zaustavljeno.");
        }
    }
}