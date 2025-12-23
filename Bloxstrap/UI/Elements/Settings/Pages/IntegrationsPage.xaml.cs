using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Voidstrap.Integrations;
using Voidstrap.UI.ViewModels.Settings;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class IntegrationsPage
    {
        public IntegrationsPage()
        {
            InitializeComponent();
            ActivityWatcher watcher = new ActivityWatcher();
            DataContext = new IntegrationsViewModel(watcher);
        }

        private void ValidateUInt32(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !uint.TryParse(e.Text, out _);
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
    }
}
