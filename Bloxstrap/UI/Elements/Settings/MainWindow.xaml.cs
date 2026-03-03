using DiscordRPC;
using DiscordRPC.Logging;
using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.ObjectModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Voidstrap.Integrations;
using Voidstrap.UI.Elements.Base;
using Voidstrap.UI.Elements.Controls;
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
        private bool _spotifyInitialized = false;
        private Vector _currentOffset;
        private Vector _targetOffset;
        private double _currentRotation;
        private double _targetRotation;
        private DispatcherTimer _searchDebounceTimer;
        private List<TextBlock> _allTextBlocksCache = new List<TextBlock>();
        private Page _lastPage = null;
        private const double MaxOffset = 0.04;
        private const double MaxRotation = 5.0;
        private const double FollowSpeed = 0.035;
        private string TabsConfigPath => Path.Combine(Paths.Base, "TabsConfig.json");
        private readonly List<Type> _pagesToHideSearchBox = new List<Type> // idfk my lazy bum ass didnt wanna spent 4000hours tranna figure another way for all tis bullshit of work took me 1 day for this shit FAHHHHHHHHHH WSEIEWMIEWOMHGEW
        {
        typeof(FastFlagEditorPage),
        typeof(NewsPage),
        typeof(NvidiaFFlagEditorPage),
        typeof(ReleasesPage),
        typeof(DonoPage),
        typeof(ServerBrowserPage),
        };

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
            GlobalSearchBox.TextChanged += GlobalSearchBox_TextChanged;
            GlobalSearchBox.LostFocus += GlobalSearchBox_LostFocus;
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

            RootFrame.Navigated += RootFrame_Navigated;

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

            if (DataContext is MainWindowViewModel vm && vm.Tabs == null)
                vm.Tabs = new ObservableCollection<TabItemViewModel>();

            if (showAlreadyRunningWarning)
                _ = ShowAlreadyRunningSnackbarAsync();
        }

        private void SaveTabsStructure()
        {
            if (DataContext is not MainWindowViewModel vm) return;

            var blueprint = vm.Tabs.Select(tab => new TabBlueprint
            {
                Title = tab.Title,
                Headers = GetHeaderControls(tab),
                Options = GetOptionControls(tab)
            }).ToList();

            string json = JsonSerializer.Serialize(blueprint, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(TabsConfigPath, json);
        }

        private List<HeaderControlData> GetHeaderControls(TabItemViewModel tab)
        {
            var list = new List<HeaderControlData>();
            if (tab.PageInstance?.Content is not Grid root) return list;

            var headers = root.Children.OfType<StackPanel>().FirstOrDefault();
            if (headers == null) return list;

            foreach (var child in headers.Children)
            {
                if (child is System.Windows.Controls.Button btn)
                {
                    string content = btn.Content?.ToString();
                    if (content == "X" || content == "+") continue;
                }

                if (child is System.Windows.Controls.TextBox tb)
                {
                    list.Add(new HeaderControlData
                    {
                        Type = "TextBox",
                        Text = tb.Text,
                        Width = tb.Width,
                        Height = tb.Height,
                        Margin = new SerializableThickness(tb.Margin)
                    });
                }
                else if (child is System.Windows.Controls.Button actionBtn)
                {
                    list.Add(new HeaderControlData
                    {
                        Type = "Button",
                        Text = actionBtn.Content?.ToString() ?? "",
                        Width = actionBtn.Width,
                        Height = actionBtn.Height,
                        Margin = new SerializableThickness(actionBtn.Margin)
                    });
                }
            }
            return list;
        }

        private List<OptionControlData> GetOptionControls(TabItemViewModel tab)
        {
            var list = new List<OptionControlData>();
            if (tab.PageInstance?.Content is not Grid root) return list;

            var scroll = root.Children.OfType<ScrollViewer>().FirstOrDefault();
            if (scroll?.Content is not Grid controlsGrid) return list;

            foreach (var child in controlsGrid.Children.OfType<OptionControl>())
            {
                if (child.Content is Border border && border.Child is StackPanel stack)
                {
                    var textBlocks = stack.Children.OfType<TextBlock>().ToList();
                    var headerText = textBlocks.FirstOrDefault()?.Text ?? "";
                    var descText = textBlocks.Count > 1 ? textBlocks[1].Text : "";

                    var toggle = stack.Children.OfType<ToggleSwitch>().FirstOrDefault();

                    list.Add(new OptionControlData
                    {
                        Header = headerText,
                        Description = descText,
                        ControlType = "ToggleSwitch",
                        IsChecked = toggle?.IsChecked ?? false,
                        Margin = new SerializableThickness(toggle?.Margin ?? new Thickness(0, 5, 0, 0))
                    });
                }
            }
            return list;
        }
        private void LoadTabsStructure()
        {
            if (!File.Exists(TabsConfigPath)) return;
            if (DataContext is not MainWindowViewModel vm) return;

            try
            {
                string json = File.ReadAllText(TabsConfigPath);
                var blueprint = JsonSerializer.Deserialize<List<TabBlueprint>>(json);
                if (blueprint == null) return;

                vm.Tabs.Clear();

                foreach (var tabData in blueprint)
                {
                    var newTab = new TabItemViewModel { Title = tabData.Title };
                    var rootGrid = new Grid();
                    rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                    var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };

                    var deleteBtn = new System.Windows.Controls.Button
                    {
                        Content = "X",
                        Width = 34,
                        Height = 34,
                        Margin = new Thickness(0, 0, 5, 0)
                    };
                    deleteBtn.Click += (s, e) =>
                    {
                        vm.Tabs.Remove(newTab);
                        if (vm.SelectedTab == newTab)
                            vm.SelectedTab = vm.Tabs.FirstOrDefault();
                        SaveTabsStructure();
                    };
                    headerPanel.Children.Add(deleteBtn);
                    var plusBtn = new System.Windows.Controls.Button
                    {
                        Content = "+",
                        Width = 34,
                        Height = 34,
                        Margin = new Thickness(5, 0, 5, 0)
                    };
                    plusBtn.Click += (s, e) => OpenToolbox(newTab, vm);
                    headerPanel.Children.Add(plusBtn);

                    if (tabData.Headers != null)
                    {
                        foreach (var header in tabData.Headers)
                        {
                            if (header.Text == "X" || header.Text == "+") continue;

                            var btn = new System.Windows.Controls.Button
                            {
                                Content = header.Text,
                                Width = header.Width,
                                Height = header.Height,
                                Margin = header.Margin.ToThickness()
                            };
                            headerPanel.Children.Add(btn);
                        }
                    }

                    Grid.SetRow(headerPanel, 0);
                    rootGrid.Children.Add(headerPanel);

                    var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                    var controlsGrid = new Grid { Margin = new Thickness(10) };
                    for (int i = 0; i < 3; i++) controlsGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    scrollViewer.Content = controlsGrid;
                    Grid.SetRow(scrollViewer, 1);
                    rootGrid.Children.Add(scrollViewer);

                    newTab.PageInstance = new Page { Background = Brushes.Transparent, Content = rootGrid };

                    if (tabData.Options != null)
                    {
                        foreach (var option in tabData.Options)
                        {
                            AddOption(option.Header, option.Description, true, newTab, vm);
                        }
                    }

                    vm.Tabs.Add(newTab);
                }

                if (vm.Tabs.Any()) vm.SelectedTab = vm.Tabs.First();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("MainWindow", $"Load Error: {ex.Message}");
            }
        }

        private void OpenToolbox(TabItemViewModel targetTab, MainWindowViewModel vm)
        {
            var toolboxWindow = new Window
            {
                Title = "Add Tools",
                Width = 320,
                Height = 400,
                Owner = this,
                Background = Brushes.Transparent
            };

            var backgroundGradient = new LinearGradientBrush
            {
                StartPoint = new Point(1, 1),
                EndPoint = new Point(0, 0)
            };

            backgroundGradient.GradientStops.Add(new GradientStop((Color)TryFindResource("WindowBackgroundColorPrimary"), 0.00));
            backgroundGradient.GradientStops.Add(new GradientStop((Color)TryFindResource("WindowBackgroundColorSecondary"), 0.80));
            backgroundGradient.GradientStops.Add(new GradientStop((Color)TryFindResource("WindowBackgroundColorThird"), 1.10));

            var rootGrid = new Grid { Background = backgroundGradient };

            var toolboxPanel = new StackPanel { Margin = new Thickness(15) };
            var scrollViewer = new ScrollViewer
            {
                Content = toolboxPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            rootGrid.Children.Add(scrollViewer);
            toolboxWindow.Content = rootGrid;

            void CreateToolItem(string name, string desc)
            {
                var toolBtn = new System.Windows.Controls.Button
                {
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = new Thickness(10),
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.Medium });
                stack.Children.Add(new TextBlock { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                toolBtn.Content = stack;

                toolBtn.Click += (_, _) =>
                {
                    AddOption(name, desc, true, targetTab, vm);
                    SaveTabsStructure();
                    toolboxWindow.Close();
                };
                toolboxPanel.Children.Add(toolBtn);
            }

            CreateToolItem("FullBright", "Attempt to recreate Fullbright without FFlags.");
            CreateToolItem("Windows FPS Counter", "Displays your computer's FPS.");
            CreateToolItem("Enable Overlay", "Enables the Overlay Mods to work over Roblox.");
            CreateToolItem("Crosshair", "Show a crosshair on screen. (In-Game Only)");
            CreateToolItem("Current Time", "Displays the current system time.");
            CreateToolItem("Lighting Changer (BETA)", "Adds a Experimental overlay-based lighting changer. Customize your Roblox lighting.");
            CreateToolItem(Strings.Menu_Integrations_EnableActivityTracking_Title, Strings.Menu_Integrations_EnableActivityTracking_Description);
            CreateToolItem(Strings.Menu_Integrations_QueryServerLocation_Title, Strings.Menu_Integrations_QueryServerLocation_Description);
            CreateToolItem(Strings.Menu_Integrations_DesktopApp_Title, Strings.Menu_Integrations_DesktopApp_Description);
            CreateToolItem(Strings.Menu_Integrations_ShowGameActivity_Title, Strings.Menu_Integrations_ShowGameActivity_Description);
            CreateToolItem(Strings.Menu_Integrations_ShowAccountOnProfile_Title, Strings.Menu_Integrations_ShowAccountOnProfile_Description);
            CreateToolItem(Strings.Menu_Behaviour_ConfirmLaunches_Title, Strings.Menu_Behaviour_ConfirmLaunches_Description);
            CreateToolItem(Strings.Menu_Behaviour_ForceRobloxLanguage_Title, Strings.Menu_Behaviour_ForceRobloxLanguage_Description);
            CreateToolItem("Disable Background Window", "Disables Background Window when Launching Roblox");
            CreateToolItem("Disable RobloxCrashHandler", "Disables the RobloxCrashHandler that runs on startup, improving memory and RAM efficiency.");
            CreateToolItem("Exclusive Fullscreen", "Enables exclusive fullscreen mode. This may fix latency issues.");
            CreateToolItem("Background Snow", "Adds Snow to Voidstraps background (Restart Required)");
            CreateToolItem("Gradient Movement", "Adds a Gradient Movement with Cursor (Restart Required)");
            CreateToolItem("Smooth ScrollBar", "Adds a Smooth ScrollBar Movement (Restart Required)");

            toolboxWindow.ShowDialog();
        }

        private void AddOption(string header, string description, bool isShared, TabItemViewModel newTab, MainWindowViewModel vm)
        {
            if (newTab.PageInstance?.Content is not Grid rootGrid) return;
            var controlsGrid = rootGrid.Children.OfType<ScrollViewer>().FirstOrDefault()?.Content as Grid;
            if (controlsGrid == null) return;

            int columns = 3;
            int index = controlsGrid.Children.Count;
            int row = index / columns;
            int col = index % columns;
            while (controlsGrid.RowDefinitions.Count <= row)
                controlsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var optionControl = new OptionControl { Margin = new Thickness(5) };
            var border = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = header, FontWeight = FontWeights.Bold, Foreground = Brushes.White });

            if (!string.IsNullOrEmpty(description))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = description,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 5),
                    Foreground = Brushes.White
                });
            }

            bool currentValue = GlobalToggleManager.Get(header);
            var toggle = new ToggleSwitch { IsChecked = currentValue, Margin = new Thickness(0, 5, 0, 0), Tag = header };

            toggle.Checked += (s, e) =>
            {
                GlobalToggleManager.Set(header, true);
                SaveTabsStructure();
            };

            toggle.Unchecked += (s, e) =>
            {
                GlobalToggleManager.Set(header, false);
                SaveTabsStructure();
            };

            stack.Children.Add(toggle);
            border.Child = stack;
            optionControl.Content = border;

            Grid.SetRow(optionControl, row);
            Grid.SetColumn(optionControl, col);
            controlsGrid.Children.Add(optionControl);

            newTab.Options[header] = currentValue;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var sub in FindVisualChildren<T>(child))
                    yield return sub;
            }
        }

        private void AddTab_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            if (vm.Tabs.Count >= 4)
            {
                // hmmmm yea 4 :nerd:
                return;
            }

            int nextNumber = 1;
            if (vm.Tabs.Any())
            {
                var existingNumbers = vm.Tabs
                    .Select(t => System.Text.RegularExpressions.Regex.Match(t.Title, @"\d+"))
                    .Where(m => m.Success)
                    .Select(m => int.Parse(m.Value))
                    .ToList();

                if (existingNumbers.Any())
                {
                    nextNumber = existingNumbers.Max() + 1;
                }
                else
                {
                    nextNumber = vm.Tabs.Count + 1;
                }
            }

            var newTab = new TabItemViewModel
            {
                Title = $"Tab #{nextNumber}"
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10)
            };

            var deleteButton = new System.Windows.Controls.Button
            {
                Content = "X",
                Width = 34,
                Height = 34,
                Margin = new Thickness(0, 0, 5, 0)
            };

            deleteButton.Click += (s, ev) =>
            {
                vm.Tabs.Remove(newTab);
                if (vm.SelectedTab == newTab)
                    vm.SelectedTab = vm.Tabs.FirstOrDefault();

                SaveTabsStructure();
            };

            var plusButton = new System.Windows.Controls.Button
            {
                Content = "+",
                Width = 34,
                Height = 34,
                Margin = new Thickness(5, 0, 0, 0)
            };
            headerPanel.Children.Add(deleteButton);
            headerPanel.Children.Add(plusButton);

            Grid.SetRow(headerPanel, 0);
            rootGrid.Children.Add(headerPanel);

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(scrollViewer, 1);
            rootGrid.Children.Add(scrollViewer);

            var controlsGrid = new Grid { Margin = new Thickness(10) };
            scrollViewer.Content = controlsGrid;

            int columns = 3;
            for (int i = 0; i < columns; i++)
                controlsGrid.ColumnDefinitions.Add(new ColumnDefinition());

            void AddOption(string header, string description, bool isShared)
            {
                var optionControl = new OptionControl { Margin = new Thickness(5) };
                var border = new Border
                {
                    Background = Brushes.Transparent,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8)
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = header,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                });
                if (!string.IsNullOrEmpty(description))
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = description,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 5, 0, 5),
                        Foreground = Brushes.White
                    });
                }

                bool currentValue = isShared ? GlobalToggleManager.Get(header) : newTab.Options.GetValueOrDefault(header, false);

                var toggle = new ToggleSwitch
                {
                    IsChecked = currentValue,
                    Margin = new Thickness(0, 5, 0, 0),
                    Tag = header
                };

                toggle.Checked += (_, _) =>
                {
                    if (isShared) GlobalToggleManager.Set(header, true);
                    else newTab.Options[header] = true;

                    ApplyGlobalEffect(header, true);
                    RefreshAllTabs(vm, header);
                    SaveTabsStructure();
                };

                toggle.Unchecked += (_, _) =>
                {
                    if (isShared) GlobalToggleManager.Set(header, false);
                    else newTab.Options[header] = false;

                    ApplyGlobalEffect(header, false);
                    RefreshAllTabs(vm, header);
                    SaveTabsStructure();
                };

                stack.Children.Add(toggle);
                border.Child = stack;
                optionControl.Content = border;

                int index = controlsGrid.Children.Count;
                int row = index / columns;
                int col = index % columns;

                while (controlsGrid.RowDefinitions.Count <= row)
                    controlsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetRow(optionControl, row);
                Grid.SetColumn(optionControl, col);
                controlsGrid.Children.Add(optionControl);

                newTab.Options[header] = currentValue;
            }

            plusButton.Click += (_, _) =>
            {
                OpenToolbox(newTab, vm);
            };

            newTab.PageInstance = new Page { Background = Brushes.Transparent, Content = rootGrid };
            vm.Tabs.Add(newTab);
            vm.SelectedTab = newTab;

            SaveTabsStructure();

            void RefreshAllTabs(MainWindowViewModel viewModel, string key)
            {
                foreach (var tab in viewModel.Tabs)
                {
                    if (tab.PageInstance?.Content is Grid root)
                    {
                        foreach (var toggle in FindVisualChildren<ToggleSwitch>(root))
                        {
                            if (toggle.Tag?.ToString() == key)
                                toggle.IsChecked = GlobalToggleManager.Get(key);
                        }
                    }
                }
            }

            static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is T t) yield return t;
                    foreach (var sub in FindVisualChildren<T>(child)) yield return sub;
                }
            }

            void SaveTabs()
            {
                if (vm.Tabs == null) return;
                var blueprint = vm.Tabs.Select(t => new TabBlueprint
                {
                    Title = t.Title,
                    Headers = new List<HeaderControlData>(),
                    Options = t.Options.Select(o => new OptionControlData
                    {
                        Header = o.Key,
                        Description = "",
                        ControlType = "ToggleSwitch",
                        IsChecked = o.Value,
                        Margin = new SerializableThickness(0, 5, 0, 0)
                    }).ToList()
                }).ToList();

                File.WriteAllText(TabsConfigPath, JsonSerializer.Serialize(blueprint, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        private void ApplyGlobalEffect(string header, bool isOn)
        {
            switch (header)
            {
                case "FullBright": App.Settings.Prop.Fullbright = isOn; break;
                case "Crosshair": App.Settings.Prop.Crosshair = isOn; break;
                case "Enable Overlay": App.Settings.Prop.OverlaysEnabled = isOn; break;
                case "Windows FPS Counter": App.Settings.Prop.FPSCounter = isOn; break;
                case "Current Time": App.Settings.Prop.CurrentTimeDisplay = isOn; break;
                case "Lighting Changer (BETA)": App.Settings.Prop.MotionBlurOverlay = isOn; break;
                case var s when s == Strings.Menu_Integrations_EnableActivityTracking_Title: App.Settings.Prop.EnableActivityTracking = isOn; break;
                case var s when s == Strings.Menu_Integrations_QueryServerLocation_Title: App.Settings.Prop.ShowServerDetails = isOn; break;
                case var s when s == Strings.Menu_Integrations_DesktopApp_Title: App.Settings.Prop.UseDisableAppPatch = isOn; break;
                case var s when s == Strings.Menu_Integrations_ShowGameActivity_Title: App.Settings.Prop.UseDiscordRichPresence = isOn; break;
                case var s when s == Strings.Menu_Integrations_ShowAccountOnProfile_Title: App.Settings.Prop.ShowAccountOnRichPresence = isOn; break;
                case var s when s == Strings.Menu_Behaviour_ConfirmLaunches_Title: App.Settings.Prop.ConfirmLaunches = isOn; break;
                case var s when s == Strings.Menu_Behaviour_ForceRobloxLanguage_Title: App.Settings.Prop.ForceRobloxLanguage = isOn; break;
                case "Disable Background Window": App.Settings.Prop.BackgroundWindow = isOn; break;
                case "Disable RobloxCrashHandler": App.Settings.Prop.DisableCrash = isOn; break;
                case "Exclusive Fullscreen": App.Settings.Prop.ExclusiveFullscreen = isOn; break;
                case "Background Snow": App.Settings.Prop.SnowWOWSOCOOLWpfSnowbtw = isOn; break;
                case "Gradient Movement": App.Settings.Prop.GRADmentFR = isOn; break;
                case "Smooth ScrollBar": App.Settings.Prop.SmooothBARRyesirikikthxlucipook = isOn; break;
            }
        }

        public static class GlobalToggleManager
        {
            public static bool Get(string key)
            {
                return key switch
                {
                    "FullBright" => App.Settings.Prop.Fullbright,
                    "Crosshair" => App.Settings.Prop.Crosshair,
                    "Enable Overlay" => App.Settings.Prop.OverlaysEnabled,
                    "Windows FPS Counter" => App.Settings.Prop.FPSCounter,
                    "Current Time" => App.Settings.Prop.CurrentTimeDisplay,
                    "Lighting Changer (BETA)" => App.Settings.Prop.MotionBlurOverlay,
                    var s when s == Strings.Menu_Integrations_EnableActivityTracking_Title => App.Settings.Prop.EnableActivityTracking,
                    var s when s == Strings.Menu_Integrations_QueryServerLocation_Title => App.Settings.Prop.ShowServerDetails,
                    var s when s == Strings.Menu_Integrations_DesktopApp_Title => App.Settings.Prop.UseDisableAppPatch,
                    var s when s == Strings.Menu_Integrations_ShowGameActivity_Title => App.Settings.Prop.UseDiscordRichPresence,
                    var s when s == Strings.Menu_Integrations_ShowAccountOnProfile_Title => App.Settings.Prop.ShowAccountOnRichPresence,
                    var s when s == Strings.Menu_Behaviour_ConfirmLaunches_Title => App.Settings.Prop.ConfirmLaunches,
                    var s when s == Strings.Menu_Behaviour_ForceRobloxLanguage_Title => App.Settings.Prop.ForceRobloxLanguage,
                    "Disable Background Window" => App.Settings.Prop.BackgroundWindow,
                    "Disable RobloxCrashHandler" => App.Settings.Prop.DisableCrash,
                    "Exclusive Fullscreen" => App.Settings.Prop.ExclusiveFullscreen,
                    "Background Snow" => App.Settings.Prop.SnowWOWSOCOOLWpfSnowbtw,
                    "Gradient Movement" => App.Settings.Prop.GRADmentFR,
                    "Smooth ScrollBar" => App.Settings.Prop.SmooothBARRyesirikikthxlucipook,
                    _ => false
                };
            }

            public static void Set(string key, bool value)
            {
                switch (key)
                {
                    case "FullBright": App.Settings.Prop.Fullbright = value; break;
                    case "Crosshair": App.Settings.Prop.Crosshair = value; break;
                    case "Enable Overlay": App.Settings.Prop.OverlaysEnabled = value; break;
                    case "Windows FPS Counter": App.Settings.Prop.FPSCounter = value; break;
                    case "Current Time": App.Settings.Prop.CurrentTimeDisplay = value; break;
                    case "Lighting Changer (BETA)": App.Settings.Prop.MotionBlurOverlay = value; break;
                    case var s when s == Strings.Menu_Integrations_EnableActivityTracking_Title: App.Settings.Prop.EnableActivityTracking = value; break;
                    case var s when s == Strings.Menu_Integrations_QueryServerLocation_Title: App.Settings.Prop.ShowServerDetails = value; break;
                    case var s when s == Strings.Menu_Integrations_DesktopApp_Title: App.Settings.Prop.UseDisableAppPatch = value; break;
                    case var s when s == Strings.Menu_Integrations_ShowGameActivity_Title: App.Settings.Prop.UseDiscordRichPresence = value; break;
                    case var s when s == Strings.Menu_Integrations_ShowAccountOnProfile_Title: App.Settings.Prop.ShowAccountOnRichPresence = value; break;
                    case var s when s == Strings.Menu_Behaviour_ConfirmLaunches_Title: App.Settings.Prop.ConfirmLaunches = value; break;
                    case var s when s == Strings.Menu_Behaviour_ForceRobloxLanguage_Title: App.Settings.Prop.ForceRobloxLanguage = value; break;
                    case "Disable Background Window": App.Settings.Prop.BackgroundWindow = value; break;
                    case "Disable RobloxCrashHandler": App.Settings.Prop.DisableCrash = value; break;
                    case "Exclusive Fullscreen": App.Settings.Prop.ExclusiveFullscreen = value; break;
                    case "Background Snow": App.Settings.Prop.SnowWOWSOCOOLWpfSnowbtw = value; break;
                    case "Gradient Movement": App.Settings.Prop.GRADmentFR = value; break;
                    case "Smooth ScrollBar": App.Settings.Prop.SmooothBARRyesirikikthxlucipook = value; break;
                }

                RefreshAllSwitches(key, value);
            }

            private static void RefreshAllSwitches(string key, bool value)
            {
                foreach (Window window in Application.Current.Windows)
                {
                    foreach (var toggle in FindVisualChildren<ToggleSwitch>(window))
                    {
                        if (toggle.Tag?.ToString() == key)
                        {
                            toggle.IsChecked = value;
                        }
                    }
                }
            }

            private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
            {
                if (parent == null) yield break;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is T t) yield return t;
                    foreach (var sub in FindVisualChildren<T>(child)) yield return sub;
                }
            }
        }

        private void WorkspaceTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WorkspaceTabs.SelectedItem is TabItemViewModel tab)
            {
                if (tab.PageInstance != null)
                {
                    RootFrame.Navigate(tab.PageInstance);
                }
            }
        }

        private void RootFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            _allTextBlocksCache.Clear();
            _lastPage = null;

            var currentPage = e.Content;
            if (currentPage != null && _pagesToHideSearchBox.Contains(currentPage.GetType()))
            {
                GlobalSearchBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                GlobalSearchBox.Visibility = Visibility.Visible;
            }
        }

        //fuck man I dont even understand whats going on in this code dont go asking me 👇 nvm it was just 3am I do understand..
        private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_searchDebounceTimer == null)
            {
                _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                _searchDebounceTimer.Tick += (s, args) =>
                {
                    _searchDebounceTimer.Stop();
                    PerformSearch(GlobalSearchBox.Text.Trim().ToLower());
                };
            }

            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void GlobalSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            GlobalSearchBox.Text = "";
            PerformSearch("");
        }

        private void PerformSearch(string query)
        {
            if (!(RootFrame.Content is Page page)) return;

            if (page != _lastPage)
            {
                _allTextBlocksCache.Clear();
                _lastPage = page;
            }

            if (!_allTextBlocksCache.Any())
                CacheAllTextBlocks(page);

            if (string.IsNullOrEmpty(query))
            {
                foreach (var tb in _allTextBlocksCache)
                    tb.Background = Brushes.Transparent;
                return;
            }

            var matches = new List<TextBlock>();

            foreach (var tb in _allTextBlocksCache)
            {
                if (IsFuzzyMatch(tb.Text, query))
                {
                    tb.Background = (SolidColorBrush)SystemParameters.WindowGlassBrush; // fuckass windows accent color
                    FlashHighlight(tb);
                    matches.Add(tb);
                }
                else
                {
                    tb.Background = Brushes.Transparent;
                }
            }

            ScrollToClosestMatch(matches);
        }

        private void CacheAllTextBlocks(Page page)
        {
            _allTextBlocksCache.Clear();

            void Recurse(DependencyObject parent)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);

                    if (child is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
                        _allTextBlocksCache.Add(textBlock);

                    Recurse(child);
                }
            }

            Recurse(page);
        }

        private void ScrollToClosestMatch(List<TextBlock> matches)
        {
            if (!matches.Any()) return;

            foreach (var textBlock in matches)
            {
                ScrollViewer scrollViewer = null;
                DependencyObject parent = textBlock;
                while (parent != null)
                {
                    if (parent is ScrollViewer sv)
                    {
                        scrollViewer = sv;
                        break;
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }

                if (scrollViewer != null)
                {
                    GeneralTransform transform = textBlock.TransformToAncestor(scrollViewer);
                    Point position = transform.Transform(new Point(0, 0));

                    double viewportHeight = scrollViewer.ViewportHeight;
                    double elementTop = position.Y;
                    double elementBottom = elementTop + textBlock.ActualHeight;

                    if (elementBottom < 0 || elementTop > viewportHeight)
                    {
                        SmoothScrollTo(scrollViewer, scrollViewer.VerticalOffset + position.Y);
                    }

                    break;
                }
            }
        }

        private void SmoothScrollTo(ScrollViewer scrollViewer, double targetOffset)
        {
            double startOffset = scrollViewer.VerticalOffset;
            double distance = targetOffset - startOffset;
            int steps = 15;
            int currentStep = 0;

            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            timer.Tick += (s, e) =>
            {
                currentStep++;
                double t = (double)currentStep / steps;
                t = t * t * (3 - 2 * t);
                scrollViewer.ScrollToVerticalOffset(startOffset + distance * t);

                if (currentStep >= steps)
                    timer.Stop();
            };
            timer.Start();
        }

        private void FlashHighlight(TextBlock tb)
        {
            var originalBrush = tb.Background;
            DispatcherTimer flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            flashTimer.Tick += (s, e) =>
            {
                flashTimer.Stop();
                tb.Background = originalBrush;
            };
            flashTimer.Start();
        }

        private static bool IsFuzzyMatch(string text, string query)
        {
            text = text.ToLower();
            query = query.ToLower();

            if (text.Contains(query)) return true;

            int distance = LevenshteinDistance(text, query);
            int threshold = Math.Max(1, query.Length / 3);
            return distance <= threshold;
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }
            return d[n, m];
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
            string currentTime = DateTime.Now.ToString("hh:mm tt");

            _discordClient.SetPresence(new DiscordRPC.RichPresence()
            {
                Details = $"Viewing {pageName}", // the fuck was there state I just relized that it already displays fucking voidstrap THE FUCK
                State = $"Current Time: {currentTime}",
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
                Label = "Github",  // sick of this shit ❤️‍🔥 why the fuck I put fire emoji it came out as a heart + fire fah
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
            LoadTabsStructure();
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
            if (_state.LeftUpdateV2 > SystemParameters.VirtualScreenWidth || _state.TopUpdateV2 > SystemParameters.VirtualScreenHeight)
            {
                _state.LeftUpdateV2 = 0;
                _state.TopUpdateV2 = 0;
            }

            if (_state.WidthUpdateV2 > 0) Width = _state.WidthUpdateV2;
            if (_state.HeightUpdateV2 > 0) Height = _state.HeightUpdateV2;

            if (_state.LeftUpdateV2 > 0 && _state.TopUpdateV2 > 0)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = _state.LeftUpdateV2;
                Top = _state.TopUpdateV2;
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
            SaveTabsStructure();
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
            _state.WidthUpdateV2 = Width;
            _state.HeightUpdateV2 = Height;
            _state.TopUpdateV2 = Top;
            _state.LeftUpdateV2 = Left;

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

        public class TabItemViewModel
        {
            public string Title { get; set; } = "";
            public Page PageInstance { get; set; } = null!;
            public Dictionary<string, bool> Options { get; } = new();
            public override string ToString() => Title;
        }

        public class TabBlueprint
        {
            public string Title { get; set; } = "";
            public List<HeaderControlData> Headers { get; set; } = new();
            public List<OptionControlData> Options { get; set; } = new();
        }

        public struct SerializableThickness
        {
            public double Left;
            public double Top;
            public double Right;
            public double Bottom;

            public SerializableThickness(double left, double top, double right, double bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public SerializableThickness(Thickness thickness)
            {
                Left = thickness.Left;
                Top = thickness.Top;
                Right = thickness.Right;
                Bottom = thickness.Bottom;
            }

            public Thickness ToThickness() => new Thickness(Left, Top, Right, Bottom);
        }

        public class HeaderControlData
        {
            public string Type { get; set; } = "TextBox";
            public string Text { get; set; } = "";
            public double Width { get; set; }
            public double Height { get; set; }
            public SerializableThickness Margin { get; set; } = new SerializableThickness();
        }

        public class OptionControlData
        {
            public string Header { get; set; } = "";
            public string Description { get; set; } = "";
            public string ControlType { get; set; } = "ToggleSwitch";
            public bool IsChecked { get; set; }
            public SerializableThickness Margin { get; set; } = new SerializableThickness();
        }

        #endregion
    }
}