using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Voidstrap.Integrations.SwiftTunnel.Models;

namespace Voidstrap.Integrations.SwiftTunnel
{
    /// <summary>
    /// Manages VPN connection lifecycle using the native library
    /// </summary>
    public class VpnConnection : IDisposable
    {
        private bool _initialized;
        private bool _disposed;
        private bool _splitTunnelEnabled;
        private CancellationTokenSource? _splitTunnelRefreshCts;
        private readonly object _lock = new();

        public event EventHandler<ConnectionState>? StateChanged;
        public event EventHandler<List<string>>? TunneledProcessesChanged;

        /// <summary>
        /// Current connection state
        /// </summary>
        public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;

        /// <summary>
        /// Last error message
        /// </summary>
        public string? LastError { get; private set; }

        /// <summary>
        /// Currently tunneled process names
        /// </summary>
        public List<string> TunneledProcesses { get; private set; } = new();

        /// <summary>
        /// Whether split tunneling is active
        /// </summary>
        public bool IsSplitTunnelActive => _splitTunnelEnabled;

        /// <summary>
        /// Check if VPN is connected
        /// </summary>
        public bool IsConnected => CurrentState == ConnectionState.Connected;

        /// <summary>
        /// Check if VPN is connecting
        /// </summary>
        public bool IsConnecting => CurrentState switch
        {
            ConnectionState.FetchingConfig => true,
            ConnectionState.CreatingAdapter => true,
            ConnectionState.Connecting => true,
            ConnectionState.ConfiguringSplitTunnel => true,
            ConnectionState.ConfiguringRoutes => true,
            _ => false
        };

