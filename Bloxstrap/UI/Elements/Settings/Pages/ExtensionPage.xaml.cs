using System.Windows;
using Voidstrap.UI.Elements.ContextMenu;
using Voidstrap.UI.Elements.Overlay;
using Voidstrap.UI.ViewModels.Settings;
using Wpf.Ui.Controls;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class ExtensionPage
    {
        private ExtensionViewModel _vm;
        public ExtensionPage()
        {
            InitializeComponent();

            _vm = new ExtensionViewModel();
            DataContext = _vm;
            _vm.OnProgressChanged += Vm_OnProgressChanged;
        }

        private void Vm_OnProgressChanged(string title, bool show)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressTitle.Text = title;
                ProgressOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            _vm?.CancelDownload();
        }

        private void OpenFleasion_Click(object sender, RoutedEventArgs e)
        {
            string fleasionDir = Path.Combine(Paths.Base, "Fleasion");
            string fleasionExe = Path.Combine(fleasionDir, "Fleasion.exe");
            if (Directory.Exists(fleasionDir) && File.Exists(fleasionExe))
            {
                try
                {
                    var running = Process.GetProcessesByName("Fleasion");
                    if (running.Length == 0)
                    {
                        Process.Start(fleasionExe);
                    }
                }
                catch (Exception ex)
                {
                    Frontend.ShowMessageBox("Failed to open Fleasion: " + ex.Message);
                }
            }
            else
            {
                Frontend.ShowMessageBox("Fleasion Extension is not Enabled/Installed");
            }
        }


        private void OpenAniWatchWindow_Click(object sender, RoutedEventArgs e)
        {
            var animeWindow = new AnimeWindow
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            animeWindow.Show();
        }
    }
}