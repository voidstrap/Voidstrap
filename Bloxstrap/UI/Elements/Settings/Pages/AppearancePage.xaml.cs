using Voidstrap.UI.ViewModels.Settings;
using System.Windows.Controls;
using System.Windows;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class AppearancePage
    {
        private AppearanceViewModel _appearanceViewModel;

        public AppearancePage()
        {
            InitializeComponent();
            DataContext = new AppearanceViewModel();
        }

        #region Existing Theme Logic

        public void CustomThemeSelection(object sender, SelectionChangedEventArgs e)
        {
            _appearanceViewModel.SelectedCustomTheme = (string)((ListBox)sender).SelectedItem;
            _appearanceViewModel.SelectedCustomThemeName = _appearanceViewModel.SelectedCustomTheme;

            _appearanceViewModel.OnPropertyChanged(nameof(_appearanceViewModel.SelectedCustomTheme));
            _appearanceViewModel.OnPropertyChanged(nameof(_appearanceViewModel.SelectedCustomThemeName));
        }

        private bool isThemeInitialized = false;

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isThemeInitialized)
            {
                isThemeInitialized = true;
                return;
            }

            Frontend.ShowMessageBox("This feature is buggy right now so reset the app to apply the new theme!");
        }

        #endregion
    }
}
