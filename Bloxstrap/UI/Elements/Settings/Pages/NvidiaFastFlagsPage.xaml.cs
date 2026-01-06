using System.Windows;
using Voidstrap.UI.ViewModels.Settings;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class NvidiaFastFlagsPage
    {
        private readonly NvidiaFastFlagsViewModel _viewModel;

        public NvidiaFastFlagsPage()
        {
            InitializeComponent();
            _viewModel = new NvidiaFastFlagsViewModel();
            DataContext = _viewModel;
        }

        private void OpenRawEditor_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new NvidiaFFlagEditorPage());
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Apply();
        }

        private void OpenFastFlagSettings_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new FastFlagsPage());
        }
    }
}
