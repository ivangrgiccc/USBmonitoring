using System;
using System.Management;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace UsbDeviceMonitor
{
    public class UsbDeviceMonitor
    {
        public event Action<string> UsbDeviceConnected;
        public event Action<string> UsbDeviceDisconnected;

        private readonly string reportFilePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "usb_device_report.txt");

        private List<FileSystemWatcher> usbWatchers = new List<FileSystemWatcher>(); // Lista za praćenje više USB uređaja
        private FileSystemWatcher? pcWatcher;
        private DateTime lastUsbFileChangeTime = DateTime.MinValue;
        private string lastUsbFileName;

        private Timer eventTimer;  // Timer za grupiranje događaja
        private List<string> pendingEvents = new List<string>();  // Lista događaja koji čekaju na obradu

        private Dictionary<string, DateTime> lastEventTimes = new Dictionary<string, DateTime>(); // Pohranjuje vrijeme zadnjeg događaja za svaku datoteku
        private readonly TimeSpan eventCooldown = TimeSpan.FromMilliseconds(500); // 500 ms cooldown za duple događaje

        public void StartMonitoring()
        {
            CheckCurrentUsbDevices();

            ManagementEventWatcher insertWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2"));
            insertWatcher.EventArrived += (sender, e) =>
            {
                Thread.Sleep(1000); // Čekaj 1 sekundu da se uređaj stabilizira
                CheckCurrentUsbDevices(); // Provjeri sve USB uređaje nakon povezivanja
            };
            insertWatcher.Start();

            ManagementEventWatcher removeWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3"));
            removeWatcher.EventArrived += (sender, e) =>
            {
                Thread.Sleep(1000); // Čekaj 1 sekundu da se uređaj stabilizira
                CheckCurrentUsbDevices(); // Provjeri sve USB uređaje nakon odspajanja
            };
            removeWatcher.Start();
        }

        private void CheckCurrentUsbDevices()
        {
            var currentDriveLetters = GetUsbDriveLetters();

            if (currentDriveLetters.Count > 0)
            {
                foreach (var driveLetter in currentDriveLetters)
                {
                    if (!usbWatchers.Any(w => w.Path == driveLetter)) // Ako uređaj već nije u praćenju
                    {
                        string message = $"USB uređaj spojen u {DateTime.Now} - USB: {driveLetter}";
                        UsbDeviceConnected?.Invoke(message);
                        GenerateReport(message);
                        StartFileSystemWatcher(driveLetter);
                        PrintUsbDriveTree(driveLetter); // Dodano za ispis strukture direktorija
                    }
                }
            }
            else
            {
                Console.WriteLine("Trenutno nema spojenih USB uređaja.");
            }

            // Zaustavi praćenje za uređaje koji više nisu spojeni
            foreach (var watcher in usbWatchers.ToList())
            {
                if (!currentDriveLetters.Contains(watcher.Path))
                {
                    StopFileSystemWatcher(watcher.Path);
                    string message = $"USB uređaj odspojen u {DateTime.Now} - USB: {watcher.Path}";
                    UsbDeviceDisconnected?.Invoke(message);
                    GenerateReport(message);
                }
            }
        }

        private void StartFileSystemWatcher(string driveLetter)
        {
            var watcher = new FileSystemWatcher
            {
                Path = driveLetter,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Created += OnUsbFileCreated;
            watcher.Changed += OnUsbFileChanged;
            watcher.Renamed += OnUsbFileRenamed;
            watcher.Deleted += OnUsbFileDeleted;

            usbWatchers.Add(watcher); // Dodaj watcher u listu
            Console.WriteLine($"Započeto praćenje promjena na USB uređaju: {driveLetter}");
        }

        private void StopFileSystemWatcher(string driveLetter)
        {
            var watcher = usbWatchers.FirstOrDefault(w => w.Path == driveLetter);
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                usbWatchers.Remove(watcher); // Ukloni watcher iz liste
                Console.WriteLine($"Zaustavljeno praćenje promjena na USB uređaju: {driveLetter}");
            }
        }

        private List<string> GetUsbDriveLetters()
        {
            var driveLetters = new List<string>();
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                {
                    driveLetters.Add(drive.Name);
                }
            }
            return driveLetters;
        }

        private void StartPcFileSystemWatcher(string pcDirectoryPath)
        {
            if (pcWatcher != null)
            {
                pcWatcher.Dispose();
            }

            pcWatcher = new FileSystemWatcher
            {
                Path = pcDirectoryPath,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            pcWatcher.Created += OnPcFileCreated;

            Console.WriteLine($"Započeto praćenje promjena na direktoriju računala: {pcDirectoryPath}");
        }

        private void StopPcFileSystemWatcher()
        {
            if (pcWatcher != null)
            {
                pcWatcher.EnableRaisingEvents = false;
                pcWatcher.Dispose();
                pcWatcher = null;
                Console.WriteLine("Zaustavljeno praćenje promjena na direktoriju računala.");
            }
        }

        private void OnUsbFileCreated(object sender, FileSystemEventArgs e)
        {
            if (ShouldProcessEvent(e.FullPath))
            {
                string message = $"{DateTime.Now}: Datoteka kreirana na USB-u: {e.FullPath}";
                pendingEvents.Add(message);
                GenerateReport(message); // Dodano za zapisivanje u log fajl
                StartEventTimer();
            }
        }

        private void OnUsbFileChanged(object sender, FileSystemEventArgs e)
        {
            if (ShouldProcessEvent(e.FullPath))
            {
                lastUsbFileChangeTime = DateTime.Now;
                lastUsbFileName = Path.GetFileName(e.FullPath);
                string message = $"{DateTime.Now}: Datoteka promijenjena na USB-u: {e.FullPath}";
                pendingEvents.Add(message);
                GenerateReport(message); // Dodano za zapisivanje u log fajl
                StartEventTimer();
            }
        }

        private void OnUsbFileRenamed(object sender, RenamedEventArgs e)
        {
            if (ShouldProcessEvent(e.FullPath))
            {
                string message = $"{DateTime.Now}: Datoteka preimenovana na USB-u: {e.OldFullPath} -> {e.FullPath}";
                pendingEvents.Add(message);
                GenerateReport(message); // Dodano za zapisivanje u log fajl
                StartEventTimer();
            }
        }

        private void OnUsbFileDeleted(object sender, FileSystemEventArgs e)
        {
            if (ShouldProcessEvent(e.FullPath))
            {
                string message = $"{DateTime.Now}: Datoteka obrisana s USB-a: {e.FullPath}";
                pendingEvents.Add(message);
                GenerateReport(message); // Dodano za zapisivanje u log fajl
                StartEventTimer();
            }
        }

        private void OnPcFileCreated(object sender, FileSystemEventArgs e)
        {
            if ((DateTime.Now - lastUsbFileChangeTime).TotalSeconds < 1 &&
                Path.GetFileName(e.FullPath) == lastUsbFileName)
            {
                string message = $"{DateTime.Now}: Datoteka kopirana s USB-a na računalo: {e.FullPath}";
                Console.WriteLine(message);
                GenerateReport(message); // Dodano za zapisivanje u log fajl
            }
        }

        private void StartEventTimer()
        {
            if (eventTimer == null)
            {
                eventTimer = new Timer(EventTimerCallback, null, 100, Timeout.Infinite);
            }
        }

        public void EventTimerCallback(object state)
        {
            if (pendingEvents.Count > 0)
            {
                Console.WriteLine(string.Join(Environment.NewLine, pendingEvents));
                pendingEvents.Clear();
            }

            CleanupOldEvents(); // Očisti stare zapise

            eventTimer?.Dispose();
            eventTimer = null;
        }

        public bool ShouldProcessEvent(string filePath)
        {
            if (lastEventTimes.TryGetValue(filePath, out DateTime lastTime))
            {
                if (DateTime.Now - lastTime < eventCooldown)
                {
                    return false; // Ignoriraj događaj jer je preblizu prethodnom
                }
            }

            lastEventTimes[filePath] = DateTime.Now; // Ažuriraj vrijeme zadnjeg događaja
            return true;
        }

        public void CleanupOldEvents()
        {
            var cutoffTime = DateTime.Now - TimeSpan.FromMinutes(5); // Očisti zapise starije od 5 minuta
            var oldKeys = lastEventTimes.Where(kvp => kvp.Value < cutoffTime).Select(kvp => kvp.Key).ToList();

            foreach (var key in oldKeys)
            {
                lastEventTimes.Remove(key);
            }
        }

        public void PrintUsbDriveTree(string driveLetter)
        {
            Console.WriteLine($"Struktura direktorija za USB uređaj {driveLetter}:");
            PrintDirectoryTree(driveLetter, 0);
            Console.WriteLine("---------------------------");
        }

        private void PrintDirectoryTree(string path, int indentLevel)
        {
            try
            {
                // Lista direktorija i fajlova koje želimo ignorirati
                var ignoreList = new List<string>
        {
            "System Volume Information",
            "FOUND.000",
            ".Spotlight-V100",
            ".fseventsd",
            ".Trashes",
            ".TemporaryItems",
            ".DS_Store",
            "Thumbs.db",
            "desktop.ini"
        };

                foreach (var directory in Directory.GetDirectories(path))
                {
                    string dirName = Path.GetFileName(directory);
                    if (ignoreList.Contains(dirName))
                    {
                        continue; // Ignoriraj nebitne direktorije
                    }

                    Console.WriteLine(new string(' ', indentLevel) + dirName);
                    PrintDirectoryTree(directory, indentLevel + 2);
                }

                foreach (var file in Directory.GetFiles(path))
                {
                    string fileName = Path.GetFileName(file);
                    if (ignoreList.Contains(fileName) || fileName.StartsWith("~$") || fileName.StartsWith("._")) continue; // Ignoriraj nebitne fajlove

                    Console.WriteLine(new string(' ', indentLevel) + fileName);
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine(new string(' ', indentLevel) + "Pristup nekim direktorijima ili datotekama je odbijen.");
            }
        }


        public void GenerateReport(string message)
        {
            try
            {
                Console.WriteLine($"Pokušavam spremiti zapis u: {reportFilePath}");
                File.AppendAllText(reportFilePath, $"{DateTime.Now}: {message}" + Environment.NewLine);
                Console.WriteLine("Zapis dodan u log datoteku.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri pisanju u log datoteku: {ex.Message}");
            }
        }
    }
}