using LibreHardwareMonitor.Hardware;
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
    // REQUIRED BY LIBREHARDWAREMONITOR
    public sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
                sub.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }

    public class OverlayWindow : Window, INotifyPropertyChanged
    {
        private int _frames;
        private int _fps;
        private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();

        private readonly Computer _computer;
        private double _cpuTemp;

        private TextBlock _fpsTextBlock;
        private TextBlock _cpuTextBlock;
        private TextBlock _pingTextBlock;
        private TextBlock _locationTextBlock;
        private TextBlock _timeTextBlock;

        private readonly DispatcherTimer _updateTimer;

        private readonly bool _showFPS = App.Settings.Prop.FPSCounter;
        private readonly bool _showCPU = App.Settings.Prop.CPUTempCounter;
        private readonly bool _showPing = App.Settings.Prop.ServerPingCounter;
        private readonly bool _showTime = App.Settings.Prop.CurrentTimeDisplay;
        private readonly bool _showLocation = App.Settings.Prop.ShowServerDetailsUI;

        private string _serverIp;
        private string _lastServerIp;
        private bool _locationFetching;
        private string _serverLocation = "Location: --";

        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        public OverlayWindow()
        {
            Width = 260;
            Height = 150;

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

            if (_showCPU)
            {
                _cpuTextBlock = CreateTextBlock(Brushes.Orange);
                panel.Children.Add(_cpuTextBlock);
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

            // 🔥 FIXED HARDWARE INIT
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true
            };

            _computer.Open();
            _computer.Accept(new UpdateVisitor());
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
            if (_showCPU)
            {
                _cpuTemp = GetCpuTemperature();
                _cpuTextBlock.Text = double.IsNaN(_cpuTemp)
                    ? "CPU Temp: --"
                    : $"CPU Temp: {_cpuTemp:0}°C";
            }

            if (_showTime)
                _timeTextBlock.Text = DateTime.Now.ToString("h:mm tt");

            if (_showPing)
            {
                _serverIp = GetRobloxServerIp();

                if (!string.IsNullOrEmpty(_serverIp))
                {
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
                else
                {
                    _pingTextBlock.Text = "Ping: --";
                }
            }
        }

        // 🔥 CPU TEMP FIX (NO MORE 0°C)
        private double GetCpuTemperature()
        {
            try
            {
                _computer.Accept(new UpdateVisitor());

                foreach (var hw in _computer.Hardware)
                {
                    if (hw.HardwareType != HardwareType.Cpu)
                        continue;

                    var package = hw.Sensors.FirstOrDefault(s =>
                        s.SensorType == SensorType.Temperature &&
                        s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) &&
                        s.Value.HasValue);

                    if (package?.Value != null)
                        return package.Value.Value;

                    var cores = hw.Sensors
                        .Where(s =>
                            s.SensorType == SensorType.Temperature &&
                            s.Value.HasValue &&
                            s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        .Select(s => s.Value.Value);

                    if (cores.Any())
                        return cores.Max();
                }

                return double.NaN;
            }
            catch
            {
                return double.NaN;
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
            catch { return null; }
        }

        private async Task<int> PingServerAsync(string ip)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, 1000);
                return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : -1;
            }
            catch { return -1; }
        }

        private async Task<string> GetServerLocationAsync(string ip)
        {
            try
            {
                string json = await Http.GetStringAsync($"https://ipinfo.io/{ip}/json");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string city = root.TryGetProperty("city", out var c) ? c.GetString() : null;
                string country = root.TryGetProperty("country", out var co) ? co.GetString() : null;

                return !string.IsNullOrEmpty(city)
                    ? $"Location: {city} {CountryToFlag(country)}"
                    : "Location: Unknown";
            }
            catch { return "Location: Unknown"; }
        }

        private string CountryToFlag(string cc)
        {
            if (string.IsNullOrEmpty(cc) || cc.Length != 2) return "";
            int o = 0x1F1E6;
            return char.ConvertFromUtf32(o + cc[0] - 'A') +
                   char.ConvertFromUtf32(o + cc[1] - 'A');
        }

        private TextBlock CreateTextBlock(Brush color) => new()
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

        private bool IsRobloxForeground()
        {
            IntPtr hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out uint pid);
            try
            {
                return Process.GetProcessById((int)pid)
                    .ProcessName.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
