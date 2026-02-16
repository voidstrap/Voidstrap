using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DRPC = DiscordRPC;

namespace Voidstrap.UI.ViewModels.ContextMenu
{
    public class MusicPlayerViewModel : INotifyPropertyChanged, IDisposable
    {
        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    OnPropertyChanged(nameof(SearchQuery));
                    UpdateFilteredLibrary();
                }
            }
        }

        public ObservableCollection<TrackItem> MusicLibrary { get; set; } = new();
        public ObservableCollection<TrackItem> FilteredMusicLibrary { get; set; } = new();
        public RelayCommand<TrackItem> DownloadCommand { get; }


        private void OnDownload(TrackItem track)
        {
            if (track == null || string.IsNullOrEmpty(track.FilePath) || !File.Exists(track.FilePath))
            {
                Frontend.ShowMessageBox("File not found.");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Title = $"Save a copy of {track.Title}",
                FileName = Path.GetFileName(track.FilePath),
                Filter = "Audio Files|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.wma|All Files|*.*"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(track.FilePath, saveDialog.FileName, overwrite: true);
                    Frontend.ShowMessageBox($"Saved: {saveDialog.FileName}");
                }
                catch (Exception ex)
                {
                    Frontend.ShowMessageBox($"Failed to save:\n{ex.Message}");
                }
            }
        }

