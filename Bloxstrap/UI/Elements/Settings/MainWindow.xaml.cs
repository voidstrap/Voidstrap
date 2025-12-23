using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Voidstrap.Integrations;
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
        private readonly Random _snowRandom = new();
        private readonly List<Snowflake> _snowflakes = new();
        private readonly DispatcherTimer _snowTimer;
        private readonly DispatcherTimer _visibilityTimer = new DispatcherTimer();

        public MainWindow(bool showAlreadyRunningWarning)
        {
            InitializeComponent();
            InitializeViewModel();
            InitializeWindowState();
            UpdateButtonContent();
            // shi finna be laggy :sob:
            _visibilityTimer.Interval = TimeSpan.FromSeconds(0.8);
            _visibilityTimer.Tick += (s, e) => UpdateFastFlagEditorVisibility();
            _visibilityTimer.Start();
            _snowTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _snowTimer.Tick += SnowTimer_Tick;

            Loaded += MainWindow_Loaded;
            SizeChanged += MainWindow_SizeChanged;

            App.Logger.WriteLine("MainWindow", "Initializing settings window");
            if (showAlreadyRunningWarning)
                _ = ShowAlreadyRunningSnackbarAsync();
        }

        private void UpdateFastFlagEditorVisibility()
        {
            if (FastFlagEditorNavItem == null)
                return;

            var shouldBeVisible = !App.Settings.Prop.LockDefault;
            if (FastFlagEditorNavItem.Visibility == (shouldBeVisible ? Visibility.Visible : Visibility.Collapsed))
                return;

            FastFlagEditorNavItem.Visibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
           InitializeNavigation();
            if (App.Settings.Prop.SnowWOWSOCOOLWpfSnowbtw)
            {
                InitSnow();
                _snowTimer.Start();
                if (SnowCanvas != null)
                    SnowCanvas.Visibility = Visibility.Visible;
            }
            else
            {
                if (SnowCanvas != null)
                    SnowCanvas.Visibility = Visibility.Collapsed;
            }

            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

            var storyboard = TryFindResource("IntroStoryboard") as Storyboard;
            if (storyboard != null)
            {
                storyboard.Completed += (_, _) =>
                {
                    IntroOverlay.Visibility = Visibility.Collapsed;
                    IntroOverlay.Opacity = 1.0;
                };

                IntroOverlay.Visibility = Visibility.Visible;
                storyboard.Begin(IntroOverlay, true);
            }
            else
            {
                IntroOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private Size _lastSnowCanvasSize = Size.Empty;
        private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (SnowCanvas == null)
                return;

            var newSize = new Size(SnowCanvas.ActualWidth, SnowCanvas.ActualHeight);
            if (newSize.Width <= 0 || newSize.Height <= 0)
                return;
            const double minDelta = 20.0;
            if (Math.Abs(newSize.Width - _lastSnowCanvasSize.Width) < minDelta &&
                Math.Abs(newSize.Height - _lastSnowCanvasSize.Height) < minDelta)
                return;

            _lastSnowCanvasSize = newSize;
            InitSnow();
        }

        private const int FlakeCount = 60;
        private void InitSnow()
        {
            if (SnowCanvas == null) return;

            double width = SnowCanvas.ActualWidth;
            double height = SnowCanvas.ActualHeight;

            if (width <= 0 || height <= 0)
                return;

            if (_snowflakes.Count == FlakeCount)
                return;

            _snowflakes.Clear();
            SnowCanvas.Children.Clear();

            for (int i = 0; i < FlakeCount; i++)
            {
                double size = _snowRandom.Next(2, 6);
                var ellipse = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = Brushes.White,
                    Opacity = _snowRandom.NextDouble() * 0.6 + 0.3
                };
                SnowCanvas.Children.Add(ellipse);

                _snowflakes.Add(new Snowflake
                {
                    Shape = ellipse,
                    X = _snowRandom.NextDouble() * width,
                    Y = _snowRandom.NextDouble() * height,
                    SpeedY = 0.7 + _snowRandom.NextDouble() * 1.5,
                    DriftX = -0.3 + _snowRandom.NextDouble() * 0.6,
                    Size = size
                });
            }
        }

        private void UpdateSnow()
        {
            if (SnowCanvas == null) return;

            double width = SnowCanvas.ActualWidth;
            double height = SnowCanvas.ActualHeight;

            for (int i = 0; i < _snowflakes.Count; i++)
            {
                var flake = _snowflakes[i];
                flake.Y += flake.SpeedY;
                flake.X += flake.DriftX;

                if (flake.Y > height + flake.Size) flake.Y = -flake.Size;
                if (flake.X < -flake.Size) flake.X = width + flake.Size;
                else if (flake.X > width + flake.Size) flake.X = -flake.Size;

                Canvas.SetLeft(flake.Shape, flake.X);
                Canvas.SetTop(flake.Shape, flake.Y);
            }
        }

        private void SnowTimer_Tick(object? sender, EventArgs e)
        {
            UpdateSnow();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (App.Settings.Prop.SnowWOWSOCOOLWpfSnowbtw)
                _snowTimer.Start();
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            _snowTimer.Stop();
        }

        private sealed class Snowflake
        {
            public Ellipse Shape { get; set; } = null!;
            public double X { get; set; }
            public double Y { get; set; }
            public double SpeedY { get; set; }
            public double DriftX { get; set; }
            public double Size { get; set; }
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

        private void NavigationItem_Click(object sender, RoutedEventArgs e) { }
        private void NavigationItem_Click_1(object sender, RoutedEventArgs e) { }


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // Save Lua script when Save button is clicked
            SaveLuaScript();
        }


        private void Button_Click_2(object sender, RoutedEventArgs e) { }

        private void SaveLuaScript()
        {
            try
            {
                // Find the ModsPage in the navigation
                var modsPage = RootNavigation?.Items
                    .OfType<Wpf.Ui.Controls.NavigationItem>()
                    .FirstOrDefault(item => item.PageType == typeof(Pages.ModsPage));

                if (modsPage != null && RootFrame.Content is Pages.ModsPage page)
                {
                    page.SaveCurrentLuaScript();
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine("MainWindow::SaveLuaScript", $"Error saving Lua script: {ex.Message}");
            }
        }

        #endregion
    }
}
