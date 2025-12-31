using RobloxLightingOverlay;
using RobloxLightingOverlay.Effects;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Voidstrap.Integrations;
using Voidstrap.UI.Elements.Crosshair;
using Voidstrap.UI.Elements.FPS;
using Voidstrap.UI.Elements.Settings.Pages;
using Voidstrap.UI.ViewModels;
using Voidstrap.UI.ViewModels.Settings;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Appearance;
using RobloxLightingOverlay;

namespace Voidstrap.UI.Elements.ContextMenu
{
    /// <summary>
    /// Interaction logic for NotifyIconMenu.xaml
    /// </summary>
    public partial class MenuContainer
    {
        // i wouldve gladly done this as mvvm but turns out that data binding just does not work with menuitems for some reason so idk this sucks

        private readonly Watcher _watcher;

        private DispatcherTimer closestServerTimer;
        private Server? lastClosestServer;

        private ActivityWatcher? _activityWatcher => _watcher.ActivityWatcher;

        private ServerInformation? _serverInformationWindow;

        private ServerHistory? _gameHistoryWindow;

        private MusicPlayer? _musicplayerWindow;

        private GamePassConsole? _GamepassWindow;

        private BetterBloxDataCenterConsole? _betterbloxWindow;

        private OutputConsole? _OutputConsole;

        private ChatLogs? _ChatLogs;

        private TimeSpan playTime = TimeSpan.Zero;
        private DispatcherTimer playTimer;
        private Watcher watcher;

        private static string TrimWithThreeDots(string text, int maxChars = 18)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
                return text;

            const string dots = "...";
            int take = maxChars - dots.Length;

            if (take <= 0)
                return dots;

            return text.Substring(0, take) + dots;
        }

