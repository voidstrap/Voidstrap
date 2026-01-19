using System.Text.Json.Serialization;

namespace Voidstrap.Integrations.SwiftTunnel.Models
{
    /// <summary>
    /// VPN server information
    /// </summary>
    public class ServerInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("countryCode")]
        public string CountryCode { get; set; } = string.Empty;

        [JsonPropertyName("endpoint")]
        public string Endpoint { get; set; } = string.Empty;

        [JsonPropertyName("latency")]
        public int? Latency { get; set; }

        [JsonPropertyName("phantunEnabled")]
        public bool PhantunEnabled { get; set; }

        [JsonPropertyName("available")]
        public bool Available { get; set; } = true;
    }

    /// <summary>
    /// API response wrapper for server list
    /// </summary>
    public class ServerListResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("servers")]
        public List<ServerInfo> Servers { get; set; } = new();

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// Gaming regions with pre-defined server info
    /// </summary>
    public static class GamingRegions
    {
        public static readonly Dictionary<string, ServerInfo> Regions = new()
        {
            ["singapore"] = new ServerInfo { Region = "singapore", DisplayName = "Singapore", CountryCode = "SG" },
            ["mumbai"] = new ServerInfo { Region = "mumbai", DisplayName = "Mumbai", CountryCode = "IN" },
            ["sydney"] = new ServerInfo { Region = "sydney", DisplayName = "Sydney", CountryCode = "AU" },
            ["tokyo"] = new ServerInfo { Region = "tokyo", DisplayName = "Tokyo", CountryCode = "JP" },
            ["germany"] = new ServerInfo { Region = "germany", DisplayName = "Germany", CountryCode = "DE" },
            ["paris"] = new ServerInfo { Region = "paris", DisplayName = "Paris", CountryCode = "FR" },
            ["america"] = new ServerInfo { Region = "america", DisplayName = "America", CountryCode = "US" },
            ["brazil"] = new ServerInfo { Region = "brazil", DisplayName = "Brazil", CountryCode = "BR" }
        };

        public static IEnumerable<ServerInfo> GetAll() => Regions.Values;

        public static ServerInfo? GetByRegion(string region)
        {
            return Regions.TryGetValue(region.ToLowerInvariant(), out var server) ? server : null;
        }
    }
}
