using System;
using System.Collections.Generic;
using System.IO;

public static class AppearanceSettings
{
    private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appearance_settings.txt");

    public static void Save(double blackOverlay, double gradient, string backgroundPath)
    {
        var lines = new List<string>
        {
            $"BlackOverlayOpacity={blackOverlay}",
            $"GradientOpacity={gradient}",
            $"BackgroundPath={backgroundPath ?? ""}"
        };
        File.WriteAllLines(FilePath, lines);
    }

    public static (double BlackOverlay, double Gradient, string BackgroundPath) Load()
    {
        if (!File.Exists(FilePath))
            return (0.0, 1.0, string.Empty);

        double blackOverlay = 0;
        double gradient = 1.0;
        string backgroundPath = string.Empty;

        foreach (var line in File.ReadAllLines(FilePath))
        {
            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;
            switch (parts[0])
            {
                case "BlackOverlayOpacity": double.TryParse(parts[1], out blackOverlay); break;
                case "GradientOpacity": double.TryParse(parts[1], out gradient); break;
                case "BackgroundPath": backgroundPath = parts[1]; break;
            }
        }

        return (blackOverlay, gradient, backgroundPath);
    }
}