using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Voidstrap;
using Voidstrap.AppData;
using Voidstrap.RobloxInterfaces;
using Voidstrap.UI.Elements.ContextMenu;
using Wpf.Ui.Appearance;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class ChannelViewModel : INotifyPropertyChanged
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll")]
        private static extern int ChangeDisplaySettings(ref DEVMODE lpDevMode, int dwFlags);

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int CDS_UPDATEREGISTRY = 0x01;
        private const int DISP_CHANGE_SUCCESSFUL = 0;

        private string _oldPlayerVersionGuid = "";
        private string _oldStudioVersionGuid = "";
        private CancellationTokenSource? _loadChannelCts;

        public ObservableCollection<int> CpuLimitOptions { get; set; }

        private bool _showLoadingError;
        private bool _showChannelWarning;
        private DeployInfo? _channelDeployInfo;
        private string _channelInfoLoadingText = string.Empty;

        private bool _potatoQuality;
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private readonly string npiFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NPI");
        private string npiPath => Path.Combine(npiFolder, "NVIDIAProfileInspector.exe");
        private string potatoProfilePath => Path.Combine(npiFolder, "RobloxApply.nip");
        private string defaultProfilePath => Path.Combine(npiFolder, "RobloxRevert.nip");
        private string settingsFile => Path.Combine(npiFolder, "settings.txt");
        private readonly DispatcherTimer _refreshTimer;

        private readonly string npiUrl = "https://github.com/Orbmu2k/nvidiaProfileInspector/releases/download/2.4.0.29/nvidiaProfileInspector.zip";
        private readonly string potatoUrl = "https://github.com/KloBraticc/nividaVoidstrap/raw/main/RobloxApply.nip";
        private readonly string defaultUrl = "https://github.com/KloBraticc/nividaVoidstrap/raw/main/RobloxRevert.nip";
        public ChannelViewModel()
        {
            Directory.CreateDirectory(npiFolder);
            LoadPotatoQualitySetting();
            LoadAvailableResolutions();
            _ = EnsureProfilesExistAsync();
            _ = LoadNetworkStreamingStateAsync();

            CpuLimitOptions = new ObservableCollection<int>();
            int coreCount = Environment.ProcessorCount;

            for (int i = 1; i <= coreCount; i++)
                CpuLimitOptions.Add(i);

            if (!CpuLimitOptions.Contains(App.Settings.Prop.CpuCoreLimit))
                SelectedCpuLimit = coreCount;

            PriorityOptions = new ObservableCollection<string>
            {
                "Realtime",
                "High",
                "Above Normal",
                "Normal",
                "Below Normal",
                "Low"
            };

        _selectedPriority = App.Settings.Prop.PriorityLimit ?? "Normal";
            _ = LoadChannelDeployInfoSafeAsync(App.Settings.Prop.Channel);
        }

        public ObservableCollection<DisplayMode> AvailableResolutions { get; } = new();

        private DisplayMode? _selectedResolution;
        public DisplayMode? SelectedResolution
        {
            get => _selectedResolution;
            set
            {
                if (_selectedResolution != value)
                {
                    _selectedResolution = value;
                    OnPropertyChanged(nameof(SelectedResolution));
                    if (_selectedResolution != null)
                        ApplyResolution(_selectedResolution);
                }
            }
        }
        private void LoadAvailableResolutions()
        {
            List<DisplayMode> modes = new();
            DEVMODE devMode = new();
            devMode.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));

            int modeIndex = 0;
            while (EnumDisplaySettings(null, modeIndex++, ref devMode))
            {
                if (devMode.dmPelsWidth == 0 || devMode.dmPelsHeight == 0)
                    continue;

                var existing = modes.FirstOrDefault(m => m.Width == devMode.dmPelsWidth && m.Height == devMode.dmPelsHeight);
                if (existing == null)
                    modes.Add(new DisplayMode { Width = (int)devMode.dmPelsWidth, Height = (int)devMode.dmPelsHeight, RefreshRate = (int)devMode.dmDisplayFrequency });
                else if (devMode.dmDisplayFrequency > existing.RefreshRate)
                    existing.RefreshRate = (int)devMode.dmDisplayFrequency;
            }

            AvailableResolutions.Clear();
            foreach (var m in modes.OrderBy(m => m.Width).ThenBy(m => m.Height))
                AvailableResolutions.Add(m);
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                SelectedResolution = AvailableResolutions.FirstOrDefault(m =>
                    m.Width == devMode.dmPelsWidth &&
                    m.Height == devMode.dmPelsHeight &&
                    m.RefreshRate == devMode.dmDisplayFrequency);
            }
        }

        private void ApplyResolution(DisplayMode mode)
        {
            DEVMODE dm = new();
            dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm))
            {
                MessageBox.Show("Failed to read current display settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            dm.dmPelsWidth = (uint)mode.Width;
            dm.dmPelsHeight = (uint)mode.Height;
            dm.dmDisplayFrequency = (uint)mode.RefreshRate;
            dm.dmBitsPerPel = 32;
            dm.dmFields = 0x180000 | 0x400000;
            int result = ChangeDisplaySettings(ref dm, CDS_UPDATEREGISTRY);
            if (result != DISP_CHANGE_SUCCESSFUL)
                MessageBox.Show($"Failed to change resolution. Error code: {result}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public ObservableCollection<string> PriorityOptions { get; set; }

        private string _selectedPriority;
        public string SelectedPriority
        {
            get => _selectedPriority;
            set
            {
                if (_selectedPriority != value)
                {
                    _selectedPriority = value;
                    OnPropertyChanged(nameof(SelectedPriority));
                    App.Settings.Prop.PriorityLimit = value;
                }
            }
        }

        public bool PotatoQuality
        {
            get => _potatoQuality;
            set
            {
                if (_potatoQuality != value)
                {
                    _potatoQuality = value;
                    OnPropertyChanged(nameof(PotatoQuality));
                    SavePotatoQualitySetting();
                    _ = ApplyPotatoProfileAsync(_potatoQuality);
                }
            }
        }

        private void SavePotatoQualitySetting() =>
            File.WriteAllText(settingsFile, _potatoQuality ? "1" : "0");

        private void LoadPotatoQualitySetting()
        {
            if (File.Exists(settingsFile))
            {
                string content = File.ReadAllText(settingsFile);
                _potatoQuality = content == "1";
            }
        }

        private async Task EnsureProfilesExistAsync()
        {
            using var client = new WebClient();

            if (!File.Exists(potatoProfilePath))
                await client.DownloadFileTaskAsync(new Uri(potatoUrl), potatoProfilePath);

            if (!File.Exists(defaultProfilePath))
                await client.DownloadFileTaskAsync(new Uri(defaultUrl), defaultProfilePath);
        }

        private async Task EnsureNPIInstalledAsync()
        {
            if (File.Exists(npiPath))
                return;

            string tempZip = Path.Combine(npiFolder, "NPI.zip");

            using var client = new WebClient();
            await client.DownloadFileTaskAsync(new Uri(npiUrl), tempZip);

            ZipFile.ExtractToDirectory(tempZip, npiFolder, true);
            File.Delete(tempZip);

            if (!File.Exists(npiPath))
                throw new Exception("Failed to install NVIDIA Profile Inspector.");
        }

        private async Task ApplyPotatoProfileAsync(bool enablePotato)
        {
            await EnsureNPIInstalledAsync();
            await EnsureProfilesExistAsync();

            string profile = enablePotato ? potatoProfilePath : defaultProfilePath;
            await ImportNipByDragDrop(profile);
        }

        private async Task ImportNipByDragDrop(string profilePath)
        {
            if (!File.Exists(profilePath))
                throw new FileNotFoundException("Profile not found", profilePath);

            if (!File.Exists(npiPath))
                throw new FileNotFoundException("NVIDIA Profile Inspector not found", npiPath);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = npiPath,
                Arguments = $"\"{profilePath}\"",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(psi);
                if (process != null)
                    await Task.Run(() => process.WaitForExit());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to import profile into NVIDIA Profile Inspector.\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        public async Task ApplyPotatoAsync() => await ApplyPotatoProfileAsync(true);
        public async Task RevertPotatoAsync() => await ApplyPotatoProfileAsync(false);

        public int SelectedCpuLimit
        {
            get => App.Settings.Prop.CpuCoreLimit;
            set
            {
                if (App.Settings.Prop.CpuCoreLimit != value)
                {
                    App.Settings.Prop.CpuCoreLimit = value;
                    OnPropertyChanged(nameof(SelectedCpuLimit));
                    App.Settings.Save();
                    CpuCoreLimiter.SetCpuCoreLimit(value);
                }
            }
        }

        public bool UpdateCheckingEnabled
        {
            get => App.Settings.Prop.CheckForUpdates;
            set => App.Settings.Prop.CheckForUpdates = value;
        }

        public bool IsChannelEnabled
        {
            get => App.Settings.Prop.IsChannelEnabled;
            set
            {
                if (App.Settings.Prop.IsChannelEnabled != value)
                {
                    App.Settings.Prop.IsChannelEnabled = value;
                    OnPropertyChanged(nameof(IsChannelEnabled));
                }
            }
        }

        public bool ShowLoadingError
        {
            get => _showLoadingError;
            private set
            {
                if (_showLoadingError != value)
                {
                    _showLoadingError = value;
                    OnPropertyChanged(nameof(ShowLoadingError));
                }
            }
        }

        public bool ShowChannelWarning
        {
            get => _showChannelWarning;
            private set
            {
                if (_showChannelWarning != value)
                {
                    _showChannelWarning = value;
                    OnPropertyChanged(nameof(ShowChannelWarning));
                }
            }
        }

        public DeployInfo? ChannelDeployInfo
        {
            get => _channelDeployInfo;
            private set
            {
                if (_channelDeployInfo != value)
                {
                    _channelDeployInfo = value;
                    OnPropertyChanged(nameof(ChannelDeployInfo));
                }
            }
        }

        public string ChannelInfoLoadingText
        {
            get => _channelInfoLoadingText;
            private set
            {
                if (_channelInfoLoadingText != value)
                {
                    _channelInfoLoadingText = value;
                    OnPropertyChanged(nameof(ChannelInfoLoadingText));
                }
            }
        }

        public bool VoidNotify
        {
            get => App.Settings.Prop.VoidNotify;
            set => App.Settings.Prop.VoidNotify = value;
        }

        public string BufferSizeKbte
        {
            get => App.Settings.Prop.BufferSizeKbte;
            set => App.Settings.Prop.BufferSizeKbte = value;
        }

        public string BufferSizeKbtes
        {
            get => App.Settings.Prop.BufferSizeKbtes;
            set => App.Settings.Prop.BufferSizeKbtes = value;
        }

        private string _viewChannel;
        public string ViewChannel
        {
            get => _viewChannel ?? App.Settings?.Prop?.Channel ?? "production";
            set
            {
                string newValue = string.IsNullOrWhiteSpace(value)
                    ? App.Settings?.Prop?.Channel ?? "production"
                    : value.Trim().ToLowerInvariant();

                if (_viewChannel == newValue)
                    return;

                _viewChannel = newValue;
                OnPropertyChanged(nameof(ViewChannel));

                try
                {
                    if (App.Settings?.Prop != null)
                        App.Settings.Prop.Channel = newValue;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChannelViewModel] Failed to save channel: {ex}");
                }

                try
                {
                    if (!string.IsNullOrEmpty(newValue) && Regex.IsMatch(newValue, @"^[a-z0-9_-]+$"))
                    {
                        DeleteDirectorySafe(Paths.Versions);
                        DeleteDirectorySafe(Paths.Downloads);
                        DeleteRobloxLocalStorageFiles();
                        Debug.WriteLine($"[ChannelViewModel] Cleared Roblox cache for channel '{newValue}'.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChannelViewModel] Failed to clear old data: {ex}");
                }

                RunSafeAsync(() => LoadChannelDeployInfoAsync(newValue));
            }
        }
        private static void DeleteRobloxLocalStorageFiles()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string localStoragePath = Path.Combine(localAppData, "Roblox", "LocalStorage");

                if (!Directory.Exists(localStoragePath))
                    return;

                var files = Directory.GetFiles(localStoragePath, "memProfStorage*.json", SearchOption.TopDirectoryOnly);

                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        Debug.WriteLine($"[ChannelViewModel] Deleted {file}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ChannelViewModel] Failed to delete {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChannelViewModel] Error deleting LocalStorage files: {ex.Message}");
            }
        }

        private static void DeleteDirectorySafe(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            try
            {
                Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChannelViewModel] Error deleting {path}: {ex.Message}");
            }
        }

        private bool _networkStreamingEnabled;
        public bool NetworkStreamingEnabled
        {
            get => _networkStreamingEnabled;
            set
            {
                if (_networkStreamingEnabled != value)
                {
                    _networkStreamingEnabled = value;
                    OnPropertyChanged(nameof(NetworkStreamingEnabled));
                    _ = SaveNetworkStreamingStateAsync(value);
                }
            }
        }

        private readonly string _robloxLocalStorage = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox",
            "LocalStorage"
        );
        private async Task LoadNetworkStreamingStateAsync()
        {
            try
            {
                if (!Directory.Exists(_robloxLocalStorage))
                {
                    NetworkStreamingEnabled = false;
                    return;
                }

                var files = Directory.GetFiles(_robloxLocalStorage, "memProfStorage*.json",
                    SearchOption.TopDirectoryOnly);

                if (files.Length == 0)
                {
                    NetworkStreamingEnabled = false;
                    return;
                }

                bool? foundValue = null;

                foreach (var file in files)
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(file);
                        var match = Regex.Match(json, "\"NetworkStreamingEnabled\"\\s*:\\s*\"?(\\d+)\"?");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int value))
                        {
                            foundValue = value == 1;
                            break;
                        }
                    }
                    catch (IOException) { }
                }

                NetworkStreamingEnabled = foundValue ?? false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkStreaming] Read error: {ex.Message}");
                NetworkStreamingEnabled = false;
            }
        }

        private async Task SaveNetworkStreamingStateAsync(bool isEnabled)
        {
            try
            {
                if (!Directory.Exists(_robloxLocalStorage))
                    return;

                var files = Directory.GetFiles(_robloxLocalStorage, "memProfStorage*.json",
                    SearchOption.TopDirectoryOnly);

                foreach (var file in files)
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(file);

                        if (json.Contains("\"NetworkStreamingEnabled\""))
                        {
                            json = Regex.Replace(json, "\"NetworkStreamingEnabled\"\\s*:\\s*\"?\\d+\"?",
                                $"\"NetworkStreamingEnabled\":\"{(isEnabled ? 1 : 0)}\"");
                        }
                        else
                        {
                            json = json.TrimEnd('}', ' ', '\n', '\r');
                            json += $", \"NetworkStreamingEnabled\":\"{(isEnabled ? 1 : 0)}\" }}";
                        }

                        await File.WriteAllTextAsync(file, json);
                    }
                    catch (IOException ioEx)
                    {
                        Debug.WriteLine($"[NetworkStreaming] Failed to update {file}: {ioEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkStreaming] Write error: {ex.Message}");
            }
        }


        public string ChannelHash
        {
            get => App.Settings.Prop.ChannelHash;
            set
            {
                const string VersionHashPattern = @"version-(.*)";
                if (string.IsNullOrEmpty(value) || Regex.IsMatch(value, VersionHashPattern))
                    App.Settings.Prop.ChannelHash = value;
            }
        }

        public bool UpdateRoblox
        {
            get => App.Settings.Prop.UpdateRoblox;
            set => App.Settings.Prop.UpdateRoblox = value;
        }

        public bool HWAccelEnabled
        {
            get => !App.Settings.Prop.WPFSoftwareRender;
            set => App.Settings.Prop.WPFSoftwareRender = !value;
        }

        private async Task LoadChannelDeployInfoSafeAsync(string channel)
        {
            try
            {
                await LoadChannelDeployInfoAsync(channel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load channel info: {ex.Message}");
            }
        }

        private async Task LoadChannelDeployInfoAsync(string channel)
        {
            _loadChannelCts?.Cancel();
            _loadChannelCts = new CancellationTokenSource();
            var token = _loadChannelCts.Token;

            ShowLoadingError = false;
            ChannelDeployInfo = null;
            ChannelInfoLoadingText = "Fetching latest deploy info, please wait...";
            ShowChannelWarning = false;

            try
            {
                var info = await Deployment.GetInfo(channel).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                if (!token.IsCancellationRequested)
                {
                    ShowChannelWarning = info.IsBehindDefaultChannel;
                    ChannelDeployInfo = new DeployInfo
                    {
                        Version = info.Version,
                        VersionGuid = info.VersionGuid
                    };
                    App.State.Prop.IgnoreOutdatedChannel = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    ShowLoadingError = true;
                    ChannelInfoLoadingText =
                        $"The channel is likely private. Please change the channel or try again later.\nError: {ex.Message}";
                }
            }
        }

        private async void RunSafeAsync(Func<Task> asyncFunc)
        {
            try
            {
                await asyncFunc().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in background task: {ex}");
            }
        }
    }
}
