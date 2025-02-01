using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Hellstrap.Integrations;
using CommunityToolkit.Mvvm.Input;

namespace Hellstrap.UI.ViewModels.ContextMenu
{
    internal class ServerInformationViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ActivityWatcher _activityWatcher;

        public string InstanceId => _activityWatcher?.Data?.JobId ?? Strings.Common_NotAvailable;

        public string ServerType => _activityWatcher?.Data?.ServerType.ToTranslatedString() ?? Strings.Common_NotAvailable;

        private string _serverLocation = Strings.Common_Loading;
        public string ServerLocation
        {
            get => _serverLocation;
            private set
            {
                if (_serverLocation != value)
                {
                    _serverLocation = value;
                    OnPropertyChanged(nameof(ServerLocation));
                }
            }
        }

        public Visibility ServerLocationVisibility => App.Settings.Prop.ShowServerDetails ? Visibility.Visible : Visibility.Collapsed;

        public ICommand CopyInstanceIdCommand { get; }
        public ICommand RefreshServerLocationCommand { get; }

        public ServerInformationViewModel(Watcher watcher)
        {
            _activityWatcher = watcher?.ActivityWatcher ?? throw new ArgumentNullException(nameof(watcher));

            CopyInstanceIdCommand = new RelayCommand(CopyInstanceId);
            RefreshServerLocationCommand = new AsyncRelayCommand(QueryServerLocationAsync);

            // Begin loading server location immediately if details are visible.
            if (ServerLocationVisibility == Visibility.Visible)
                _ = QueryServerLocationAsync();
        }

        private async Task QueryServerLocationAsync()
        {
            try
            {
                // Display "Loading..." initially
                ServerLocation = Strings.Common_Loading;

                // Fetch the server location
                string? location = await _activityWatcher.Data.QueryServerLocation();

                // Update with the retrieved location or fallback to "Not Available"
                ServerLocation = string.IsNullOrEmpty(location)
                    ? Strings.Common_NotAvailable
                    : location;
            }
            catch (Exception ex)
            {
                // Handle errors gracefully
                ServerLocation = Strings.Common_ErrorFetchingLocation;
                Console.WriteLine($"Error querying server location: {ex.Message}");
            }
        }

        private void CopyInstanceId()
        {
            try
            {
                Clipboard.SetDataObject(InstanceId);
            }
            catch (Exception ex)
            {
                // Log or handle clipboard errors
                Console.WriteLine($"Error copying instance ID: {ex.Message}");
            }
        }
    }

    // Strings resource class (fully defined)
    public static class Strings
    {
        public static string Common_Loading => "Loading...";
        public static string Common_NotAvailable => "Not Available";
        public static string Common_ErrorFetchingLocation => "Error fetching server location.";
    }
}
