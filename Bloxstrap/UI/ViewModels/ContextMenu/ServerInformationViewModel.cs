using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Voidstrap.Integrations;

namespace Voidstrap.UI.ViewModels.ContextMenu
{
    internal class ServerInformationViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ActivityWatcher _activityWatcher;

        public string InstanceId => _activityWatcher?.Data?.JobId ?? Strings.Common_NotAvailable;
        public string ServerType => _activityWatcher?.Data?.ServerType.ToTranslatedString() ?? Strings.Common_NotAvailable;

        private string _serverLocation = Strings.Common_Loading;
        public string ServerLocation
        {
            get => _serverLocation;
            private set
            {
                if (_serverLocation != value)
                {
                    _serverLocation = value;
                    OnPropertyChanged(nameof(ServerLocation));
                }
            }
        }

        public Visibility ServerLocationVisibility => App.Settings.Prop.ShowServerDetails ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ServerUptimeVisibility => App.Settings.Prop.ServerUptimeBetterBLOXcuzitsbetterXD ? Visibility.Visible : Visibility.Collapsed;

        private string _username = Strings.Common_Loading;
        public string Username
        {
            get => _username;
            private set { if (_username != value) { _username = value; OnPropertyChanged(nameof(Username)); } }
        }

        private string _playerCount = Strings.Common_Loading;
        public string PlayerCount
        {
            get => _playerCount;
            private set { if (_playerCount != value) { _playerCount = value; OnPropertyChanged(nameof(PlayerCount)); } }
        }

        private string _playersInGame = Strings.Common_Loading;
        public string PlayersInGame
        {
            get => _playersInGame;
            private set { if (_playersInGame != value) { _playersInGame = value; OnPropertyChanged(nameof(PlayersInGame)); } }
        }

        private string _uptime = Strings.Common_Loading;
        public string Uptime
        {
            get => _uptime;
            private set { if (_uptime != value) { _uptime = value; OnPropertyChanged(nameof(Uptime)); } }
        }

        private string _cpuUsage = Strings.Common_Loading;
        public string CpuUsage
        {
            get => _cpuUsage;
            private set { if (_cpuUsage != value) { _cpuUsage = value; OnPropertyChanged(nameof(CpuUsage)); } }
        }

        private string _memoryUsage = Strings.Common_Loading;
        public string MemoryUsage
        {
            get => _memoryUsage;
            private set { if (_memoryUsage != value) { _memoryUsage = value; OnPropertyChanged(nameof(MemoryUsage)); } }
        }

        private string _connectedServerId = "Unknown";
        public string ConnectedServerId
        {
            get => _connectedServerId;
            private set
            {
                if (_connectedServerId != value)
                {
                    _connectedServerId = value;
                    OnPropertyChanged(nameof(ConnectedServerId));
                }
            }
        }

        public ICommand CopyInstanceIdCommand { get; }
        public ICommand RefreshServerLocationCommand { get; }

        public ServerInformationViewModel(Watcher watcher)
        {
            _activityWatcher = watcher?.ActivityWatcher ?? throw new ArgumentNullException(nameof(watcher));

            RefreshServerLocationCommand = new AsyncRelayCommand(QueryServerLocationAsync);
            CopyInstanceIdCommand = new RelayCommand(CopyInstanceId);

            _activityWatcher.OnGameJoin += async (s, e) => await SafeUpdateMetricsAsync();
            _activityWatcher.OnGameLeave += async (s, e) => await SafeUpdateMetricsAsync();
            _activityWatcher.OnLogEntry += async (s, e) => await SafeUpdateMetricsAsync();
            _activityWatcher.OnNewPlayerRequest += async (s, e) => await SafeUpdateMetricsAsync();

            if (ServerLocationVisibility == Visibility.Visible) // a goofball did this :(
                _ = QueryServerLocationAsync();
                _ = QueryServerMetricsAsync();
        }

        private async Task QueryServerLocationAsync(CancellationToken cancellationToken = default)
        {
            ServerLocation = Strings.Common_Loading;

            try
            {
                if (_activityWatcher.Data == null)
                {
                    ServerLocation = Strings.Common_NotAvailable;
                    return;
                }

                string? location = await _activityWatcher.Data.QueryServerLocation();
                ServerLocation = !string.IsNullOrWhiteSpace(location)
                    ? location
                    : "Location not available";
            }
            catch (Exception ex)
            {
                ServerLocation = $"Error fetching location: {ex.Message}";
            }
        }

        private async Task QueryServerMetricsAsync()
        {
            while (!_activityWatcher.IsDisposed)
            {
                await SafeUpdateMetricsAsync();
                await Task.Delay(2000);
            }
        }

