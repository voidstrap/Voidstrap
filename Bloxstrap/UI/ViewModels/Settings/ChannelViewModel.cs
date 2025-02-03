using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hellstrap.AppData;
using Hellstrap.RobloxInterfaces;

namespace Hellstrap.UI.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for managing Roblox channel settings.
    /// </summary>
    public class ChannelViewModel : NotifyPropertyChangedViewModel
    {
        private string _oldPlayerVersionGuid = string.Empty;
        private string _oldStudioVersionGuid = string.Empty;

        public ChannelViewModel()
        {
            Task.Run(async () => await LoadChannelDeployInfoAsync(App.Settings.Prop.Channel));
        }

        /// <summary>
        /// Gets or sets whether update checking is enabled.
        /// </summary>
        public bool UpdateCheckingEnabled
        {
            get => App.Settings.Prop.CheckForUpdates;
            set
            {
                if (App.Settings.Prop.CheckForUpdates != value)
                {
                    App.Settings.Prop.CheckForUpdates = value;
                    OnPropertyChanged(nameof(UpdateCheckingEnabled));
                }
            }
        }

        public bool AnalyticsEnabled
        {
            get => App.Settings.Prop.EnableAnalytics;
            set
            {
                if (App.Settings.Prop.EnableAnalytics != value)
                {
                    App.Settings.Prop.EnableAnalytics = value;
                    OnPropertyChanged(nameof(AnalyticsEnabled));
                }
            }
        }

        public bool ExportConfig
        {
            get => App.Settings.Prop.ShouldExportConfig;
            set
            {
                if (App.Settings.Prop.ShouldExportConfig != value)
                {
                    App.Settings.Prop.ShouldExportConfig = value;
                    OnPropertyChanged(nameof(ExportConfig));
                }
            }
        }

        /// <summary>
        /// Loads deployment information asynchronously.
        /// </summary>
        private async Task LoadChannelDeployInfoAsync(string channel)
        {
            try
            {
                ShowLoadingError = false;
                ChannelInfoLoadingText = "Fetching latest deploy info, please wait...";
                OnPropertyChanged(nameof(ShowLoadingError));
                OnPropertyChanged(nameof(ChannelInfoLoadingText));

                // Simulating fetch process
                await Task.Delay(500);

                ChannelDeployInfo = null; // Assume a fetch operation here
                OnPropertyChanged(nameof(ChannelDeployInfo));
            }
            catch (Exception ex)
            {
                ShowLoadingError = true;
                ChannelInfoLoadingText = $"The channel is likely private. Try a different channel or version hash.\nError: {ex.Message}";
                OnPropertyChanged(nameof(ShowLoadingError));
                OnPropertyChanged(nameof(ChannelInfoLoadingText));
            }
        }

        public bool ShowLoadingError { get; private set; }
        public bool ShowChannelWarning { get; private set; }

        public DeployInfo? ChannelDeployInfo { get; private set; }
        public string ChannelInfoLoadingText { get; private set; } = string.Empty;

        /// <summary>
        /// Gets or sets the currently viewed channel.
        /// </summary>
        public string ViewChannel
        {
            get => App.Settings.Prop.Channel;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;

                string trimmedValue = value.Trim();
                Task.Run(async () => await LoadChannelDeployInfoAsync(trimmedValue));

                App.Settings.Prop.Channel = trimmedValue.Equals("live", StringComparison.OrdinalIgnoreCase) ||
                                            trimmedValue.Equals("zlive", StringComparison.OrdinalIgnoreCase)
                    ? "production"
                    : trimmedValue;

                OnPropertyChanged(nameof(ViewChannel));
            }
        }

        /// <summary>
        /// Gets or sets the channel hash while ensuring it follows the expected format.
        /// </summary>
        public string ChannelHash
        {
            get => App.Settings.Prop.ChannelHash;
            set
            {
                const string VersionHashFormat = @"version-(.*)";
                if (Regex.IsMatch(value, VersionHashFormat) || string.IsNullOrEmpty(value))
                {
                    App.Settings.Prop.ChannelHash = value;
                    OnPropertyChanged(nameof(ChannelHash));
                }
            }
        }

        /// <summary>
        /// Determines whether a forced reinstallation of Roblox is needed.
        /// </summary>
        public bool ForceRobloxReinstallation
        {
            get => string.IsNullOrEmpty(App.State.Prop.Player.VersionGuid) &&
                   string.IsNullOrEmpty(App.State.Prop.Studio.VersionGuid);
            set
            {
                if (value)
                {
                    _oldPlayerVersionGuid = App.State.Prop.Player.VersionGuid;
                    _oldStudioVersionGuid = App.State.Prop.Studio.VersionGuid;
                    App.State.Prop.Player.VersionGuid = string.Empty;
                    App.State.Prop.Studio.VersionGuid = string.Empty;
                }
                else
                {
                    App.State.Prop.Player.VersionGuid = _oldPlayerVersionGuid;
                    App.State.Prop.Studio.VersionGuid = _oldStudioVersionGuid;
                }

                OnPropertyChanged(nameof(ForceRobloxReinstallation));
            }
        }
    }
}
