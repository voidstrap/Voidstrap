using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public class ExtensionViewModel
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly string fleasionDir = Path.Combine(Paths.Base, "Fleasion");
        private static readonly SemaphoreSlim _downloadLock = new SemaphoreSlim(1, 1);
        private static bool _isDownloading = false;
        private CancellationTokenSource _downloadCts;
        public event Action<string, bool> OnProgressChanged;

        public ExtensionViewModel()
        {
            if (App.Settings.Prop.Fleasion)
            {
                string exePath = Path.Combine(fleasionDir, "Fleasion.exe");
                if (!File.Exists(exePath))
                    _ = Task.Run(() => DownloadFleasion());
            }
            else
            {
                _ = Task.Run(() => UninstallFleasion());
            }
        }

        public bool aniwatchenabler
        {
            get => App.Settings.Prop.AniWatch;
            set => App.Settings.Prop.AniWatch = value;
        }

        public bool fleasionenabler
        {
            get => App.Settings.Prop.Fleasion;
            set
            {
                if (App.Settings.Prop.Fleasion == value) return;

                App.Settings.Prop.Fleasion = value;

                if (value)
                {
                    _ = Task.Run(() => DownloadFleasion());
                }
                else
                {
                    _ = Task.Run(() => UninstallFleasion());
                }
            }
        }

        public void CancelDownload()
        {
            _downloadCts?.Cancel();
        }

        private void ReplaceFileSafely(string sourcePath, string targetPath)
        {
            const int retries = 10;
            const int delayMs = 500;

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    if (File.Exists(targetPath))
                        File.Delete(targetPath);

                    File.Move(sourcePath, targetPath);
                    return;
                }
                catch (IOException) { Thread.Sleep(delayMs); }
                catch (UnauthorizedAccessException) { Thread.Sleep(delayMs); }
            }

            if (File.Exists(sourcePath))
                File.Delete(sourcePath);

            Frontend.ShowMessageBox(
                "Failed to replace Fleasion.exe because it is still in use. " +
                "Please make sure no Fleasion processes are running and try again.\nOpening folder for manual replacement.");

            Process.Start("explorer.exe", Path.GetFullPath(fleasionDir));
            throw new IOException("Could not replace Fleasion.exe, file still in use.");
        }

        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch
            {
                return true;
            }
        }

        private async Task DownloadFleasion()
        {
            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();
            CancellationToken ct = _downloadCts.Token;

            OnProgressChanged?.Invoke("Preparing", true);

            await _downloadLock.WaitAsync();
            if (_isDownloading)
            {
                _downloadLock.Release();
                OnProgressChanged?.Invoke("", false);
                return;
            }

            _isDownloading = true;
            string outputPath = Path.Combine(fleasionDir, "Fleasion.exe");

            try
            {
                Directory.CreateDirectory(fleasionDir);
                foreach (Process proc in Process.GetProcessesByName("Fleasion"))
                {
                    try { proc.Kill(); proc.WaitForExit(5000); } catch { }
                }

                int waitRetries = 10;
                while (File.Exists(outputPath) && IsFileLocked(outputPath) && waitRetries-- > 0)
                {
                    Thread.Sleep(500);
                }

                if (File.Exists(outputPath) && IsFileLocked(outputPath))
                {
                    Frontend.ShowMessageBox(
                        "Fleasion.exe is still running. Close it and try again.");
                    return;
                }

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Voidstrap");

                using HttpResponseMessage response = await client.GetAsync(
                    "https://github.com/qrhrqiohj/Fleasion/releases/latest/download/Fleasion.exe",
                    HttpCompletionOption.ResponseHeadersRead, ct);

                response.EnsureSuccessStatusCode();

                using Stream stream = await response.Content.ReadAsStreamAsync(ct);
                using FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

                long totalBytes = response.Content.Headers.ContentLength ?? -1L;
                long totalRead = 0L;
                byte[] buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, bytesRead, ct);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        double percent = (double)totalRead / totalBytes * 100;
                        OnProgressChanged?.Invoke($"Fleasion... {percent:0}%", true);
                    }
                }

                OnProgressChanged?.Invoke("Complete!", true);
            }
            catch (OperationCanceledException)
            {
                OnProgressChanged?.Invoke("Canceled", false);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox("Failed to download Fleasion:\n" + ex.Message);
            }
            finally
            {
                _isDownloading = false;
                _downloadLock.Release();
                OnProgressChanged?.Invoke("", false);
            }
        }

        private void UninstallFleasion()
        {
            OnProgressChanged?.Invoke("Uninstalling Fleasion...", true);

            try
            {
                if (!Directory.Exists(fleasionDir))
                {
                    OnProgressChanged?.Invoke("", false);
                    return;
                }

                foreach (Process proc in Process.GetProcessesByName("Fleasion"))
                {
                    try { proc.Kill(); proc.WaitForExit(5000); } catch { }
                }

                bool deleted = false;
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        Directory.Delete(fleasionDir, true);
                        deleted = true;
                        break;
                    }
                    catch { Thread.Sleep(500); }
                }

                if (!deleted && Directory.Exists(fleasionDir))
                {
                    Frontend.ShowMessageBox(
                        "Failed to delete Fleasion folder automatically. Opening folder for manual deletion.");
                    Process.Start("explorer.exe", Path.GetFullPath(fleasionDir));
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox("Failed to uninstall Fleasion: " + ex.Message);
            }
            finally
            {
                OnProgressChanged?.Invoke("", false);
            }
        }
    }
}