using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Interop;
using Voidstrap.UI.ViewModels.Settings;

namespace Voidstrap.UI.Elements.Crosshair
{
    public partial class CrosshairWindow : Window
    {
        private readonly ModsViewModel _viewModel;
        private readonly DispatcherTimer _robloxCheckTimer;

        public CrosshairWindow(ModsViewModel vm)
        {
            InitializeComponent();

            _viewModel = vm;
            _viewModel.PropertyChanged += (s, e) => UpdateCrosshair();

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;

            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            CrosshairCanvas.Width = Width;
            CrosshairCanvas.Height = Height;

            Loaded += CrosshairWindow_Loaded;
            Closed += (_, __) => _robloxCheckTimer.Stop();
            UpdateCrosshair();

            _robloxCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _robloxCheckTimer.Tick += (_, __) => UpdateVisibilityBasedOnRoblox();
            _robloxCheckTimer.Start();

            Show();
        }

        private void CrosshairWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MakeWindowClickThrough();
        }

        private void MakeWindowClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private void UpdateCrosshair()
        {
            CrosshairCanvas.Children.Clear();

            double centerX = Width / 2;
            double centerY = Height / 2;

            double size = _viewModel.CursorSize;
            double thickness = _viewModel.CrosshairThickness;
            double gap = _viewModel.Gap;

            double opacity = Math.Clamp(_viewModel.CursorOpacity, 0.05, 1.0);
            Brush mainBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_viewModel.CursorColorHex))
            {
                Opacity = opacity
            };

            Brush outlineBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_viewModel.CursorOutlineColorHex))
            {
                Opacity = opacity
            };

            switch (_viewModel.SelectedShape)
            {
                case ModsViewModel.CrosshairShape.Cross:
                    {
                        double innerThickness = Math.Max(1, thickness * 0.5);

                        DrawLine(centerX - size / 2, centerY,
                                 centerX + size / 2, centerY,
                                 outlineBrush, thickness);

                        DrawLine(centerX - size / 2, centerY,
                                 centerX + size / 2, centerY,
                                 mainBrush, innerThickness);

                        DrawLine(centerX, centerY - size / 2,
                                 centerX, centerY + size / 2,
                                 outlineBrush, thickness);

                        DrawLine(centerX, centerY - size / 2,
                                 centerX, centerY + size / 2,
                                 mainBrush, innerThickness);
                        break;
                    }

                case ModsViewModel.CrosshairShape.Dot:
                    {
                        DrawEllipse(
                            centerX, centerY,
                            size, size,
                            outlineBrush, outlineBrush, thickness);

                        double innerSize = size - (thickness * 2);
                        if (innerSize < 2) innerSize = 2;

                        DrawEllipse(
                            centerX, centerY,
                            innerSize, innerSize,
                            mainBrush, Brushes.Transparent, 0);

                        break;
                    }

                case ModsViewModel.CrosshairShape.Circle:
                    {
                        double radius = (size / 2) - gap;
                        if (radius < 1) radius = 1;

                        DrawEllipse(
                            centerX, centerY,
                            radius * 2, radius * 2,
                            outlineBrush, outlineBrush, thickness);

                        double innerRadius = radius - thickness;
                        if (innerRadius < 1) innerRadius = 1;

                        DrawEllipse(
                            centerX, centerY,
                            innerRadius * 2, innerRadius * 2,
                            mainBrush, Brushes.Transparent, 0);

                        break;
                    }
            }
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

        private void DrawEllipse(double cx, double cy, double width, double height, Brush fill, Brush stroke, double thickness)
        {
            var ellipse = new Ellipse
            {
                Width = width,
                Height = height,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = thickness
            };
            Canvas.SetLeft(ellipse, cx - width / 2);
            Canvas.SetTop(ellipse, cy - height / 2);
            CrosshairCanvas.Children.Add(ellipse);
        }
    }
}
