using System;
using System.IO;
using System.Text;

namespace UsbDeviceMonitor
{
    class Program
    {
        static void Main(string[] args)
        {
            // Postavljanje putanje do log datoteke
            string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "usb_device_log.txt");

            // Otvaranje StreamWriter-a za pisanje u log datoteku
            using (StreamWriter logWriter = new StreamWriter(logFilePath, append: true))
            {
                // Preusmjeravanje konzolnog ispisa u log datoteku i na konzolu
                var consoleWriter = new ConsoleAndLogWriter(logWriter);
                Console.SetOut(consoleWriter);

                UsbDeviceMonitor deviceMonitor = new UsbDeviceMonitor();

                // Pretplata na USB Connected event
                deviceMonitor.UsbDeviceConnected += (message) =>
                {
                    Console.WriteLine(message); // Ispisuje i na konzolu i u log datoteku
                };

                // Pretplata na USB Disconnected event
                deviceMonitor.UsbDeviceDisconnected += (message) =>
                {
                    Console.WriteLine(message); // Ispisuje i na konzolu i u log datoteku
                };

                // Pokretanje praćenja USB uređaja
                deviceMonitor.StartMonitoring();

                Console.WriteLine("USB uređaj monitoring počeo. Stisni enter za prekid...");
                Console.ReadLine(); // Drži aplikaciju aktivnom
            }
        }
    }

    // Custom TextWriter koji piše i na konzolu i u log datoteku
    public class ConsoleAndLogWriter : TextWriter
    {
        private readonly TextWriter _consoleWriter;
        private readonly TextWriter _logWriter;

        public ConsoleAndLogWriter(TextWriter logWriter)
        {
            _consoleWriter = Console.Out;
            _logWriter = logWriter;
        }

        public override void WriteLine(string value)
        {
            _consoleWriter.WriteLine(value); // Ispis na konzolu
            _logWriter.WriteLine(value); // Ispis u log datoteku
        }

        public override Encoding Encoding => _consoleWriter.Encoding;
    }
}