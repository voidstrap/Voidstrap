using Markdig.Extensions.CustomContainers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Voidstrap.Integrations;
using Voidstrap.UI.ViewModels.Settings;
using Wpf.Ui.Mvvm.Contracts;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class FastFlagsPage // meowr
    {
        private bool _initialLoad = false;
        private bool _isLoading = true;
        public ObservableCollection<FFlagItem> FFlags { get; } = new();
        public ObservableCollection<NvidiaFFlag> CustomFFlags { get; } = new();

        private FastFlagsViewModel _viewModel = null!;
        private static readonly string SavedFilePath =
            Path.Combine(Paths.Base, "Settings.ini");

        private string _editorBaseProfile = "Default Settings";
        private bool _editorModified;

        public class NvidiaFFlag : INotifyPropertyChanged, IDataErrorInfo
        {
            private string _name = string.Empty;
            private string _value = string.Empty;

            public string Name
            {
                get => _name;
                set
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }

            public string Value
            {
                get => _value;
                set
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }

            public string Error => null;

            public string this[string columnName]
            {
                get
                {
                    if (columnName == nameof(Name) && string.IsNullOrWhiteSpace(Name))
                        return "Flag name is required";

                    if (columnName == nameof(Value) && string.IsNullOrWhiteSpace(Value))
                        return "Value is required";

                    return null;
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string prop)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public FastFlagsPage()
        {
            SetupViewModel();
            InitializeComponent();
            Loaded += FastFlagsPage_Loaded;
            Loaded += async (_, _) => await LoadFFlagsAsync();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private async Task LoadFFlagsAsync()
        {
            const string url =
                "https://raw.githubusercontent.com/LeventGameing/allowlist/main/allowlist.json";

            try
            {
                string json = await _httpClient.GetStringAsync(url).ConfigureAwait(false);

                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (dict == null)
                    throw new InvalidOperationException("FFlags JSON returned null.");

                await Dispatcher.InvokeAsync(() =>
                {
                    FFlags.Clear();

                    foreach (var kv in dict)
                    {
                        FFlags.Add(new FFlagItem
                        {
                            Name = kv.Key,
                            Value = kv.Value.ValueKind switch
                            {
                                JsonValueKind.String => kv.Value.GetString(),
                                JsonValueKind.Number => kv.Value.GetRawText(),
                                JsonValueKind.True => "true",
                                JsonValueKind.False => "false",
                                _ => kv.Value.GetRawText()
                            }
                        });
                    }

                    DataGrid.ItemsSource = null;
                    DataGrid.ItemsSource = FFlags;
                });
            }
            catch (HttpRequestException ex)
            {
                Frontend.ShowMessageBox(
                    $"Network error while loading FFlags:\n{ex.Message}");
            }
            catch (JsonException ex)
            {
                Frontend.ShowMessageBox(
                    $"Invalid FFlags JSON format:\n{ex.Message}");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    $"Failed to load FFlags:\n{ex}");
            }
        }

        public class FFlagItem
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        private void FastFlagsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
        }

        private void OpenCustomEditor_Click(object sender, RoutedEventArgs e)
        {
            if (IsVulkanSelected())
            {
                Frontend.ShowMessageBox(
                    "Some FFlags may not work while Vulkan Rendering Mode is enabled in the NVIDIA FFlags Editor.\n\n" +
                    "Please switch to DirectX/Direct3D Rendering Mode."
                );
            }

            NavigationService.Navigate(new NvidiaFastFlagsPage());
        }

        private bool IsVulkanSelected()
        {
            if (_viewModel?.SelectedRenderingMode == null)
                return false;

            string modeText = _viewModel.SelectedRenderingMode.ToString();
            return modeText.Contains("Vulkan", StringComparison.OrdinalIgnoreCase);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            SetupViewModel();
        }

        private void SetupViewModel()
        {
            _viewModel = new FastFlagsViewModel();

            _viewModel.OpenFlagEditorEvent += OpenFlagEditor;
            _viewModel.RequestPageReloadEvent += (_, _) => SetupViewModel();
            DataContext = _viewModel;
        }

        private void ValidateIntInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            e.Handled = !Regex.IsMatch(newText, @"^[\+\-]?[0-9]*$");
        }

        private void OpenFlagEditor(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
            {
                window.Navigate(typeof(FastFlagEditorPage));
            }
        }

        private void ValidateInt32(object sender, TextCompositionEventArgs e) => e.Handled = e.Text != "-" && !Int32.TryParse(e.Text, out int _);

        private void ValidateUInt32(object sender, TextCompositionEventArgs e) => e.Handled = !UInt32.TryParse(e.Text, out uint _);

        private void ToggleSwitch_Checked(object sender, RoutedEventArgs e)
        {
        }

        private void ToggleSwitch_Checked_1(object sender, RoutedEventArgs e)
        {
        }

        private void OptionControl_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void OptionControl_Loaded_1(object sender, RoutedEventArgs e)
        {
        }

        private async void CheckSystemButton_Click(object sender, RoutedEventArgs e)
        {
            CheckSystemButton.IsEnabled = false;
            SystemCheckProgress.Visibility = Visibility.Visible;
            SystemCheckProgress.Value = 0;

            string logFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SystemCheckLog.txt");

            string configFolder = Path.Combine(Paths.Base, "VoidstrapMods", "ClientSettings");
            string configFile = Path.Combine(configFolder, "ClientAppSettings.json");
            Directory.CreateDirectory(configFolder);

            var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
            double totalRamGb = Math.Round(ci.TotalPhysicalMemory / (1024.0 * 1024.0 * 1024.0), 2);
            int cpuCores = Environment.ProcessorCount;

            SystemCheckProgress.Value = 20;
            await Task.Delay(200);

            var tierFlags = new Dictionary<string, Dictionary<string, string>>
            {
                ["Low"] = new Dictionary<string, string> { ["DFFlagDebugPerfMode"] = "True", ["FFlagHandleAltEnterFullscreenManually"] = "False", ["DFFlagDisableDPIScale"] = "True", ["FFlagDebugGraphicsPreferVulkan"] = "True", ["FFlagDebugGraphicsDisableDirect3D11"] = "True" },
                ["Mid"] = new Dictionary<string, string> { ["FFlagRenderDisableShadows"] = "False", ["FFlagGraphicsTextureQuality"] = "2", ["FFlagTerrainEnable"] = "True", ["FIntRenderShadowmapBias"] = "-1", ["DFFlagDebugPauseVoxelizer"] = "True", ["FIntRenderShadowIntensity"] = "0", ["DFFlagDebugRenderForceTechnologyVoxel"] = "True", ["FIntFullscreenTitleBarTriggerDelayMillis"] = "3600000", ["FFlagDisablePostFx"] = "True", ["DFIntTextureQualityOverride"] = "2", ["DFFlagTextureQualityOverrideEnabled"] = "True", ["DFFlagDisableDPIScale"] = "True" },
                ["High"] = new Dictionary<string, string> { ["FFlagRenderDisableShadows"] = "False", ["FFlagGraphicsTextureQuality"] = "3", ["FFlagTerrainEnable"] = "True", ["DFIntTextureQualityOverride"] = "2", ["DFFlagTextureQualityOverrideEnabled"] = "True", ["FFlagDisablePostFx"] = "True", ["FIntFullscreenTitleBarTriggerDelayMillis"] = "3600000", ["FFlagDebugForceFutureIsBrightPhase2"] = "True", ["DFFlagDisableDPIScale"] = "True" },
                ["Ultra"] = new Dictionary<string, string> { ["FFlagRenderDisableShadows"] = "False", ["FFlagGraphicsTextureQuality"] = "3", ["FFlagTerrainEnable"] = "True", ["FFlagDisablePostFx"] = "True", ["FIntFullscreenTitleBarTriggerDelayMillis"] = "3600000", ["DFFlagDisableDPIScale"] = "True" }
            };

            SystemCheckProgress.Value = 40;
            await Task.Delay(200);

            string tier = "Mid";
            if (totalRamGb < 4 || cpuCores <= 2)
                tier = "Low";
            else if (totalRamGb >= 16 && cpuCores >= 12)
                tier = "Ultra";
            else if (totalRamGb >= 12 && cpuCores >= 8)
                tier = "High";

            SystemCheckProgress.Value = 60;
            await Task.Delay(200);

            var flags = tierFlags.ContainsKey(tier) ? new Dictionary<string, string>(tierFlags[tier]) : new Dictionary<string, string>();

            SystemCheckProgress.Value = 80;
            await Task.Delay(200);

            string json = System.Text.Json.JsonSerializer.Serialize(flags, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configFile, json);

            SystemCheckProgress.Value = 90;
            await Task.Delay(200);

            using (StreamWriter writer = new StreamWriter(logFile, false))
            {
                await writer.WriteLineAsync($"System Check Log - {DateTime.Now}");
                await writer.WriteLineAsync($"Machine: {Environment.MachineName}");
                await writer.WriteLineAsync($"OS: {Environment.OSVersion}");
                await writer.WriteLineAsync($"CPU Cores: {cpuCores}");
                await writer.WriteLineAsync($"RAM: {totalRamGb} GB");
                await writer.WriteLineAsync($"Chosen Tier: {tier}");
                await writer.WriteLineAsync($"FFlags written to: {configFile}");
            }

            SystemCheckProgress.Value = 100;
            await Task.Delay(200);
            Frontend.ShowMessageBox($"System check complete! Flags applied for {tier}-tier.\nConfig saved to: {configFile}\nLog saved to Documents.\nApp Restart is needed!", MessageBoxImage.Information);

            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
    }
}
