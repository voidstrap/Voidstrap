using System.Diagnostics;
using Voidstrap.Integrations.SwiftTunnel.Models;

namespace Voidstrap.Integrations.SwiftTunnel
{
    /// <summary>
    /// Main orchestrator for SwiftTunnel VPN integration.
    /// Coordinates authentication, VPN connection, and lifecycle management.
    /// </summary>
    public class SwiftTunnelService : IDisposable
    {
        private static SwiftTunnelService? _instance;
        private static readonly object _lock = new();

        private readonly SwiftTunnelApiClient _apiClient;
        private readonly SwiftTunnelAuthManager _authManager;
        private readonly VpnConnection _vpnConnection;
        private bool _disposed;

        /// <summary>
        /// Get or create the singleton instance
        /// </summary>
        public static SwiftTunnelService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SwiftTunnelService();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// API client for SwiftTunnel requests
        /// </summary>
        public SwiftTunnelApiClient ApiClient => _apiClient;

        /// <summary>
        /// Authentication manager
        /// </summary>
        public SwiftTunnelAuthManager AuthManager => _authManager;

        /// <summary>
        /// VPN connection manager
        /// </summary>
        public VpnConnection VpnConnection => _vpnConnection;

        /// <summary>
        /// Check if user is authenticated
        /// </summary>
        public bool IsAuthenticated => _authManager.IsAuthenticated;

        /// <summary>
        /// Check if VPN is connected
        /// </summary>
        public bool IsConnected => _vpnConnection.IsConnected;

        /// <summary>
        /// Current connection state
        /// </summary>
        public ConnectionState ConnectionState => _vpnConnection.CurrentState;

        /// <summary>
        /// Event fired when connection state changes
        /// </summary>
        public event EventHandler<ConnectionState>? ConnectionStateChanged;

        /// <summary>
        /// Event fired when authentication state changes
        /// </summary>
        public event EventHandler<bool>? AuthStateChanged;

        private SwiftTunnelService()
        {
            _apiClient = new SwiftTunnelApiClient();
            _authManager = new SwiftTunnelAuthManager(_apiClient);
            _vpnConnection = new VpnConnection();

            // Forward events
            _vpnConnection.StateChanged += (s, state) => ConnectionStateChanged?.Invoke(this, state);
            _authManager.SessionChanged += (s, session) => AuthStateChanged?.Invoke(this, session != null);
        }

        /// <summary>
        /// Initialize the service
        /// </summary>
        public async Task InitializeAsync()
        {
            App.Logger.WriteLine("SwiftTunnelService", "Initializing...");

            // Initialize auth manager (loads stored session)
            await _authManager.InitializeAsync();

            // Initialize VPN connection
            if (VpnConnection.IsAvailable())
            {
                _vpnConnection.Initialize();
            }
            else
            {
                App.Logger.WriteLine("SwiftTunnelService", "VPN native library not available");
            }

            App.Logger.WriteLine("SwiftTunnelService", "Initialized");
        }

        /// <summary>
        /// Connect to VPN server
        /// </summary>
        public async Task<(bool Success, string? Error)> ConnectAsync(string? region = null)
        {
            if (!IsAuthenticated)
            {
                return (false, "Not authenticated");
            }

            region ??= App.Settings.Prop.SwiftTunnelRegion;

            App.Logger.WriteLine("SwiftTunnelService", $"Connecting to region: {region}");

            // Find the best server in the selected region based on latency
            var serverToConnect = await GetBestServerForRegionAsync(region);
            App.Logger.WriteLine("SwiftTunnelService", $"Best server for region '{region}': {serverToConnect}");

            // Get access token
            var accessToken = await _authManager.GetValidAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                return (false, "Failed to get access token");
            }

            // Fetch VPN config from API using the best server
            var (config, error) = await _apiClient.GetVpnConfigAsync(accessToken, serverToConnect);
            if (config == null)
            {
                return (false, error ?? "Failed to get VPN configuration");
            }

            // Get split tunnel apps
            List<string>? splitTunnelApps = null;
            if (App.Settings.Prop.SwiftTunnelSplitTunnel)
            {
                splitTunnelApps = GetRobloxProcessNames();
            }

            // Connect
            return await _vpnConnection.ConnectAsync(config, splitTunnelApps);
        }