private CancellationTokenSource? _searchCts;
private void UpdateFilteredLibrary()
{
    _searchCts?.Cancel();
    _searchCts = new CancellationTokenSource();
    var token = _searchCts.Token;
    var query = SearchQuery?.ToLower() ?? string.Empty;

    Task.Run(() =>
    {
        var filtered = MusicLibrary
            .Where(t => (t.Title?.ToLower().Contains(query) ?? false)
                     || (t.Artist?.ToLower().Contains(query) ?? false))
            .ToList();

        if (!token.IsCancellationRequested)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                FilteredMusicLibrary.Clear();
                foreach (var item in filtered)
                    FilteredMusicLibrary.Add(item);
            });
        }
    }, token);
}


        private static readonly MediaPlayer _player = new();
        private static bool _playerInitialized;

        private readonly DispatcherTimer _timer;
        private double _positionSeconds;
        private double _durationSeconds;
        private double _volume = 1;
        private bool _isPlaying;
        private string _status = "Ready. | ";
        private bool _isLooping;

        private TrackItem? _selectedTrack;
        public TrackItem NowPlaying { get; private set; } = new();

        private readonly string _savePath = Path.Combine(Paths.Base, "music.json");
        private DRPC.DiscordRpcClient? _rpcClient;
        private bool _rpcConnected;
        private DateTime _lastRpcRefreshUtc = DateTime.MinValue;

        public ObservableCollection<TrackItem> Tracks { get; } = new();

        private bool _isShuffling = true;
        public bool IsShuffling
        {
            get => _isShuffling;
            set
            {
                if (_isShuffling != value)
                {
                    _isShuffling = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShuffleLabel));
                    Status = IsShuffling ? "Shuffle On | " : "Shuffle Off | ";
                }
            }
        }
        public string ShuffleLabel => IsShuffling ? "Shuffle On" : "Shuffle Off";

        public RelayCommand<TrackItem> RemoveTrackCommand { get; }
        public RelayCommand OpenFilesCommand { get; }
        public RelayCommand ConnectRpcCommand { get; }
        public RelayCommand PlayPauseCommand { get; }
        public RelayCommand NextCommand { get; }
        public RelayCommand PreviousCommand { get; }
        public RelayCommand StopCommand { get; }
        public RelayCommand ToggleLoopCommand { get; }
        public RelayCommand ToggleShuffleCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public string RpcButtonLabel => _rpcConnected ? "Disconnect RPC" : "Connect RPC";
        private DateTime _lastSaveUtc = DateTime.MinValue;
        private void SaveLibraryThrottled()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastSaveUtc) >= TimeSpan.FromSeconds(1.5))
            {
                _lastSaveUtc = now;
                SaveLibrary();
            }
        }

        private static readonly Random _rng = new Random();
        public MusicPlayerViewModel()
        {
            Directory.CreateDirectory(Paths.Base);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (!_playerInitialized)
            {
                _player.MediaOpened += Player_MediaOpened;
                _player.MediaEnded += Player_MediaEnded;
                _player.Volume = _volume;
                _playerInitialized = true;
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += (_, __) =>
            {
                if (_player.NaturalDuration.HasTimeSpan)
                    NowPlayingDurationSeconds = _player.NaturalDuration.TimeSpan.TotalSeconds;

                var currentPos = _player.Position.TotalSeconds;

                if (_isPlaying && Math.Abs(PositionSeconds - currentPos) > 0.25)
                {
                    PositionSeconds = currentPos;
                    SaveLibraryThrottled();
                }

                if (_isPlaying && _rpcConnected && _rpcClient?.IsInitialized == true)
                {
                    if (DateTime.UtcNow - _lastRpcRefreshUtc > TimeSpan.FromSeconds(8))
                        UpdateRpcPresence();
                }
            };
            _timer.Start();

            OpenFilesCommand = new RelayCommand(OpenFiles);
            ConnectRpcCommand = new RelayCommand(() => ConnectRpc());
            PlayPauseCommand = new RelayCommand(PlayPause);
            NextCommand = new RelayCommand(Next);
            PreviousCommand = new RelayCommand(Previous);
            StopCommand = new RelayCommand(Stop);
            RemoveTrackCommand = new RelayCommand<TrackItem>(RemoveTrack);
            ToggleLoopCommand = new RelayCommand(() =>
            {
                IsLooping = !IsLooping;
                Status = IsLooping ? "Loop On | " : "Loop Off | ";
                UpdateRpcPresence(true);
                SaveLibraryThrottled();
            });
            ToggleShuffleCommand = new RelayCommand(() =>
            {
                IsShuffling = !IsShuffling;
                UpdateRpcPresence(true);
                SaveLibraryThrottled();
            });

            LoadLibrary();
            foreach (var track in Tracks)
                track.PropertyChanged += Track_PropertyChanged;

            Tracks.CollectionChanged += Tracks_CollectionChanged;

            UpdateNowPlayingBindings();

            if (Tracks.Count > 0 && string.IsNullOrEmpty(NowPlaying.FilePath))
            {
                SelectedTrack = Tracks.First();
                LoadAndPlay(SelectedTrack!, autoPlay: true);
            }
            MusicLibrary.Add(new TrackItem
            {
                Title = "𝕊𝕝𝕠𝕨𝕖𝕕 𝕁𝕦𝕚𝕔𝕖 𝕎𝕣𝕝𝕕 𝕊𝕠𝕟𝕘𝕤 𝕥𝕠 𝕧𝕚𝕓𝕖 𝕥𝕠 𝕒𝕥 𝟙𝔸𝕄",
                Artist = "Juice WRLD - 999",
                FilePath = "https://drive.google.com/file/d/1_gnZeKti2DyomKTVyik3i7I_wpbT0mF_/view?usp=sharing",
                Icon = null
            });
            MusicLibrary.Add(new TrackItem
            {
                Title = "LUA NA PRACA Super slowed & reverb 1 HOUR by Bratic",
                Artist = "Dj Samir",
                FilePath = "https://drive.google.com/file/d/1fS3uH9ExBdShOE8nUVuzlrJaHjIc-5pq/view?usp=sharing",
                Icon = null
            });
            MusicLibrary.Add(new TrackItem
            {
                Title = "Juice WRLD - Come & Go (with Marshmello)",
                Artist = "Juice WRLD X Marshmello",
                FilePath = "https://drive.google.com/file/d/1jLx2E3O6fu7YHXkLzee9IL-sySHBXtaT/view?usp=sharing",
                Icon = null
            });
            MusicLibrary.Add(new TrackItem
            {
                Title = "VICTIMiZED",
                Artist = "STYXVII",
                FilePath = "https://drive.google.com/file/d/1lvjXjTQcVbsMjEeYYhxJkLWbFkewcnzB/view?usp=sharing",
                Icon = null
            });
            MusicLibrary.Add(new TrackItem
            {
                Title = "Fighting My Demons",
                Artist = "Ken Carson",
                FilePath = "https://drive.google.com/file/d/1Y8nQDh75YT1V28Up4gCKaHbMj2xP6STA/view?usp=sharing",
                Icon = null
            });
            MusicLibrary.Add(new TrackItem
            {
                Title = "NUMBER",
                Artist = "TWERKNATION28 😏",
                FilePath = "https://drive.google.com/file/d/1sW9G6qGJpAiRxeJdPyQkkIQQQrtdmLsm/view?usp=sharing",
                Icon = null
            });
            MusicLibrary.Add(new TrackItem
            {
                Title = "ITS NOT ME ITS YOU",
                Artist = "STYXVII",
                FilePath = "https://drive.google.com/file/d/1HiKzafmcEzSGYsjk_sdmyhbiUd-yxg_n/view?usp=sharing",
                Icon = null
            });
            MusicLibrary.Add(new TrackItem
            {
                Title = "David Goggins Motivation!!!!",
                Artist = "No Artist",
                FilePath = "https://drive.google.com/file/d/1uayWptCKn_Jwwivj4cijIoOasFH9uFOJ/view?usp=sharing",
                Icon = null
            });
            MusicLibrary.Add(new TrackItem
            {
                Title = "The Exaltation of Styx",
                Artist = "STYXVII",
                FilePath = "https://drive.google.com/file/d/1TFGAJ4JVR_nivenkCLNvNqwp80UZhZL9/view?usp=sharing",
                Icon = null
            });
            MusicLibrary.Add(new TrackItem
            {
                Title = "Annamaria, What Have They Done To You",
                Artist = "STYXVII",
                FilePath = "https://drive.google.com/file/d/1s4HHI2wrDblV0mbgOjK8yqt0BuQU0_bs/view?usp=sharing",
                Icon = null
            });
            MusicLibrary.Add(new TrackItem
            {
                Title = "BIRDBRAIN (w OK Glass) feat. Kasane Teto",
                Artist = "Jamie Paige / JamieP",
                FilePath = "https://drive.google.com/file/d/1b4YxZdDtoWJbh1EFYjLSx7aHTI0bFc1C/view?usp=sharing",
                Icon = null
            });
            MusicLibrary.Add(new TrackItem
            {
                Title = "Sugarcube Hailstorm - PaperKitty",
                Artist = "PaperKitty",
                FilePath = "https://drive.google.com/file/d/1ZspIeh1rinejQsKSF_4iIXKrqygxui5k/view?usp=sharing",
                Icon = null
            });
            _ = Task.Run(DownloadLibraryTracksAsync);
            DownloadCommand = new RelayCommand<TrackItem>(OnDownload);
            UpdateFilteredLibrary();
        }

        private void Tracks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (TrackItem newItem in e.NewItems)
                    newItem.PropertyChanged += Track_PropertyChanged;
            }

            if (e.OldItems != null)
            {
                foreach (TrackItem oldItem in e.OldItems)
                    oldItem.PropertyChanged -= Track_PropertyChanged;
            }

            SaveLibraryThrottled();
        }

        private void Track_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TrackItem.Title) ||
                e.PropertyName == nameof(TrackItem.Artist))
            {
                var track = sender as TrackItem;
                Status = $"Updated: {track?.Title ?? "Unknown"} | ";
                SaveLibraryThrottled();
            }
        }

        #region Commands

        private void OpenFiles()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import audio or video files (audio-only playback)",
                Filter = "Audio/Video files (*.mp3;*.wav;*.wma;*.aac;*.m4a;*.flac;*.mp4;*.mkv;*.mov;*.avi)|*.mp3;*.wav;*.wma;*.aac;*.m4a;*.flac;*.mp4;*.mkv;*.mov;*.avi|All files (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true)
                return;

            foreach (var path in dlg.FileNames)
            {
                if (Tracks.Any(t => string.Equals(t.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var extension = Path.GetExtension(path).Trim('.').ToUpperInvariant();
                var isVideo = new[] { "MP4", "MKV", "MOV", "AVI", "WEBM" }.Contains(extension);

                var item = new TrackItem
                {
                    FilePath = path,
                    Title = Path.GetFileNameWithoutExtension(path),
                    FileType = isVideo ? "VIDEO (AUDIO ONLY)" : extension,
                    Icon = GetFileIcon(path)
                };

                TryProbeDuration(item);
                Tracks.Add(item);
            }

            if (Tracks.Count > 0 && string.IsNullOrEmpty(NowPlaying.FilePath))
            {
                SelectedTrack = Tracks.First();
                LoadAndPlay(SelectedTrack!, autoPlay: true);
            }

            SaveLibraryThrottled();
            UpdateRpcPresence(true);
        }

        private void RemoveTrack(TrackItem? track)
        {
            if (track == null) return;

            bool wasNowPlaying = !string.IsNullOrEmpty(NowPlaying.FilePath) &&
                                 string.Equals(NowPlaying.FilePath, track.FilePath, StringComparison.OrdinalIgnoreCase);

            Tracks.Remove(track);

            if (wasNowPlaying)
            {
                Stop();
                NowPlaying = new TrackItem();
                UpdateNowPlayingBindings();
            }

            Status = $"Removed: {track.Title} | ";
            SaveLibraryThrottled();
            UpdateRpcPresence(true);
        }

        #endregion

        #region Properties

        public TrackItem? SelectedTrack
        {
            get => _selectedTrack;
            set
            {
                _selectedTrack = value;
                OnPropertyChanged();
                if (value != null)
                    LoadAndPlay(value, autoPlay: true);
                UpdateNowPlayingBindings();
            }
        }

        public double Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0, 1);
                _player.Volume = _volume;
                OnPropertyChanged();
                SaveLibraryThrottled();
                UpdateRpcPresence();
            }
        }

        public bool IsLooping
        {
            get => _isLooping;
            set { _isLooping = value; OnPropertyChanged(); OnPropertyChanged(nameof(LoopLabel)); }
        }

        public string LoopLabel => IsLooping ? "Loop On" : "Loop Off";
        public string PlayPauseLabel => _isPlaying ? "Pause" : "Play";
        public string PositionString => FormatTime(PositionSeconds);
        public string DurationString => FormatTime(NowPlayingDurationSeconds);

        public double PositionSeconds
        {
            get => _positionSeconds;
            set
            {
                if (Math.Abs(_positionSeconds - value) > 0.25)
                {
                    _positionSeconds = value;
                    try
                    {
                        _player.Position = TimeSpan.FromSeconds(Math.Max(0, _positionSeconds));
                    }
                    catch {}

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PositionString));

                    if (_isPlaying)
                        UpdateRpcPresence(true);
                }
            }
        }

        public double NowPlayingDurationSeconds
        {
            get => _durationSeconds;
            private set
            {
                _durationSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationString));
            }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        #endregion

        #region Player

        private void PlayPause()
        {
            try
            {
                if (string.IsNullOrEmpty(NowPlaying.FilePath) || !File.Exists(NowPlaying.FilePath))
                {
                    if (Tracks.Count > 0)
                    {
                        SelectedTrack = Tracks.First();
                        LoadAndPlay(SelectedTrack!, autoPlay: true);
                    }
                    else
                    {
                        Status = "No track selected. | ";
                    }
                    return;
                }

                if (_isPlaying)
                {
                    _player.Pause();
                    _isPlaying = false;
                    Status = "Paused. | ";
                }
                else
                {
                    _player.Play();
                    _isPlaying = true;
                    Status = $"Playing: {NowPlaying.Title} | ";
                }

                RefreshUI();
                SaveLibraryThrottled();
                UpdateRpcPresence(true);
            }
            catch (Exception ex)
            {
                Status = $"Play failed: {ex.Message} | ";
            }
        }

        private void LoadAndPlay(TrackItem item, bool autoPlay)
        {
            try
            {
                _player.Stop();

                if (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
                {
                    Status = "File not found. | ";
                    return;
                }
                var uri = Uri.TryCreate(item.FilePath, UriKind.Absolute, out var u) ? u : new Uri(item.FilePath, UriKind.Absolute);

                _player.Open(uri);
                NowPlaying = item;
                UpdateNowPlayingBindings();

                EventHandler? opened = null;
                opened = (_, __) =>
                {
                    try
                    {
                        if (autoPlay)
                        {
                            _isPlaying = true;
                            _player.Play();
                            Status = $"Playing: {item.Title} | ";
                        }
                        else
                        {
                            _isPlaying = false;
                            Status = $"Ready: {item.Title} | ";
                        }
                        RefreshUI();
                        SaveLibraryThrottled();
                        UpdateRpcPresence(true);
                    }
                    finally
                    {
                        _player.MediaOpened -= opened!;
                    }
                };
                _player.MediaOpened += opened;
            }
            catch (Exception ex)
            {
                Status = $"Failed to load: {item.Title} ({ex.Message}) | ";
            }
        }

        private void Next()
        {
            if (Tracks.Count == 0) return;

            int nextIndex;

            if (IsShuffling)
            {
                if (Tracks.Count == 1)
                {
                    _player.Position = TimeSpan.Zero;
                    _player.Play();
                    _isPlaying = true;
                    Status = $"Playing: {NowPlaying?.Title} | ";
                    RefreshUI();
                    return;
                }

                do
                {
                    nextIndex = _rng.Next(Tracks.Count);
                } while (Tracks[nextIndex] == SelectedTrack);
            }
            else
            {
                int currentIndex = SelectedTrack != null ? Tracks.IndexOf(SelectedTrack) : -1;
                nextIndex = (currentIndex + 1) % Tracks.Count;
            }

            SelectedTrack = Tracks[nextIndex];
            NowPlaying = SelectedTrack!;
            LoadAndPlay(NowPlaying, autoPlay: true);
            UpdateNowPlayingBindings();
            RefreshUI();
            SaveLibraryThrottled();
            UpdateRpcPresence(true);
        }

        private void Previous()
        {
            if (Tracks.Count == 0) return;
            int idx = SelectedTrack != null ? Tracks.IndexOf(SelectedTrack) : 0;
            idx = (idx - 1 + Tracks.Count) % Tracks.Count;
            SelectedTrack = Tracks[idx];
            LoadAndPlay(SelectedTrack!, autoPlay: true);
        }

        private void Stop()
        {
            try
            {
                _player.Stop();
            }
            catch { /* ignore */ }

            _isPlaying = false;
            PositionSeconds = 0;
            Status = "Stopped. | ";
            RefreshUI();
            SaveLibraryThrottled();
            UpdateRpcPresence(true);
        }

        #endregion

        #region RPC

        private void ConnectRpc(bool isAutoReconnect = false)
        {
            try
            {
                if (_rpcConnected && _rpcClient != null)
                {
                    try
                    {
                        _rpcClient.ClearPresence();
                    }
                    catch {}

                    _rpcClient.Dispose();
                    _rpcClient = null;
                    _rpcConnected = false;
                    Status = "RPC Disconnected. | ";
                    OnPropertyChanged(nameof(RpcButtonLabel));
                    Frontend.ShowMessageBox("Discord RPC disconnected.");
                    return;
                }

                Status = "Connecting to RPC... | ";
                _rpcClient?.Dispose();
                _rpcConnected = false;

                const string clientId = "1375529225230094507";
                _rpcClient = new DRPC.DiscordRpcClient(clientId);

                _rpcClient.OnReady += (sender, e) =>
                {
                    _rpcConnected = true;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Status = $"RPC Connected as {e.User.Username} | ";
                        OnPropertyChanged(nameof(RpcButtonLabel));
                        if (!isAutoReconnect)
                            Frontend.ShowMessageBox($"Discord RPC connected as {e.User.Username}.\nThis makes Roblox RPC not display on your Profile!");
                        UpdateRpcPresence(true);
                    });
                };

                _rpcClient.OnError += (sender, e) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _rpcConnected = false;
                        Status = $"RPC Error: {e.Message} | ";
                        OnPropertyChanged(nameof(RpcButtonLabel));
                        Frontend.ShowMessageBox($"RPC Error: {e.Message}");
                    });
                };

                _rpcClient.Initialize();
                UpdateRpcPresence(true);
            }
            catch (Exception ex)
            {
                _rpcConnected = false;
                Status = $"Failed to connect RPC: {ex.Message} | ";
                OnPropertyChanged(nameof(RpcButtonLabel));
                Frontend.ShowMessageBox($"Failed to connect to RPC:\n{ex.Message}");
            }
        }

        private void UpdateRpcPresence(bool force = false)
        {
            if (!_rpcConnected || _rpcClient == null || !_rpcClient.IsInitialized)
                return;
            if (!force)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastRpcRefreshUtc) < TimeSpan.FromSeconds(3))
                    return;
                _lastRpcRefreshUtc = now;
            }
            else
            {
                _lastRpcRefreshUtc = DateTime.UtcNow;
            }

            DRPC.RichPresence presence;

            if (NowPlaying == null || string.IsNullOrWhiteSpace(NowPlaying.Title))
            {
                presence = new DRPC.RichPresence
                {
                    Details = "Idle",
                    State = "Voidstrap Music Player",
                    Assets = new DRPC.Assets
                    {
                        LargeImageKey = "voidstrap_logo",
                        LargeImageText = "Voidstrap Music Player"
                    }
                };
            }
            else
            {
                string loopText = IsLooping ? " (Loop)" : string.Empty;
                string playIcon = _isPlaying ? "play_icon" : "pause_icon";

                double elapsed = Math.Max(0, PositionSeconds);
                double total = Math.Max(1, NowPlaying.Duration.TotalSeconds);
                double remaining = Math.Max(0, total - elapsed);

                string elapsedStr = FormatTime(elapsed);
                string totalStr = FormatTime(total);
                string infoString = $"{(NowPlaying.FileType ?? "FILE").ToUpperInvariant()} • {elapsedStr} / {totalStr} • {(_isPlaying ? "Playing" : "Paused")} in Voidstrap{loopText}";

                presence = new DRPC.RichPresence
                {
                    Details = _isPlaying ? $"🎵 {NowPlaying.Title}" : $"⏸️ {NowPlaying.Title}",
                    State = infoString,
                    Assets = new DRPC.Assets
                    {
                        LargeImageKey = "voidstrap_logo",
                        LargeImageText = "Voidstrap Music Player",
                        SmallImageKey = playIcon,
                        SmallImageText = _isPlaying ? "Playing" : "Paused"
                    }
                };
                if (_isPlaying && total > 1)
                {
                    DateTime nowUtc = DateTime.UtcNow;
                    presence.Timestamps = new DRPC.Timestamps
                    {
                        Start = nowUtc - TimeSpan.FromSeconds(elapsed),
                        End = nowUtc + TimeSpan.FromSeconds(remaining)
                    };
                }
            }

            try
            {
                _rpcClient.SetPresence(presence);
            }
            catch {}
        }

        #endregion

        #region Helpers

        private void Player_MediaOpened(object? sender, EventArgs e)
        {
            if (_player.NaturalDuration.HasTimeSpan)
            {
                NowPlaying.Duration = _player.NaturalDuration.TimeSpan;
                NowPlayingDurationSeconds = NowPlaying.Duration.TotalSeconds;
                RefreshUI();
                UpdateRpcPresence(true);
            }
        }

        private void Player_MediaEnded(object? sender, EventArgs e)
        {
            try
            {
                if (IsLooping)
                {
                    _player.Position = TimeSpan.Zero;
                    _player.Play();
                    _isPlaying = true;
                    Status = $"Looping: {NowPlaying.Title} | ";
                    RefreshUI();
                    UpdateRpcPresence(true);
                    return;
                }
                Next();
            }
            catch (Exception ex)
            {
                Status = $"Error advancing track: {ex.Message} | ";
            }
        }

        private void RefreshUI()
        {
            OnPropertyChanged(nameof(PlayPauseLabel));
            OnPropertyChanged(nameof(PositionString));
            OnPropertyChanged(nameof(DurationString));
        }

        private void TryProbeDuration(TrackItem item)
        {
            try
            {
                var probe = new MediaPlayer();
                EventHandler onOpened = null!;
                onOpened = (_, __) =>
                {
                    try
                    {
                        if (probe.NaturalDuration.HasTimeSpan)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                item.Duration = probe.NaturalDuration.TimeSpan;
                            });
                        }
                    }
                    finally
                    {
                        probe.MediaOpened -= onOpened;
                        try { probe.Close(); } catch { }
                    }
                };
                probe.MediaOpened += onOpened;
                probe.Open(new Uri(item.FilePath));
            }
            catch { }
        }

        private static ImageSource? GetFileIcon(string path)
        {
            try
            {
                using Icon? icon = Icon.ExtractAssociatedIcon(path);
                if (icon == null) return null;
                using var bmp = icon.ToBitmap();
                var hbitmap = bmp.GetHbitmap();
                try
                {
                    return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                finally
                {
                    try { NativeMethods.DeleteObject(hbitmap); } catch { }
                }
            }
            catch { return null; }
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 0.5) return "0:00";
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{t.Minutes}:{t.Seconds:00}";
        }

        private void UpdateNowPlayingBindings()
        {
            if (NowPlaying == null || string.IsNullOrEmpty(NowPlaying.FilePath))
                NowPlaying = new TrackItem { Title = "—", FileType = "", FilePath = "" };

            OnPropertyChanged(nameof(NowPlaying));
            OnPropertyChanged(nameof(NowPlayingDurationSeconds));
            OnPropertyChanged(nameof(DurationString));
            OnPropertyChanged(nameof(PositionString));
        }

        private void SaveLibrary()
        {
            try
            {
                var saveData = new
                {
                    Tracks = Tracks.Select(t => new
                    {
                        t.Title,
                        t.Artist,
                        t.FilePath,
                        t.FileType,
                        Duration = Math.Max(0, t.Duration.TotalSeconds)
                    }).ToList(),
                    NowPlaying = NowPlaying?.FilePath ?? "",
                    Volume = _volume,
                    Position = Math.Max(0, _player?.Position.TotalSeconds ?? 0),
                    Selected = SelectedTrack?.FilePath ?? "",
                    Looping = IsLooping,
                    WasPlaying = _isPlaying,
                    RpcConnected = _rpcConnected
                };

                var json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_savePath, json);
            }
            catch (Exception ex)
            {
                Status = $"Failed to save library: {ex.Message} | ";
            }
        }

        private void LoadLibrary()
        {
            try
            {
                if (!File.Exists(_savePath)) return;

                var json = File.ReadAllText(_savePath);
                using var data = JsonDocument.Parse(json);

                if (data.RootElement.TryGetProperty("Tracks", out var tracksElem))
                {
                    foreach (var t in tracksElem.EnumerateArray())
                    {
                        string path = t.TryGetProperty("FilePath", out var fp) ? (fp.GetString() ?? "") : "";
                        if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                        var item = new TrackItem
                        {
                            Title = t.TryGetProperty("Title", out var titleProp)
                                ? titleProp.GetString() ?? Path.GetFileNameWithoutExtension(path)
                                : Path.GetFileNameWithoutExtension(path),

                            Artist = t.TryGetProperty("Artist", out var artistProp)
                                ? artistProp.GetString() ?? ""
                                : "",

                            FilePath = path,
                            FileType = t.TryGetProperty("FileType", out var ft) ? (ft.GetString() ?? "FILE") : "FILE",
                            Icon = GetFileIcon(path),
                            Duration = TimeSpan.FromSeconds(t.TryGetProperty("Duration", out var dur) ? SafeGetDouble(dur) : 0)
                        };
                        Tracks.Add(item);
                    }
                }

                string? selected = data.RootElement.TryGetProperty("Selected", out var sel) ? sel.GetString() : null;
                string? last = data.RootElement.TryGetProperty("NowPlaying", out var np) ? np.GetString() : null;
                double pos = data.RootElement.TryGetProperty("Position", out var ps) ? SafeGetDouble(ps) : 0;
                _volume = data.RootElement.TryGetProperty("Volume", out var vol) ? SafeGetDouble(vol) : 1;
                _player.Volume = _volume;
                _isLooping = data.RootElement.TryGetProperty("Looping", out var lp) && lp.GetBoolean();
                bool wasPlaying = data.RootElement.TryGetProperty("WasPlaying", out var wp) && wp.GetBoolean();
                _rpcConnected = data.RootElement.TryGetProperty("RpcConnected", out var rpc) && rpc.GetBoolean();

                if (!string.IsNullOrEmpty(selected))
                {
                    var selTrack = Tracks.FirstOrDefault(t => string.Equals(t.FilePath, selected, StringComparison.OrdinalIgnoreCase));
                    if (selTrack != null)
                        SelectedTrack = selTrack;
                }

                if (!string.IsNullOrEmpty(last))
                {
                    var found = Tracks.FirstOrDefault(t => string.Equals(t.FilePath, last, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        NowPlaying = found;
                        UpdateNowPlayingBindings();

                        _player.Open(new Uri(found.FilePath, UriKind.Absolute));
                        EventHandler opened = null!;
                        opened = (_, __) =>
                        {
                            try
                            {
                                if (_player.NaturalDuration.HasTimeSpan)
                                {
                                    var total = _player.NaturalDuration.TimeSpan.TotalSeconds;
                                    var clamped = (pos >= 0 && pos < total) ? pos : 0;
                                    _player.Position = TimeSpan.FromSeconds(clamped);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        PositionSeconds = clamped;
                                    });
                                }
                                if (wasPlaying)
                                {
                                    _player.Play();
                                    _isPlaying = true;
                                    Status = $"Resumed playing: {found.Title} at {FormatTime(PositionSeconds)} | ";
                                }
                                else
                                {
                                    _isPlaying = false;
                                    Status = $"Ready: {found.Title} | Resumed at {FormatTime(PositionSeconds)} | ";
                                }

                                OnPropertyChanged(nameof(PlayPauseLabel));
                                UpdateRpcPresence(true);
                            }
                            finally
                            {
                                _player.MediaOpened -= opened;
                            }
                        };
                        _player.MediaOpened += opened;
                    }
                }

                if (_rpcConnected)
                {
                    ConnectRpc(isAutoReconnect: true);
                }

                Status = $"Loaded {Tracks.Count} tracks. | ";
            }
            catch (Exception ex)
            {
                Status = $"Failed to load library: {ex.Message} | ";
            }
        }

        private static double SafeGetDouble(JsonElement el)
        {
            try { return el.GetDouble(); } catch { return 0; }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr hObject);
        }

        private static string NormalizeDownloadUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;
            try
            {
                if (url.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(url, @"[-\w]{25,}");
                    if (match.Success)
                    {
                        string id = match.Value;
                        return $"https://drive.google.com/uc?export=download&id={id}";
                    }
                }
                if (url.Contains("dropbox.com", StringComparison.OrdinalIgnoreCase))
                {
                    if (url.Contains("?dl=0"))
                        return url.Replace("?dl=0", "?dl=1");
                    if (url.Contains("www.dropbox.com"))
                        return url.Replace("www.dropbox.com", "dl.dropboxusercontent.com");
                }
                if (url.Contains("1drv.ms", StringComparison.OrdinalIgnoreCase))
                {
                    if (!url.Contains("download=1"))
                        return url.Contains("?") ? $"{url}&download=1" : $"{url}?download=1";
                }
                if (url.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase))
                {
                    if (url.Contains("/d/"))
                    {
                        var idMatch = Regex.Match(url, @"/d/([a-zA-Z0-9_-]+)");
                        if (idMatch.Success)
                        {
                            string id = idMatch.Groups[1].Value;
                            return $"https://drive.google.com/uc?export=download&id={id}";
                        }
                    }
                }
                if (url.Contains("view?usp=", StringComparison.OrdinalIgnoreCase))
                    url = url.Replace("view?usp=", "uc?export=download&usp=");
                return url;
            }
            catch
            {
                return url;
            }
        }

        private async Task DownloadLibraryTracksAsync()
        {
            try
            {
                string downloadsFolder = Path.Combine(Paths.Base, "MusicDownloads");
                Directory.CreateDirectory(downloadsFolder);
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    UseCookies = true
                };
                using var client = new HttpClient(handler);

                foreach (var track in MusicLibrary.ToList())
                {
                    if (string.IsNullOrWhiteSpace(track.FilePath))
                        continue;

                    string fixedUrl = NormalizeDownloadUrl(track.FilePath);
                    track.FilePath = fixedUrl;

                    string safeName = Regex.Replace(track.Title ?? "track", @"[\\/:*?""<>|]", "_");
                    string fileName = $"{safeName}.mp3";
                    string localPath = Path.Combine(downloadsFolder, fileName);

                    if (File.Exists(localPath))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            track.FilePath = localPath;
                            track.Icon = GetFileIcon(localPath);
                            Status = $"Ready: {track.Title} | ";
                        });
                        continue;
                    }

                    try
                    {
                        Status = $"Downloading {track.Title}...";
                        OnPropertyChanged(nameof(Status));

                        using var response = await client.GetAsync(fixedUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();

                        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        await using var file = File.Create(localPath);
                        await stream.CopyToAsync(file).ConfigureAwait(false);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            track.FilePath = localPath;
                            track.Icon = GetFileIcon(localPath);
                            Status = $"Downloaded: {track.Title} | ";
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Status = $"Failed to download {track.Title}: {ex.Message} | ";
                        });
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    SaveLibrary();
                    UpdateFilteredLibrary();
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Status = $"Download failed: {ex.Message} | ";
                });
            }
        }
        #endregion

        public void Dispose()
        {
            try
            {
                SaveLibrary();
                try { _player.Stop(); } catch { }

                if (_rpcConnected && _rpcClient != null)
                {
                    try
                    {
                        _rpcClient.ClearPresence();
                        _rpcClient.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Status = $"Error disconnecting RPC: {ex.Message} | ";
                    }
                    finally
                    {
                        _rpcClient = null;
                        _rpcConnected = false;
                        Status = "RPC disconnected on app close. | ";
                    }
                }
            }
            catch (Exception ex)
            {
                Status = $"Error during cleanup: {ex.Message} | ";
            }
        }
    }
}
