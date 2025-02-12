using System;
using System.Management;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace UsbDeviceMonitor
{
    public class UsbDeviceMonitor
    {
        public event Action<string> UsbDeviceConnected;
        public event Action<string> UsbDeviceDisconnected;

        // Spremi log datoteku u direktorij projekta
        private readonly string reportFilePath = Path.Combine(
            Directory.GetCurrentDirectory(), // Trenutni radni direktorij (obično bin\Debug\netX.X)
            "usb_device_report.txt");
        private bool isDeviceConnected = false;  // Zastavica za praćenje stanja uređaja
        private FileSystemWatcher usbWatcher;  // FileSystemWatcher za praćenje promjena na USB uređaju
        private FileSystemWatcher pcWatcher;   // FileSystemWatcher za praćenje promjena na racunalu
        private DateTime lastUsbFileChangeTime = DateTime.MinValue; // Vrijeme zadnje promjene na USB-u
        private string lastUsbFileName; // Ime zadnje datoteke promijenjene na USB-u

        private Timer eventTimer;  // Timer za grupiranje događaja
        private List<string> pendingEvents = new List<string>();  // Lista događaja koji čekaju na obradu

        public void StartMonitoring()
        {
            // Provjera trenutno povezanih uređaja prilikom pokretanja aplikacije
            CheckCurrentUsbDevices();

            // Praćenje kada je uređaj spojen (EventType = 2)
            ManagementEventWatcher insertWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2"));
            insertWatcher.EventArrived += (sender, e) =>
            {
                Thread.Sleep(1000); // Kratka odgoda za stabilnost

                if (!isDeviceConnected) // Provjeri je li uređaj već prijavljen
                {
                    string deviceId = GetUsbDeviceId();
                    string driveLetter = GetUsbDriveLetter();
                    string message = $"USB uređaj spojen u {DateTime.Now} - ID uređaja: {deviceId}";

                    if (!string.IsNullOrEmpty(driveLetter))
                    {
                        message += $" - Usb: {driveLetter}";
                        PrintUsbDriveTree(driveLetter); // Ispis strukture direktorija
                        StartFileSystemWatcher(driveLetter); // Pokreni FileSystemWatcher za USB
                        StartPcFileSystemWatcher(@"C:\Users\Korisnik\Desktop"); // Pokreni FileSystemWatcher za racunalo
                    }
                    else
                    {
                        message += " - Slovo usb particije nije pronađeno";
                    }

                    UsbDeviceConnected?.Invoke(message);
                    GenerateReport(message); // Dodaj zapis u log datoteku
                    isDeviceConnected = true; // Postavi zastavicu da je uređaj spojen
                }
            };
            insertWatcher.Start();

            // Praćenje kada je uređaj odspojen (EventType = 3)
            ManagementEventWatcher removeWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3"));
            removeWatcher.EventArrived += (sender, e) =>
            {
                // Dodavanje kratkog odgode prije nego što obradimo odspajanje
                Thread.Sleep(1000); // Ovdje čekamo 1 sekundu

                if (isDeviceConnected)
                {
                    string deviceId = GetUsbDeviceId();
                    string message = $"USB uređaj odspojen u {DateTime.Now} - ID uređaja: {deviceId}";

                    UsbDeviceDisconnected?.Invoke(message);
                    GenerateReport(message); // Dodaj zapis u log datoteku
                    isDeviceConnected = false; // Postavljamo zastavicu na false jer je uređaj sada odspojen

                    StopFileSystemWatcher(); // Zaustavi FileSystemWatcher za USB
                    StopPcFileSystemWatcher(); // Zaustavi FileSystemWatcher za racunalo
                }
            };
            removeWatcher.Start();
        }

        // Pokreni FileSystemWatcher za praćenje promjena na USB uređaju
        private void StartFileSystemWatcher(string driveLetter)
        {
            if (usbWatcher != null)
            {
                usbWatcher.Dispose(); // Oslobodi postojeći FileSystemWatcher
            }

            usbWatcher = new FileSystemWatcher
            {
                Path = driveLetter,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            // Dodaj event handlere za praćenje promjena na USB-u
            usbWatcher.Created += OnUsbFileCreated;
            usbWatcher.Changed += OnUsbFileChanged;
            usbWatcher.Renamed += OnUsbFileRenamed;

            Console.WriteLine($"Započeto praćenje promjena na USB uredaju: {driveLetter}");
        }

        // Pokreni FileSystemWatcher za praćenje promjena na računalu
        private void StartPcFileSystemWatcher(string pcDirectoryPath)
        {
            if (pcWatcher != null)
            {
                pcWatcher.Dispose(); // Oslobodi postojeći FileSystemWatcher
            }

            pcWatcher = new FileSystemWatcher
            {
                Path = pcDirectoryPath,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            // Dodaj event handlere za praćenje promjena na racunalu
            pcWatcher.Created += OnPcFileCreated;

            Console.WriteLine($"Započeto praćenje promjena na direktoriju računala: {pcDirectoryPath}");
        }

        // Zaustavi FileSystemWatcher za USB
        private void StopFileSystemWatcher()
        {
            if (usbWatcher != null)
            {
                usbWatcher.EnableRaisingEvents = false;
                usbWatcher.Dispose();
                usbWatcher = null;
                Console.WriteLine("Zaustavljeno praćenje promjena na USB uredaju.");
            }
        }

        // Zaustavi FileSystemWatcher za racunalu
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

        // Event handleri za USB promjene
        private void OnUsbFileCreated(object sender, FileSystemEventArgs e)
        {
            pendingEvents.Add($"Datoteka kreirana na USB-u: {e.FullPath}");
            StartEventTimer();
        }

        private void OnUsbFileChanged(object sender, FileSystemEventArgs e)
        {
            lastUsbFileChangeTime = DateTime.Now;
            lastUsbFileName = Path.GetFileName(e.FullPath);
            pendingEvents.Add($"Datoteka promijenjena na USB-u: {e.FullPath}");
            StartEventTimer();
        }

        private void OnUsbFileRenamed(object sender, RenamedEventArgs e)
        {
            pendingEvents.Add($"Datoteka preimenovana na USB-u: {e.OldFullPath} -> {e.FullPath}");
            StartEventTimer();
        }

        // Event handler za kreiranje datoteka na računalu
        private void OnPcFileCreated(object sender, FileSystemEventArgs e)
        {
            if ((DateTime.Now - lastUsbFileChangeTime).TotalSeconds < 1 &&
                Path.GetFileName(e.FullPath) == lastUsbFileName)
            {
                Console.WriteLine($"Datoteka kopirana s USB-a na racunalo: {e.FullPath}");
            }
        }

        // Pokreni Timer za grupiranje događaja
        private void StartEventTimer()
        {
            if (eventTimer == null)
            {
                eventTimer = new Timer(EventTimerCallback, null, 100, Timeout.Infinite);
            }
        }

        // Callback za Timer
        private void EventTimerCallback(object state)
        {
            if (pendingEvents.Count > 0)
            {
                Console.WriteLine(string.Join(Environment.NewLine, pendingEvents));
                pendingEvents.Clear();
            }

            eventTimer?.Dispose();
            eventTimer = null;
        }

        // Provjera svih trenutno povezanih USB uređaja
        private void CheckCurrentUsbDevices()
        {
            var currentDeviceIds = GetAllConnectedUsbDeviceIds();
            if (currentDeviceIds.Count > 0)
            {
                foreach (var deviceId in currentDeviceIds)
                {
                    string driveLetter = GetUsbDriveLetter();
                    string message = $"USB uređaj već spojen u {DateTime.Now} - ID uređaja: {deviceId}";

                    if (!string.IsNullOrEmpty(driveLetter))
                    {
                        message += $" - USB: {driveLetter}";
                        PrintUsbDriveTree(driveLetter); // Ispis strukture direktorija
                        StartFileSystemWatcher(driveLetter); // Pokreni FileSystemWatcher za USB
                        StartPcFileSystemWatcher(@"C:\Users\Korisnik\Desktop"); // Pokreni FileSystemWatcher za racunalo
                    }
                    else
                    {
                        message += " - Slovo USB particije nije pronađeno";
                    }

                    UsbDeviceConnected?.Invoke(message);
                    GenerateReport(message); // Dodaj zapis u log datoteku
                    isDeviceConnected = true;
                }
            }
            else
            {
                Console.WriteLine("Trenutno nema spojenih USB uređaja.");
            }
        }

        // Dohvaća sve trenutno povezane USB uređaje
        private List<string> GetAllConnectedUsbDeviceIds()
        {
            var deviceIds = new List<string>();
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_DiskDrive WHERE MediaType='Removable Media'");

            foreach (ManagementObject disk in searcher.Get())
            {
                string deviceId = disk["PNPDeviceID"]?.ToString();
                if (!string.IsNullOrEmpty(deviceId))
                {
                    deviceIds.Add(deviceId);
                }
            }

            return deviceIds;
        }

        // Funkcija za dohvaćanje jedinstvenog ID-a USB uređaja
        private string GetUsbDeviceId()
        {
            string deviceId = null;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_DiskDrive WHERE MediaType='Removable Media'");

            foreach (ManagementObject disk in searcher.Get())
            {
                deviceId = disk["PNPDeviceID"]?.ToString();
                if (!string.IsNullOrEmpty(deviceId))
                {
                    break;
                }
            }

            return deviceId;
        }

        // Funkcija za dobivanje slova particije za USB uređaj
        private string GetUsbDriveLetter()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                {
                    return drive.Name; // Vrati slovo usb particije
                }
            }
            return null;
        }

        // Ispis strukture direktorija USB uređaja
        private void PrintUsbDriveTree(string driveLetter)
        {
            Console.WriteLine($"Struktura direktorija za USB uredaj {driveLetter}:");
            PrintDirectoryTree(driveLetter, 0);
        }

        // Rekurzivna funkcija za ispis strukture direktorija
        private void PrintDirectoryTree(string path, int indentLevel)
        {
            try
            {
                foreach (var directory in Directory.GetDirectories(path))
                {
                    Console.WriteLine(new string(' ', indentLevel) + Path.GetFileName(directory));
                    PrintDirectoryTree(directory, indentLevel + 2);
                }

                foreach (var file in Directory.GetFiles(path))
                {
                    Console.WriteLine(new string(' ', indentLevel) + Path.GetFileName(file));
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
                File.AppendAllText(reportFilePath, message + Environment.NewLine);
                Console.WriteLine("Zapis dodan u log datoteku.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška pri pisanju u log datoteku: {ex.Message}");
            }
        }
    }
}