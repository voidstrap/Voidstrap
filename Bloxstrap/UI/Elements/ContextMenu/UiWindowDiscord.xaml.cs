using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Voidstrap.UI.Chat;

namespace Voidstrap.UI.Elements.Overlay
{
    public partial class DiscordChatOverlayWindow : Window
    {
        public DiscordChatViewModel ViewModel { get; }

        public DiscordChatOverlayWindow()
        {
            InitializeComponent();
            ViewModel = new DiscordChatViewModel();
            DataContext = ViewModel;
            Left = 5;
            Top = 60;

            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private void WindowDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (App.Current.Resources.Contains("DiscordChatOverlayWindow"))
                App.Current.Resources.Remove("DiscordChatOverlayWindow");
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Image img && img.Source != null)
            {
                var win = new Window
                {
                    Title = "Image Preview",
                    Width = 600,
                    Height = 600,
                    Content = new System.Windows.Controls.Image
                    {
                        Source = img.Source,
                        Stretch = System.Windows.Media.Stretch.Uniform
                    }
                };
                win.Show();
            }
        }

        private void Reaction_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBlock tb && tb.DataContext is DiscordChatViewModel.MessageReaction reaction)
            {
                var tab = ViewModel.SelectedTab;
                var msg = tab.Messages.LastOrDefault();
                if (msg != null)
                {
                    tab.ReactToMessage(msg, reaction.Emoji).ConfigureAwait(false);
                }
            }
        }

        private void MakeClickThrough(bool enable)
        {
            if (!enable) return;

            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, -20);
            SetWindowLong(hwnd, -20, style | 0x20 | 0x80);
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public static void ShowOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (App.Current.Resources["DiscordChatOverlayWindow"] is DiscordChatOverlayWindow existing)
                {
                    existing.Show();
                    return;
                }

                var overlay = new DiscordChatOverlayWindow();
                overlay.Show();
                App.Current.Resources["DiscordChatOverlayWindow"] = overlay;
            });
        }

        public static void CloseOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (App.Current.Resources["DiscordChatOverlayWindow"] is DiscordChatOverlayWindow overlay)
                {
                    overlay.Close();
                }
            });
        }
    }
}