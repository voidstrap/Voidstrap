using System.Windows;
using Voidstrap.UI.Elements.ContextMenu;
using Voidstrap.UI.Elements.Overlay;
using Voidstrap.UI.ViewModels.Settings;
using Wpf.Ui.Controls;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class ExtensionPage
    {
        public ExtensionPage()
        {
            InitializeComponent();
            DataContext = new ExtensionViewModel();
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