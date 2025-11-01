using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Voidstrap.Integrations;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;

namespace Voidstrap.UI.ViewModels.ContextMenu
{
    internal class ServerHistoryViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private readonly ActivityWatcher _activityWatcher;
        private readonly EventHandler _onGameLeaveHandler;
        public List<ActivityData> GameHistory { get; private set; } = new();
        public GenericTriState LoadState { get; private set; } = GenericTriState.Unknown;
        public string Error { get; private set; } = string.Empty;

        public ICommand CloseWindowCommand { get; }
        public ICommand CopyDeeplinkCommand { get; }
        public ICommand LaunchDeeplinkCommand { get; }

        public event EventHandler? RequestCloseEvent;

        private readonly string _historyFilePath = Path.Combine(Paths.Base, "ServerHistory.json");
        private const int MaxHistoryEntries = 30;

        public ServerHistoryViewModel(ActivityWatcher activityWatcher)
        {
            _activityWatcher = activityWatcher ?? throw new ArgumentNullException(nameof(activityWatcher));

            CloseWindowCommand = new RelayCommand(RequestClose);
            CopyDeeplinkCommand = new RelayCommand<ActivityData>(CopyDeeplinkToClipboard);
            LaunchDeeplinkCommand = new RelayCommand<ActivityData>(LaunchDeeplink);

            _onGameLeaveHandler = (_, _) => LoadDataAsync();
            _activityWatcher.OnGameLeave += _onGameLeaveHandler;

            LoadHistoryFromFile();
            LoadDataAsync();
        }

        private void LoadHistoryFromFile()
        {
            try
            {
                if (!File.Exists(_historyFilePath)) return;

                var json = File.ReadAllText(_historyFilePath);
                var savedHistory = JsonSerializer.Deserialize<List<ActivityData>>(json);

                if (savedHistory != null)
                {
                    MergeAndConsolidateHistory(savedHistory);
                    OnPropertyChanged(nameof(GameHistory));
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerHistoryViewModel::LoadHistoryFromFile", ex);
            }
        }

        private async void LoadDataAsync()
        {
            SetLoadingState();

            var history = _activityWatcher.History.ToList();
            var entriesWithoutDetails = history.Where(x => x.UniverseDetails == null).ToList();

            if (entriesWithoutDetails.Any())
                await TryLoadUniverseDetailsAsync(entriesWithoutDetails);

            MergeAndConsolidateHistory(history);
            foreach (var entry in GameHistory)
            {
                entry.ComputeDisplayTimes();
            }

            OnPropertyChanged(nameof(GameHistory));
            SaveHistoryToFile();
            SetSuccessState();
        }

        private void MergeAndConsolidateHistory(IEnumerable<ActivityData> incoming)
        {
            var dict = GameHistory.ToDictionary(
                x => $"{x.PlaceId}_{x.JobId}",
                x => x
            );

            foreach (var entry in incoming)
            {
                string key = $"{entry.PlaceId}_{entry.JobId}";

                if (dict.TryGetValue(key, out var existing))
                {
                    if (existing.TimeJoined > entry.TimeJoined)
                        existing.TimeJoined = entry.TimeJoined;
                    if (existing.TimeLeft < entry.TimeLeft)
                        existing.TimeLeft = entry.TimeLeft;
                    if (existing.RootActivity == null && entry.RootActivity != null)
                        existing.RootActivity = entry.RootActivity;
                    foreach (var kvp in entry.PlayerLogs)
                        existing.PlayerLogs[kvp.Key] = kvp.Value;
                    foreach (var kvp in entry.MessageLogs)
                        existing.MessageLogs[kvp.Key] = kvp.Value;
                    if (existing.UniverseDetails == null && entry.UniverseDetails != null)
                        existing.UniverseDetails = entry.UniverseDetails;
                }
                else
                {
                    dict[key] = entry;
                }
            }

            var seenRoots = new HashSet<string>();
            foreach (var kvp in dict.Values)
            {
                if (kvp.RootActivity != null)
                {
                    if (kvp.RootActivity.TimeLeft < kvp.TimeLeft)
                        kvp.RootActivity.TimeLeft = kvp.TimeLeft;

                    string rootKey = $"{kvp.PlaceId}_{kvp.JobId}";

                    if (!seenRoots.Contains(rootKey))
                    {
                        kvp.RootActivity.JobId = kvp.JobId;
                        seenRoots.Add(rootKey);
                    }
                    else
                    {
                        kvp.RootActivity = null;
                    }
                }
            }

            GameHistory = dict.Values
                .OrderByDescending(x => x.TimeJoined)
                .Take(MaxHistoryEntries)
                .ToList();
        }

        private void SaveHistoryToFile()
        {
            try
            {
                Directory.CreateDirectory(Paths.Base);
                string json = JsonSerializer.Serialize(GameHistory);
                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerHistoryViewModel::SaveHistoryToFile", ex);
            }
        }

        private void LaunchDeeplink(ActivityData data)
        {
            if (data == null) return;

            try
            {
                string url = data.GetInviteDeeplink();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerHistoryViewModel::LaunchDeeplink", ex);
            }
        }

        private void CopyDeeplinkToClipboard(ActivityData data)
        {
            if (data == null) return;

            try
            {
                Clipboard.SetText(data.GetInviteDeeplink());
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerHistoryViewModel::CopyDeeplinkToClipboard", ex);
            }
        }

        private void SetLoadingState()
        {
            LoadState = GenericTriState.Unknown;
            OnPropertyChanged(nameof(LoadState));
        }

        private async Task TryLoadUniverseDetailsAsync(List<ActivityData> entries)
        {
            try
            {
                string universeIds = string.Join(',', entries.Select(x => x.UniverseId).Distinct());
                await UniverseDetails.FetchBulk(universeIds);

                foreach (var entry in entries)
                    entry.UniverseDetails = UniverseDetails.LoadFromCache(entry.UniverseId);
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

        private void SetSuccessState()
        {
            LoadState = GenericTriState.Successful;
            OnPropertyChanged(nameof(LoadState));
        }

        private void RequestClose() => RequestCloseEvent?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            _activityWatcher.OnGameLeave -= _onGameLeaveHandler;
        }
    }
}
