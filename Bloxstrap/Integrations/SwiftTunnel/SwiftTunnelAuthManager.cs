using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Voidstrap.Integrations.SwiftTunnel.Models;

namespace Voidstrap.Integrations.SwiftTunnel
{
    /// <summary>
    /// Manages SwiftTunnel authentication state and credentials
    /// </summary>
    public class SwiftTunnelAuthManager : IDisposable
    {
        private const string SessionFileName = "swifttunnel_session.dat";

        private readonly SwiftTunnelApiClient _apiClient;
        private AuthSession? _currentSession;
        private bool _disposed;

        public event EventHandler<AuthSession?>? SessionChanged;

        public SwiftTunnelAuthManager(SwiftTunnelApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        /// <summary>
        /// Current authentication session
        /// </summary>
        public AuthSession? CurrentSession => _currentSession;

        /// <summary>
        /// Check if user is authenticated
        /// </summary>
        public bool IsAuthenticated => _currentSession?.IsValid == true;

        /// <summary>
        /// Get the current user's email
        /// </summary>
        public string? UserEmail => _currentSession?.User?.Email;

        /// <summary>
        /// Get the current user's display name
        /// </summary>
        public string? UserDisplayName => _currentSession?.User?.DisplayName;

        /// <summary>
        /// Initialize auth manager and load stored session
        /// </summary>
        public async Task InitializeAsync()
        {
            App.Logger.WriteLine("SwiftTunnelAuthManager", "Initializing auth manager...");

            if (!App.Settings.Prop.SwiftTunnelRememberLogin)
            {
                App.Logger.WriteLine("SwiftTunnelAuthManager", "Remember login disabled, skipping session load");
                return;
            }

            var session = LoadStoredSession();
            if (session == null)
            {
                App.Logger.WriteLine("SwiftTunnelAuthManager", "No stored session found");
                return;
            }

            if (session.IsExpired && !string.IsNullOrEmpty(session.RefreshToken))
            {
                App.Logger.WriteLine("SwiftTunnelAuthManager", "Session expired, attempting refresh...");
                var (refreshedSession, error) = await _apiClient.RefreshTokenAsync(session.RefreshToken);

                if (refreshedSession != null)
                {
                    _currentSession = refreshedSession;
                    SaveSession(refreshedSession);
                    App.Logger.WriteLine("SwiftTunnelAuthManager", "Session refreshed successfully");
                }
                else
                {
                    App.Logger.WriteLine("SwiftTunnelAuthManager", $"Session refresh failed: {error}");
                    ClearStoredSession();
                }
            }
            else if (session.IsValid)
            {
                _currentSession = session;
                App.Logger.WriteLine("SwiftTunnelAuthManager", $"Loaded valid session for {session.User?.Email}");
            }
            else
            {
                App.Logger.WriteLine("SwiftTunnelAuthManager", "Stored session is invalid");
                ClearStoredSession();
            }

            SessionChanged?.Invoke(this, _currentSession);
        }

        /// <summary>
        /// Sign in with email and password
        /// </summary>
        public async Task<(bool Success, string? Error)> SignInAsync(string email, string password)
        {
            App.Logger.WriteLine("SwiftTunnelAuthManager", $"Signing in as {email}...");

            var (session, error) = await _apiClient.SignInAsync(email, password);

            if (session == null)
            {
                App.Logger.WriteLine("SwiftTunnelAuthManager", $"Sign in failed: {error}");
                return (false, error);
            }

            _currentSession = session;

            if (App.Settings.Prop.SwiftTunnelRememberLogin)
            {
                SaveSession(session);
            }

            App.Logger.WriteLine("SwiftTunnelAuthManager", "Sign in successful");
            SessionChanged?.Invoke(this, _currentSession);
            return (true, null);
        }

        /// <summary>
        /// Handle OAuth callback with tokens from URL hash
        /// </summary>
        public async Task<(bool Success, string? Error)> HandleOAuthCallbackAsync(string accessToken, string refreshToken)
        {
            App.Logger.WriteLine("SwiftTunnelAuthManager", "Handling OAuth callback...");

            // Create session from tokens (we don't have full session info from hash)
            _currentSession = new AuthSession
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "bearer",
                ExpiresIn = 3600,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
            };

            // Try to get user info by refreshing the token
            var (session, error) = await _apiClient.RefreshTokenAsync(refreshToken);
            if (session != null)
            {
                _currentSession = session;
            }

            if (App.Settings.Prop.SwiftTunnelRememberLogin)
            {
                SaveSession(_currentSession);
            }

            App.Logger.WriteLine("SwiftTunnelAuthManager", "OAuth sign in successful");
            SessionChanged?.Invoke(this, _currentSession);
            return (true, null);
        }

        /// <summary>
        /// Sign out and clear stored credentials
        /// </summary>
        public async Task SignOutAsync()
        {
            App.Logger.WriteLine("SwiftTunnelAuthManager", "Signing out...");

            if (_currentSession != null && !string.IsNullOrEmpty(_currentSession.AccessToken))
            {
                await _apiClient.SignOutAsync(_currentSession.AccessToken);
            }

            _currentSession = null;
            ClearStoredSession();
            SessionChanged?.Invoke(this, null);
        }

        /// <summary>
        /// Get a valid access token, refreshing if needed
        /// </summary>
        public async Task<string?> GetValidAccessTokenAsync()
        {
            if (_currentSession == null)
                return null;

            if (!_currentSession.IsExpired)
                return _currentSession.AccessToken;

            // Try to refresh
            if (string.IsNullOrEmpty(_currentSession.RefreshToken))
                return null;

            var (session, _) = await _apiClient.RefreshTokenAsync(_currentSession.RefreshToken);
            if (session != null)
            {
                _currentSession = session;
                if (App.Settings.Prop.SwiftTunnelRememberLogin)
                {
                    SaveSession(session);
                }
                SessionChanged?.Invoke(this, _currentSession);
                return session.AccessToken;
            }

            return null;
        }

        private string GetSessionFilePath()
        {
            return Path.Combine(Paths.Base, "SwiftTunnel", SessionFileName);
        }

        private void SaveSession(AuthSession session)
        {
            try
            {
                var dir = Path.GetDirectoryName(GetSessionFilePath())!;
                Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(session);
                var bytes = Encoding.UTF8.GetBytes(json);

                // Simple obfuscation (XOR + base64)
                var key = new byte[] { 0x53, 0x77, 0x69, 0x66, 0x74, 0x54, 0x75, 0x6E };
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] ^= key[i % key.Length];
                }

                var encoded = Convert.ToBase64String(bytes);
                File.WriteAllText(GetSessionFilePath(), encoded);

                App.Logger.WriteLine("SwiftTunnelAuthManager", "Session saved to file");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelAuthManager", $"Failed to save session: {ex.Message}");
            }
        }

        private AuthSession? LoadStoredSession()
        {
            try
            {
                var path = GetSessionFilePath();
                if (!File.Exists(path))
                    return null;

                var encoded = File.ReadAllText(path);
                var bytes = Convert.FromBase64String(encoded);

                // Reverse obfuscation
                var key = new byte[] { 0x53, 0x77, 0x69, 0x66, 0x74, 0x54, 0x75, 0x6E };
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] ^= key[i % key.Length];
                }

                var json = Encoding.UTF8.GetString(bytes);
                return JsonSerializer.Deserialize<AuthSession>(json);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelAuthManager", $"Failed to load session: {ex.Message}");
                return null;
            }
        }

        private void ClearStoredSession()
        {
            try
            {
                var path = GetSessionFilePath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SwiftTunnelAuthManager", $"Failed to clear session: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
