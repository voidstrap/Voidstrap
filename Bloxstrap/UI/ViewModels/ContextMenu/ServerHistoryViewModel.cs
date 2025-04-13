using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Voidstrap.Integrations;
using CommunityToolkit.Mvvm.Input;

namespace Voidstrap.UI.ViewModels.ContextMenu
{
    internal class ServerHistoryViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private readonly ActivityWatcher _activityWatcher;
        private readonly EventHandler _onGameLeaveHandler;

        public List<ActivityData>? GameHistory { get; private set; }
        public GenericTriState LoadState { get; private set; } = GenericTriState.Unknown;
        public string Error { get; private set; } = string.Empty;
        public ICommand CloseWindowCommand { get; }
        public event EventHandler? RequestCloseEvent;

        public ServerHistoryViewModel(ActivityWatcher activityWatcher)
        {
            _activityWatcher = activityWatcher ?? throw new ArgumentNullException(nameof(activityWatcher));
            CloseWindowCommand = new RelayCommand(RequestClose);
            _onGameLeaveHandler = (_, _) => LoadDataAsync();
            _activityWatcher.OnGameLeave += _onGameLeaveHandler;
            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            SetLoadingState();
            var history = _activityWatcher.History.ToList();
            var entriesWithoutDetails = history.Where(x => x.UniverseDetails == null).ToList();

            if (entriesWithoutDetails.Any())
            {
                await TryLoadUniverseDetailsAsync(entriesWithoutDetails);
            }

            GameHistory = new List<ActivityData>(history);
            ConsolidateActivityEntries();
            OnPropertyChanged(nameof(GameHistory));
            SetSuccessState();
        }

        private void SetLoadingState()
        {
            if (LoadState != GenericTriState.Unknown)
            {
                LoadState = GenericTriState.Unknown;
                OnPropertyChanged(nameof(LoadState));
            }
        }

        private async Task TryLoadUniverseDetailsAsync(List<ActivityData> entries)
        {
            try
            {
                string universeIds = string.Join(',', entries.Select(x => x.UniverseId).Distinct());
                await UniverseDetails.FetchBulk(universeIds);
                foreach (var entry in entries)
                {
                    entry.UniverseDetails = UniverseDetails.LoadFromCache(entry.UniverseId);
                }
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void HandleError(Exception ex)
        {
            App.Logger.WriteException("ServerHistoryViewModel::LoadData", ex);
            Error = $"Failed to load universe details: {ex.Message}";
            OnPropertyChanged(nameof(Error));
            LoadState = GenericTriState.Failed;
            OnPropertyChanged(nameof(LoadState));
        }

        private void ConsolidateActivityEntries()
        {
            var consolidatedJobIds = new HashSet<ActivityData>();
            foreach (var entry in GameHistory ?? new List<ActivityData>())
            {
                if (entry.RootActivity != null)
                {
                    if (entry.RootActivity.TimeLeft < entry.TimeLeft)
                    {
                        entry.RootActivity.TimeLeft = entry.TimeLeft;
                    }

                    if (entry.ServerType == ServerType.Public && !consolidatedJobIds.Contains(entry))
                    {
                        entry.RootActivity.JobId = entry.JobId;
                        consolidatedJobIds.Add(entry);
                    }
                }
            }
        }

        private void SetSuccessState()
        {
            if (LoadState != GenericTriState.Successful)
            {
                LoadState = GenericTriState.Successful;
                OnPropertyChanged(nameof(LoadState));
            }
        }

        private void RequestClose() => RequestCloseEvent?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            _activityWatcher.OnGameLeave -= _onGameLeaveHandler;
        }
    }
}
