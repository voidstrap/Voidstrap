using System.Text.Json.Serialization;

namespace Voidstrap.Models.APIs
{
    public class IPInfoResponse
    {
        [JsonPropertyName("ip")]
        public string Ip { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; } = string.Empty;
    }
}
