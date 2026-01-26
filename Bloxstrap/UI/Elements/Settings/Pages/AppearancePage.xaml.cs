using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Voidstrap.Enums;
using Voidstrap.UI.Elements.ContextMenu;
using Voidstrap.UI.ViewModels.Settings;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class AppearancePage
    {
        private readonly AppearanceViewModel _appearanceViewModel;
        private bool isThemeInitialized = false;

        public AppearancePage()
        {
            InitializeComponent();

            _appearanceViewModel = new AppearanceViewModel();
            DataContext = _appearanceViewModel;
            _ = DownloadCustomThemeAsync();
        }

        #region Existing Theme Logic

        public void CustomThemeSelection(object sender, SelectionChangedEventArgs e)
        {
            _appearanceViewModel.SelectedCustomTheme = (string)((ListBox)sender).SelectedItem;
            _appearanceViewModel.SelectedCustomThemeName = _appearanceViewModel.SelectedCustomTheme;

            _appearanceViewModel.OnPropertyChanged(nameof(_appearanceViewModel.SelectedCustomTheme));
            _appearanceViewModel.OnPropertyChanged(nameof(_appearanceViewModel.SelectedCustomThemeName));
        }

        private async void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isThemeInitialized)
            {
                isThemeInitialized = true;
                return;
            }

            Frontend.ShowMessageBox(
                "Theme applied!\nIf the theme didn't apply, please restart Voidstrap.",
                MessageBoxImage.Information
            );
        }

        private void OptionControl_Loaded(object sender, RoutedEventArgs e)
        {
            var root = (DependencyObject)sender;

            var combo = FindChild<ComboBox>(root);
            var button = FindChild<Button>(root);

            if (combo == null || button == null)
                return;

            void Update()
            {
                button.Visibility =
                    combo.SelectedItem?.ToString() == "Custom"
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            }

            combo.SelectionChanged += (_, __) => Update();

            Update();
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T match)
                    return match;

                var found = FindChild<T>(child);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void OpenCustomThemeEditor_Click(object sender, RoutedEventArgs e)
        {
            var editor = new CustomThemeEditor();
            editor.Owner = Window.GetWindow(this);
            editor.ShowDialog();
        }

        #endregion

        #region Custom Theme Download

        private async Task DownloadCustomThemeAsync()
        {
            var url = "https://raw.githubusercontent.com/KloBraticc/VoidstrapCustomThemes/main/Custom.xaml";
            var destinationPath = Path.Combine(Paths.Base, "Custom.xaml");

            try
            {
                if (!File.Exists(destinationPath))
                {
                    using var http = new HttpClient();
                    var xaml = await http.GetStringAsync(url);

                    Directory.CreateDirectory(Paths.Base);
                    await File.WriteAllTextAsync(destinationPath, xaml);
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    $"Failed to download custom theme:\n{ex.Message}",
                    MessageBoxImage.Warning
                );
            }
        }

        #endregion
    }
}
