using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Voidstrap.UI.ViewModels.Settings;
using Wpf.Ui.Controls;
using System.Runtime.InteropServices;
using SD = System.Drawing;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class ShortcutsPage : UiPage
    {
        private readonly string instanceFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Voidstrap",
            "instance_id.txt"
        );

        private static readonly HttpClient _httpClient = new HttpClient();
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
        public ShortcutsPage()
        {
            InitializeComponent();
            DataContext = new ShortcutsViewModel();

            try
            {
                var dir = Path.GetDirectoryName(instanceFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(instanceFilePath))
                {
                    ((ShortcutsViewModel)DataContext).GameInstanceId = File.ReadAllText(instanceFilePath);
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to load saved settings.\n\nError: {ex.Message}");
            }
        }

        private async void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            var vm = (ShortcutsViewModel)DataContext;
            var launchGameId = vm.GameID?.Trim();
            var instanceId = vm.GameInstanceId?.Trim();
            var isPrivateServer = vm.IsPrivateServer;
            var privateServerInput = vm.PrivateServerCode?.Trim();

            if (string.IsNullOrEmpty(launchGameId) && string.IsNullOrEmpty(privateServerInput))
            {
                Frontend.ShowMessageBox("Please enter a Game ID or a Private Server Link.");
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(instanceId))
                {
                    var dir = Path.GetDirectoryName(instanceFilePath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    File.WriteAllText(instanceFilePath, instanceId);
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to save Game Instance ID.\n\nError: {ex.Message}");
            }

            if (isPrivateServer && !string.IsNullOrEmpty(privateServerInput))
            {
                try
                {
                    string folderPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Voidstrap"
                    );
                    Directory.CreateDirectory(folderPath);
                    string privateCodePath = Path.Combine(folderPath, "PrivateServerCode.txt");
                    File.WriteAllText(privateCodePath, privateServerInput);
                }
                catch (Exception ex)
                {
                    Frontend.ShowMessageBox($"Failed to save Private Server Code.\n\nError: {ex.Message}");
                }
            }

            string launchUrl;

            if (isPrivateServer)
            {
                if (string.IsNullOrEmpty(privateServerInput))
                {
                    Frontend.ShowMessageBox("Please enter your Private Server Share Link or Code.");
                    return;
                }

                string code = privateServerInput;
                string placeId = launchGameId;

                if (privateServerInput.StartsWith("https://www.roblox.com/share?", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var uri = new Uri(privateServerInput);
                        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                        var extractedCode = query["code"];
                        if (!string.IsNullOrEmpty(extractedCode))
                            code = extractedCode;

                        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
                        var resp = await client.GetAsync(privateServerInput);

                        if (resp.StatusCode == System.Net.HttpStatusCode.Found && resp.Headers.Location != null)
                        {
                            var match = Regex.Match(resp.Headers.Location.ToString(), @"/games/(\d+)/");
                            if (match.Success)
                                placeId = match.Groups[1].Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Frontend.ShowMessageBox($"Failed to parse Roblox share link.\n\nError: {ex.Message}");
                        return;
                    }
                }

                if (string.IsNullOrEmpty(placeId))
                {
                    Frontend.ShowMessageBox("Could not detect Game ID from the share link.");
                    return;
                }

                launchUrl = $"https://www.roblox.com/share?code={Uri.EscapeDataString(code)}&type=Server";
            }
            else
            {
                launchUrl = $"https://www.roblox.com/games/start?placeId={launchGameId}";
                if (!string.IsNullOrEmpty(instanceId))
                    launchUrl += $"&gameInstanceId={Uri.EscapeDataString(instanceId)}";
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = launchUrl,
                    UseShellExecute = true
                });

                await Task.Run(() =>
                {
                    Process? robloxProcess = null;
                    while (robloxProcess == null)
                    {
                        robloxProcess = Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault();
                        Thread.Sleep(500);
                    }

                    Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                });
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to launch the game.\n\nError: {ex.Message}");
            }
        }

        private async void BtnCreateShortcut_Click(object sender, RoutedEventArgs e)
        {
            var vm = (ShortcutsViewModel)DataContext;

            string displayName = string.IsNullOrWhiteSpace(vm.DisplayGameName)
                ? "Roblox Game"
                : vm.DisplayGameName;

            foreach (char c in Path.GetInvalidFileNameChars())
                displayName = displayName.Replace(c, '_');

            if (displayName.Length > 80)
                displayName = displayName[..80];

            string launchUrl;
            if (vm.IsPrivateServer && !string.IsNullOrWhiteSpace(vm.PrivateServerCode))
                launchUrl = $"https://www.roblox.com/share?code={Uri.EscapeDataString(vm.PrivateServerCode)}&type=Server";
            else
                launchUrl = $"https://www.roblox.com/games/start?placeId={vm.GameID}";

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutPath = Path.Combine(desktop, $"{displayName}.url");

            try
            {
                string? iconUrl = await FetchGameIconUrlAsync(vm.GameID);
                string iconPath = await DownloadAndForceIcoAsync(iconUrl, displayName);

                if (File.Exists(shortcutPath))
                    File.Delete(shortcutPath);

                string contents =
                    "[InternetShortcut]\n" +
                    $"URL={launchUrl}\n" +
                    $"IconFile={iconPath}\n" +
                    "IconIndex=0\n";

                await File.WriteAllTextAsync(shortcutPath, contents, Encoding.UTF8);

                Frontend.ShowMessageBox($"Shortcut created with game icon:\n{displayName}");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to create shortcut.\n\nError: {ex.Message}");
            }
        }

        private async Task<string?> FetchGameIconUrlAsync(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return null;

            string url = $"https://thumbnails.roblox.com/v1/places/gameicons?placeIds={gameId}&returnPolicy=PlaceHolder&size=150x150&format=Png&isCircular=false";

            for (int i = 0; i < 5; i++)
            {
                string json = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");

                if (data.GetArrayLength() > 0)
                {
                    var entry = data[0];
                    string? state = entry.TryGetProperty("state", out var st) ? st.GetString() : null;
                    if (state == "Completed" && entry.TryGetProperty("imageUrl", out var img))
                        return img.GetString();
                }

                await Task.Delay(500);
            }

            return null;
        }

        private async Task<string> DownloadAndForceIcoAsync(string? imageUrl, string baseName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageUrl))
                    return string.Empty;

                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Voidstrap",
                    "Icons"
                );
                Directory.CreateDirectory(folder);

                string iconPath = Path.Combine(folder, $"{baseName}.ico");

                var data = await _httpClient.GetByteArrayAsync(imageUrl);
                using var ms = new MemoryStream(data);
                using var bitmap = new SD.Bitmap(ms);
                using var resized = new SD.Bitmap(bitmap, new SD.Size(64, 64));

                IntPtr hIcon = resized.GetHicon();
                using (var icon = SD.Icon.FromHandle(hIcon))
                {
                    using var fs = new FileStream(iconPath, FileMode.Create, FileAccess.Write);
                    icon.Save(fs);
                }

                DestroyIcon(hIcon);
                return iconPath;
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to set game icon.\n\n{ex.Message}");
                return string.Empty;
            }
        }
    }
}
