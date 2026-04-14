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

namespace Voidstrap.UI.ViewModels.ContextMenu
{
    internal class ServerHistoryViewModel : NotifyPropertyChangedViewModel, IDisposable
    {
        private readonly ActivityWatcher _activityWatcher;
        private readonly EventHandler _onGameLeaveHandler;

        private readonly string _historyFilePath = Path.Combine(Paths.Base, "ServerHistory.json");
        private const int MaxHistoryEntries = 30;

        public List<ActivityData> GameHistory { get; private set; } = new();
        public IEnumerable<ActivityData> Top10RecentHistory => GameHistory.Take(10);
        public GenericTriState LoadState { get; private set; } = GenericTriState.Unknown;
        public string Error { get; private set; } = string.Empty;

        public ICommand CloseWindowCommand { get; }
        public ICommand CopyDeeplinkCommand { get; }
        public ICommand LaunchDeeplinkCommand { get; }

        public event EventHandler? RequestCloseEvent;

        public ServerHistoryViewModel(ActivityWatcher activityWatcher)
        {
            _activityWatcher = activityWatcher ?? throw new ArgumentNullException(nameof(activityWatcher));

            CloseWindowCommand = new RelayCommand(RequestClose);
            CopyDeeplinkCommand = new RelayCommand<ActivityData>(CopyDeeplinkToClipboard);
            LaunchDeeplinkCommand = new RelayCommand<ActivityData>(LaunchDeeplink);

            LoadHistoryFromFile();
            _onGameLeaveHandler = async (_, _) => await LoadDataAsync();
            _activityWatcher.OnGameLeave += _onGameLeaveHandler;
            _ = LoadDataAsync();
        }

        private void LoadHistoryFromFile()
        {
            try
            {
                if (!File.Exists(_historyFilePath))
                    return;

                var json = File.ReadAllText(_historyFilePath);
                var saved = JsonSerializer.Deserialize<List<ActivityData>>(json);
                if (saved is null || saved.Count == 0)
                    return;

                MergeAndConsolidateHistory(saved);
                NotifyHistoryChanged();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerHistoryViewModel::LoadHistoryFromFile", ex);
            }
        }

        private async Task LoadDataAsync()
        {
            SetLoadingState();

            try
            {
                var history = _activityWatcher.History.ToList();
                var needsDetails = history
                    .Where(x => x.UniverseDetails == null && x.UniverseId != 0)
                    .ToList();

                if (needsDetails.Any())
                    await TryLoadUniverseDetailsAsync(needsDetails);

                MergeAndConsolidateHistory(history);

                foreach (var entry in GameHistory)
                    entry.ComputeDisplayTimes();

                await Task.Run(() => SaveHistoryToFile());

                Application.Current.Dispatcher.Invoke(() =>
                {
                    NotifyHistoryChanged();
                    SetSuccessState();
                });
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
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
                    if (existing.UniverseDetails == null && entry.UniverseDetails != null)
                        existing.UniverseDetails = entry.UniverseDetails;

                    foreach (var kvp in entry.PlayerLogs)
                        existing.PlayerLogs[kvp.Key] = kvp.Value;
                    foreach (var kvp in entry.MessageLogs)
                        existing.MessageLogs[kvp.Key] = kvp.Value;
                }
                else
                {
                    dict[key] = entry;
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
                var json = JsonSerializer.Serialize(GameHistory,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerHistoryViewModel::SaveHistoryToFile", ex);
            }
        }

        private async Task TryLoadUniverseDetailsAsync(List<ActivityData> entries)
        {
            try
            {
                string ids = string.Join(',', entries.Select(x => x.UniverseId).Distinct());
                await UniverseDetails.FetchBulk(ids);

                foreach (var entry in entries)
                    entry.UniverseDetails = UniverseDetails.LoadFromCache(entry.UniverseId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerHistoryViewModel::TryLoadUniverseDetailsAsync", ex);
            }
        }

        private void LaunchDeeplink(ActivityData? data)
        {
            if (data is null) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = data.GetInviteDeeplink(),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerHistoryViewModel::LaunchDeeplink", ex);
            }
        }

        private void CopyDeeplinkToClipboard(ActivityData? data)
        {
            if (data is null) return;
            try
            {
                Clipboard.SetText(data.GetInviteDeeplink());
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerHistoryViewModel::CopyDeeplinkToClipboard", ex);
            }
        }

        private void NotifyHistoryChanged()
        {
            OnPropertyChanged(nameof(GameHistory));
            OnPropertyChanged(nameof(Top10RecentHistory));
        }

        private void SetLoadingState()
        {
            LoadState = GenericTriState.Unknown;
            OnPropertyChanged(nameof(LoadState));
        }

        private void SetSuccessState()
        {
            LoadState = GenericTriState.Successful;
            OnPropertyChanged(nameof(LoadState));
        }

        private void HandleError(Exception ex)
        {
            App.Logger.WriteException("ServerHistoryViewModel::HandleError", ex);
            Error = $"Failed to load history: {ex.Message}";
            LoadState = GenericTriState.Failed;
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(Error));
                OnPropertyChanged(nameof(LoadState));
            });
        }

        private void RequestClose() => RequestCloseEvent?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            _activityWatcher.OnGameLeave -= _onGameLeaveHandler;
            GC.SuppressFinalize(this);
        }
    }
}