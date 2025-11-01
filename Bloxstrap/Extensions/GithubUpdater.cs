using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Voidstrap;

public static class GithubUpdater
{
    private static readonly HttpClient http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "Voidstrap-Updater" } }
    };

    public static async Task<string?> GetLatestVersionTagAsync()
    {
        try
        {
            string url = "https://api.github.com/repos/voidstrap/Voidstrap/releases/latest";
            string response = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            return doc.RootElement.GetProperty("tag_name").GetString();
        }
        catch (Exception ex)
        {
            App.Logger.WriteLine("GitHubUpdater", $"Failed to get latest release tag: {ex}");
            return null;
        }
    }

    public static async Task<bool> DownloadAndInstallUpdate(string tag)
    {
        try
        {
            string url = "https://api.github.com/repos/voidstrap/Voidstrap/releases/latest";
            string response = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            var assets = doc.RootElement.GetProperty("assets");

            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                string downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";

                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return await UpdateExe(downloadUrl, name);

                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    return await UpdateZip(downloadUrl, name);
            }

            App.Logger.WriteLine("GitHubUpdater", "No valid .exe or .zip asset found.");
            return false;
        }
        catch (Exception ex)
        {
            App.Logger.WriteLine("GitHubUpdater", $"Update failed: {ex}");
            return false;
        }
    }

    private static async Task<bool> UpdateExe(string url, string name)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Voidstrap_Update");
        Directory.CreateDirectory(tempDir);

        string exePath = Path.Combine(tempDir, name);
        var bytes = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(exePath, bytes);

        string currentExe = Environment.ProcessPath!;
        string backupExe = currentExe + ".old";
        if (File.Exists(backupExe)) File.Delete(backupExe);
        File.Move(currentExe, backupExe);
        File.Copy(exePath, currentExe, true);

        RestartAfterUpdate(currentExe);
        return true;
    }

    private static async Task<bool> UpdateZip(string url, string name)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Voidstrap_Update");
        Directory.CreateDirectory(tempDir);

        string zipPath = Path.Combine(tempDir, name);
        var bytes = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(zipPath, bytes);

        string extractPath = Path.Combine(tempDir, "Extracted");
        if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
        ZipFile.ExtractToDirectory(zipPath, extractPath, true);

        string currentDir = AppContext.BaseDirectory;
        foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(extractPath, file);
            string dest = Path.Combine(currentDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
        }

        string mainExe = Path.Combine(currentDir, "Voidstrap.exe");
        RestartAfterUpdate(mainExe);
        return true;
    }

    private static void RestartAfterUpdate(string exePath)
    {
        Task.Delay(800).ContinueWith(_ =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });
            Environment.Exit(0);
        });
    }
}
