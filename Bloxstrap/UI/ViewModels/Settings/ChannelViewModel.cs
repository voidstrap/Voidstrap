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
            Task.Run(() => LoadChannelDeployInfo(App.Settings.Prop.Channel));
        }

        public bool UpdateCheckingEnabled
        {
            get => App.Settings.Prop.CheckForUpdates;
            set => App.Settings.Prop.CheckForUpdates = value;
        }

        private async Task LoadChannelDeployInfo(string channel)
        {
            try
            {
                // Reset UI states
                ShowLoadingError = false;
                ChannelDeployInfo = null;
                ChannelInfoLoadingText = "Fetching latest deploy info, please wait...";
                OnPropertyChanged(nameof(ShowLoadingError));
                OnPropertyChanged(nameof(ChannelDeployInfo));
                OnPropertyChanged(nameof(ChannelInfoLoadingText));

                // Fetch deployment info
                ClientVersion info = await Deployment.GetInfo(channel);

                // Update properties based on fetched data
                ShowChannelWarning = info.IsBehindDefaultChannel;
                ChannelDeployInfo = new DeployInfo
                {
                    Version = info.Version,
                    VersionGuid = info.VersionGuid
                };

                App.State.Prop.IgnoreOutdatedChannel = true;

                // Notify UI about updates
                OnPropertyChanged(nameof(ShowChannelWarning));
                OnPropertyChanged(nameof(ChannelDeployInfo));
            }
            catch (Exception ex)
            {
                // Handle errors gracefully
                ShowLoadingError = true;
                ChannelInfoLoadingText =
                    $"The channel is likely private. Use version hash, change channel, or try again later.\nError: {ex.Message}";

                // Notify UI about error state
                OnPropertyChanged(nameof(ShowLoadingError));
                OnPropertyChanged(nameof(ChannelInfoLoadingText));
            }
        }


        public bool ShowLoadingError { get; set; } = false;
        public bool ShowChannelWarning { get; set; } = false;

        public DeployInfo? ChannelDeployInfo { get; private set; } = null;
        public string ChannelInfoLoadingText { get; private set; } = null!;

        public string ViewChannel
        {
            get => App.Settings.Prop.Channel;
            set
            {
                // Trim and make sure the value is in lowercase for consistent comparison
                string trimmedValue = value.Trim().ToLower();

                // Handle the logic for setting the channel
                // Set the channel to the provided value directly
                App.Settings.Prop.Channel = trimmedValue;

                // Asynchronously load channel deploy info, but without blocking the setter.
                Task.Run(async () => await LoadChannelDeployInfo(trimmedValue));
            }
        }





        public bool UpdateRoblox
        {
            get => App.Settings.Prop.UpdateRoblox;
            set => App.Settings.Prop.UpdateRoblox = value;
        }

        public bool ForceRobloxReinstallation
        {
            // wouldnt it be better to check old version guids?
            // what about fresh installs?
            get => String.IsNullOrEmpty(App.State.Prop.Player.VersionGuid) && String.IsNullOrEmpty(App.State.Prop.Studio.VersionGuid);
            set
            {
                if (value)
                {
                    _oldPlayerVersionGuid = App.State.Prop.Player.VersionGuid;
                    _oldStudioVersionGuid = App.State.Prop.Studio.VersionGuid;
                    App.State.Prop.Player.VersionGuid = "";
                    App.State.Prop.Studio.VersionGuid = "";
                }
                else
                {
                    App.State.Prop.Player.VersionGuid = _oldPlayerVersionGuid;
                    App.State.Prop.Studio.VersionGuid = _oldStudioVersionGuid;
                }
            }
        }
    }
}