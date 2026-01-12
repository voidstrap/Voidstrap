using DiscordRPC;
using DiscordRPC.Logging;
using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Voidstrap.Integrations;
using Voidstrap.UI.Elements.Dialogs;
using Voidstrap.UI.Elements.Settings.Pages;
using Voidstrap.UI.ViewModels.Settings;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using Path = System.IO.Path;

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
        private DiscordRpcClient? _discordClient;
        private bool _discordRpcEnabled = App.Settings.Prop.VoidRPC;
        private AppearanceViewModel _appearanceViewModel;
        private DispatcherTimer _backgroundUpdateTimer;
        private string? _currentBackgroundPath;
        private FileSystemWatcher? _appearanceViewModelWatcher;
        private Vector _currentOffset;
        private Vector _targetOffset;
        private double _currentRotation;
        private double _targetRotation;
        private const double MaxOffset = 0.04;
        private const double MaxRotation = 5.0;
        private const double FollowSpeed = 0.035;

        public MainWindow(bool showAlreadyRunningWarning)
        {
            InitializeComponent();
            InitializeViewModel();
            InitializeWindowState();
            UpdateButtonContent();
            InitializeDiscordRPC();
            _appearanceViewModel = new AppearanceViewModel();
            InitializeBackgroundSettingsWatcher();
            ApplyBackgroundSettings();
            // shi finna be laggy :sob:
            _visibilityTimer.Interval = TimeSpan.FromSeconds(0.8);
            _visibilityTimer.Tick += (s, e) => UpdateFastFlagEditorVisibility();
            _visibilityTimer.Start();
            _snowTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _snowTimer.Tick += SnowTimer_Tick;
            _currentBackgroundPath = _appearanceViewModel.BackgroundFilePath;

            _backgroundUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _backgroundUpdateTimer.Tick += BackgroundUpdateTimer_Tick;
            _backgroundUpdateTimer.Start();

            Loaded += MainWindow_Loaded;
            SizeChanged += MainWindow_SizeChanged;

            App.Logger.WriteLine("MainWindow", "Initializing settings window");
            if (showAlreadyRunningWarning)
                _ = ShowAlreadyRunningSnackbarAsync();
        }

        private void AnimateOpacity(UIElement element, double toOpacity, double durationSeconds = 0.5)
        {
            if (element == null) return;

            var animation = new DoubleAnimation
            {
                To = toOpacity,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private void InitializeBackgroundSettingsWatcher()
        {
            string filePath = Path.Combine(Paths.Base, "backgroundSettings.json");
            string? directory = Path.GetDirectoryName(filePath);
            string? fileName = Path.GetFileName(filePath);

            if (directory == null || fileName == null)
                return;

            _appearanceViewModelWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _appearanceViewModelWatcher.Changed += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var newSettings = new AppearanceViewModel();

                        _appearanceViewModel.BackgroundFilePath = newSettings.BackgroundFilePath;
                        _appearanceViewModel.GradientOpacity = newSettings.GradientOpacity;
                    }
                    catch {}
                });
            };

            _appearanceViewModelWatcher.EnableRaisingEvents = true;
        }

        private void BackgroundUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_appearanceViewModel == null) return;

            string? newPath = _appearanceViewModel.BackgroundFilePath;

            if (newPath != _currentBackgroundPath)
            {
                SetBackgroundImage(newPath);
            }

            if (_gradientLayerOpacity != _appearanceViewModel.GradientOpacity)
                GradientLayerOpacity = _appearanceViewModel.GradientOpacity;
        }

        private void ApplyBackgroundSettings()
        {
            if (!string.IsNullOrEmpty(_appearanceViewModel.BackgroundFilePath))
                SetBackgroundImage(_appearanceViewModel.BackgroundFilePath);

            GradientLayerOpacity = _appearanceViewModel.GradientOpacity;
        }

        private double _gradientLayerOpacity = 0;
        public double GradientLayerOpacity
        {
            get => _gradientLayerOpacity;
            set
            {
                if (_gradientLayerOpacity != value)
                {
                    _gradientLayerOpacity = value;

                    if (GradientLayer != null)
                        AnimateOpacity(GradientLayer, _gradientLayerOpacity);

                    if (_appearanceViewModel != null)
                        _appearanceViewModel.GradientOpacity = _gradientLayerOpacity;
                }
            }
        }

        public async Task SetBackgroundImage(string? path, bool loop = true)
        {
            if (BackgroundImage == null || BackgroundMedia == null || GradientLayer == null)
                return;

            if (BackgroundImage.Visibility == Visibility.Visible)
                await FadeOutElementAsync(BackgroundImage, 0.3);

            if (BackgroundMedia.Visibility == Visibility.Visible)
            {
                BackgroundMedia.Stop();
                BackgroundMedia.MediaEnded -= BackgroundMedia_MediaEnded;
                await FadeOutElementAsync(BackgroundMedia, 0.3);
            }

            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(BackgroundImage, null);

            AnimateOpacity(GradientLayer, _appearanceViewModel?.GradientOpacity ?? 0.5);
            GradientLayer.Visibility = Visibility.Visible;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _currentBackgroundPath = null;
                BackgroundImage.Visibility = Visibility.Collapsed;
                BackgroundMedia.Visibility = Visibility.Collapsed;
                return;
            }

            _currentBackgroundPath = path;
            string ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                BackgroundImage.Source = bitmap;
                BackgroundImage.Visibility = Visibility.Visible;
                BackgroundMedia.Visibility = Visibility.Collapsed;

                await FadeInElementAsync(BackgroundImage, 0.5);
            }
            else if (ext == ".gif")
            {
                var gifSource = new BitmapImage(new Uri(path, UriKind.Absolute));
                WpfAnimatedGif.ImageBehavior.SetAnimatedSource(BackgroundImage, gifSource);
                WpfAnimatedGif.ImageBehavior.SetRepeatBehavior(
                    BackgroundImage,
                    loop ? System.Windows.Media.Animation.RepeatBehavior.Forever : new System.Windows.Media.Animation.RepeatBehavior(1)
                );

                BackgroundImage.Visibility = Visibility.Visible;
                BackgroundMedia.Visibility = Visibility.Collapsed;

                await FadeInElementAsync(BackgroundImage, 0.5);
            }
            else if (ext is ".mp4" or ".webm" or ".avi" or ".mov")
            {
                BackgroundMedia.Source = new Uri(path, UriKind.Absolute);
                BackgroundMedia.Visibility = Visibility.Visible;
                BackgroundImage.Visibility = Visibility.Collapsed;
                BackgroundMedia.LoadedBehavior = MediaState.Manual;
                BackgroundMedia.UnloadedBehavior = MediaState.Stop;
                BackgroundMedia.Volume = 0;

                if (loop)
                    BackgroundMedia.MediaEnded += BackgroundMedia_MediaEnded;

                BackgroundMedia.Play();
                await FadeInElementAsync(BackgroundMedia, 0.5);
            }
        }

        private Task FadeOutElementAsync(UIElement element, double durationSeconds)
        {
            if (element == null) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            var animation = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            animation.Completed += (s, e) =>
            {
                element.Visibility = Visibility.Collapsed;
                tcs.SetResult(true);
            };
            element.BeginAnimation(UIElement.OpacityProperty, animation);
            return tcs.Task;
        }

        private Task FadeInElementAsync(UIElement element, double durationSeconds)
        {
            if (element == null) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            element.Visibility = Visibility.Visible;

            var animation = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            animation.Completed += (s, e) => tcs.SetResult(true);
            element.BeginAnimation(UIElement.OpacityProperty, animation);

            return tcs.Task;
        }

        private void BackgroundMedia_MediaEnded(object? sender, RoutedEventArgs e)
        {
            if (sender is MediaElement media)
            {
                media.Position = TimeSpan.Zero;
                media.Play();
            }
        }

        private void RootGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not FrameworkElement fe)
                return;

            var pos = e.GetPosition(fe);
            var nx = (pos.X / fe.ActualWidth - 0.5) * 2;
            var ny = (pos.Y / fe.ActualHeight - 0.5) * 2;

            _targetOffset = new Vector(
                nx * MaxOffset,
                ny * MaxOffset
            );

            _targetRotation = nx * MaxRotation;
        }

        private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            _targetOffset = new Vector(0, 0); // blah this just resets the values all back to normal value 0 :)
            _targetRotation = 0;
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            _currentOffset += (_targetOffset - _currentOffset) * FollowSpeed;
            _currentRotation += (_targetRotation - _currentRotation) * FollowSpeed;

            BackgroundGradientTranslate.X = _currentOffset.X;
            BackgroundGradientTranslate.Y = _currentOffset.Y;
            BackgroundGradientRotate.Angle = _currentRotation;
        }

        private void InitializeDiscordRPC()
        {
            _discordClient = new DiscordRpcClient("1459679943498661910");

            _discordClient.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
            _discordClient.OnReady += (sender, e) =>
            {
                App.Logger.WriteLine("DiscordRPC", $"Connected to Discord as {e.User.Username}");
            };

            _discordClient.OnError += (sender, e) =>
            {
                App.Logger.WriteLine("DiscordRPC", $"DiscordRPC Error: {e.Message}");
            };

            _discordClient.Initialize();

            if (RootNavigation != null)
            {
                RootNavigation.Navigated += (s, e) => UpdateDiscordPresence();
            }

            UpdateDiscordPresence();
        }

        private string GetCurrentPageName()
        {
            if (RootNavigation == null)
                return "Idle";

            object? selectedItem = null;
            if (RootNavigation.Items != null &&
                RootNavigation.SelectedPageIndex >= 0 &&
                RootNavigation.SelectedPageIndex < RootNavigation.Items.Count)
            {
                selectedItem = RootNavigation.Items[RootNavigation.SelectedPageIndex];
            }

            if (selectedItem is Wpf.Ui.Controls.NavigationItem navItem)
            {
                if (!string.IsNullOrWhiteSpace(navItem.Content?.ToString()))
                    return navItem.Content!.ToString();

                if (navItem.PageType != null)
                    return navItem.PageType.Name;
            }

            if (RootFrame?.Content != null)
            {
                return RootFrame.Content.GetType().Name;
            }

            return "Idle";
        }

        public void ToggleDiscordRPC(bool enabled)
        {
            _discordRpcEnabled = enabled;

            if (_discordClient == null) return;

            if (!_discordRpcEnabled)
            {
                _discordClient.ClearPresence();
                App.Logger.WriteLine("DiscordRPC", "DiscordRPC disabled.");
            }
            else
            {
                UpdateDiscordPresence();
                App.Logger.WriteLine("DiscordRPC", "DiscordRPC enabled.");
            }
        }

        private void UpdateDiscordPresence()
        {
            if (_discordClient == null || !_discordRpcEnabled) return;

            string pageName = GetCurrentPageName();

            _discordClient.SetPresence(new DiscordRPC.RichPresence()
            {
                Details = $"Viewing {pageName}",
                State = "Voidstrap",
                Timestamps = DiscordRPC.Timestamps.Now,
                Buttons = new[]
                {
            new DiscordRPC.Button
            {
                Label = "Discord",
                Url = "https://discord.gg/bzdbHHytFR"
            },
            new DiscordRPC.Button
            {
                Label = "Repo",
                Url = "https://github.com/voidstrap/Voidstrap"
            }
        }
            });
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
            if (App.Settings.Prop.GRADmentFR)
            {
                CompositionTarget.Rendering += CompositionTarget_Rendering;
            }
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

        private const int FlakeCount = 40;
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
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
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
            UpdateDiscordPresence();
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
        }


        private void Button_Click_2(object sender, RoutedEventArgs e) { }

        #endregion
    }
}
