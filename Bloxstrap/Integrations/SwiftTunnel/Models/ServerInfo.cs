using System.Text.Json.Serialization;

namespace Voidstrap.Integrations.SwiftTunnel.Models
{
    /// <summary>
    /// VPN server information from API
    /// </summary>
    public class ServerInfo
    {
        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("country_code")]
        public string CountryCode { get; set; } = string.Empty;

        [JsonPropertyName("ip")]
        public string Ip { get; set; } = string.Empty;

        [JsonPropertyName("port")]
        public int Port { get; set; } = 51820;

        [JsonPropertyName("phantun_available")]
        public bool PhantunAvailable { get; set; }

        [JsonPropertyName("phantun_port")]
        public int? PhantunPort { get; set; }

        /// <summary>
        /// Measured latency in milliseconds (set locally)
        /// </summary>
        [JsonIgnore]
        public int? Latency { get; set; }

        /// <summary>
        /// Get the endpoint string (ip:port)
        /// </summary>
        public string Endpoint => $"{Ip}:{Port}";
    }

    /// <summary>
    /// Gaming region grouping servers
    /// </summary>
    public class GamingRegion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("country_code")]
        public string CountryCode { get; set; } = string.Empty;

        [JsonPropertyName("servers")]
        public List<string> Servers { get; set; } = new();

        /// <summary>
        /// Best latency among servers in this region (set locally)
        /// </summary>
        [JsonIgnore]
        public int? BestLatency { get; set; }
    }

    /// <summary>
    /// API response from /api/vpn/servers
    /// </summary>
    public class ServerListResponse
    {
        [JsonPropertyName("servers")]
        public List<ServerInfo> Servers { get; set; } = new();

        [JsonPropertyName("regions")]
        public List<GamingRegion> Regions { get; set; } = new();

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    /// <summary>
    /// Cached server list with timestamp
    /// </summary>
    public class CachedServerList
    {
        [JsonPropertyName("data")]
        public ServerListResponse Data { get; set; } = new();

        [JsonPropertyName("cached_at")]
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// Check if cache is still fresh (1 hour TTL)
        /// </summary>
        public bool IsFresh => (DateTime.UtcNow - CachedAt).TotalHours < 1;
    }

    /// <summary>
    /// Source of the server list data
    /// </summary>
    public enum ServerListSource
    {
        Loading,
        Api,
        Cache,
        StaleCache,
        Error
    }
}
