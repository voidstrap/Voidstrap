using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRichPresence = DiscordRPC.RichPresence;

namespace Voidstrap.UI.ViewModels.ContextMenu
{
    public class RPCCustomizerViewModel : INotifyPropertyChanged
    {
        private readonly SemaphoreSlim _opGate = new(1, 1);
        private readonly object _rpcLock = new();

        private DiscordRpcClient _client;
        private bool _isStarting, _isStopping, _rpcConnected, _isLoadingConfig;

        private readonly string _configPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                         "Voidstrap", "discord-rpc.json");

        private CancellationTokenSource _saveCts;
        private CancellationTokenSource _presenceCts;

        private readonly DispatcherTimer _reconnectTimer;
        private readonly Dispatcher _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        private string _applicationId;
        private string _appName = "Voidstrap";
        private string _details = "";
        private string _state = "";
        private string _largeImageKey = "large";
        private string _smallImageKey = "small";
        private bool _button1Enabled, _button2Enabled;
        private string _button1Label = "Website";
        private string _button1Url = "https://example.com";
        private string _button2Label = "Join";
        private string _button2Url = "https://discord.gg/";
        private string _statusMessage = "Idle";
        private Brush _statusColor = Brushes.Gray;

        public RelayCommand StartRpcCommand { get; }
        public RelayCommand StopRpcCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand UpdatePresenceCommand { get; }

        private bool _autoStartRpc;
        public bool AutoStartRpc
        {
            get => _autoStartRpc;
            set => SetValue(ref _autoStartRpc, value);
        }

        public RPCCustomizerViewModel()
        {
            StartRpcCommand = new RelayCommand(() => _ = SafeStartRpcAsync(), CanStartRpc);
            StopRpcCommand = new RelayCommand(() => _ = SafeStopRpcAsync(), CanStopRpc);
            UpdatePresenceCommand = new RelayCommand(() => _ = SafeManualUpdateAsync());
            CloseCommand = new RelayCommand(async () =>
            {
                try { await SafeStopRpcAsync().ConfigureAwait(false); }
                finally
                {
                    var window = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                    window?.Close();
                }
            });

            _reconnectTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromSeconds(10),
                IsEnabled = false
            };
            _reconnectTimer.Tick += async (_, __) => await CheckReconnectAsync().ConfigureAwait(false);

