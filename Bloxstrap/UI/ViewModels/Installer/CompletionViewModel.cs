using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voidstrap.Resources;
using Microsoft.Win32;
using Voidstrap.RobloxInterfaces;

namespace Voidstrap.UI.ViewModels.Installer
{
    public class CompletionViewModel : ObservableObject
    {
        private readonly System.Timers.Timer _saveTimer = new System.Timers.Timer(5000);

        public ICommand LaunchSettingsCommand { get; }
        public ICommand LaunchRobloxCommand { get; }
        public event EventHandler<NextAction>? CloseWindowRequest;

        private bool _showLoadingError;
        public bool ShowLoadingError
        {
            get => _showLoadingError;
            set => SetProperty(ref _showLoadingError, value);
        }

        private string _channelInfoLoadingText = "";
        public string ChannelInfoLoadingText
        {
            get => _channelInfoLoadingText;
            set => SetProperty(ref _channelInfoLoadingText, value);
        }

        private DeployInfo? _channelDeployInfo;
        public DeployInfo? ChannelDeployInfo
        {
            get => _channelDeployInfo;
            set => SetProperty(ref _channelDeployInfo, value);
        }

        private bool _showChannelWarning;
        public bool ShowChannelWarning
        {
            get => _showChannelWarning;
            set => SetProperty(ref _showChannelWarning, value);
        }

        private string _viewChannel;
        public string ViewChannel
        {
            get => _viewChannel;
            set
            {
                string trimmedValue = value?.Trim() ?? "production";
                if (_viewChannel == trimmedValue) return;

                _viewChannel = trimmedValue;
                OnPropertyChanged();
                _ = LoadChannelDeployInfoAsync(trimmedValue);
                App.Settings.Prop.Channel = trimmedValue;

                _saveTimer.Stop();
                _saveTimer.Start();
            }
        }

        public CompletionViewModel()
        {
            LaunchSettingsCommand = new RelayCommand(() => CloseWindowRequest?.Invoke(this, NextAction.LaunchSettings));
            LaunchRobloxCommand = new RelayCommand(() => CloseWindowRequest?.Invoke(this, NextAction.LaunchRoblox));

            _viewChannel = App.Settings.Prop.Channel;
            _ = LoadChannelDeployInfoAsync(App.Settings.Prop.Channel);

            // Configure the Timer correctly
            _saveTimer.Elapsed += (s, e) => App.State.Save();
            _saveTimer.AutoReset = false; // Prevent it from running indefinitely
        }

        private async Task LoadChannelDeployInfoAsync(string channel)
        {
            try
            {
                // Reset UI states before fetching
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
                ChannelDeployInfo = new DeployInfo { Version = info.Version, VersionGuid = info.VersionGuid };
                App.State.Prop.IgnoreOutdatedChannel = true;

                // Notify UI about updates
                OnPropertyChanged(nameof(ShowChannelWarning));
                OnPropertyChanged(nameof(ChannelDeployInfo));
            }
            catch (HttpRequestException)
            {
                ShowLoadingError = true;
                ChannelInfoLoadingText = "The channel is likely private or unreachable. Try using a version hash or change the channel.";
            }
            catch (TaskCanceledException)
            {
                ShowLoadingError = true;
                ChannelInfoLoadingText = "The request timed out. Please check your internet connection and try again.";
            }
            catch (Exception ex)
            {
                ShowLoadingError = true;
                ChannelInfoLoadingText = $"An unexpected error occurred: {ex.Message}";
            }
            finally
            {
                // Ensure UI reflects error state
                OnPropertyChanged(nameof(ShowLoadingError));
                OnPropertyChanged(nameof(ChannelInfoLoadingText));
            }
        }
    }
}