using System.Text.Json.Serialization;

namespace Voidstrap.Integrations.SwiftTunnel.Models
{
    /// <summary>
    /// WireGuard VPN configuration from SwiftTunnel API
    /// </summary>
    public class VpnConfig
    {
        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty;

        [JsonPropertyName("serverEndpoint")]
        public string Endpoint { get; set; } = string.Empty;

        [JsonPropertyName("serverPublicKey")]
        public string ServerPublicKey { get; set; } = string.Empty;

        [JsonPropertyName("privateKey")]
        public string PrivateKey { get; set; } = string.Empty;

        [JsonPropertyName("publicKey")]
        public string PublicKey { get; set; } = string.Empty;

        [JsonPropertyName("assignedIp")]
        public string AssignedIp { get; set; } = string.Empty;

        [JsonPropertyName("allowedIps")]
        public List<string> AllowedIps { get; set; } = new() { "0.0.0.0/0" };

        [JsonPropertyName("dns")]
        public List<string> Dns { get; set; } = new() { "1.1.1.1", "8.8.8.8" };

        [JsonPropertyName("phantunEnabled")]
        public bool PhantunEnabled { get; set; } = false;

        /// <summary>
        /// Generates WireGuard configuration file content
        /// </summary>
        public string ToWireGuardConfig()
        {
            return $@"[Interface]
PrivateKey = {PrivateKey}
Address = {AssignedIp}
DNS = {string.Join(", ", Dns)}

[Peer]
PublicKey = {ServerPublicKey}
Endpoint = {Endpoint}
AllowedIPs = {string.Join(", ", AllowedIps)}
PersistentKeepalive = 25";
        }
    }

    /// <summary>
    /// API response wrapper for VPN config
    /// </summary>
    public class VpnConfigResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("config")]
        public VpnConfig? Config { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
