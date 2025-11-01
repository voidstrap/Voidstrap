using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Voidstrap.Utility
{
    internal static class Filesystem
    {
        internal static long GetFreeDiskSpace(string path)
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                // https://github.com/Bloxstraplabs/Bloxstrap/issues/1648#issuecomment-2192571030
                if (path.ToUpperInvariant().StartsWith(drive.Name.ToUpperInvariant()))
                    return drive.AvailableFreeSpace;
            }

            return -1;
        }

        internal static void AssertReadOnly(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists || !fileInfo.IsReadOnly)
                return;

            fileInfo.IsReadOnly = false;
            App.Logger.WriteLine("Filesystem::AssertReadOnly", $"The following file was set as read-only: {filePath}");
        }

        internal static void AssertReadOnlyDirectory(string directoryPath)
        {
            var directory = new DirectoryInfo(directoryPath);

            if (!directory.Exists)
                return;
            directory.Attributes = FileAttributes.Normal;

            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                try
                {
                    info.Attributes = FileAttributes.Normal;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("Filesystem::AssertReadOnlyDirectory",
                        $"Failed to change attributes for {info.FullName}: {ex.Message}");
                }
            }

            App.Logger.WriteLine("Filesystem::AssertReadOnlyDirectory",
                $"Removed read-only attributes from directory: {directoryPath}");
        }
    }
}
