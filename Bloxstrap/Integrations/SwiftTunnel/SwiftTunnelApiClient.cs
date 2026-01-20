using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using Voidstrap.Integrations.SwiftTunnel.Models;

namespace Voidstrap.Integrations.SwiftTunnel
{
    /// <summary>
    /// HTTP client for SwiftTunnel API calls
    /// </summary>
    public class SwiftTunnelApiClient : IDisposable
    {
        private const string BaseUrl = "https://swifttunnel.net";
        private const string AuthUrl = "https://auth.swifttunnel.net";
        private const string ServersApiUrl = "https://swifttunnel.net/api/vpn/servers";
        private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InpvbnVnanZvcWtsdmdibmh4c2hnIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NjUyNTU3ODksImV4cCI6MjA4MDgzMTc4OX0.Jmme0whahuX2KEmklBZQzCcJnsHJemyO8U9TdynbyNE";

        private readonly HttpClient _httpClient;
        private bool _disposed;

        // Cached server list
        private ServerListResponse? _cachedServers;
        private ServerListSource _serverListSource = ServerListSource.Loading;
        private string? _serverListError;

        public SwiftTunnelApiClient()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
        }

        /// <summary>
        /// Current server list source
        /// </summary>
        public ServerListSource ServerListSource => _serverListSource;

        /// <summary>
        /// Server list error message if any
        /// </summary>
        public string? ServerListError => _serverListError;

        /// <summary>
        /// Get cached server list (null if not loaded)
        /// </summary>
        public ServerListResponse? CachedServers => _cachedServers;

        #region Authentication

        /// <summary>
        /// Sign in with email and password
        /// </summary>
        public async Task<(AuthSession? Session, string? Error)> SignInAsync(string email, string password)
        {
            try
            {
                var request = new
                {
                    email,
                    password
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(
                    $"{AuthUrl}/auth/v1/token?grant_type=password",
                    content
                );

                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<AuthError>(json);
                    return (null, error?.ErrorDescription ?? error?.Message ?? "Authentication failed");
                }

                var session = JsonSerializer.Deserialize<AuthSession>(json);
                return (session, null);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", $"SignIn error: {ex.Message}");
                return (null, ex.Message);
            }
        }

        /// <summary>
        /// Refresh the access token using refresh token
        /// </summary>
        public async Task<(AuthSession? Session, string? Error)> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                var request = new
                {
                    refresh_token = refreshToken
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(
                    $"{AuthUrl}/auth/v1/token?grant_type=refresh_token",
                    content
                );

                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<AuthError>(json);
                    return (null, error?.ErrorDescription ?? error?.Message ?? "Token refresh failed");
                }

