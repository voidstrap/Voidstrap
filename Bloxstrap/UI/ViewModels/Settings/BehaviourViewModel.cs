using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Voidstrap.AppData;
using Voidstrap.RobloxInterfaces;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class BehaviourViewModel : NotifyPropertyChangedViewModel
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly ConcurrentDictionary<string, (string Url, DateTime Expiry)> _gameIconCache = new();
        private static readonly ConcurrentDictionary<string, Task<string>> _ongoingRequests = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
        public BehaviourViewModel()
        {
            CleanerItems = new List<string>(App.Settings.Prop.CleanerDirectories);
            LoadCpuOptions();
        }

        private string _cpuModelName;
        public string CpuModelName
        {
            get => _cpuModelName;
            set
            {
                _cpuModelName = value;
                OnPropertyChanged(nameof(CpuModelName));
            }
        }

        private string _cpuSummary;
        public string CpuSummary
        {
            get => _cpuSummary;
            set
            {
                _cpuSummary = value;
                OnPropertyChanged(nameof(CpuSummary));
            }
        }

        public bool ExclusiveFullscreen
        {
            get => App.Settings.Prop.ExclusiveFullscreen;
            set => App.Settings.Prop.ExclusiveFullscreen = value;
        }

        private void LoadCpuOptions()
        {
            try
            {
                CpuOptions.Clear();

                int logicalCount = Environment.ProcessorCount;
                int physicalCount = GetPhysicalCoreCount();
                string model = "Unknown CPU";
                try
                {
                    using var searcher = new ManagementObjectSearcher("select Name from Win32_Processor");
                    foreach (var item in searcher.Get())
                    {
                        model = item["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                        break;
                    }
                }
                catch { }

                CpuModelName = model;
                CpuSummary = $"{model} | {physicalCount} cores / {logicalCount} threads";

                CpuOptions.Add("Automatic");
                for (int i = 1; i <= logicalCount; i++)
                    CpuOptions.Add($"{i} Core{(i > 1 ? "s" : "")}");
                if (string.IsNullOrWhiteSpace(App.Settings.Prop.SelectedCpuPriority))
                {
                    App.Settings.Prop.SelectedCpuPriority = "Automatic";
                }

                _selectedCpuPriority = App.Settings.Prop.SelectedCpuPriority;
                OnPropertyChanged(nameof(SelectedCpuPriority));
                App.Settings.Prop.TotalLogicalCores = logicalCount;
                App.Settings.Prop.TotalPhysicalCores = physicalCount;
            }
            catch
            {
                CpuOptions.Clear();
                CpuOptions.Add("Automatic");
                _selectedCpuPriority = "Automatic";
                CpuModelName = "Unknown CPU";
                CpuSummary = "CPU information unavailable";
                OnPropertyChanged(nameof(SelectedCpuPriority));
            }
        }


        private int GetPhysicalCoreCount()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("select NumberOfCores from Win32_Processor");
                int cores = 0;
                foreach (var item in searcher.Get())
                {
                    cores += Convert.ToInt32(item["NumberOfCores"]);
                }
                return cores > 0 ? cores : Environment.ProcessorCount;
            }
            catch
            {
                return Environment.ProcessorCount;
            }
        }

        public ObservableCollection<string> CpuOptions { get; } = new ObservableCollection<string>();

        private string _selectedCpuPriority = "Automatic";
        public string SelectedCpuPriority
        {
            get => _selectedCpuPriority;
            set
            {
                if (_selectedCpuPriority != value)
                {
                    _selectedCpuPriority = value;
                    OnPropertyChanged(nameof(SelectedCpuPriority));
                    App.Settings.Prop.SelectedCpuPriority = value;
                }
            }
        }

        public bool MultiInstanceLaunchingEnabled
        {
            get => App.Settings.Prop.MultiInstanceLaunching;
            set
            {
                if (App.Settings.Prop.MultiInstanceLaunching != value)
                {
                    App.Settings.Prop.MultiInstanceLaunching = value;
                    OnPropertyChanged(nameof(MultiInstanceLaunchingEnabled));

                    if (!value)
                    {
                        FixTeleportsEnabled = false;
                    }
                }
            }
        }

        public bool FixTeleportsEnabled
        {
            get => App.Settings.Prop.FixTeleports;
            set
            {
                if (App.Settings.Prop.FixTeleports != value)
                {
                    App.Settings.Prop.FixTeleports = value;
                    OnPropertyChanged(nameof(FixTeleportsEnabled));
                }
            }
        }

        public bool disablecrashhandleryayyysocool
        {
            get => App.Settings.Prop.DisableCrash;
            set
            {
                if (App.Settings.Prop.DisableCrash != value)
                {
                    App.Settings.Prop.DisableCrash = value;
                    OnPropertyChanged(nameof(disablecrashhandleryayyysocool));
                }
            }
        }

        public bool ConfirmLaunches
        {
            get => App.Settings.Prop.ConfirmLaunches;
            set => App.Settings.Prop.ConfirmLaunches = value;
        }

        public bool IsBetterServersEnabled
        {
            get => App.Settings.Prop.IsBetterServersEnabled;
            set => App.Settings.Prop.IsBetterServersEnabled = value;
        }

        public bool OverClockCPU
        {
            get => App.Settings.Prop.OverClockCPU;
            set => App.Settings.Prop.OverClockCPU = value;
        }

        public bool IsGameEnabled
        {
            get => App.Settings.Prop.IsGameEnabled;
            set => App.Settings.Prop.IsGameEnabled = value;
        }

        public bool OverClockGPU
        {
            get => App.Settings.Prop.OverClockGPU;
            set => App.Settings.Prop.OverClockGPU = value;
        }

        public bool OptimizeRoblox
        {
            get => App.Settings.Prop.OptimizeRoblox;
            set => App.Settings.Prop.OptimizeRoblox = value;
        }

        public bool MultiAccount
        {
            get => App.Settings.Prop.MultiAccount;
            set => App.Settings.Prop.MultiAccount = value;
        }

        public bool DX12Likely
        {
            get => App.Settings.Prop.DX12Like;
            set => App.Settings.Prop.DX12Like = value;
        }

        public bool ForceRobloxLanguage
        {
            get => App.Settings.Prop.ForceRobloxLanguage;
            set => App.Settings.Prop.ForceRobloxLanguage = value;
        }

        public bool RenameClientToEurotrucks2
        {
            get => App.Settings.Prop.RenameClientToEuroTrucks2;
            set => App.Settings.Prop.RenameClientToEuroTrucks2 = value;
        }

        public CleanerOptions SelectedCleanUpMode
        {
            get => App.Settings.Prop.CleanerOptions;
            set => App.Settings.Prop.CleanerOptions = value;
        }

        public IEnumerable<CleanerOptions> CleanerOptions => CleanerOptionsEx.Selections;

        public CleanerOptions CleanerOption
        {
            get => App.Settings.Prop.CleanerOptions;
            set => App.Settings.Prop.CleanerOptions = value;
        }

        private List<string> CleanerItems;

        private void UpdateCleanerItems()
        {
            App.Settings.Prop.CleanerDirectories = new List<string>(CleanerItems);
        }

        public bool CleanerLogs
        {
            get => CleanerItems.Contains("RobloxLogs");
            set
            {
                if (value && !CleanerItems.Contains("RobloxLogs"))
                {
                    CleanerItems.Add("RobloxLogs");
                    UpdateCleanerItems();
                }
                else if (!value && CleanerItems.Contains("RobloxLogs"))
                {
                    CleanerItems.Remove("RobloxLogs");
                    UpdateCleanerItems();
                }
                OnPropertyChanged(nameof(CleanerLogs));
            }
        }

        public bool CleanerCache
        {
            get => CleanerItems.Contains("RobloxCache");
            set
            {
                if (value && !CleanerItems.Contains("RobloxCache"))
                {
                    CleanerItems.Add("RobloxCache");
                    UpdateCleanerItems();
                }
                else if (!value && CleanerItems.Contains("RobloxCache"))
                {
                    CleanerItems.Remove("RobloxCache");
                    UpdateCleanerItems();
                }
                OnPropertyChanged(nameof(CleanerCache));
            }
        }

        public bool CleanerVoidstrap
        {
            get => CleanerItems.Contains("VoidstrapLogs");
            set
            {
                if (value && !CleanerItems.Contains("VoidstrapLogs"))
                {
                    CleanerItems.Add("VoidstrapLogs");
                    UpdateCleanerItems();
                }
                else if (!value && CleanerItems.Contains("VoidstrapLogs"))
                {
                    CleanerItems.Remove("VoidstrapLogs");
                    UpdateCleanerItems();
                }
                OnPropertyChanged(nameof(CleanerVoidstrap));
            }
        }
    }
}
