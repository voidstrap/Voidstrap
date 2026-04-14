using System;

namespace Voidstrap.Models
{
    public class PlayHistoryEntry
    {
        public long PlaceId { get; set; }
        public string GameName { get; set; } = "Unknown Game";
        public string ThumbnailUrl { get; set; } = "";
        public DateTime LastPlayed { get; set; }
        public int PlayCount { get; set; } = 1;

        public string LaunchUrl => $"roblox://placeId={PlaceId}";
    }
}