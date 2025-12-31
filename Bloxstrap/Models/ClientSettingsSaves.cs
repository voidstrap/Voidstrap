using System;
using System.IO;
using System.Text.Json;
using Voidstrap;

public static class VoidstrapRobloxSettingsManager // lowk didnt know what tf to name this file
{
    public class VoidstrapRobloxSettings
    {
        public int MemoryCleanerIntervalSeconds { get; set; }
    }

    private static readonly string FolderPath = Paths.Base;

    private static readonly string FilePath =
        Path.Combine(FolderPath, "VoidstrapRobloxSaves.json");

    public static VoidstrapRobloxSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new VoidstrapRobloxSettings();

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<VoidstrapRobloxSettings>(json)
                   ?? new VoidstrapRobloxSettings();
        }
        catch
        {
            return new VoidstrapRobloxSettings();
        }
    }

    public static void Save(VoidstrapRobloxSettings settings)
    {
        try
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
        }
    }
}
