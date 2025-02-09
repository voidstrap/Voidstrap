using Hellstrap.AppData;
using Hellstrap.RobloxInterfaces;
using Hellstrap.UI.ViewModels;
using Hellstrap;

namespace Hellstrap.UI.ViewModels.Settings
{
    public class ChannelViewModel : NotifyPropertyChangedViewModel
    {
        private string _oldPlayerVersionGuid = "";
        private string _oldStudioVersionGuid = "";

        public ChannelViewModel()
        {
            // Initial loading of channel deploy info
            Task.Run(() => LoadChannelDeployInfo(App.Settings.Prop.Channel));
        }

        /// <summary>
        /// Enable or disable the update checking feature.
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

        /// <summary>
        /// Fetches channel deployment information asynchronously.
        /// </summary>
        private async Task LoadChannelDeployInfo(string channel)
        {
            // Reset loading and error states
            ShowLoadingError = false;
            OnPropertyChanged(nameof(ShowLoadingError));

            ChannelInfoLoadingText = "Fetching latest deploy info, please wait...";
            OnPropertyChanged(nameof(ChannelInfoLoadingText));

            ChannelDeployInfo = null;
            OnPropertyChanged(nameof(ChannelDeployInfo));

            try
            {
                // Fetch deployment info for the given channel
                ClientVersion info = await Deployment.GetInfo(false, channel);

                // Display warning if the channel is behind the default channel
                ShowChannelWarning = info.IsBehindDefaultChannel;
                OnPropertyChanged(nameof(ShowChannelWarning));

                // Update the deploy info with the fetched data
                ChannelDeployInfo = new DeployInfo
                {
                    Version = info.Version,
                    VersionGuid = info.VersionGuid
                };

                // Ensure the app doesn't ignore outdated channels
                App.State.Prop.IgnoreOutdatedChannel = true;
                OnPropertyChanged(nameof(ChannelDeployInfo));
            }
            catch (HttpRequestException)
            {
                // Handle HTTP request exceptions specifically
                ShowLoadingError = true;
                ChannelInfoLoadingText = "The channel is likely private or unreachable. Try using a version hash or change the channel.";
                OnPropertyChanged(nameof(ChannelInfoLoadingText));
            }
            catch (Exception ex)
            {
                // General error handler
                ShowLoadingError = true;
                ChannelInfoLoadingText = $"An error occurred while fetching the data: {ex.Message}";
                OnPropertyChanged(nameof(ChannelInfoLoadingText));
            }
        }

        /// <summary>
        /// Indicates if there is an error while loading channel info.
        /// </summary>
        public bool ShowLoadingError { get; set; }

        /// <summary>
        /// Displays a warning if the channel is behind the default one.
        /// </summary>
        public bool ShowChannelWarning { get; set; }

        /// <summary>
        /// Contains the deployment info of the channel.
        /// </summary>
        public DeployInfo? ChannelDeployInfo { get; private set; }

        /// <summary>
        /// Text indicating the status of the channel deployment info loading process.
        /// </summary>
        public string ChannelInfoLoadingText { get; private set; } = string.Empty;

        /// <summary>
        /// Sets and retrieves the current channel. Fetches deploy info upon update.
        /// </summary>
        public string ViewChannel
        {
            get => App.Settings.Prop.Channel;
            set
            {
                value = value.Trim();
                if (value.Equals("live", StringComparison.OrdinalIgnoreCase) || value.Equals("zlive", StringComparison.OrdinalIgnoreCase))
                {
                    App.Settings.Prop.Channel = "production";
                }
                else
                {
                    App.Settings.Prop.Channel = value;
                }

                // Trigger fetching deploy info for the selected channel
                Task.Run(() => LoadChannelDeployInfo(value));
            }
        }

        /// <summary>
        /// Enables or disables Roblox updates.
        /// </summary>
        public bool UpdateRoblox
        {
            get => App.Settings.Prop.UpdateRoblox;
            set => App.Settings.Prop.UpdateRoblox = value;
        }

        /// <summary>
        /// Forces Roblox reinstallation based on the version GUID.
        /// </summary>
        public bool ForceRobloxReinstallation
        {
            get
            {
                // Check for fresh installs (empty version GUIDs)
                return string.IsNullOrEmpty(App.State.Prop.Player.VersionGuid) && string.IsNullOrEmpty(App.State.Prop.Studio.VersionGuid);
            }
            set
            {
                if (value)
                {
                    // Store the current GUIDs to restore later
                    _oldPlayerVersionGuid = App.State.Prop.Player.VersionGuid;
                    _oldStudioVersionGuid = App.State.Prop.Studio.VersionGuid;

                    // Clear the current version GUIDs to trigger reinstallation
                    App.State.Prop.Player.VersionGuid = string.Empty;
                    App.State.Prop.Studio.VersionGuid = string.Empty;
                }
                else
                {
                    // Restore the original GUIDs after reinstallation is done
                    App.State.Prop.Player.VersionGuid = _oldPlayerVersionGuid;
                    App.State.Prop.Studio.VersionGuid = _oldStudioVersionGuid;
                }
            }
        }
    }
}