            LoadConfigInternal();
            AppDomain.CurrentDomain.ProcessExit += (_, __) =>
            {
                try { SaveConfigInternal(); } catch { }
            };
        }

        #region Properties
        public string ApplicationId { get => _applicationId; set { if (SetField(ref _applicationId, value)) { DebouncedSave(); UpdateCommands(); } } }
        public string AppName { get => _appName; set => SetValue(ref _appName, value); }
        public string Details { get => _details; set => SetValue(ref _details, value); }
        public string State { get => _state; set => SetValue(ref _state, value); }
        public string LargeImageKey { get => _largeImageKey; set => SetValue(ref _largeImageKey, value); }
        public string SmallImageKey { get => _smallImageKey; set => SetValue(ref _smallImageKey, value); }

        public bool Button1Enabled { get => _button1Enabled; set => SetValue(ref _button1Enabled, value); }
        public bool Button2Enabled { get => _button2Enabled; set => SetValue(ref _button2Enabled, value); }

        public string Button1Label { get => _button1Label; set => SetValue(ref _button1Label, value); }
        public string Button1Url { get => _button1Url; set => SetValue(ref _button1Url, value); }
        public string Button2Label { get => _button2Label; set => SetValue(ref _button2Label, value); }
        public string Button2Url { get => _button2Url; set => SetValue(ref _button2Url, value); }

        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
        public Brush StatusColor { get => _statusColor; set => SetField(ref _statusColor, value); }
        #endregion

        #region Helpers
        private bool SetValue<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (!SetField(ref field, value, name)) return false;
            DebouncedSave();
            SchedulePresenceUpdate();
            return true;
        }

        private void DebouncedSave()
        {
            if (_isLoadingConfig) return;
            _saveCts?.Cancel();
            _saveCts = new CancellationTokenSource();
            _ = DebounceAsync(async ct =>
            {
                SafeUpdateStatus("Saving pending...", Brushes.DarkGray);
                await Task.Delay(1000, ct).ConfigureAwait(false);
                SaveConfigInternal();
            }, _saveCts.Token);
        }

        private void SchedulePresenceUpdate()
        {
            if (_isLoadingConfig) return;
            if (_client == null || !_rpcConnected) return;

            _presenceCts?.Cancel();
            _presenceCts = new CancellationTokenSource();
            _ = DebounceAsync(async ct =>
            {
                await Task.Delay(1500, ct).ConfigureAwait(false);
                await _dispatcher.InvokeAsync(UpdatePresence, DispatcherPriority.Background);
            }, _presenceCts.Token);
        }

        private static async Task DebounceAsync(Func<CancellationToken, Task> work, CancellationToken ct)
        {
            try { await work(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        private void DispatcherInvokeSafe(Action action)
        {
            try
            {
                if (_dispatcher == null || _dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished) return;
                if (_dispatcher.CheckAccess()) action();
                else _dispatcher.BeginInvoke(action, DispatcherPriority.Background);
            }
            catch { }
        }

        private void SafeUpdateStatus(string msg, Brush color)
        {
            DispatcherInvokeSafe(() =>
            {
                StatusMessage = msg;
                StatusColor = color;
            });
        }

        private void UpdateCommands()
        {
            DispatcherInvokeSafe(() =>
            {
                StartRpcCommand.RaiseCanExecuteChanged();
                StopRpcCommand.RaiseCanExecuteChanged();
            });
        }
        #endregion

        #region RPC Control
        private bool CanStartRpc() => !_isStarting && !_isStopping && !string.IsNullOrWhiteSpace(ApplicationId);
        private bool CanStopRpc() => !_isStopping && _client != null;

        private async Task SafeStartRpcAsync()
        {
            if (!CanStartRpc()) return;

            await _opGate.WaitAsync().ConfigureAwait(false);
            try
            {
                _isStarting = true; UpdateCommands();
                SafeUpdateStatus("Starting Discord RPC...", Brushes.Orange);

                StopClientIfRunning();

                if (string.IsNullOrWhiteSpace(ApplicationId))
                {
                    SafeUpdateStatus("Missing Application ID", Brushes.Red);
                    return;
                }

                _rpcConnected = false;

                var client = new DiscordRpcClient(ApplicationId)
                {
                    Logger = new ConsoleLogger { Level = LogLevel.Warning }
                };

                client.OnReady += (_, e) =>
                {
                    _rpcConnected = true;
                    SafeUpdateStatus($"Connected as {e.User.Username}", Brushes.LimeGreen);
                    DispatcherInvokeSafe(UpdatePresence);
                };

                client.OnClose += (_, __) =>
                {
                    _rpcConnected = false;
                    SafeUpdateStatus("Disconnected from Discord", Brushes.OrangeRed);
                };

                client.OnError += (_, e) =>
                {
                    _rpcConnected = false;
                    SafeUpdateStatus($"Error: {e.Message}", Brushes.Red);
                };

                bool initSuccess;
                try { initSuccess = client.Initialize(); }
                catch (Exception ex)
                {
                    SafeUpdateStatus($"Failed to initialize RPC client: {ex.Message}", Brushes.Red);
                    client.Dispose();
                    return;
                }

                if (!initSuccess)
                {
                    SafeUpdateStatus("Failed to initialize RPC client", Brushes.Red);
                    client.Dispose();
                    return;
                }

                lock (_rpcLock) { _client = client; }

                _reconnectTimer.IsEnabled = true;
                SafeUpdateStatus("Discord RPC running", Brushes.DeepSkyBlue);
            }
            finally
            {
                _isStarting = false; UpdateCommands();
                _opGate.Release();
            }
        }

        private async Task SafeStopRpcAsync()
        {
            if (!CanStopRpc()) return;

            await _opGate.WaitAsync().ConfigureAwait(false);
            try
            {
                _isStopping = true; UpdateCommands();
                SafeUpdateStatus("Stopping RPC...", Brushes.Orange);

                _reconnectTimer.IsEnabled = false;
                _presenceCts?.Cancel();
                _saveCts?.Cancel();

                StopClientIfRunning();
                _client = null;
                _rpcConnected = false;

                SafeUpdateStatus("RPC Stopped", Brushes.Gray);
            }
            finally
            {
                _isStopping = false; UpdateCommands();
                _opGate.Release();
            }
        }

        private void StopClientIfRunning()
        {
            lock (_rpcLock)
            {
                if (_client == null) return;
                try { _client.ClearPresence(); } catch { }
                try { _client.Dispose(); } catch { }
            }
        }

        private async Task SafeManualUpdateAsync()
        {
            if (_client == null || !_rpcConnected) return;
            SafeUpdateStatus("Manually updating presence...", Brushes.Orange);
            await _dispatcher.InvokeAsync(UpdatePresence, DispatcherPriority.Background);
        }

        private void UpdatePresence()
        {
            if (_client == null || !_rpcConnected) return;

            try
            {
                var presence = new DiscordRichPresence
                {
                    Details = string.IsNullOrWhiteSpace(Details) ? "Using Voidstrap" : Details,
                    State = State,
                    Assets = new Assets
                    {
                        LargeImageKey = string.IsNullOrWhiteSpace(LargeImageKey) ? null : LargeImageKey,
                        LargeImageText = AppName,
                        SmallImageKey = string.IsNullOrWhiteSpace(SmallImageKey) ? null : SmallImageKey,
                        SmallImageText = "Voidstrap RPC"
                    }
                };

                var buttons = BuildValidButtons();
                if (buttons.Count > 0) presence.Buttons = buttons.ToArray();

                lock (_rpcLock)
                {
                    _client?.SetPresence(presence);
                }

                SafeUpdateStatus("Presence updated successfully", Brushes.LightSkyBlue);
            }
            catch (Exception ex)
            {
                SafeUpdateStatus($"Presence update failed: {ex.Message}", Brushes.Red);
            }
        }

        private List<Button> BuildValidButtons()
        {
            var list = new List<Button>();

            bool Valid(string url)
            {
                if (string.IsNullOrWhiteSpace(url)) return false;
                if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
                return u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp;
            }

            if (Button1Enabled && Valid(Button1Url))
                list.Add(new Button { Label = string.IsNullOrWhiteSpace(Button1Label) ? "Link" : Button1Label, Url = Button1Url });

            if (Button2Enabled && Valid(Button2Url))
                list.Add(new Button { Label = string.IsNullOrWhiteSpace(Button2Label) ? "Link" : Button2Label, Url = Button2Url });

            return list;
        }

        private async Task CheckReconnectAsync()
        {
            if (_isStopping || _isStarting) return;
            bool needReconnect = false;
            lock (_rpcLock)
            {
                if (_client == null) needReconnect = true;
            }

            if (needReconnect || !_rpcConnected)
            {
                SafeUpdateStatus("Attempting reconnect...", Brushes.Orange);
                await SafeStartRpcAsync().ConfigureAwait(false);
            }
        }
        #endregion

        #region Config + INotify
        private record RpcConfig(
            string ApplicationId, string AppName, string Details, string State,
            string LargeImageKey, string SmallImageKey,
            bool Button1Enabled, bool Button2Enabled,
            string Button1Label, string Button1Url,
            string Button2Label, string Button2Url,
            bool AutoStartRpc);

        private void SaveConfigInternal()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
                var data = new RpcConfig(_applicationId, _appName, _details, _state,
                    _largeImageKey, _smallImageKey,
                    _button1Enabled, _button2Enabled,
                    _button1Label, _button1Url,
                    _button2Label, _button2Url,
                    _autoStartRpc);

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
                SafeUpdateStatus("Config Saved", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                SafeUpdateStatus($"Save Failed: {ex.Message}", Brushes.Red);
            }
        }

        private void LoadConfigInternal()
        {
            if (!File.Exists(_configPath))
            {
                UpdateCommands();
                return;
            }

            try
            {
                _isLoadingConfig = true;
                var json = File.ReadAllText(_configPath);
                var data = JsonSerializer.Deserialize<RpcConfig>(json);
                if (data == null) return;

                ApplicationId = data.ApplicationId;
                AppName = data.AppName;
                Details = data.Details;
                State = data.State;
                LargeImageKey = data.LargeImageKey;
                SmallImageKey = data.SmallImageKey;
                Button1Enabled = data.Button1Enabled;
                Button2Enabled = data.Button2Enabled;
                Button1Label = data.Button1Label;
                Button1Url = data.Button1Url;
                Button2Label = data.Button2Label;
                Button2Url = data.Button2Url;
                AutoStartRpc = data.AutoStartRpc;

                DispatcherInvokeSafe(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)));
                UpdateCommands();
                SafeUpdateStatus("Config Loaded", Brushes.LightBlue);

                if (AutoStartRpc && !string.IsNullOrWhiteSpace(ApplicationId))
                {
                    SafeUpdateStatus("Auto-starting RPC...", Brushes.DarkOrange);
                    _ = SafeStartRpcAsync();
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to load config: {ex.Message}");
            }
            finally
            {
                _isLoadingConfig = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
        #endregion
    }
}
