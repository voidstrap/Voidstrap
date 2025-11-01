using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Voidstrap.Models.APIs.RoValra
{
    public class RoValraTimeResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("servers")]
        public List<RoValraServer> Servers { get; set; } = new();
    }

    public class RoValraServer
    {
        [JsonPropertyName("server_id")]
        public string ServerId { get; set; } = string.Empty;

        [JsonPropertyName("place_id")]
        public long PlaceId { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("first_seen")]
        public DateTime? FirstSeen { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [JsonPropertyName("player_count")]
        public int PlayerCount { get; set; }

        [JsonPropertyName("ping_ms")]
        public double PingMs { get; set; }
    }
}
