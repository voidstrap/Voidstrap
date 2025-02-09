using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using Hellstrap.UI.ViewModels.Settings;
using Wpf.Ui.Common;
using Hellstrap.UI.Elements.Dialogs;

namespace Hellstrap.UI.Elements.Settings
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;

        public MainWindow(bool showAlreadyRunningWarning)
        {
            InitializeComponent();
            InitializeViewModel();
            InitializeWindowState();
            InitializeNavigation();

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

            if (showAlreadyRunningWarning)
                _ = ShowAlreadyRunningSnackbar();
        }

        /// <summary>
        /// Initializes the ViewModel and event handlers.
        /// </summary>
        private void InitializeViewModel()
        {
            var viewModel = new MainWindowViewModel();
            DataContext = viewModel;

            viewModel.RequestSaveNoticeEvent += OnRequestSaveNotice;
            viewModel.RequestSaveLaunchNoticeEvent += OnRequestSaveLaunchNotice;
            viewModel.RequestCloseWindowEvent += OnRequestCloseWindow;
        }

        /// <summary>
        /// Handles save notice event.
        /// </summary>
        private void OnRequestSaveNotice(object? sender, EventArgs e) // Added nullable reference type
        {
            SettingsSavedSnackbar.Show();
        }

        private void OnRequestSaveLaunchNotice(object? sender, EventArgs e) // Added nullable reference type
        {
            SettingsSavedLaunchSnackbar.Show();
        }

        /// <summary>
        /// Handles close window event.
        /// </summary>
        private async void OnRequestCloseWindow(object? sender, EventArgs e) // Added nullable reference type
        {
            await Task.Yield(); // Explicitly await to fix "async method lacks await"
            Close();
        }

        /// <summary>
        /// Restores the window state based on saved settings.
        /// </summary>
        private void InitializeWindowState()
        {
            // Ensure the window is within screen bounds
            if (_state.Left > SystemParameters.VirtualScreenWidth) _state.Left = 0;
            if (_state.Top > SystemParameters.VirtualScreenHeight) _state.Top = 0;

            if (_state.Width > 0) Width = _state.Width;
            if (_state.Height > 0) Height = _state.Height;

            if (_state.Left > 0 && _state.Top > 0)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = _state.Left;
                Top = _state.Top;
            }
        }

        /// <summary>
        /// Initializes navigation and restores the last selected page.
        /// </summary>
        private void InitializeNavigation()
        {
            RootNavigation.SelectedPageIndex = App.State.Prop.LastPage;
            RootNavigation.Navigated += SaveNavigation;
        }

        /// <summary>
        /// Saves the last visited navigation page index.
        /// </summary>
        private void SaveNavigation(INavigation sender, RoutedNavigationEventArgs e)
        {
            App.State.Prop.LastPage = RootNavigation.SelectedPageIndex;
        }

        /// <summary>
        /// Displays the "Already Running" snackbar after a brief delay.
        /// </summary>
        private async Task ShowAlreadyRunningSnackbar()
        {
            await Task.Delay(225).ConfigureAwait(false); // Ensure async execution
            Dispatcher.Invoke(() => AlreadyRunningSnackbar.Show());
        }

        #region INavigationWindow Implementation

        public Frame GetFrame() => RootFrame;
        public INavigation GetNavigation() => RootNavigation;
        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;
        public void ShowWindow() => Show();
        public void CloseWindow() => Close();

        #endregion

        /// <summary>
        /// Handles window closing, ensuring unsaved changes are confirmed.
        /// </summary>
        private void WpfUiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (App.FastFlags.Changed || App.PendingSettingTasks.Any())
            {
                var result = Frontend.ShowMessageBox(
                    Strings.Menu_UnsavedChanges,
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo
                );

                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // Save window state
            SaveWindowState();
        }

        /// <summary>
        /// Saves the window state to the application settings.
        /// </summary>
        private void SaveWindowState()
        {
            _state.Width = Width;
            _state.Height = Height;
            _state.Top = Top;
            _state.Left = Left;

            App.State.Save();
        }

        /// <summary>
        /// Handles post-close logic.
        /// </summary>
        private void WpfUiWindow_Closed(object sender, EventArgs e)
        {
            if (App.LaunchSettings.TestModeFlag.Active)
                LaunchHandler.LaunchRoblox(LaunchMode.Player);
            else
                App.SoftTerminate();
        }

        /// <summary>
        /// Placeholder event handlers.
        /// </summary>
        private void NavigationItem_Click(object sender, RoutedEventArgs e) { }
        private void Button_Click(object sender, RoutedEventArgs e) { }
        private void NavigationItem_Click_1(object sender, RoutedEventArgs e) { }

        private void Button_Click_1(object sender, RoutedEventArgs e) { }
    }
}
