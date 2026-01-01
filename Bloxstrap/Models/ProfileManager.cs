using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Voidstrap;

namespace Voidstrap.Integrations
{
    public static class NvidiaProfileManager
    {
        private const string NVIDIA_INSPECTOR_URL =
            "https://github.com/Orbmu2k/nvidiaProfileInspector/releases/download/2.4.0.3/nvidiaProfileInspector.zip";

        private const string GITHUB_RAW_BASE =
            "https://raw.githubusercontent.com/KloBraticc/VoidstrapResources/main/NvidiaProfiles/";

        private static readonly string NvidiaInspectorDir =
            Path.Combine(Paths.Integrations, "NvidiaProfileInspector");

        private static readonly string NvidiaInspectorExe =
            Path.Combine(NvidiaInspectorDir, "nvidiaProfileInspector.exe");

        private static readonly string NipProfilesDir =
            Path.Combine(Paths.Base, "NipProfiles");

        private static async Task<bool> EnsureNvidiaInspectorDownloaded()
        {
            if (File.Exists(NvidiaInspectorExe))
                return true;

            string zipPath = Path.Combine(NvidiaInspectorDir, "nvidiaProfileInspector.zip");

            try
            {
                Directory.CreateDirectory(NvidiaInspectorDir);

                foreach (var p in Process.GetProcessesByName("nvidiaProfileInspector"))
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                    catch { }
                }

                if (File.Exists(zipPath))
                {
                    try { File.Delete(zipPath); } catch { }
                }

                using (var response = await App.HttpClient.GetAsync(
                    NVIDIA_INSPECTOR_URL,
                    HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using (var fs = new FileStream(
                        zipPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                        await fs.FlushAsync();
                    }
                }

                await Task.Delay(100);
                ZipFile.ExtractToDirectory(zipPath, NvidiaInspectorDir, true);

                File.Delete(zipPath);

                return true;
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    "Failed to download NVIDIA Profile Inspector:\n\n" + ex.Message,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }
        private static async Task<string?> EnsureProfileDownloaded(string profileName)
        {
            Directory.CreateDirectory(NipProfilesDir);
            string localPath = Path.Combine(NipProfilesDir, profileName);

            if (File.Exists(localPath))
                return localPath;

            try
            {
                using var response = await App.HttpClient.GetAsync(GITHUB_RAW_BASE + profileName);
                if (!response.IsSuccessStatusCode)
                    return null;

                using var fs = new FileStream(
                    localPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None);

                await response.Content.CopyToAsync(fs);
                await fs.FlushAsync();

                return localPath;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<bool> DragDropImportWithDetection(string profilePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = NvidiaInspectorExe,
                    Arguments = $"\"{profilePath}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return false;

                for (int i = 0; i < 50; i++)
                {
                    await Task.Delay(100);

                    if (process.HasExited)
                        return false;

                    process.Refresh();
                    if (process.MainWindowHandle != IntPtr.Zero)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static async Task ManualDeleteRobloxVR()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = NvidiaInspectorExe,
                UseShellExecute = true,
                Verb = "runas"
            });

            Frontend.ShowMessageBox(
                "NVIDIA Profile Inspector opened.\r\n\r\n• Search for: Roblox VR\r\n• Select the profile\r\n• Click the ❌ Delete Profile button\r\n• Click Apply Changes\r\n• Close NVIDIA Profile Inspector completely",
                System.Windows.MessageBoxImage.Warning);

            await Task.Delay(1000);
        }

        private static async Task<bool> ApplyProfile(string nipName, string displayName)
        {
            if (!await EnsureNvidiaInspectorDownloaded())
                return false;

            string? profilePath = await EnsureProfileDownloaded(nipName);
            if (profilePath == null)
            {
                Frontend.ShowMessageBox(
                    $"Failed to download {displayName} profile.",
                    System.Windows.MessageBoxImage.Error);
                return false;
            }

            if (await DragDropImportWithDetection(profilePath))
                return true;

            await ManualDeleteRobloxVR();

            if (await DragDropImportWithDetection(profilePath))
                return true;

            Frontend.ShowMessageBox(
                $"Failed to apply {displayName} profile.\n\n" +
                "Possible causes:\n" +
                "• UAC denied\n" +
                "• NVIDIA driver lock\n" +
                "• Antivirus interference",
                System.Windows.MessageBoxImage.Error);

            return false;
        }

        public static Task<bool> ApplyBlurSettings()
            => ApplyProfile("VoidsTrap_Blur.nip", "Blur");

        public static Task<bool> ApplyWithoutSettings()
            => ApplyProfile("VoidsTrap_Without.nip", "Without");

        public static Task<bool> ApplyDefaultSettings()
            => ApplyProfile("VoidsTrap_Default.nip", "Default");
    }
}
