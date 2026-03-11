using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Voidstrap.UI.Elements.Overlay
{
    public partial class NotificationWindow : Window
    {
        private Queue<NotificationItem> _queue = new();
        private bool _visible = false;

        private double _hiddenLeft;
        private double _visibleLeft;

        private bool _isProcessing = false;

        public NotificationWindow()
        {
            InitializeComponent();
            ProgressBar.Fill = SystemParameters.WindowGlassBrush;

            Loaded += Window_Loaded;
            SourceInitialized += (_, __) => MakeClickThrough();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _visibleLeft = SystemParameters.WorkArea.Width - Width - 10;
            _hiddenLeft = SystemParameters.WorkArea.Width;

            Left = _hiddenLeft;
            Top = 10;
            Show();
        }

        #region Public API
        public void ShowNotification(string message, string imagePathOrUrl = null, double durationSeconds = 5)
        {
            _queue.Enqueue(new NotificationItem
            {
                Text = message,
                ImagePathOrUrl = imagePathOrUrl,
                Duration = durationSeconds
            });

            if (!_isProcessing)
                _ = ProcessQueue();
        }

        #endregion
        #region Notification Logic

        private async System.Threading.Tasks.Task ProcessQueue()
        {
            _isProcessing = true;

            while (_queue.Count > 0)
            {
                var item = _queue.Dequeue();
                NotificationText.Text = item.Text;

                if (!string.IsNullOrEmpty(item.ImagePathOrUrl))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(item.ImagePathOrUrl, UriKind.RelativeOrAbsolute);
                        bitmap.EndInit();
                        NotificationImage.Source = bitmap;
                        NotificationImage.Visibility = Visibility.Visible;
                    }
                    catch
                    {
                        NotificationImage.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    NotificationImage.Visibility = Visibility.Collapsed;
                }

                NotificationBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                NotificationBorder.Arrange(new Rect(NotificationBorder.DesiredSize));
                ProgressContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                ProgressContainer.Arrange(new Rect(ProgressContainer.DesiredSize));

                double maxWidth = 200;
                ProgressBar.Width = 0;

                _visibleLeft = SystemParameters.WorkArea.Width - ActualWidth - 10;
                _hiddenLeft = SystemParameters.WorkArea.Width;

                var slideIn = new DoubleAnimation(_hiddenLeft, _visibleLeft, TimeSpan.FromMilliseconds(580))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(Window.LeftProperty, slideIn);

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(580))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                NotificationBorder.BeginAnimation(OpacityProperty, fadeIn);
                var progressAnim = new DoubleAnimation
                {
                    From = 0,
                    To = maxWidth,
                    Duration = TimeSpan.FromSeconds(item.Duration),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                ProgressBar.BeginAnimation(WidthProperty, progressAnim);

                _visible = true;
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(item.Duration));

                var slideOut = new DoubleAnimation(_visibleLeft, _hiddenLeft, TimeSpan.FromMilliseconds(580))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                BeginAnimation(Window.LeftProperty, slideOut);

                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(580))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                NotificationBorder.BeginAnimation(OpacityProperty, fadeOut);
                ProgressBar.Width = 0;
                _visible = false;

                await System.Threading.Tasks.Task.Delay(500);
            }

            _isProcessing = false;
        }

        #endregion
        #region Click Through

        private void MakeClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        #endregion

        private class NotificationItem
        {
            public string Text { get; set; }
            public string ImagePathOrUrl { get; set; }
            public double Duration { get; set; } = 5; // this is here as fallback so dw about it
        }
    }
}