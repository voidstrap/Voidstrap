using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Voidstrap.UI.Elements.Dialogs;
using Voidstrap.UI.ViewModels.Settings;
using Wpf.Ui.Common;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using static System.Net.Mime.MediaTypeNames;

namespace Voidstrap.UI.Elements.Settings
{
    public partial class MainWindow : INavigationWindow
    {
        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;
        private bool _isSaveAndLaunchClicked = false;

        public MainWindow(bool showAlreadyRunningWarning)
        {
            InitializeComponent();
            InitializeViewModel();
            InitializeWindowState();
            InitializeNavigation();
            UpdateButtonContent();
            App.Logger.WriteLine("MainWindow", "Initializing settings window");
            if (showAlreadyRunningWarning)
                _ = ShowAlreadyRunningSnackbarAsync();
        }



        #region Initialization

        private void InitializeViewModel()
        {
            var viewModel = new MainWindowViewModel();
            DataContext = viewModel;

            viewModel.RequestSaveNoticeEvent += OnRequestSaveNotice;
            viewModel.RequestSaveLaunchNoticeEvent += OnRequestSaveLaunchNotice;
            viewModel.RequestCloseWindowEvent += OnRequestCloseWindow;
        }

        private void UpdateButtonContent()
        {
            if (InstallLaunchButton == null)
                return;

            string versionsPath = Paths.Versions;

            InstallLaunchButton.Content =
                (Directory.Exists(versionsPath) && Directory.EnumerateFileSystemEntries(versionsPath).Any())
                    ? "Save and Launch"
                    : "Install";
        }


        private void InitializeWindowState()
        {
            if (_state.Left > SystemParameters.VirtualScreenWidth || _state.Top > SystemParameters.VirtualScreenHeight)
            {
                _state.Left = 0;
                _state.Top = 0;
            }

            if (_state.Width > 0) Width = _state.Width;
            if (_state.Height > 0) Height = _state.Height;

            if (_state.Left > 0 && _state.Top > 0)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = _state.Left;
                Top = _state.Top;
            }
        }

        private void InitializeNavigation()
        {
            if (RootNavigation == null)
                return;

            RootNavigation.SelectedPageIndex = App.State.Prop.LastPage;
            RootNavigation.Navigated += SaveNavigation;
        }


        #endregion

        #region Snackbar Events

        private void OnRequestSaveNotice(object? sender, EventArgs e)
        {
            if (!_isSaveAndLaunchClicked)
                SettingsSavedSnackbar.Show();
        }

        private void OnRequestSaveLaunchNotice(object? sender, EventArgs e)
        {
            if (!_isSaveAndLaunchClicked)
                SettingsSavedLaunchSnackbar.Show();
        }

        private async Task ShowAlreadyRunningSnackbarAsync()
        {
            await Task.Delay(225);
            if (!Dispatcher.HasShutdownStarted)
                Dispatcher.InvokeAsync(() => AlreadyRunningSnackbar?.Show());
        }


        #endregion

        #region ViewModel Events

        private async void OnRequestCloseWindow(object? sender, EventArgs e)
        {
            await Task.Yield();
            Close();
        }

        private void OnSaveAndLaunchButtonClick(object sender, EventArgs e)
        {
            _isSaveAndLaunchClicked = true;
        }

        #endregion

        #region Window Events

        private void WpfUiWindow_Closing(object sender, CancelEventArgs e)
        {
            SaveWindowState();
        }

        private void WpfUiWindow_Closed(object sender, EventArgs e)
        {
            if (App.LaunchSettings.TestModeFlag.Active)
                LaunchHandler.LaunchRoblox(LaunchMode.Player);
            else
                App.SoftTerminate();
        }

        private void SaveWindowState()
        {
            _state.Width = Width;
            _state.Height = Height;
            _state.Top = Top;
            _state.Left = Left;

            App.State.Save();
        }

        #endregion

        #region Navigation

        private void SaveNavigation(INavigation sender, RoutedNavigationEventArgs e)
        {
            App.State.Prop.LastPage = RootNavigation.SelectedPageIndex;
        }

        #endregion

        #region INavigationWindow Implementation

        public Frame GetFrame() => RootFrame;
        public INavigation GetNavigation() => RootNavigation;
        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;
        public void ShowWindow() => Show();
        public void CloseWindow() => Close();

        #endregion

        #region Placeholder Events

        // TODO: Implement these handlers or remove if unused
        private void NavigationItem_Click(object sender, RoutedEventArgs e) { }
        private void NavigationItem_Click_1(object sender, RoutedEventArgs e) { }
        private void Button_Click(object sender, RoutedEventArgs e) { }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
        }


        private void Button_Click_2(object sender, RoutedEventArgs e) { }

        #endregion
    }
}
