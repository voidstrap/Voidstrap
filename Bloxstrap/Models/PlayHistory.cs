using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Voidstrap;

namespace Voidstrap.Models
{
    public static class PlayHistory
    {
        private static string FilePath =>
            Path.Combine(Paths.Base, "PlayHistory.json");

        public static List<PlayHistoryEntry> Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new List<PlayHistoryEntry>();

                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<PlayHistoryEntry>>(json)
                       ?? new List<PlayHistoryEntry>();
            }
            catch { return new List<PlayHistoryEntry>(); }
        }

        public static void Save(List<PlayHistoryEntry> entries)
        {
            var json = JsonSerializer.Serialize(entries,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }

        public static void Record(long placeId, string gameName, string thumbnailUrl = "")
        {
            var entries = Load();
            var existing = entries.FirstOrDefault(e => e.PlaceId == placeId);

            if (existing != null)
            {
                existing.LastPlayed = DateTime.Now;
                existing.PlayCount += 1;
                existing.GameName = gameName;
                existing.ThumbnailUrl = thumbnailUrl;
            }
            else
            {
                entries.Insert(0, new PlayHistoryEntry
                {
                    PlaceId = placeId,
                    GameName = gameName,
                    ThumbnailUrl = thumbnailUrl,
                    LastPlayed = DateTime.Now,
                    PlayCount = 1
                });
            }

            if (entries.Count > 50)
                entries = entries.Take(50).ToList();

            Save(entries);
        }
    }
}