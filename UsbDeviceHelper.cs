using System;
using System.IO;
using System.Linq;
using System.Management;

namespace UsbDeviceMonitor
{
    public static class UsbDeviceHelper
    {
        public static string GetUsbDriveLetter()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                {
                    return drive.Name; // npr. "E:\"
                }
            }
            return "Nepoznat";
        }
    }
}