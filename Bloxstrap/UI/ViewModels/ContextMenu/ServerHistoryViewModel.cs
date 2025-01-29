using System.Windows.Input;
using Hellstrap.Integrations;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace Hellstrap.UI.ViewModels.ContextMenu
{
    internal class ServerHistoryViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ActivityWatcher _activityWatcher;

        public List<ActivityData>? GameHistory { get; private set; }
        public GenericTriState LoadState { get; private set; } = GenericTriState.Unknown;
        public string Error { get; private set; } = string.Empty;
        public ICommand CloseWindowCommand => new RelayCommand(RequestClose);

        public EventHandler? RequestCloseEvent;

        // Constructor with activity watcher injected
        public ServerHistoryViewModel(ActivityWatcher activityWatcher)
        {
            _activityWatcher = activityWatcher;
            _activityWatcher.OnGameLeave += (_, _) => LoadDataAsync(); // Calls LoadData asynchronously

            // Initially load data
            LoadDataAsync();
        }

        // Async method to load data
        private async void LoadDataAsync()
        {
            LoadState = GenericTriState.Unknown;
            OnPropertyChanged(nameof(LoadState));

            var entries = _activityWatcher.History.Where(x => x.UniverseDetails == null).ToList(); // Avoid multiple enumeration

            if (entries.Any())
            {
                string universeIds = string.Join(',', entries.Select(x => x.UniverseId).Distinct());

                try
                {
                    // Fetch details for all the unique universe IDs
                    await UniverseDetails.FetchBulk(universeIds);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("ServerHistoryViewModel::LoadData", ex);

                    Error = ex.Message;
                    OnPropertyChanged(nameof(Error));

                    LoadState = GenericTriState.Failed;
                    OnPropertyChanged(nameof(LoadState));
                    return;
                }

                // Populate UniverseDetails from the cache
                foreach (var entry in entries)
                {
                    entry.UniverseDetails = UniverseDetails.LoadFromCache(entry.UniverseId);
                }
            }

            // Initialize the GameHistory with the current state
            GameHistory = new List<ActivityData>(_activityWatcher.History);

            var consolidatedJobIds = new List<ActivityData>();

            // Consolidate activity entries from in-universe teleports
            foreach (var entry in _activityWatcher.History)
            {
                if (entry.RootActivity != null)
                {
                    // Update TimeLeft for root activities if necessary
                    if (entry.RootActivity.TimeLeft < entry.TimeLeft)
                    {
                        entry.RootActivity.TimeLeft = entry.TimeLeft;
                    }

                    // Consolidate the JobId for public servers
                    if (entry.ServerType == ServerType.Public && !consolidatedJobIds.Contains(entry))
                    {
                        entry.RootActivity.JobId = entry.JobId;
                        consolidatedJobIds.Add(entry);
                    }

                    GameHistory.Remove(entry);
                }
            }

            OnPropertyChanged(nameof(GameHistory));

            LoadState = GenericTriState.Successful;
            OnPropertyChanged(nameof(LoadState));
        }

        // Close window handler
        private void RequestClose() => RequestCloseEvent?.Invoke(this, EventArgs.Empty);

        // Optional: Ensure we unsubscribe from events to prevent memory leaks
        public void Dispose()
        {
            _activityWatcher.OnGameLeave -= (_, _) => LoadDataAsync();
        }
    }
}