        /// <summary>
        /// Get the best server for a gaming region based on measured latency.
        /// Falls back to the first server in the region if no latency data exists.
        /// </summary>
        private async Task<string> GetBestServerForRegionAsync(string regionId)
        {
            // Get all servers in this gaming region
            var serversInRegion = _apiClient.GetServersInRegion(regionId);

            if (serversInRegion.Count == 0)
            {
                // No servers found for region, return the region ID itself
                // (it might be a direct server ID)
                App.Logger.WriteLine("SwiftTunnelService", $"No servers found for region '{regionId}', using region ID directly");
                return regionId;
            }

            // Find server with lowest latency
            var bestServer = serversInRegion
                .Where(s => s.Latency.HasValue)
                .OrderBy(s => s.Latency!.Value)
                .FirstOrDefault();

            if (bestServer != null)
            {
                App.Logger.WriteLine("SwiftTunnelService",
                    $"Using server '{bestServer.Region}' ({bestServer.Latency}ms) as best in region '{regionId}'");
                return bestServer.Region;
            }

            // No latency data available, measure now
            App.Logger.WriteLine("SwiftTunnelService", $"No latency data for region '{regionId}', measuring now...");

            var (measuredBest, latency) = await _apiClient.FindBestServerInRegionAsync(regionId);
            if (measuredBest != null)
            {
                App.Logger.WriteLine("SwiftTunnelService",
                    $"Measured best server: '{measuredBest.Region}' ({latency}ms)");
                return measuredBest.Region;
            }

            // Fall back to first server in region
            var fallback = serversInRegion.First();
            App.Logger.WriteLine("SwiftTunnelService",
                $"Could not measure latency, falling back to first server: '{fallback.Region}'");
            return fallback.Region;
        }

        /// <summary>
        /// Disconnect from VPN
        /// </summary>
        public async Task<(bool Success, string? Error)> DisconnectAsync()
        {
            return await _vpnConnection.DisconnectAsync();
        }

        /// <summary>
        /// Connect VPN before Roblox launch (used by Bootstrapper)
        /// </summary>
        public async Task<bool> AutoConnectBeforeLaunchAsync()
        {
            if (!App.Settings.Prop.SwiftTunnelEnabled)
            {
                return true; // Not enabled, skip
            }

            if (!App.Settings.Prop.SwiftTunnelAutoConnect)
            {
                return true; // Auto-connect disabled, skip
            }

            if (!IsAuthenticated)
            {
                App.Logger.WriteLine("SwiftTunnelService", "Auto-connect skipped: not authenticated");
                return true; // Not authenticated, skip (don't block launch)
            }

            if (IsConnected)
            {
                App.Logger.WriteLine("SwiftTunnelService", "Auto-connect skipped: already connected");
                return true; // Already connected
            }

            App.Logger.WriteLine("SwiftTunnelService", "Auto-connecting VPN before Roblox launch...");

            var (success, error) = await ConnectAsync();

            if (!success)
            {
                App.Logger.WriteLine("SwiftTunnelService", $"Auto-connect failed: {error}");
                // Don't block Roblox launch on VPN failure
                return true;
            }

            App.Logger.WriteLine("SwiftTunnelService", "Auto-connect successful");
            return true;
        }

        /// <summary>
        /// Disconnect VPN when Roblox exits
        /// </summary>
        public async Task AutoDisconnectOnExitAsync()
        {
            if (!App.Settings.Prop.SwiftTunnelEnabled)
                return;

            if (!IsConnected)
                return;

            // Check if any Roblox processes are still running
            var robloxRunning = IsRobloxRunning();
            if (robloxRunning)
            {
                App.Logger.WriteLine("SwiftTunnelService", "Roblox still running, keeping VPN connected");
                return;
            }

            App.Logger.WriteLine("SwiftTunnelService", "Roblox exited, disconnecting VPN...");
            await DisconnectAsync();
        }

        /// <summary>
        /// Check if Roblox is running
        /// </summary>
        private static bool IsRobloxRunning()
        {
            var processNames = new[] { "RobloxPlayerBeta", "RobloxStudioBeta" };

            foreach (var name in processNames)
            {
                try
                {
                    var processes = Process.GetProcessesByName(name);
                    if (processes.Length > 0)
                    {
                        foreach (var p in processes) p.Dispose();
                        return true;
                    }
                }
                catch
                {
                    // Ignore
                }
            }

            return false;
        }

        /// <summary>
        /// Get Roblox process names for split tunneling
        /// </summary>
        private static List<string> GetRobloxProcessNames()
        {
            return new List<string>
            {
                "RobloxPlayerBeta.exe",
                "RobloxPlayerLauncher.exe",
                "RobloxStudioBeta.exe",
                "RobloxStudioLauncherBeta.exe",
                "RobloxStudioLauncher.exe",
                "Windows10Universal.exe"
            };
        }

        /// <summary>
        /// Check if native VPN library is available
        /// </summary>
        public static bool IsVpnAvailable()
        {
            return VpnConnection.IsAvailable();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _vpnConnection.Dispose();
            _apiClient.Dispose();
            _authManager.Dispose();
        }
    }
}
