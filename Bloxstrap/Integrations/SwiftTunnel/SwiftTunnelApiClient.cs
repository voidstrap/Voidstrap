using System.Net.Http;
using System.Net.Http.Headers;
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
        private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InpvbnVnanZvcWtsdmdibmh4c2hnIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NjUyNTU3ODksImV4cCI6MjA4MDgzMTc4OX0.Jmme0whahuX2KEmklBZQzCcJnsHJemyO8U9TdynbyNE";

        private readonly HttpClient _httpClient;
        private bool _disposed;

        public SwiftTunnelApiClient()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
        }

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

        /// <summary>
        /// Get list of available servers
        /// </summary>
        public async Task<(List<ServerInfo>? Servers, string? Error)> GetServersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/vpn/servers");
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Return static list if API fails
                    return (GamingRegions.GetAll().ToList(), null);
                }

                var serverResponse = JsonSerializer.Deserialize<ServerListResponse>(json);

                if (serverResponse?.Success != true)
                {
                    return (GamingRegions.GetAll().ToList(), null);
                }

                return (serverResponse.Servers, null);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelApiClient", $"GetServers error: {ex.Message}");
                // Return static list on error
                return (GamingRegions.GetAll().ToList(), null);
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
