using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Voidstrap.AppData;
using Voidstrap.RobloxInterfaces;
using Voidstrap.UI.ViewModels;
using Voidstrap;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class ChannelViewModel : NotifyPropertyChangedViewModel
    {

        private string _oldPlayerVersionGuid = "";
        private string _oldStudioVersionGuid = "";
        private CancellationTokenSource? _loadChannelCts;
        public ObservableCollection<int> CpuLimitOptions { get; set; }

        private static readonly IReadOnlyDictionary<string, ChannelChangeMode> _channelChangeModes =
            new Dictionary<string, ChannelChangeMode>(3)
            {
                { Strings.Menu_Channel_ChangeAction_Automatic, ChannelChangeMode.Automatic },
                { Strings.Menu_Channel_ChangeAction_Prompt, ChannelChangeMode.Prompt },
                { Strings.Menu_Channel_ChangeAction_Ignore, ChannelChangeMode.Ignore }
            };

        private bool _showLoadingError = false;
        private bool _showChannelWarning = false;
        private DeployInfo? _channelDeployInfo;
        private string _channelInfoLoadingText = string.Empty;

        public ChannelViewModel()
        {
            CpuLimitOptions = new ObservableCollection<int>();
            int coreCount = Environment.ProcessorCount;

            for (int i = 1; i <= coreCount; i++)
            {
                CpuLimitOptions.Add(i);
            }

            // Validate and assign default CPU core limit
            if (!CpuLimitOptions.Contains(App.Settings.Prop.CpuCoreLimit))
            {
                SelectedCpuLimit = coreCount;
            }

            // Safely load deployment info
            _ = LoadChannelDeployInfoSafeAsync(App.Settings.Prop.Channel);
        }

        private async Task LoadChannelDeployInfoSafeAsync(string channel)
        {
            try
            {
                await LoadChannelDeployInfoAsync(channel);
            }
            catch (Exception ex)
            {
                // Log or handle error appropriately
                Debug.WriteLine($"Failed to load channel info: {ex.Message}");
            }
        }

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

        public string ViewChannel
        {
            get => App.Settings.Prop.Channel;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;

                var trimmedValue = value.Trim().ToLowerInvariant();

                if (string.Equals(App.Settings.Prop.Channel, trimmedValue, StringComparison.Ordinal))
                    return;

                App.Settings.Prop.Channel = trimmedValue;
                RunSafeAsync(() => LoadChannelDeployInfoAsync(trimmedValue));
            }
        }

        public string ChannelHash
        {
            get => App.Settings.Prop.ChannelHash;
            set
            {
                const string VersionHashPattern = @"version-(.*)";
                if (string.IsNullOrEmpty(value) || Regex.IsMatch(value, VersionHashPattern))
                {
                    App.Settings.Prop.ChannelHash = value;
                }
            }
        }

        public bool UpdateRoblox
        {
            get => App.Settings.Prop.UpdateRoblox;
            set => App.Settings.Prop.UpdateRoblox = value;
        }

        public bool HWAsselEnabled
        {
            get => App.Settings.Prop.WPFSoftwareRender;
            set => App.Settings.Prop.WPFSoftwareRender = value;
        }

        public IReadOnlyDictionary<string, ChannelChangeMode> ChannelChangeModes => _channelChangeModes;

        public string SelectedChannelChangeMode
        {
            get => _channelChangeModes.FirstOrDefault(x => x.Value == App.Settings.Prop.ChannelChangeMode).Key ?? string.Empty;
            set
            {
                if (_channelChangeModes.TryGetValue(value, out var mode))
                {
                    App.Settings.Prop.ChannelChangeMode = mode;
                }
            }
        }

        

        private async Task LoadChannelDeployInfoAsync(string channel)
        {
            // Cancel any previous loading operation
            _loadChannelCts?.Cancel();
            _loadChannelCts = new CancellationTokenSource();
            var token = _loadChannelCts.Token;

            ShowLoadingError = false;
            ChannelDeployInfo = null;
            ChannelInfoLoadingText = "Fetching latest deploy info, please wait...";
            ShowChannelWarning = false;

            RaiseUIProperties();

            try
            {
                // Fetch info with cancellation support
                var info = await Deployment.GetInfo(channel).ConfigureAwait(false);

                // Throw if cancellation requested
                token.ThrowIfCancellationRequested();

                // Update properties only if this task is still the latest
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
                // Ignore cancellation exceptions — they're expected when switching channels
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    ShowLoadingError = true;
                    ChannelInfoLoadingText = $"The channel is likely private. Change channel, or try again later.\nError: {ex.Message}";
                }
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    RaiseUIProperties();
                }
            }
        }

        private void RaiseUIProperties()
        {
            OnPropertyChanged(nameof(ShowLoadingError));
            OnPropertyChanged(nameof(ChannelDeployInfo));
            OnPropertyChanged(nameof(ChannelInfoLoadingText));
            OnPropertyChanged(nameof(ShowChannelWarning));
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
