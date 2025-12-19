using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Voidstrap.Integrations;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Voidstrap.UI.Elements.ContextMenu
{
    /// <summary>
    /// Interaction logic for NotifyIconMenu.xaml
    /// </summary>
    public partial class MenuContainer
    {
        // i wouldve gladly done this as mvvm but turns out that data binding just does not work with menuitems for some reason so idk this sucks

        private readonly Watcher _watcher;

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


        public MenuContainer(Watcher watcher)
        {
            InitializeComponent();
            StartPlayTimeTimer();
            _watcher = watcher;

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

        public void ActivityWatcher_OnLogOpen(object? sender, EventArgs e) => 
            Dispatcher.Invoke(() => LogTracerMenuItem.Visibility = Visibility.Visible);

        public void ActivityWatcher_OnGameJoin(object? sender, EventArgs e)
        {
            if (_activityWatcher is null)
                return;

            Dispatcher.Invoke(() =>
            {
                if (_activityWatcher.Data.ServerType == ServerType.Public)
                    InviteDeeplinkMenuItem.Visibility = Visibility.Visible;

                ServerDetailsMenuItem.Visibility = Visibility.Visible;
                GamePassDetailsMenuItem.Visibility = Visibility.Visible;

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
            Dispatcher.Invoke(() => {
                InviteDeeplinkMenuItem.Visibility = Visibility.Collapsed;
                ServerDetailsMenuItem.Visibility = Visibility.Collapsed;
                GamePassDetailsMenuItem.Visibility = Visibility.Collapsed;

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
