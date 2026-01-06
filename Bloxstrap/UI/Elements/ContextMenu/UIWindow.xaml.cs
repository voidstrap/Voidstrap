using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Voidstrap.UI.Elements.Overlay
{
    public class OverlayWindow : Window, INotifyPropertyChanged
    {
        private int _frames;
        private int _fps;
        private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();

        private TextBlock _fpsTextBlock;
        private TextBlock _pingTextBlock;
        private TextBlock _locationTextBlock;
        private TextBlock _timeTextBlock;

        private readonly DispatcherTimer _updateTimer;

        private readonly bool _showFPS = App.Settings.Prop.FPSCounter;
        private readonly bool _showPing = App.Settings.Prop.ServerPingCounter;
        private readonly bool _showTime = App.Settings.Prop.CurrentTimeDisplay;
        private readonly bool _showLocation = App.Settings.Prop.ShowServerDetailsUI;

        private string _serverIp;
        private string _lastServerIp;
        private bool _locationFetching;
        private string _serverLocation = "Location: --";

        private static readonly HttpClient Http;

        static OverlayWindow()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Http = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            {
                Timeout = TimeSpan.FromSeconds(4)
            };

            Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Voidstrap/1.0 (+https://github.com/voidstrap)"
            );
            Http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        public OverlayWindow()
        {
            Width = 260;
            Height = 140;

            Left = SystemParameters.PrimaryScreenWidth - Width - -95;
            Top = 10;

            AllowsTransparency = true;
            Background = null;
            WindowStyle = WindowStyle.None;
            Topmost = true;
            ShowInTaskbar = false;

            var panel = new StackPanel { Orientation = Orientation.Vertical };

            if (_showFPS)
            {
                _fpsTextBlock = CreateTextBlock(Brushes.Lime);
                panel.Children.Add(_fpsTextBlock);
            }

            if (_showPing)
            {
                _pingTextBlock = CreateTextBlock(Brushes.LightSkyBlue);
                panel.Children.Add(_pingTextBlock);
            }

            if (_showLocation)
            {
                _locationTextBlock = CreateTextBlock(Brushes.LightGreen);
                _locationTextBlock.Text = _serverLocation;
                panel.Children.Add(_locationTextBlock);
            }

            if (_showTime)
            {
                _timeTextBlock = CreateTextBlock(Brushes.Cyan);
                panel.Children.Add(_timeTextBlock);
            }

            Content = panel;

            CompositionTarget.Rendering += OnRendering;

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _updateTimer.Tick += async (_, __) => await UpdateStatsAsync();
            _updateTimer.Start();

            Loaded += (_, __) => MakeClickThrough();
            Closing += (_, __) => CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (_showFPS)
            {
                _frames++;
                if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
                {
                    _fps = _frames;
                    _frames = 0;
                    _fpsStopwatch.Restart();
                    _fpsTextBlock.Text = $"FPS: {_fps}";
                }
            }

            if (!IsRobloxForeground())
            {
                if (IsVisible) Hide();
            }
            else if (!IsVisible)
            {
                Show();
            }
        }

        private async Task UpdateStatsAsync()
        {
            if (_showTime)
                _timeTextBlock.Text = DateTime.Now.ToString("h:mm tt");

            if (!_showPing)
                return;

            _serverIp = GetRobloxServerIp();

            if (string.IsNullOrEmpty(_serverIp))
            {
                _pingTextBlock.Text = "Ping: --";
                return;
            }

            int ping = await PingServerAsync(_serverIp);
            _pingTextBlock.Text = ping > 0 ? $"Ping: {ping} ms" : "Ping: --";

            if (_showLocation && !_locationFetching && _serverIp != _lastServerIp)
            {
                _lastServerIp = _serverIp;
                _locationFetching = true;
                _locationTextBlock.Text = "Location: --";

                _ = Task.Run(async () =>
                {
                    string loc = await GetServerLocationAsync(_serverIp);
                    Dispatcher.Invoke(() =>
                    {
                        _serverLocation = loc;
                        _locationTextBlock.Text = loc;
                        _locationFetching = false;
                    });
                });
            }
        }

        private string GetRobloxServerIp()
        {
            try
            {
                return IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpConnections()
                    .FirstOrDefault(c =>
                        c.State == TcpState.Established &&
                        !IPAddress.IsLoopback(c.RemoteEndPoint.Address))
                    ?.RemoteEndPoint.Address.ToString();
            }
            catch
            {
                return null;
            }
        }

        private async Task<int> PingServerAsync(string ip)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, 1000);
                return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : -1;
            }
            catch
            {
                return -1;
            }
        }

        private async Task<string> GetServerLocationAsync(string ip)
        {
            try
            {
                using var res = await Http.GetAsync($"https://ipinfo.io/{ip}/json");
                if (!res.IsSuccessStatusCode)
                    return "Location: Unknown";

                string json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string city = root.TryGetProperty("city", out var c) ? c.GetString() : null;
                string country = root.TryGetProperty("country", out var co) ? co.GetString() : null;

                return !string.IsNullOrEmpty(city)
                    ? $"Location: {city} {CountryToFlag(country)}"
                    : "Location: Unknown";
            }
            catch
            {
                return "Location: Unknown";
            }
        }

        private static string CountryToFlag(string cc)
        {
            if (string.IsNullOrEmpty(cc) || cc.Length != 2)
                return "";

            int offset = 0x1F1E6;
            return char.ConvertFromUtf32(offset + cc[0] - 'A') +
                   char.ConvertFromUtf32(offset + cc[1] - 'A');
        }

        private static TextBlock CreateTextBlock(Brush color) => new()
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = color
        };

        private void MakeClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, -20);
            SetWindowLong(hwnd, -20, style | 0x20 | 0x80);
        }

        private static bool IsRobloxForeground()
        {
            IntPtr hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out uint pid);

            try
            {
                return Process.GetProcessById((int)pid)
                    .ProcessName.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
