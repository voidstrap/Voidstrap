using System.Windows.Controls;

using Voidstrap.UI.ViewModels.Settings;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for IntegrationsPage.xaml
    /// </summary>
    public partial class IntegrationsPage
    {
        public IntegrationsPage()
        {
            DataContext = new IntegrationsViewModel();
            InitializeComponent();
        }

        public void CustomIntegrationSelection(object sender, SelectionChangedEventArgs e)
        {
            IntegrationsViewModel viewModel = (IntegrationsViewModel)DataContext;
            viewModel.SelectedCustomIntegration = (CustomIntegration)((ListBox)sender).SelectedItem;
            viewModel.OnPropertyChanged(nameof(viewModel.SelectedCustomIntegration));
        }

        private void ToggleSwitch_Checked(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void ActivityTrackingOption_Loaded()
        {

        }
    }
}