        private async Task SafeUpdateMetricsAsync()
        {
            try
            {
                await UpdateMetricsAsync();
                await UpdateConnectedServerIdAsync();
            }
            catch (Exception ex)
            {
                Username = Strings.Common_NotAvailable;
                PlayerCount = Strings.Common_ErrorFetchingPlayerCount;
                PlayersInGame = Strings.Common_NotAvailable;
                Uptime = CpuUsage = MemoryUsage = ConnectedServerId = "Error fetching metrics";
                Console.WriteLine($"ServerInformationViewModel: Error updating metrics: {ex}");
            }
        }
        private Task UpdateMetricsAsync()
        {
            if (_activityWatcher.Data == null)
            {
                Username = Strings.Common_NotAvailable;
                PlayerCount = "0";
                PlayersInGame = Strings.Common_NotAvailable;
                Uptime = CpuUsage = MemoryUsage = Strings.Common_NotAvailable;
                return Task.CompletedTask;
            }

            try
            {
                var logs = _activityWatcher.Data.PlayerLogs?.Values;
                if (logs != null && logs.Any())
                {
                    var activeCount = logs
                        .Where(p => p != null)
                        .GroupBy(p => p.UserId?.Trim() ?? "")
                        .Select(g => g.OrderByDescending(x => x.Time).First())
                        .Count(u => string.Equals(u.Type, "added", StringComparison.OrdinalIgnoreCase));

                    PlayerCount = activeCount.ToString();

                    var allPlayers = logs
                        .Where(p => !string.IsNullOrWhiteSpace(p?.Username))
                        .Select(p => p.Username)
                        .Distinct()
                        .ToList();

                    PlayersInGame = allPlayers.Count > 0
                        ? string.Join(", ", allPlayers)
                        : Strings.Common_NotAvailable;

                    var localUserLog = logs
                        .Where(u => (u.UserId?.Trim() ?? "") == _activityWatcher.Data.UserId.ToString())
                        .OrderByDescending(u => u.Time)
                        .FirstOrDefault();

                    Username = localUserLog?.Username ?? allPlayers.FirstOrDefault() ?? Strings.Common_NotAvailable;
                }
                else
                {
                    PlayerCount = "0";
                    PlayersInGame = Strings.Common_NotAvailable;
                    Username = Strings.Common_NotAvailable;
                }

                Uptime = _activityWatcher.Data.TimeJoined != default
                    ? (DateTime.Now - _activityWatcher.Data.TimeJoined).ToString(@"hh\:mm\:ss")
                    : Strings.Common_NotAvailable;

                CpuUsage = "N/A";
                MemoryUsage = "N/A";
            }
            catch (Exception)
            {
                PlayerCount = "0";
                PlayersInGame = Strings.Common_NotAvailable;
                Username = Strings.Common_NotAvailable;
                Uptime = CpuUsage = MemoryUsage = "Error fetching metrics";
            }

            return Task.CompletedTask;
        }


        private bool _betterRobloxFetched = false;
        private async Task UpdateConnectedServerIdAsync()
        {
            try
            {
                if (_betterRobloxFetched)
                    return;

                _betterRobloxFetched = true;

                string serverId = _activityWatcher.Data?.JobId?.Trim() ?? "Unknown";
                if (string.IsNullOrEmpty(serverId) || serverId == "Unknown")
                {
                    ConnectedServerId = "No server ID found";
                    Uptime = "Not Available";
                    return;
                }

                string apiUrl = $"https://api.betterroblox.com/servers/server/{serverId}";
                using var http = new HttpClient();

                var response = await http.GetAsync(apiUrl);
                string json = await response.Content.ReadAsStringAsync();

                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                bool success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

                if (!success)
                {
                    string errorMsg = root.TryGetProperty("error", out var errProp)
                        ? errProp.GetString()
                        : "Unknown error";

                    ConnectedServerId = errorMsg ?? "Server not found In database";
                    Uptime = "Not Available";
                    return;
                }

                var server = root.GetProperty("server");

                string? age = server.TryGetProperty("serverAge", out var ageProp)
                    ? ageProp.GetString()
                    : null;

                string? firstSeen = server.TryGetProperty("firstSeen", out var fsProp)
                    ? fsProp.GetString()
                    : null;

                string displayUptime;

                if (!string.IsNullOrEmpty(age))
                {
                    displayUptime = age;
                }
                else if (DateTime.TryParse(firstSeen, out var fsTime))
                {
                    var uptime = DateTime.UtcNow - fsTime;
                    displayUptime = $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
                }
                else
                {
                    displayUptime = "Unknown";
                }

                Uptime = displayUptime;
                ConnectedServerId = displayUptime;
            }
            catch (System.Text.Json.JsonException)
            {
                ConnectedServerId = "Invalid JSON response";
                Uptime = "Unknown";
            }
            catch (HttpRequestException ex)
            {
                ConnectedServerId = $"Network error ({ex.Message})";
                Uptime = "Unknown";
            }
            catch (Exception ex)
            {
                ConnectedServerId = $"Error ({ex.Message})";
                Uptime = "Error fetching uptime";
            }
        }


        private void CopyInstanceId()
        {
            try
            {
                Clipboard.SetDataObject(InstanceId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying instance ID: {ex.Message}", "Clipboard Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public static class Strings
    {
        public static string Common_Loading => "Loading...";
        public static string Common_NotAvailable => "Not Available";
        public static string Common_ErrorFetchingPlayerCount => "Error fetching player count";
    }
}
