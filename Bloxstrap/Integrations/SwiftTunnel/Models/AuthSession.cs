using System.Text.Json.Serialization;

namespace Voidstrap.Integrations.SwiftTunnel.Models
{
    /// <summary>
    /// Authentication session from Supabase
    /// </summary>
    public class AuthSession
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("expires_at")]
        public long? ExpiresAt { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "bearer";

        [JsonPropertyName("user")]
        public AuthUser? User { get; set; }

        /// <summary>
        /// Check if the session is expired
        /// </summary>
        public bool IsExpired
        {
            get
            {
                if (ExpiresAt.HasValue)
                {
                    var expiryTime = DateTimeOffset.FromUnixTimeSeconds(ExpiresAt.Value);
                    return DateTimeOffset.UtcNow >= expiryTime.AddMinutes(-5); // 5 minute buffer
                }
                return true;
            }
        }

        /// <summary>
        /// Check if we have valid tokens
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(AccessToken) && !IsExpired;
    }

    /// <summary>
    /// User information from Supabase
    /// </summary>
    public class AuthUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("email_confirmed_at")]
        public string? EmailConfirmedAt { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("user_metadata")]
        public Dictionary<string, object>? UserMetadata { get; set; }

        /// <summary>
        /// Get display name from metadata or fallback to email
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (UserMetadata?.TryGetValue("full_name", out var name) == true)
                    return name?.ToString() ?? Email;
                if (UserMetadata?.TryGetValue("name", out var n) == true)
                    return n?.ToString() ?? Email;
                return Email;
            }
        }
    }

    /// <summary>
    /// Supabase auth error response
    /// </summary>
    public class AuthError
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        [JsonPropertyName("error_description")]
        public string ErrorDescription { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
