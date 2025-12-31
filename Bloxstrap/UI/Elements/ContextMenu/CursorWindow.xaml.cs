using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Voidstrap.UI.ViewModels.Settings;

namespace Voidstrap.UI.Elements.Crosshair
{
    public partial class CrosshairWindow : Window
    {
        private readonly ModsViewModel _viewModel;
        private readonly DispatcherTimer _robloxCheckTimer;

        private Image _imageCrosshair;
        private MediaElement _gifCrosshair;

        public CrosshairWindow(ModsViewModel vm)
        {
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

            InitializeComponent();

            _viewModel = vm;
            _viewModel.PropertyChanged += (_, __) => UpdateCrosshair();

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;

            UpdateScreenBounds();

            Loaded += (_, __) => MakeWindowClickThrough();
            Closed += (_, __) =>
            {
                _robloxCheckTimer.Stop();
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            };

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            _robloxCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _robloxCheckTimer.Tick += (_, __) => UpdateVisibilityBasedOnRoblox();
            _robloxCheckTimer.Start();

            UpdateCrosshair();
            Show();
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            UpdateScreenBounds();
        }

        private void UpdateScreenBounds()
        {
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            CrosshairCanvas.Width = Width;
            CrosshairCanvas.Height = Height;

            UpdateCrosshair();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            UpdateScreenBounds();
        }

        private void MakeWindowClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }

        private void UpdateVisibilityBasedOnRoblox()
        {
            if (!IsLoaded) return;

            if (IsRobloxForeground())
            {
                if (!IsVisible) Show();
            }
            else
            {
                if (IsVisible) Hide();
            }
        }

        private static bool IsRobloxForeground()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            GetWindowThreadProcessId(hwnd, out uint pid);
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                return proc.ProcessName.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void UpdateCrosshair()
        {
            CrosshairCanvas.Children.Clear();

            double centerX = Width / 2.0;
            double centerY = Height / 2.0;

            if (IsRobloxForeground())
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out RECT rect))
                {
                    centerX = rect.Left + (rect.Right - rect.Left) / 2.0 - Left;
                    centerY = rect.Top + (rect.Bottom - rect.Top) / 2.0 - Top;
                }
            }

            double scale = 0.75;
            double size = _viewModel.CursorSize * scale;
            double gap = _viewModel.Gap * scale;
            double thickness = Math.Max(1, _viewModel.CrosshairThickness * scale);
            double opacity = Math.Clamp(_viewModel.CursorOpacity, 0.05, 1.0);

            var mainBrush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_viewModel.CursorColorHex))
            { Opacity = opacity };

            var outlineBrush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_viewModel.CursorOutlineColorHex))
            { Opacity = opacity };

            switch (_viewModel.SelectedShape)
            {
                case ModsViewModel.CrosshairShape.Image:
                    {
                        if (string.IsNullOrWhiteSpace(_viewModel.ImageUrl))
                            return;

                        string path = _viewModel.ImageUrl;
                        bool isGif = path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);

                        if (isGif)
                        {
                            if (_gifCrosshair == null)
                            {
                                _gifCrosshair = new MediaElement
                                {
                                    LoadedBehavior = MediaState.Manual,
                                    UnloadedBehavior = MediaState.Manual,
                                    IsMuted = true,
                                    Stretch = Stretch.Uniform,
                                    Opacity = opacity,
                                    IsHitTestVisible = false,
                                    ScrubbingEnabled = true
                                };

                                _gifCrosshair.MediaOpened += (_, __) =>
                                {
                                    _gifCrosshair.Position = TimeSpan.Zero;
                                    _gifCrosshair.Play();
                                };

                                _gifCrosshair.MediaEnded += (_, __) =>
                                {
                                    _gifCrosshair.Position = TimeSpan.Zero;
                                    _gifCrosshair.Play();
                                };
                            }

                            _gifCrosshair.Source = new Uri(path, UriKind.Absolute);

                            _gifCrosshair.Stop();
                            _gifCrosshair.Position = TimeSpan.Zero;
                            _gifCrosshair.Play();
                            _gifCrosshair.Width = size;
                            _gifCrosshair.Height = size;
                            _gifCrosshair.Opacity = opacity;

                            Canvas.SetLeft(_gifCrosshair, centerX - size / 2);
                            Canvas.SetTop(_gifCrosshair, centerY - size / 2);

                            CrosshairCanvas.Children.Add(_gifCrosshair);
                            _gifCrosshair.Play();
                        }
                        else
                        {
                            if (_imageCrosshair == null)
                            {
                                _imageCrosshair = new Image
                                {
                                    Stretch = Stretch.Uniform
                                };

                                RenderOptions.SetBitmapScalingMode(
                                    _imageCrosshair,
                                    BitmapScalingMode.HighQuality);
                            }

                            _imageCrosshair.Source = LoadBitmap(path);
                            _imageCrosshair.Width = size;
                            _imageCrosshair.Height = size;
                            _imageCrosshair.Opacity = opacity;

                            Canvas.SetLeft(_imageCrosshair, centerX - size / 2);
                            Canvas.SetTop(_imageCrosshair, centerY - size / 2);

                            CrosshairCanvas.Children.Add(_imageCrosshair);
                        }

                        break;
                    }

                case ModsViewModel.CrosshairShape.Cross:
                    DrawCross(centerX, centerY, size, gap, thickness, outlineBrush, true);
                    DrawCross(centerX, centerY, size, gap, thickness, mainBrush, false);
                    break;

                case ModsViewModel.CrosshairShape.Dot:
                    DrawEllipse(centerX, centerY, size / 3 + 2, outlineBrush);
                    DrawEllipse(centerX, centerY, size / 3, mainBrush);
                    break;

                case ModsViewModel.CrosshairShape.Circle:
                    DrawCircle(centerX, centerY, size / 2, thickness + 2, outlineBrush);
                    DrawCircle(centerX, centerY, size / 2 - 2, thickness, mainBrush);
                    break;
            }
        }

        private static BitmapImage LoadBitmap(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private void DrawCross(double cx, double cy, double size, double gap, double thickness, Brush brush, bool outline)
        {
            double t = outline ? thickness + 2 : thickness;

            DrawLine(cx - size, cy, cx - gap, cy, brush, t);
            DrawLine(cx + gap, cy, cx + size, cy, brush, t);
            DrawLine(cx, cy - size, cx, cy - gap, brush, t);
            DrawLine(cx, cy + gap, cx, cy + size, brush, t);
        }

        private void DrawLine(double x1, double y1, double x2, double y2, Brush brush, double thickness)
        {
            CrosshairCanvas.Children.Add(new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = brush,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }

        private void DrawEllipse(double cx, double cy, double radius, Brush fill)
        {
            var e = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = fill
            };

            Canvas.SetLeft(e, cx - radius);
            Canvas.SetTop(e, cy - radius);
            CrosshairCanvas.Children.Add(e);
        }

        private void DrawCircle(double cx, double cy, double radius, double thickness, Brush stroke)
        {
            var e = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = stroke,
                StrokeThickness = thickness
            };

            Canvas.SetLeft(e, cx - radius);
            Canvas.SetTop(e, cy - radius);
            CrosshairCanvas.Children.Add(e);
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
