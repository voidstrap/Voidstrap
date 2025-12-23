using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using Voidstrap.UI.Elements.Dialogs;
using Voidstrap.UI.ViewModels.Settings;
using Wpf.Ui.Controls;
using Wpf.Ui.Hardware;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class ChannelPage
    {
        public ChannelPage()
        {
            InitializeComponent();
            _ = AutoUpdateRobloxVersionAsync();
            DataContext = new ChannelViewModel();
        }

        private void ToggleSwitch_Checked_1(object sender, RoutedEventArgs e)
        {
            HardwareAcceleration.DisableAllAnimations();
            HardwareAcceleration.FreeMemory();
            HardwareAcceleration.OptimizeVisualRendering();
            HardwareAcceleration.DisableTransparencyEffects();
            HardwareAcceleration.MinimizeMemoryFootprint();
        }

        private void ToggleSwitch_Unchecked_1(object sender, RoutedEventArgs e)
        {
            Frontend.ShowMessageBox(
                "Please restart the application to re-enable animations.",
                MessageBoxImage.Warning,
                MessageBoxButton.OK
            );
        }

        private async Task AutoUpdateRobloxVersionAsync()
        {
            while (true)
            {
                try
                {
                    await GetRobloxVersionAPPAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AutoUpdate] Error updating Roblox version: {ex.Message}");
                }
                await Task.Delay(1000);
            }
        }

        private async Task GetRobloxVersionAPPAsync()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string localStoragePath = Path.Combine(localAppData, "Roblox", "LocalStorage");

                if (!Directory.Exists(localStoragePath))
                {
                    RobloxVersionAPP.Header = "Not Installed";
                    return;
                }

                var files = Directory.GetFiles(localStoragePath, "memProfStorage*.json", SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
                {
                    RobloxVersionAPP.Header = "Not Installed";
                    return;
                }

                string? version = null;

                foreach (var file in files)
                {
                    try
                    {
                        string jsonContent = await File.ReadAllTextAsync(file);

                        var match = Regex.Match(jsonContent, "\"AppVersion\"\\s*:\\s*\"([^\"]+)\"");
                        if (match.Success)
                        {
                            version = match.Groups[1].Value;
                            break;
                        }
                    }
                    catch (IOException ioEx)
                    {
                        Debug.WriteLine($"[RobloxVersion] Error reading {file}: {ioEx.Message}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(version))
                {
                    RobloxVersionAPP.Header = $"Roblox {version}";
                }
                else
                {
                    RobloxVersionAPP.Header = "Not Installed";
                }
            }
            catch (Exception ex)
            {
                RobloxVersionAPP.Header = $"Roblox Version Error: {ex.Message}";
            }
        }

        private void ApplyNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string basePath = Paths.Base;

                string appSettingsSource = Path.Combine(basePath, "AppSettings.json");
                string modsSource = Path.Combine(basePath, "VoidstrapMods");

                var strapDirs = Directory.GetDirectories(localAppData)
                                         .Where(d => d.EndsWith("strap", StringComparison.OrdinalIgnoreCase))
                                         .ToList();
                foreach (var dir in strapDirs)
                {
                    string settingsTarget = Path.Combine(dir, "Settings.json");
                    string modsTarget = Path.Combine(dir, "Modifications");

                    BackupIfExists(settingsTarget);
                    BackupIfExists(modsTarget);
                    SafeCopy(appSettingsSource, settingsTarget);
                    SafeCopy(modsSource, modsTarget);
                    Console.WriteLine($"Updated: {dir}");
                }
                Frontend.ShowMessageBox("Voidstrap data synchronized!");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Error: {ex.Message}");
            }
        }

        private void SafeCopy(string sourcePath, string destPath)
        {
            if (File.Exists(sourcePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(sourcePath, destPath, overwrite: true);
            }
            else if (Directory.Exists(sourcePath))
            {
                CopyDirectory(sourcePath, destPath);
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        private void BackupIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Move(path, path + ".bak", overwrite: true);
            }
            else if (Directory.Exists(path))
            {
                string backup = path + "_bak";
                if (Directory.Exists(backup))
                    Directory.Delete(backup, true);
                Directory.Move(path, backup);
            }
        }

        private async void Check_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                string latestVersion = await GetLatestGitHubVersion();

                if (IsNewerVersion(latestVersion, currentVersion))
                {
                    Frontend.ShowMessageBox(
                        $"A new version ({latestVersion}) is available!"
                    );
                    string exeUrl = "https://github.com/voidstrap/Voidstrap/releases/latest/download/voidstrap.exe";
                    string tempPath = Path.Combine(Path.GetTempPath(), "voidstrap_update.exe");

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "Voidstrap-Updater");
                        var data = await client.GetByteArrayAsync(exeUrl);
                        await File.WriteAllBytesAsync(tempPath, data);
                    }
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tempPath,
                        UseShellExecute = true
                    });
                    Application.Current.Shutdown();
                }
                else
                {
                    Frontend.ShowMessageBox(
                        "You are already running the latest version of Voidstrap."
                    );
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    $"Error checking for updates:\n{ex.Message}"
                );
            }
        }

        private async Task<string> GetLatestGitHubVersion()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Voidstrap-Updater");

            string apiUrl = "https://api.github.com/repos/voidstrap/Voidstrap/releases/latest";
            string json = await client.GetStringAsync(apiUrl);

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("tag_name").GetString();
        }

        private bool IsNewerVersion(string latest, string current)
        {
            if (Version.TryParse(latest.TrimStart('v'), out var latestV) &&
                Version.TryParse(current, out var currentV))
            {
                return latestV > currentV;
            }
            return false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string basePath = Paths.Base;
                string appSettingsPath = Path.Combine(basePath, "AppSettings.json");
                if (File.Exists(appSettingsPath))
                {
                    File.Delete(appSettingsPath);
                }
                Process.Start(Application.ResourceAssembly.Location);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
               Frontend.ShowMessageBox($"An error occurred: {ex.Message}");
            }
        }

        private void OpenChannelListDialog_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ChannelListsDialog();
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
        }
    }
}