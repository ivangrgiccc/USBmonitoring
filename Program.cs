using System;

namespace UsbDeviceMonitor
{
    class Program
    {
        static void Main(string[] args)
        {
            UsbDeviceMonitor deviceMonitor = new UsbDeviceMonitor();

            // Pretplata na USB Connected event
            deviceMonitor.UsbDeviceConnected += (message) =>
            {
                Console.WriteLine(message);
            };

            // Pretplata na USB Disconnected event
            deviceMonitor.UsbDeviceDisconnected += (message) =>
            {
                Console.WriteLine(message);
            };

            // Pokretanje praćenja USB uređaja
            deviceMonitor.StartMonitoring();

            Console.WriteLine("USB uređaj monitoring počeo. Stisni enter za prekid...");
            Console.ReadLine(); // Drži aplikaciju aktivnom
        }
    }
}