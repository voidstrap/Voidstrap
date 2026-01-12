using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class MobilePage
    {
        private readonly string mobileFolder = Path.Combine(Paths.Base, "Mobile");
        private readonly string githubZipUrl = "https://github.com/KloBraticc/This-is-for-Voidstrap-Mobile-Support-its-the-installer/archive/refs/heads/main.zip";
        private readonly string extractedFolder;
        private readonly string remoteDesktopUrl = "https://remotedesktop.google.com/access";

        public MobilePage()
        {
            InitializeComponent();
            extractedFolder = Path.Combine(mobileFolder, "InstallerRepo");
            Loaded += MobilePage_Loaded;
        }

        private async void MobilePage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            await StartInstallationAsync();
            OpenRemoteDesktopUrl();
        }

        private void OpenRemoteDesktopUrl()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c start \"\" \"{remoteDesktopUrl}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);

                Frontend.ShowMessageBox(
                    "After completing all the steps and setting up a remote device, open your tablet or phone and go to:\n" +
                    "https://remotedesktop.google.com/access\n\n" +
                    "Then follow the instructions to connect to your PC."
                );

                NavigationService.Navigate(new IntegrationsPage());
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to open Chrome Remote Desktop URL. Copy it manually: " + remoteDesktopUrl;
                Debug.WriteLine($"Error opening URL: {ex}");
            }
        }

        private async Task StartInstallationAsync()
        {
            InstallerProgressBar.Value = 0;

            if (IsChromeRemoteDesktopInstalled())
            {
                StatusText.Text = ":3 meow";
                InstallerProgressBar.Value = 100;
                InstallerCard.Visibility = System.Windows.Visibility.Collapsed;
                CompletionCard.Visibility = System.Windows.Visibility.Visible;
                return;
            }

            if (!Directory.Exists(mobileFolder))
                Directory.CreateDirectory(mobileFolder);

            StatusText.Text = "Downloading Mobile Support...";
            var zipPath = Path.Combine(mobileFolder, "MobileInstaller.zip");
            if (!File.Exists(zipPath))
                await DownloadFileAsync(githubZipUrl, zipPath);

            StatusText.Text = "Extracting Mobile Support...";
            if (Directory.Exists(extractedFolder))
                Directory.Delete(extractedFolder, true);

            ZipFile.ExtractToDirectory(zipPath, extractedFolder);

            var batPath = Path.Combine(extractedFolder, "Installer.bat");
            if (!File.Exists(batPath))
            {
                var files = Directory.GetFiles(extractedFolder, "Installer.bat", SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    StatusText.Text = "Installer.bat not found!";
                    return;
                }
                batPath = files[0];
            }

            StatusText.Text = "Running Mobile Support Installer...";
            await RunBatchAndWaitAsync(batPath);

            InstallerProgressBar.Value = 100;
            InstallerCard.Visibility = System.Windows.Visibility.Collapsed;
            CompletionCard.Visibility = System.Windows.Visibility.Visible;
        }

        private bool IsChromeRemoteDesktopInstalled()
        {
            string[] uninstallKeys = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var keyPath in uninstallKeys)
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key == null) continue;
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                        {
                            var displayName = subKey?.GetValue("DisplayName") as string;
                            if (!string.IsNullOrEmpty(displayName) &&
                                displayName.Contains("Chrome Remote Desktop Host", StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        private async Task DownloadFileAsync(string url, string destination)
        {
            using HttpClient client = new HttpClient();
            byte[] data = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(destination, data);
        }

        private async Task RunBatchAndWaitAsync(string path)
        {
            using Process process = new Process();
            process.StartInfo.FileName = path;
            process.StartInfo.WorkingDirectory = Path.GetDirectoryName(path);
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            process.Start();

            InstallerProgressBar.Value = 0;
            double progress = 0;

            while (!process.HasExited)
            {
                await Task.Delay(50);
                progress += 1;
                if (progress > 95) progress = 95;
                InstallerProgressBar.Value = progress;
            }

            while (InstallerProgressBar.Value < 100)
            {
                InstallerProgressBar.Value += 1;
                await Task.Delay(20);
            }
        }
    }
}