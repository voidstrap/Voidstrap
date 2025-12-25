using LibreHardwareMonitor.Hardware;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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
        private readonly Stopwatch _fpsStopwatch;

        private double _cpuTemp;
        private readonly Computer _computer;

        private readonly TextBlock _fpsTextBlock;
        private readonly TextBlock _cpuTextBlock;
        private readonly TextBlock _timeTextBlock;

        private readonly DispatcherTimer _updateTimer;
        private readonly bool _showFPS = App.Settings.Prop.FPSCounter;
        private readonly bool _showCPU = App.Settings.Prop.CPUTempCounter;
        private readonly bool _showTime = App.Settings.Prop.CurrentTimeDisplay;

        public OverlayWindow()
        {
            Width = 220;
            Height = 80;

            var screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = screenWidth - Width - -85;
            Top = 10;

            AllowsTransparency = true;
            Background = null;
            WindowStyle = WindowStyle.None;
            Topmost = true;
            ShowInTaskbar = false;

            var panel = new StackPanel { Orientation = Orientation.Vertical };

            if (_showFPS)
            {
                _fpsTextBlock = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Lime };
                panel.Children.Add(_fpsTextBlock);
            }

            if (_showCPU)
            {
                _cpuTextBlock = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Orange };
                panel.Children.Add(_cpuTextBlock);
            }

            if (_showTime)
            {
                _timeTextBlock = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Cyan };
                panel.Children.Add(_timeTextBlock);
            }

            Content = panel;

            _fpsStopwatch = Stopwatch.StartNew();
            CompositionTarget.Rendering += OnRendering;

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _updateTimer.Tick += (_, __) => UpdateCpuAndTime();
            _updateTimer.Start();

            Loaded += (_, __) => MakeClickThrough();
            Closing += (_, __) => CompositionTarget.Rendering -= OnRendering;
            _computer = new Computer { IsCpuEnabled = true };
            _computer.Open();
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(_fps)));
                }
            }

            if (!IsRobloxForeground())
            {
                if (IsVisible) Hide();
            }
            else
            {
                if (!IsVisible) Show();
            }
        }

        private void UpdateCpuAndTime()
        {
            if (_showCPU)
            {
                _cpuTemp = GetCpuTemperature();
                _cpuTextBlock.Text = $"CPU Temp: {_cpuTemp:0}°C";
            }

            if (_showTime)
            {
                _timeTextBlock.Text = DateTime.Now.ToString("h:mm tt");
            }
        }

        private double GetCpuTemperature()
        {
            try
            {
                var temps = _computer.Hardware
                    .Where(h => h.HardwareType == HardwareType.Cpu)
                    .SelectMany(h => { h.Update(); return h.Sensors; })
                    .Where(s => s.SensorType == SensorType.Temperature)
                    .Select(s => s.Value ?? 0);

                return temps.Any() ? temps.Average() : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void MakeClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }

        private bool IsRobloxForeground()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            GetWindowThreadProcessId(hwnd, out uint pid);
            try
            {
                var p = Process.GetProcessById((int)pid);
                return p.ProcessName.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