                var session = JsonSerializer.Deserialize<AuthSession>(json);
                return (session, null);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", $"RefreshToken error: {ex.Message}");
                return (null, ex.Message);
            }
        }

        /// <summary>
        /// Get OAuth authorization URL for Google sign-in
        /// </summary>
        public string GetGoogleOAuthUrl(string redirectUrl)
        {
            return $"{AuthUrl}/auth/v1/authorize?provider=google&redirect_to={Uri.EscapeDataString(redirectUrl)}";
        }

        /// <summary>
        /// Exchange authorization code for session (OAuth callback)
        /// </summary>
        public async Task<(AuthSession? Session, string? Error)> ExchangeCodeAsync(string code)
        {
            try
            {
                var request = new
                {
                    auth_code = code
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(
                    $"{AuthUrl}/auth/v1/token?grant_type=pkce",
                    content
                );

                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<AuthError>(json);
                    return (null, error?.ErrorDescription ?? error?.Message ?? "Code exchange failed");
                }

                var session = JsonSerializer.Deserialize<AuthSession>(json);
                return (session, null);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", $"ExchangeCode error: {ex.Message}");
                return (null, ex.Message);
            }
        }

        /// <summary>
        /// Sign out (revoke session)
        /// </summary>
        public async Task SignOutAsync(string accessToken)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{AuthUrl}/auth/v1/logout");
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                await _httpClient.SendAsync(requestMessage);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", $"SignOut error: {ex.Message}");
            }
        }

        #endregion

        #region VPN Configuration

        /// <summary>
        /// Get VPN configuration for a region
        /// </summary>
        public async Task<(VpnConfig? Config, string? Error)> GetVpnConfigAsync(string accessToken, string region)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/vpn/generate-config");
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var request = new { region };
                requestMessage.Content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.SendAsync(requestMessage);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine("SwiftTunnelApiClient", $"GetVpnConfig failed: {response.StatusCode} - {json}");
                    return (null, $"Failed to get VPN config: {response.StatusCode}");
                }

                var configResponse = JsonSerializer.Deserialize<VpnConfigResponse>(json);

                if (configResponse?.Success != true)
                {
                    return (null, configResponse?.Error ?? "Unknown error");
                }

                return (configResponse.Config, null);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", $"GetVpnConfig error: {ex.Message}");
                return (null, ex.Message);
            }
        }

        #endregion

        #region Server List (Dynamic)

        /// <summary>
        /// Get cache file path for server list
        /// </summary>
        private static string GetCachePath()
        {
            return Path.Combine(Paths.Base, "SwiftTunnel", "servers_cache.json");
        }

        /// <summary>
        /// Load cached server list from disk
        /// </summary>
        private CachedServerList? LoadCachedServers()
        {
            try
            {
                var cachePath = GetCachePath();
                if (!File.Exists(cachePath))
                {
                    App.Logger.WriteLine("SwiftTunnelApiClient", "Server cache file does not exist");
                    return null;
                }

                var content = File.ReadAllText(cachePath);
                var cached = JsonSerializer.Deserialize<CachedServerList>(content);

                if (cached != null)
                {
                    var age = DateTime.UtcNow - cached.CachedAt;
                    App.Logger.WriteLine("SwiftTunnelApiClient", $"Loaded server cache, age: {age.TotalMinutes:F0} minutes");
                }

                return cached;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", $"Failed to load server cache: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save server list to disk cache
        /// </summary>
        private void SaveServersToCache(ServerListResponse data)
        {
            try
            {
                var cachePath = GetCachePath();
                var cacheDir = Path.GetDirectoryName(cachePath);
                if (cacheDir != null && !Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }

                var cached = new CachedServerList
                {
                    Data = data,
                    CachedAt = DateTime.UtcNow
                };

                var content = JsonSerializer.Serialize(cached, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(cachePath, content);

                App.Logger.WriteLine("SwiftTunnelApiClient", "Saved server list to cache");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", $"Failed to save server cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Fetch server list from API
        /// </summary>
        private async Task<ServerListResponse?> FetchServerListFromApi()
        {
            try
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", $"Fetching server list from API: {ServersApiUrl}");

                var response = await _httpClient.GetAsync(ServersApiUrl);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine("SwiftTunnelApiClient", $"API returned error: {response.StatusCode}");
                    return null;
                }

                var data = JsonSerializer.Deserialize<ServerListResponse>(json);

                if (data != null)
                {
                    App.Logger.WriteLine("SwiftTunnelApiClient",
                        $"Fetched {data.Servers.Count} servers and {data.Regions.Count} regions (version: {data.Version})");

                    // Save to cache
                    SaveServersToCache(data);
                }

                return data;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", $"Failed to fetch server list: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load server list from API or cache.
        /// Strategy:
        /// 1. Try to load fresh cache
        /// 2. If cache is stale or missing, fetch from API
        /// 3. If API fails and cache exists (even stale), use cache
        /// 4. If all else fails, return error
        /// </summary>
        public async Task<(ServerListResponse? Data, ServerListSource Source)> LoadServerListAsync()
        {
            _serverListSource = ServerListSource.Loading;
            _serverListError = null;

            // Try to load from cache first
            var cached = LoadCachedServers();

            if (cached != null && cached.IsFresh)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", "Using fresh cached server list");
                _cachedServers = cached.Data;
                _serverListSource = ServerListSource.Cache;
                return (cached.Data, ServerListSource.Cache);
            }

            // Cache is stale or missing, try API
            if (cached != null)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", "Cache is stale, attempting to refresh from API");
            }
            else
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", "No cache found, fetching from API");
            }

            var apiData = await FetchServerListFromApi();

            if (apiData != null)
            {
                _cachedServers = apiData;
                _serverListSource = ServerListSource.Api;
                return (apiData, ServerListSource.Api);
            }

            // API failed, use stale cache if available
            if (cached != null)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", "API failed, using stale cache");
                _cachedServers = cached.Data;
                _serverListSource = ServerListSource.StaleCache;
                return (cached.Data, ServerListSource.StaleCache);
            }

            // No cache and API failed
            App.Logger.WriteLine("SwiftTunnelApiClient", "Failed to load server list: no cache and API failed");
            _serverListSource = ServerListSource.Error;
            _serverListError = "Could not load server list. Please check your internet connection.";
            return (null, ServerListSource.Error);
        }

        /// <summary>
        /// Get server by region ID
        /// </summary>
        public ServerInfo? GetServer(string region)
        {
            return _cachedServers?.Servers.FirstOrDefault(s => s.Region == region);
        }

        /// <summary>
        /// Get gaming region by ID
        /// </summary>
        public GamingRegion? GetRegion(string id)
        {
            return _cachedServers?.Regions.FirstOrDefault(r => r.Id == id);
        }

        /// <summary>
        /// Get all gaming regions
        /// </summary>
        public List<GamingRegion> GetAllRegions()
        {
            return _cachedServers?.Regions ?? new List<GamingRegion>();
        }

        /// <summary>
        /// Get all servers
        /// </summary>
        public List<ServerInfo> GetAllServers()
        {
            return _cachedServers?.Servers ?? new List<ServerInfo>();
        }

        /// <summary>
        /// Get servers in a gaming region
        /// </summary>
        public List<ServerInfo> GetServersInRegion(string regionId)
        {
            var region = GetRegion(regionId);
            if (region == null) return new List<ServerInfo>();

            return region.Servers
                .Select(serverId => GetServer(serverId))
                .Where(s => s != null)
                .Cast<ServerInfo>()
                .ToList();
        }

        /// <summary>
        /// Measure latency to a server using ICMP ping
        /// </summary>
        public async Task<int?> MeasureLatencyAsync(string ip)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, 2000);

                if (reply.Status == IPStatus.Success)
                {
                    return (int)reply.RoundtripTime;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Measure latency for all servers and update their Latency property
        /// </summary>
        public async Task MeasureAllLatenciesAsync()
        {
            if (_cachedServers == null) return;

            var tasks = _cachedServers.Servers.Select(async server =>
            {
                server.Latency = await MeasureLatencyAsync(server.Ip);
            });

            await Task.WhenAll(tasks);

            // Update region best latencies
            foreach (var region in _cachedServers.Regions)
            {
                var serversInRegion = GetServersInRegion(region.Id);
                var latencies = serversInRegion
                    .Where(s => s.Latency.HasValue)
                    .Select(s => s.Latency!.Value);

                region.BestLatency = latencies.Any() ? latencies.Min() : null;
            }
        }

        /// <summary>
        /// Find best server in a region (lowest latency)
        /// </summary>
        public async Task<(ServerInfo? Server, int? Latency)> FindBestServerInRegionAsync(string regionId)
        {
            var servers = GetServersInRegion(regionId);
            if (servers.Count == 0) return (null, null);

            ServerInfo? bestServer = null;
            int? bestLatency = null;

            foreach (var server in servers)
            {
                // Do 3 pings and average
                var latencies = new List<int>();
                for (int i = 0; i < 3; i++)
                {
                    var latency = await MeasureLatencyAsync(server.Ip);
                    if (latency.HasValue)
                    {
                        latencies.Add(latency.Value);
                    }
                    await Task.Delay(100);
                }

                if (latencies.Count > 0)
                {
                    var avgLatency = (int)latencies.Average();
                    server.Latency = avgLatency;

                    if (bestLatency == null || avgLatency < bestLatency)
                    {
                        bestLatency = avgLatency;
                        bestServer = server;
                    }
                }
            }

            return (bestServer, bestLatency);
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient.Dispose();
                _disposed = true;
            }
        }
    }
}