        /// <summary>
        /// Check if split tunnel driver is available on this system
        /// </summary>
        public static bool IsSplitTunnelDriverAvailable()
        {
            try
            {
                return NativeVpn.IsSplitTunnelAvailable();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get default apps that will be tunneled (Roblox processes)
        /// </summary>
        public static List<string> GetDefaultTunnelApps()
        {
            try
            {
                return NativeVpn.GetDefaultTunnelApps();
            }
            catch
            {
                return new List<string> { "RobloxPlayerBeta.exe", "RobloxStudioBeta.exe" };
            }
        }

        /// <summary>
        /// Check if native library is available
        /// </summary>
        public static bool IsAvailable()
        {
            try
            {
                // Check if DLL exists
                var dllPath = GetNativeDllPath();
                if (!File.Exists(dllPath))
                {
                    App.Logger.WriteLine("VpnConnection", $"Native DLL not found at: {dllPath}");
                    return false;
                }

                // Check if Wintun is available
                var wintunPath = Path.Combine(Path.GetDirectoryName(dllPath)!, "wintun.dll");
                if (!File.Exists(wintunPath))
                {
                    App.Logger.WriteLine("VpnConnection", $"Wintun.dll not found at: {wintunPath}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("VpnConnection", $"Error checking availability: {ex.Message}");
                return false;
            }
        }

        private static string GetNativeDllPath()
        {
            return Path.Combine(Paths.Base, "SwiftTunnel", "swifttunnel_vpn.dll");
        }

        /// <summary>
        /// Initialize the VPN connection
        /// </summary>
        public bool Initialize()
        {
            lock (_lock)
            {
                if (_initialized)
                    return true;

                try
                {
                    // Ensure native directory exists
                    var nativeDir = Path.Combine(Paths.Base, "SwiftTunnel");
                    Directory.CreateDirectory(nativeDir);

                    // Set DLL search path
                    SetDllDirectory(nativeDir);

                    var result = NativeVpn.swifttunnel_init();
                    if (result != NativeVpn.Success)
                    {
                        LastError = GetLastNativeError();
                        App.Logger.WriteLine("VpnConnection", $"Failed to initialize: {LastError}");
                        return false;
                    }

                    _initialized = true;
                    App.Logger.WriteLine("VpnConnection", "Initialized successfully");
                    return true;
                }
                catch (DllNotFoundException ex)
                {
                    LastError = $"Native library not found: {ex.Message}";
                    App.Logger.WriteLine("VpnConnection", LastError);
                    return false;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    App.Logger.WriteLine("VpnConnection", $"Initialize error: {ex.Message}");
                    return false;
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// Connect to VPN server
        /// </summary>
        public async Task<(bool Success, string? Error)> ConnectAsync(VpnConfig config, List<string>? splitTunnelApps = null)
        {
            if (!_initialized && !Initialize())
            {
                return (false, LastError ?? "Failed to initialize");
            }

            if (IsConnected || IsConnecting)
            {
                return (false, "Already connected or connecting");
            }

            try
            {
                UpdateState(ConnectionState.FetchingConfig);

                // Build config JSON
                var connectConfig = new
                {
                    access_token = "", // Not used directly - config already fetched
                    region = config.Region,
                    endpoint = config.Endpoint,
                    server_public_key = config.ServerPublicKey,
                    private_key = config.PrivateKey,
                    public_key = config.PublicKey,
                    assigned_ip = config.AssignedIp,
                    dns = config.Dns,
                    split_tunnel_apps = splitTunnelApps ?? new List<string>()
                };

                var configJson = JsonSerializer.Serialize(connectConfig);

                App.Logger.WriteLine("VpnConnection", $"Connecting to {config.Region} ({config.Endpoint})...");

                // Run connection on thread pool to not block UI
                var result = await Task.Run(() => NativeVpn.swifttunnel_connect(configJson));

                if (result == NativeVpn.Success)
                {
                    UpdateState(ConnectionState.Connected);
                    App.Logger.WriteLine("VpnConnection", "Connected successfully");

                    // Start split tunnel monitoring if we have apps to tunnel
                    if (splitTunnelApps != null && splitTunnelApps.Count > 0)
                    {
                        StartSplitTunnelMonitoring();
                    }

                    return (true, null);
                }
                else
                {
                    LastError = GetLastNativeError() ?? $"Connection failed with code: {result}";
                    UpdateState(ConnectionState.Error);
                    App.Logger.WriteLine("VpnConnection", $"Connection failed: {LastError}");
                    return (false, LastError);
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                UpdateState(ConnectionState.Error);
                App.Logger.WriteLine("VpnConnection", $"Connection error: {ex.Message}");
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Disconnect from VPN
        /// </summary>
        public async Task<(bool Success, string? Error)> DisconnectAsync()
        {
            if (!_initialized)
            {
                return (true, null); // Not initialized means not connected
            }

            try
            {
                UpdateState(ConnectionState.Disconnecting);

                // Stop split tunnel monitoring first
                StopSplitTunnelMonitoring();

                // Close split tunnel
                if (_splitTunnelEnabled)
                {
                    try
                    {
                        await Task.Run(() => NativeVpn.swifttunnel_split_tunnel_close());
                        _splitTunnelEnabled = false;
                        TunneledProcesses.Clear();
                        App.Logger.WriteLine("VpnConnection", "Split tunnel closed");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("VpnConnection", $"Error closing split tunnel: {ex.Message}");
                    }
                }

                var result = await Task.Run(() => NativeVpn.swifttunnel_disconnect());

                if (result == NativeVpn.Success)
                {
                    UpdateState(ConnectionState.Disconnected);
                    App.Logger.WriteLine("VpnConnection", "Disconnected successfully");
                    return (true, null);
                }
                else
                {
                    LastError = GetLastNativeError() ?? $"Disconnect failed with code: {result}";
                    App.Logger.WriteLine("VpnConnection", $"Disconnect failed: {LastError}");
                    // Still consider it disconnected
                    UpdateState(ConnectionState.Disconnected);
                    return (true, null);
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                UpdateState(ConnectionState.Disconnected);
                App.Logger.WriteLine("VpnConnection", $"Disconnect error: {ex.Message}");
                return (true, null);
            }
        }

        /// <summary>
        /// Update current state and fire event
        /// </summary>
        private void UpdateState(ConnectionState newState)
        {
            if (CurrentState != newState)
            {
                CurrentState = newState;
                StateChanged?.Invoke(this, newState);
            }
        }

        /// <summary>
        /// Get the last error from native library
        /// </summary>
        private string? GetLastNativeError()
        {
            try
            {
                var ptr = NativeVpn.swifttunnel_get_error();
                return NativeVpn.GetStringAndFree(ptr);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Refresh state from native library
        /// </summary>
        public void RefreshState()
        {
            if (!_initialized)
                return;

            try
            {
                var stateCode = NativeVpn.swifttunnel_get_state();
                var newState = stateCode switch
                {
                    NativeVpn.StateDisconnected => ConnectionState.Disconnected,
                    NativeVpn.StateFetchingConfig => ConnectionState.FetchingConfig,
                    NativeVpn.StateCreatingAdapter => ConnectionState.CreatingAdapter,
                    NativeVpn.StateConnecting => ConnectionState.Connecting,
                    NativeVpn.StateConfiguringSplitTunnel => ConnectionState.ConfiguringSplitTunnel,
                    NativeVpn.StateConfiguringRoutes => ConnectionState.ConfiguringRoutes,
                    NativeVpn.StateConnected => ConnectionState.Connected,
                    NativeVpn.StateDisconnecting => ConnectionState.Disconnecting,
                    NativeVpn.StateError => ConnectionState.Error,
                    _ => ConnectionState.Disconnected
                };

                UpdateState(newState);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("VpnConnection", $"RefreshState error: {ex.Message}");
            }
        }

        /// <summary>
        /// Start background monitoring for split tunnel process detection
        /// </summary>
        private void StartSplitTunnelMonitoring()
        {
            StopSplitTunnelMonitoring();

            _splitTunnelEnabled = true;
            _splitTunnelRefreshCts = new CancellationTokenSource();
            var token = _splitTunnelRefreshCts.Token;

            Task.Run(async () =>
            {
                App.Logger.WriteLine("VpnConnection", "Split tunnel monitoring started");

                while (!token.IsCancellationRequested && IsConnected)
                {
                    try
                    {
                        var processes = NativeVpn.RefreshSplitTunnel();
                        if (processes.Count > 0)
                        {
                            var newProcesses = processes.Except(TunneledProcesses).ToList();
                            if (newProcesses.Count > 0)
                            {
                                App.Logger.WriteLine("VpnConnection", $"Now tunneling: {string.Join(", ", newProcesses)}");
                            }
                        }

                        TunneledProcesses = processes;
                        TunneledProcessesChanged?.Invoke(this, processes);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine("VpnConnection", $"Split tunnel refresh error: {ex.Message}");
                    }

                    // Refresh every 250ms for fast game detection (matches native library)
                    try
                    {
                        await Task.Delay(250, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }

                App.Logger.WriteLine("VpnConnection", "Split tunnel monitoring stopped");
            }, token);
        }

        /// <summary>
        /// Stop split tunnel monitoring
        /// </summary>
        private void StopSplitTunnelMonitoring()
        {
            if (_splitTunnelRefreshCts != null)
            {
                _splitTunnelRefreshCts.Cancel();
                _splitTunnelRefreshCts.Dispose();
                _splitTunnelRefreshCts = null;
            }
        }

        /// <summary>
        /// Manually refresh split tunnel process detection
        /// </summary>
        public List<string> RefreshTunneledProcesses()
        {
            if (!_splitTunnelEnabled)
                return new List<string>();

            try
            {
                TunneledProcesses = NativeVpn.RefreshSplitTunnel();
                return TunneledProcesses;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("VpnConnection", $"RefreshTunneledProcesses error: {ex.Message}");
                return TunneledProcesses;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Stop split tunnel monitoring
            StopSplitTunnelMonitoring();

            // Close split tunnel if active
            if (_splitTunnelEnabled)
            {
                try
                {
                    NativeVpn.swifttunnel_split_tunnel_close();
                }
                catch { }
            }

            if (_initialized)
            {
                try
                {
                    NativeVpn.swifttunnel_cleanup();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("VpnConnection", $"Cleanup error: {ex.Message}");
                }
            }
        }
    }
}
