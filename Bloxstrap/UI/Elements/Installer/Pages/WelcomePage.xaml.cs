using System.Windows;
using System.Windows.Navigation;
using Voidstrap.UI.ViewModels.Installer;

namespace Voidstrap.UI.Elements.Installer.Pages
{
    /// <summary>
    /// Interaction logic for WelcomePage.xaml
    /// </summary>
    public partial class WelcomePage
    {
        private readonly WelcomeViewModel _viewModel = new();

        public WelcomePage()
        {
                if (Window.GetWindow(this) is MainWindow window)
                    window.SetButtonEnabled("next", true);

            DataContext = _viewModel;
            InitializeComponent();
        }

        private void UiPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow window)
                window.SetNextButtonText(Strings.Common_Navigation_Next);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        private void DonateButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://voidstrapp.netlify.app/donate/donate") { UseShellExecute = true });
        }
        private void ContributorsButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://voidstrapp.netlify.app/contributors/contributors") { UseShellExecute = true });
        }
    }
}
