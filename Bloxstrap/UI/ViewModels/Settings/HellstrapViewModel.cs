using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Hellstrap.UI.ViewModels.Settings
{
    public class HellstrapViewModel : NotifyPropertyChangedViewModel
    {
        public bool ShouldExportConfig { get; set; } = true;
        public bool ShouldExportLogs { get; set; } = true;

        public ICommand ExportDataCommand => new RelayCommand(ExportData);

        private void ExportData()
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            var dialog = new SaveFileDialog
            {
                FileName = $"Hellstrap-export-{timestamp}.zip",
                Filter = $"{Strings.FileTypes_ZipArchive}|*.zip"
            };

            if (dialog.ShowDialog() != true)
                return;

            using var memStream = new MemoryStream();
            using var zipStream = new ZipOutputStream(memStream);

            if (ShouldExportConfig)
            {
                var configFiles = new List<string>
                {
                    App.Settings.FileLocation,
                    App.State.FileLocation,
                    App.FastFlags.FileLocation
                };
                AddFilesToZipStream(zipStream, configFiles, "Config/");
            }

            if (ShouldExportLogs && Directory.Exists(Paths.Logs))
            {
                var logFiles = Directory.GetFiles(Paths.Logs)
                    .Where(file => !file.Equals(App.Logger.FileLocation, StringComparison.OrdinalIgnoreCase));
                AddFilesToZipStream(zipStream, logFiles, "Logs/");
            }

            zipStream.Finish();
            memStream.Position = 0;

            SaveZipToFile(memStream, dialog.FileName);
        }

        private void AddFilesToZipStream(ZipOutputStream zipStream, IEnumerable<string> files, string directory)
        {
            foreach (var file in files.Where(File.Exists))
            {
                var entry = new ZipEntry(directory + Path.GetFileName(file))
                {
                    DateTime = DateTime.Now
                };

                zipStream.PutNextEntry(entry);

                using var fileStream = File.OpenRead(file);
                fileStream.CopyTo(zipStream);
            }
        }

        private void SaveZipToFile(MemoryStream zipMemoryStream, string filePath)
        {
            using var outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            zipMemoryStream.CopyTo(outputStream);
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
    }
}
