using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Voidstrap.Integrations;
using Voidstrap.Models.Entities;

namespace Voidstrap.UI.ViewModels.Pages
{
    internal class HistoryPageViewModel : NotifyPropertyChangedViewModel
    {
        private readonly string _historyFilePath = Path.Combine(Paths.Base, "ServerHistory.json");
        private const int MaxHistoryEntries = 50;

        private ObservableCollection<ActivityData> _gameHistory = new();
        private GenericTriState _loadState = GenericTriState.Unknown;
        private string _error = string.Empty;

        public ObservableCollection<ActivityData> GameHistory
        {
            get => _gameHistory;
            private set
            {
                _gameHistory = value;
                OnPropertyChanged(nameof(GameHistory));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public bool IsEmpty => _gameHistory.Count == 0;

        public GenericTriState LoadState
        {
            get => _loadState;
            private set { _loadState = value; OnPropertyChanged(nameof(LoadState)); }
        }

        public string Error
        {
            get => _error;
            private set { _error = value; OnPropertyChanged(nameof(Error)); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand LaunchCommand { get; }
        public ICommand CopyLinkCommand { get; }

        public HistoryPageViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            ClearCommand = new RelayCommand(ClearHistory);
            LaunchCommand = new RelayCommand<ActivityData>(LaunchDeeplink);
            CopyLinkCommand = new RelayCommand<ActivityData>(CopyDeeplink);
            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            Error = string.Empty;

            try
            {
                var entries = await Task.Run(() => ReadFromFile());

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _gameHistory.Clear();
                    foreach (var entry in entries)
                        _gameHistory.Add(entry);

                    OnPropertyChanged(nameof(GameHistory));
                    OnPropertyChanged(nameof(IsEmpty));
                });

                if (entries.Count == 0)
                {
                    LoadState = GenericTriState.Successful;
                    return;
                }

                var missing = entries
                    .Where(x => x.UniverseDetails == null && x.UniverseId != 0)
                    .Select(x => x.UniverseId)
                    .Distinct()
                    .ToList();

                if (missing.Any())
                {
                    await TryFetchUniverseDetailsAsync(entries, missing);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _gameHistory.Clear();
                        foreach (var entry in entries)
                            _gameHistory.Add(entry);

                        OnPropertyChanged(nameof(GameHistory));
                        OnPropertyChanged(nameof(IsEmpty));
                    });
                }

                LoadState = GenericTriState.Successful;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("HistoryPageViewModel::LoadAsync", ex);
                Error = $"Failed to load history: {ex.Message}";
                LoadState = GenericTriState.Failed;
            }
        }

        private List<ActivityData> ReadFromFile()
        {
            try
            {
                if (!File.Exists(_historyFilePath))
                {
                    App.Logger.WriteLine("HistoryPageViewModel::ReadFromFile", "File does not exist");
                    return new List<ActivityData>();
                }

                var json = File.ReadAllText(_historyFilePath);
                App.Logger.WriteLine("HistoryPageViewModel::ReadFromFile", $"Read {json.Length} chars from file");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var data = JsonSerializer.Deserialize<List<ActivityData>>(json, options);
                App.Logger.WriteLine("HistoryPageViewModel::ReadFromFile", $"Deserialized {data?.Count ?? -1} entries");
                var entries = (data ?? new List<ActivityData>())
                    .OrderByDescending(x => x.TimeJoined)
                    .GroupBy(x => x.UniverseId)
                    .Select(g => g.First())
                    .Take(MaxHistoryEntries)
                    .ToList();

                foreach (var entry in entries)
                    entry.ComputeDisplayTimes();

                return entries;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("HistoryPageViewModel::ReadFromFile", ex);
                return new List<ActivityData>();
            }
        }

        private async Task TryFetchUniverseDetailsAsync(
            List<ActivityData> entries, List<long> missingIds)
        {
            try
            {
                string ids = string.Join(',', missingIds);
                await UniverseDetails.FetchBulk(ids);

                foreach (var entry in entries.Where(x => x.UniverseDetails == null))
                    entry.UniverseDetails = UniverseDetails.LoadFromCache(entry.UniverseId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("HistoryPageViewModel::TryFetchUniverseDetails", ex);
            }
        }

        private void ClearHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                    File.Delete(_historyFilePath);

                _gameHistory.Clear();
                OnPropertyChanged(nameof(GameHistory));
                OnPropertyChanged(nameof(IsEmpty));
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("HistoryPageViewModel::ClearHistory", ex);
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

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("HistoryPageViewModel::LaunchDeeplink", ex);
            }
        }

        private void CopyDeeplink(ActivityData? data)
        {
            if (data is null) return;
            try
            {
                Clipboard.SetText(data.GetInviteDeeplink());
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("HistoryPageViewModel::CopyDeeplink", ex);
            }
        }
    }
}