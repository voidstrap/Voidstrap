using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class ServerBrowserPage
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private readonly ObservableCollection<Server> servers = new();
        private DispatcherTimer refreshTimer;
        private DispatcherTimer debounceTimer;
        private CancellationTokenSource loadCts;
        private bool isUnloaded;

        private static DateTime nextAllowedLoadUtc = DateTime.MinValue;
        private const int CooldownSeconds = 20;
        private int serverDisplayLimit = 50;
        private string playerSortMode = "Lowest Players";
        private string pingSortMode = "Lowest Ping";
        private int rateLimitBackoffSeconds = CooldownSeconds;
        private string lastLoadedPlaceId;

        public ServerBrowserPage()
        {
            InitializeComponent();

            DataGrid.ItemsSource = servers;

            PlaceIdTextBox.Text = App.Settings.Prop.LastServerSave ?? string.Empty;
            lastLoadedPlaceId = PlaceIdTextBox.Text.Trim();

            PlaceIdTextBox.TextChanged += PlaceIdTextBox_TextChanged;

            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(CooldownSeconds)
            };
            refreshTimer.Tick += RefreshTimer_Tick;

            debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            debounceTimer.Tick += DebounceTimer_Tick;

            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        #region Sorting

        private void ApplyServerSorting()
        {
            if (servers.Count == 0) return;

            IOrderedEnumerable<Server> ordered = playerSortMode.Equals("Highest Players", StringComparison.OrdinalIgnoreCase)
                ? servers.OrderByDescending(s => s.Playing)
                : servers.OrderBy(s => s.Playing);

            ordered = pingSortMode.Equals("Lowest Ping", StringComparison.OrdinalIgnoreCase)
                ? ordered.ThenBy(s => s.Ping)
                : ordered.ThenByDescending(s => s.Ping);

            var limited = ordered.Take(serverDisplayLimit).ToList();

            Dispatcher.InvokeAsync(() =>
            {
                servers.Clear();
                foreach (var s in limited)
                    servers.Add(s);
            });
        }

        private void ApplyServerLimit()
        {
            if (servers.Count == 0) return;

            var limited = servers
                .OrderBy(s => s.Ping)
                .Take(serverDisplayLimit)
                .ToList();

            Dispatcher.InvokeAsync(() =>
            {
                servers.Clear();
                foreach (var s in limited)
                    servers.Add(s);
            });
        }

        #endregion

        #region UI Events

        private void PlayerSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlayerSortComboBox.SelectedItem is ComboBoxItem item)
            {
                playerSortMode = item.Content.ToString();
                ApplyServerSorting();
            }
        }

        private void PingSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PingSortComboBox.SelectedItem is ComboBoxItem item)
            {
                pingSortMode = item.Content.ToString();
                ApplyServerSorting();
            }
        }

        private void ServerLimitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerLimitComboBox.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Content.ToString(), out int limit))
            {
                serverDisplayLimit = limit;
                ApplyServerLimit();
            }
        }

        private void PlaceIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUnloaded) return;

            debounceTimer.Stop();
            debounceTimer.Start();
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataGrid.SelectedItem is Server s)
                JoinServer(s.Id);
        }

        private void CopySelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItem is Server s)
            {
                Clipboard.SetText(s.Id);
                Frontend.ShowMessageBox("Server ID copied.");
            }
        }

        private void JoinSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItem is Server s)
                JoinServer(s.Id);
        }

        private void JoinLowestPingButton_Click(object sender, RoutedEventArgs e)
        {
            var lowest = servers.FirstOrDefault(s => s.IsLowestPing);
            if (lowest != null)
                JoinServer(lowest.Id);
        }

        #endregion

        #region Page Lifecycle

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(lastLoadedPlaceId))
                await LoadServersAsync(manual: true, ignoreCooldown: true);

            refreshTimer.Start();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            isUnloaded = true;

            refreshTimer.Stop();
            refreshTimer.Tick -= RefreshTimer_Tick;

            debounceTimer.Stop();
            debounceTimer.Tick -= DebounceTimer_Tick;

            PlaceIdTextBox.TextChanged -= PlaceIdTextBox_TextChanged;
            loadCts?.Cancel();
        }

        private async void DebounceTimer_Tick(object sender, EventArgs e)
        {
            debounceTimer.Stop();

            string newPlaceId = PlaceIdTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newPlaceId))
            {
                ClearServers();
                return;
            }

            if (newPlaceId == lastLoadedPlaceId)
                return;

            lastLoadedPlaceId = newPlaceId;
            App.Settings.Prop.LastServerSave = newPlaceId;

            await LoadServersAsync(manual: true, ignoreCooldown: true);
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            await LoadServersAsync(manual: false);
        }

        #endregion

        #region Server Loading

        private async Task LoadServersAsync(bool manual, bool ignoreCooldown = false)
        {
            if (isUnloaded) return;

            string placeId = PlaceIdTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(placeId))
            {
                ClearServers();
                return;
            }

            if (!ignoreCooldown)
            {
                var now = DateTime.UtcNow;
                if (now < nextAllowedLoadUtc) return;
                nextAllowedLoadUtc = now.AddSeconds(CooldownSeconds);
            }

            loadCts?.Cancel();
            loadCts = new CancellationTokenSource();
            var token = loadCts.Token;

            SetStatus("Loading servers…");

            try
            {
                string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?limit=100";
                using var response = await Http.GetAsync(url, token);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await HandleRateLimitAsync(token);
                    return;
                }

                rateLimitBackoffSeconds = CooldownSeconds;
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(token);
                var result = JsonSerializer.Deserialize<ServerResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (token.IsCancellationRequested || isUnloaded) return;

                if (result?.Data == null || result.Data.Count == 0)
                {
                    SetStatus("No servers found.");
                    ClearServers();
                    return;
                }

                await UpdateServersAsync(result.Data, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                SetStatus("Failed to load servers.");
            }
        }

        private async Task UpdateServersAsync(List<Server> newServers, CancellationToken token)
        {
            var ordered = newServers
                .OrderBy(s => s.Ping)
                .Take(serverDisplayLimit)
                .ToList();

            var lowest = ordered.FirstOrDefault();

            foreach (var s in ordered)
            {
                s.IsLowestPing = s == lowest;
                s.Tooltip = $"Players: {s.Playing}/{s.MaxPlayers}\nPing: {s.Ping} ms";
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || isUnloaded) return;

                servers.Clear();
                foreach (var s in ordered)
                    servers.Add(s);

                ApplyServerSorting();

                DataGrid.SelectedItem = lowest;
                JoinLowestPingButton.Content = lowest != null
                    ? $"Join Lowest ({lowest.Ping}ms)"
                    : "No Server Detected";
            });
        }

        private async Task HandleRateLimitAsync(CancellationToken token)
        {
            refreshTimer.Stop();

            int remaining = rateLimitBackoffSeconds;
            while (remaining > 0)
            {
                if (token.IsCancellationRequested || isUnloaded)
                    return;

                SetStatus($"Rate limited — retrying in {remaining}s…");
                await Task.Delay(1000, token);
                remaining--;
            }

            rateLimitBackoffSeconds = Math.Min(rateLimitBackoffSeconds * 2, 120);
            refreshTimer.Start();
        }

        #endregion

        #region Utilities

        private void ClearServers()
        {
            Dispatcher.InvokeAsync(() =>
            {
                servers.Clear();
                JoinLowestPingButton.Content = "No Server Detected";
            });
        }

        private void SetStatus(string text)
        {
            Dispatcher.InvokeAsync(() =>
            {
                JoinLowestPingButton.Content = text;
            });
        }

        private void JoinServer(string serverId)
        {
            string placeId = PlaceIdTextBox.Text.Trim();
            if (string.IsNullOrEmpty(placeId))
                return;

            try
            {
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

        #endregion
    }

    public sealed class ServerResponse
    {
        public List<Server> Data { get; set; } = new();
    }

    public sealed class Server
    {
        public string Id { get; set; }
        public int MaxPlayers { get; set; }
        public int Playing { get; set; }
        public double FPS { get; set; }
        public int Ping { get; set; }
        public bool IsLowestPing { get; set; }
        public string Tooltip { get; set; }
    }
}
