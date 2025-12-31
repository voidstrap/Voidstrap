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

            try
            {
                Directory.CreateDirectory(NvidiaInspectorDir);
                string zipPath = Path.Combine(NvidiaInspectorDir, "nvidiaProfileInspector.zip");

                using var response = await App.HttpClient.GetAsync(NVIDIA_INSPECTOR_URL);
                response.EnsureSuccessStatusCode();

                await using var fs = File.Create(zipPath);
                await response.Content.CopyToAsync(fs);

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

            using var response = await App.HttpClient.GetAsync(GITHUB_RAW_BASE + profileName);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var fs = File.Create(localPath);
            await response.Content.CopyToAsync(fs);

            return localPath;
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
                "NVIDIA Profile Inspector has been opened so you can fix this manually. " +
                "In the top-right search box, type Roblox VR. Select the profile named Roblox VR. " +
                "Click the red Delete Profile (❌) button in the top toolbar. " +
                "Then click Apply Changes in the top-right corner. " +
                "After that, completely close NVIDIA Profile Inspector. ",
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
            {
                return true;
            }

            await ManualDeleteRobloxVR();

            if (await DragDropImportWithDetection(profilePath))
            {
                return true;
            }

            Frontend.ShowMessageBox(
                $"NVIDIA Profile Inspector failed to load the {displayName} profile.\n\n" +
                "Possible reasons:\n" +
                "• UAC was denied\n" +
                "• NVIDIA driver locked the profile\n" +
                "• Inspector was blocked by antivirus",
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
