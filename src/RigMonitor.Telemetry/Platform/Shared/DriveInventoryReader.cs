namespace RigMonitor.Telemetry.Platform.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Reads mounted volume inventory.
    /// </summary>
    internal static class DriveInventoryReader
    {
        internal static List<DiskVolumeTelemetry> ReadVolumes()
        {
            List<DiskVolumeTelemetry> volumes = new List<DiskVolumeTelemetry>();
            DriveInfo[] drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                try
                {
                    if (!drive.IsReady)
                    {
                        continue;
                    }

                    DiskVolumeTelemetry volume = new DiskVolumeTelemetry
                    {
                        Name = drive.Name,
                        MountPoint = drive.RootDirectory.FullName,
                        DriveType = drive.DriveType,
                        FileSystem = drive.DriveFormat,
                        TotalBytes = drive.TotalSize,
                        FreeBytes = drive.AvailableFreeSpace
                    };

                    volume.UsedBytes = Math.Max(0L, volume.TotalBytes - volume.FreeBytes);
                    if (volume.TotalBytes > 0)
                    {
                        volume.UtilizationPercent = (double)volume.UsedBytes / (double)volume.TotalBytes * 100D;
                    }

                    volumes.Add(volume);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return volumes;
        }
    }
}