        private void LoadFlags()
        {
            try
            {
                string modsPath = Path.Combine(Paths.Mods, "ClientSettings");
                string settingsFile = Path.Combine(modsPath, "ClientAppSettings.json");

                if (!File.Exists(settingsFile))
                {
                    Dispatcher.Invoke(() => FlagsTextBlock.Text = "Flags: 0");
                    return;
                }

                string json = File.ReadAllText(settingsFile);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options);

                int totalFlags = dict?.Count ?? 0;

                Dispatcher.Invoke(() => FlagsTextBlock.Text = $"Flags: {totalFlags}");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => FlagsTextBlock.Text = "Flags: Error");
                Debug.WriteLine($"Error loading flags: {ex.Message}");
            }
        }

        public MenuContainer(Watcher watcher)
        {
            InitializeComponent();
            StartPlayTimeTimer();
            _watcher = watcher;

            closestServerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            closestServerTimer.Tick += async (_, _) =>
            {
                if (_activityWatcher?.Data != null)
                    await UpdateClosestServerMenuItemText();
            };
            closestServerTimer.Start();


            if (_activityWatcher is not null)
            {
                _activityWatcher.OnLogOpen += ActivityWatcher_OnLogOpen;
                _activityWatcher.OnGameJoin += ActivityWatcher_OnGameJoin;
                _activityWatcher.OnGameLeave += ActivityWatcher_OnGameLeave;

                if (!App.Settings.Prop.UseDisableAppPatch)
                    GameHistoryMenuItem.Visibility = Visibility.Visible;
                if (!App.Settings.Prop.UseDisableAppPatch)
                    MusicMenuItem.Visibility = Visibility.Visible;
            }

            if (_watcher.RichPresence is not null)
                RichPresenceMenuItem.Visibility = Visibility.Visible;

            VersionTextBlock.Text = $"{App.ProjectName} v{App.Version}";
        }

        public void UpdateCurrentGameInfo(string gameName, string gameIconUrl)
        {
            if (string.IsNullOrEmpty(gameName))
            {
                CurrentGameMenuItem.Visibility = Visibility.Collapsed;
                CurrentGameIcon.Source = null;
                CurrentGameNameTextBlock.Text = "";
                return;
            }

            CurrentGameMenuItem.Visibility = Visibility.Visible;

            if (!string.IsNullOrEmpty(gameIconUrl))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(gameIconUrl);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                CurrentGameIcon.Source = bitmap;
            }
            else
            {
                CurrentGameIcon.Source = null;
            }
            CurrentGameNameTextBlock.Text = TrimWithThreeDots(gameName);
        }
        private async Task UpdateCurrentGameIconAsync(ActivityData data)
        {
            string universeName = data.UniverseDetails?.Data.Name ?? $"Place {data.PlaceId}";
            string? iconUrl = data.UniverseDetails?.Thumbnail.ImageUrl;

            if (iconUrl == null && data.UniverseDetails == null)
            {
                try
                {
                    await UniverseDetails.FetchSingle(data.UniverseId);
                    data.UniverseDetails = UniverseDetails.LoadFromCache(data.UniverseId);
                    iconUrl = data.UniverseDetails?.Thumbnail.ImageUrl;
                    universeName = data.UniverseDetails?.Data.Name ?? universeName;
                }
                catch {}
            }
            Dispatcher.Invoke(() => UpdateCurrentGameInfo(universeName, iconUrl));
        }

        public void ShowServerInformationWindow()
        {
            if (_serverInformationWindow is null)
            {
                _serverInformationWindow = new(_watcher);
                _serverInformationWindow.Closed += (_, _) => _serverInformationWindow = null;
            }

            if (!_serverInformationWindow.IsVisible)
                _serverInformationWindow.ShowDialog();
            else
                _serverInformationWindow.Activate();
        }

        private async Task UpdateClosestServerMenuItemText()
        {
            if (_activityWatcher?.Data == null)
            {
                JoinClosestServerMenuItem.Visibility = Visibility.Collapsed;
                return;
            }

            JoinClosestServerMenuItem.Visibility = Visibility.Visible;
            string placeId = _activityWatcher.Data.PlaceId.ToString();

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?limit=100";

                HttpResponseMessage response;
                try
                {
                    response = await http.GetAsync(url);
                }
                catch (HttpRequestException)
                {
                    if (lastClosestServer != null)
                    {
                        JoinClosestServerTextBlock.Text = $"Join Closest ({lastClosestServer.Ping}ms)";
                    }
                    else
                    {
                        JoinClosestServerTextBlock.Text = "Error Fetching Servers";
                    }
                    return;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (lastClosestServer != null)
                    {
                        JoinClosestServerTextBlock.Text = $"Join Closest ({lastClosestServer.Ping}ms)";
                    }
                    else
                    {
                        JoinClosestServerTextBlock.Text = "Rate Limited";
                    }
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var serverResponse = JsonSerializer.Deserialize<ServerResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var lowest = serverResponse?.Data?.OrderBy(s => s.Ping).FirstOrDefault();

                if (lowest != null)
                {
                    lastClosestServer = lowest;
                    JoinClosestServerTextBlock.Text = $"Join Closest ({lowest.Ping}ms)";
                }
                else
                {
                    JoinClosestServerTextBlock.Text = "No Servers Detected";
                }
            }
            catch
            {
                if (lastClosestServer != null)
                {
                    JoinClosestServerTextBlock.Text = $"Join Closest ({lastClosestServer.Ping}ms)";
                }
                else
                {
                    JoinClosestServerTextBlock.Text = "Error Fetching Servers";
                }
            }
        }

        private async void JoinClosestServerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_activityWatcher?.Data == null)
            {
                Frontend.ShowMessageBox("No active game detected.");
                return;
            }

            string placeId = _activityWatcher.Data.PlaceId.ToString();

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?limit=100";
                var response = await http.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (lastClosestServer != null)
                    {
                        JoinServer(lastClosestServer.Id, placeId);
                    }
                    else
                    {
                        Frontend.ShowMessageBox("Rate limited and no cached server available.");
                    }
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var serverResponse = JsonSerializer.Deserialize<ServerResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var lowest = serverResponse?.Data?.OrderBy(s => s.Ping).FirstOrDefault();

                if (lowest != null)
                {
                    lastClosestServer = lowest;
                    JoinServer(lowest.Id, placeId);
                }
                else if (lastClosestServer != null)
                {
                    JoinServer(lastClosestServer.Id, placeId);
                }
                else
                {
                    Frontend.ShowMessageBox("No servers available.");
                }
            }
            catch (Exception ex)
            {
                if (lastClosestServer != null)
                {
                    JoinServer(lastClosestServer.Id, placeId);
                }
                else
                {
                    Frontend.ShowMessageBox($"Failed to join server:\n{ex.Message}");
                }
            }
        }

        private void JoinServer(string serverId, string placeId)
        {
            try
            {
                _watcher?.KillRobloxProcess();
                Process.Start(new ProcessStartInfo(
                    $"roblox://experiences/start?placeId={placeId}&serverId={serverId}")
                {
                    UseShellExecute = true
                });
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to join server:\n{ex.Message}");
            }
        }

        public void ActivityWatcher_OnLogOpen(object? sender, EventArgs e) => 
            Dispatcher.Invoke(() => LogTracerMenuItem.Visibility = Visibility.Visible);

        public void ActivityWatcher_OnGameJoin(object? sender, EventArgs e)
        {
            if (_activityWatcher is null)
                return;

            if (App.Settings.Prop.MotionBlurOverlay)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (RobloxLightingOverlay.OverlayManager.UI == null)
                    {
                        RobloxLightingOverlay.OverlayManager.Start();
                    }
                    else
                    {
                        RobloxLightingOverlay.OverlayManager.UI.Show();
                        RobloxLightingOverlay.OverlayManager.UI.Activate();
                    }
                });
            }

            if (App.Settings.Prop.MotionBlurOverlay2)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!MotionBlurManager.IsEnabled)
                        MotionBlurManager.Start();
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (MotionBlurManager.IsEnabled)
                        MotionBlurManager.Stop();
                });
            }

            if (App.Settings.Prop.Crosshair)
            {
                var crosshairVM = new ModsViewModel();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var crosshairWindow = new CrosshairWindow(crosshairVM);
                    App.Current.Resources["CrosshairWindow"] = crosshairWindow;
                });
            }

            if (App.Settings.Prop.FPSCounter || App.Settings.Prop.CPUTempCounter || App.Settings.Prop.CurrentTimeDisplay || App.Settings.Prop.ServerPingCounter || App.Settings.Prop.ShowServerDetailsUI)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (App.Current.Resources["OverlayWindow"] is Voidstrap.UI.Elements.Overlay.OverlayWindow existing)
                    {
                        if (existing.IsLoaded)
                        {
                            return;
                        }
                        else
                        {
                            App.Current.Resources.Remove("OverlayWindow");
                        }
                    }

                    var overlay = new Voidstrap.UI.Elements.Overlay.OverlayWindow();
                    overlay.Show();
                    App.Current.Resources["OverlayWindow"] = overlay;
                });
            }

            Dispatcher.InvokeAsync(async () =>
            {
                if (_activityWatcher.Data.ServerType == ServerType.Public)
                    InviteDeeplinkMenuItem.Visibility = Visibility.Visible;

                ServerDetailsMenuItem.Visibility = Visibility.Visible;
                GamePassDetailsMenuItem.Visibility = Visibility.Visible;
                JoinClosestServerMenuItem.Visibility = Visibility.Visible;
                await UpdateClosestServerMenuItemText();

                if (App.FastFlags.GetPreset("Players.LogLevel") == "trace")
                {
                    OutputConsoleMenuItem.Visibility = Visibility.Visible;
                    ChatLogsMenuItem.Visibility = Visibility.Visible;
                }
            });

            _ = UpdateCurrentGameIconAsync(_activityWatcher.Data);
        }

        public void ActivityWatcher_OnGameLeave(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (App.Current.Resources["CrosshairWindow"] is CrosshairWindow crosshair)
                {
                    crosshair.Close();
                    App.Current.Resources.Remove("CrosshairWindow");
                }

                if (App.Current.Resources["OverlayWindow"] is Voidstrap.UI.Elements.Overlay.OverlayWindow overlay)
                {
                    overlay.Close();
                    App.Current.Resources.Remove("OverlayWindow");
                }

                if (RobloxLightingOverlay.OverlayManager.UI != null)
                {
                    RobloxLightingOverlay.OverlayManager.UI.Close();
                    RobloxLightingOverlay.OverlayManager.UI = null;
                }

                if (RobloxLightingOverlay.OverlayManager.Overlay != null)
                {
                    RobloxLightingOverlay.OverlayManager.Overlay.Close();
                    RobloxLightingOverlay.OverlayManager.Overlay = null;
                }

                InviteDeeplinkMenuItem.Visibility = Visibility.Collapsed;
                ServerDetailsMenuItem.Visibility = Visibility.Collapsed;
                GamePassDetailsMenuItem.Visibility = Visibility.Collapsed;
                JoinClosestServerMenuItem.Visibility = Visibility.Collapsed;

                if (App.FastFlags.GetPreset("Players.LogLevel") == "trace")
                {
                    OutputConsoleMenuItem.Visibility = Visibility.Collapsed;
                    ChatLogsMenuItem.Visibility = Visibility.Collapsed;

                    _ChatLogs?.Close();
                    _OutputConsole?.Close();
                }

                _serverInformationWindow?.Close();
                UpdateCurrentGameInfo(null, null);
            });
        }

        private void StartPlayTimeTimer()
        {
            playTimer = new DispatcherTimer();
            playTimer.Interval = TimeSpan.FromSeconds(1);
            playTimer.Tick += PlayTimer_Tick;
            playTimer.Start();
        }

        private void PlayTimer_Tick(object sender, EventArgs e)
        {
            playTime = playTime.Add(TimeSpan.FromSeconds(1));
            UpdatePlayTime(playTime);
        }

        private void UpdatePlayTime(TimeSpan playTime)
        {
            PlayTimeTextBlock.Text = $"PlayTime: {playTime:hh\\:mm\\:ss}";
        }

        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            // this is an awful hack lmao im so sorry to anyone who reads this
            // this is done to register the context menu wrapper as a tool window so it doesnt appear in the alt+tab switcher
            // https://stackoverflow.com/a/551847/11852173

            HWND hWnd = (HWND)new WindowInteropHelper(this).Handle;

            int exStyle = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            exStyle |= 0x00000080; //NativeMethods.WS_EX_TOOLWINDOW;
            PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle);
            LoadFlags();
        }

        private void Window_Closed(object sender, EventArgs e) => App.Logger.WriteLine("MenuContainer::Window_Closed", "Context menu container closed");

        private void RichPresenceMenuItem_Click(object sender, RoutedEventArgs e) => _watcher.RichPresence?.SetVisibility(((MenuItem)sender).IsChecked);

        private void InviteDeeplinkMenuItem_Click(object sender, RoutedEventArgs e) => Clipboard.SetDataObject(_activityWatcher?.Data.GetInviteDeeplink());

        private void ServerDetailsMenuItem_Click(object sender, RoutedEventArgs e) => ShowServerInformationWindow();

        private void LogTracerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string? location = _activityWatcher?.LogLocation;

            if (location is not null)
                Utilities.ShellExecute(location);
        }

        private void CloseRobloxMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = Frontend.ShowMessageBox(
                Strings.ContextMenu_CloseRobloxMessage,
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            _watcher.KillRobloxProcess();
        }

        private void JoinLastServerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_activityWatcher is null)
                throw new ArgumentNullException(nameof(_activityWatcher));

            if (_gameHistoryWindow is null)
            {
                _gameHistoryWindow = new(_activityWatcher);
                _gameHistoryWindow.Closed += (_, _) => _gameHistoryWindow = null;
            }

            if (!_gameHistoryWindow.IsVisible)
                _gameHistoryWindow.ShowDialog();
            else
                _gameHistoryWindow.Activate();
        }
        private void GamePassDetailsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_activityWatcher is null)
                throw new ArgumentNullException(nameof(_activityWatcher));
            long userId = _activityWatcher.Data.UserId;

            if (_GamepassWindow is null)
            {
                _GamepassWindow = new GamePassConsole(userId);
                _GamepassWindow.Closed += (_, _) => _GamepassWindow = null;
            }

            if (!_GamepassWindow.IsVisible)
                _GamepassWindow.ShowDialog();
            else
                _GamepassWindow.Activate();
        }

        private void BetterBloxDataCentersMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_activityWatcher is null)
                throw new ArgumentNullException(nameof(_activityWatcher));

            if (_betterbloxWindow is null)
            {
                _betterbloxWindow = new BetterBloxDataCenterConsole();
                _betterbloxWindow.Closed += (_, _) => _betterbloxWindow = null;
            }

            if (!_betterbloxWindow.IsVisible)
                _betterbloxWindow.ShowDialog();
            else
                _betterbloxWindow.Activate();
        }

        private void MusicPlayerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_activityWatcher is null)
                throw new ArgumentNullException(nameof(_activityWatcher));

            if (_musicplayerWindow is null)
            {
                _musicplayerWindow = new MusicPlayer();
                _musicplayerWindow.Closed += (_, _) => _musicplayerWindow = null;
            }

            if (!_musicplayerWindow.IsVisible)
                _musicplayerWindow.ShowDialog();
            else
                _musicplayerWindow.Activate();
        }

        private void OutputConsoleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_activityWatcher is null)
                throw new ArgumentNullException(nameof(_activityWatcher));

            if (_OutputConsole is null)
            {
                _OutputConsole = new(_activityWatcher);
                _OutputConsole.Closed += (_, _) => _OutputConsole = null;
            }

            if (!_OutputConsole.IsVisible)
                _OutputConsole.ShowDialog();
            else
                _OutputConsole.Activate();
        }

        private void ChatLogsMenuItemMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_activityWatcher is null)
                throw new ArgumentNullException(nameof(_activityWatcher));

            if (_ChatLogs is null)
            {
                _ChatLogs = new(_activityWatcher);
                _ChatLogs.Closed += (_, _) => _ChatLogs = null;
            }

            if (!_ChatLogs.IsVisible)
                _ChatLogs.ShowDialog();
            else
                _ChatLogs.Activate();
        }
    }
}
