using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Voidstrap.UI.ViewModels.Settings;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for SwiftTunnelPage.xaml
    /// </summary>
    public partial class SwiftTunnelPage
    {
        public SwiftTunnelPage()
        {
            InitializeComponent();
            DataContext = new SwiftTunnelViewModel();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelPage", $"Failed to open URL: {ex.Message}");
            }

            e.Handled = true;
        }
    }
}
