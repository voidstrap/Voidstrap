using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Voidstrap.UI.ViewModels.Settings;
using System.Windows.Interop;

namespace Voidstrap.UI.Elements.Crosshair
{
    public partial class CrosshairWindow : Window
    {
        private readonly ModsViewModel _viewModel;

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
            UpdateCrosshair();

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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

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
                    DrawLine(centerX - size / 2, centerY, centerX + size / 2, centerY, outlineBrush, thickness);
                    DrawLine(centerX, centerY - size / 2, centerX, centerY + size / 2, outlineBrush, thickness);
                    break;

                case ModsViewModel.CrosshairShape.Dot:
                    DrawEllipse(centerX, centerY, size, size, mainBrush, outlineBrush, thickness);
                    break;

                case ModsViewModel.CrosshairShape.Circle:
                    double radius = (size / 2) - gap;
                    if (radius < 1) radius = 1;
                    DrawEllipse(centerX, centerY, radius * 2, radius * 2, Brushes.Transparent, outlineBrush, thickness);
                    break;
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
