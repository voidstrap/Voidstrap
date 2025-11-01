using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Voidstrap;

namespace Voidstrap.Integrations
{
    public static class ModGenerator
    {
        private static readonly SpriteBlacklist SpriteBlacklistInstance = new SpriteBlacklist
        {
            Prefixes = new List<string>
            {
                "chat_bubble/",
                "component_assets/",
                "icons/controls/voice/",
                "icons/graphic/",
                "squircles/"
            },
            Suffixes = new List<string>(),
            Keywords = new List<string>
            {
                "goldrobux",
                "icons/common/play"
            },
            Strict = new List<string>
            {
                "gradient/gradient_0_100"
            }
        };

        public class SpriteBlacklist
        {
            public List<string> Prefixes { get; set; } = new();
            public List<string> Suffixes { get; set; } = new();
            public List<string> Keywords { get; set; } = new();
            public List<string> Strict { get; set; } = new();

            public bool IsBlacklisted(string name)
            {
                if (name.StartsWith("icons/graphic/lock", StringComparison.OrdinalIgnoreCase))
                    return false;
                foreach (var p in Prefixes)
                    if (name.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        return true;
                foreach (var s in Suffixes)
                    if (name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                        return true;
                foreach (var k in Keywords)
                    if (name.Contains(k, StringComparison.OrdinalIgnoreCase))
                        return true;
                foreach (var str in Strict)
                    if (string.Equals(name, str, StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
        }

        public record GradientStop(float Stop, Color Color);

        public static void RecolorAllPngs(
            string rootDir,
            Color? solidColor,
            List<GradientStop>? gradient = null,
            string getImageSetDataPath = "",
            string? customLogoPath = null,
            string? customSpinnerPath = null,
            float gradientAngleDeg = 0f,
            bool recolorCursors = false,
            bool recolorShiftlock = false,
            bool recolorEmoteWheel = false,
            bool recolorVoiceChat = false,
            IEnumerable<string>? extraSourceDirs = null)
        {
            const string LOG_IDENT = "UI::Recolor";

            if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            {
                App.Logger?.WriteLine(LOG_IDENT, $"Invalid rootDir '{rootDir}'");
                return;
            }

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Voidstrap.Resources.mappings.json");
            if (stream == null)
            {
                App.Logger?.WriteLine(LOG_IDENT, "mappings.json embedded resource not found");
                return;
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var mappings = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
            if (mappings == null || mappings.Count == 0)
            {
                App.Logger?.WriteLine(LOG_IDENT, "mappings.json parsed but empty");
                return;
            }

            var validFiles = new HashSet<string>(mappings.Keys, StringComparer.OrdinalIgnoreCase);

            App.Logger?.WriteLine(LOG_IDENT, $"Loaded {validFiles.Count} valid entries from mappings.json");
            App.Logger?.WriteLine(LOG_IDENT, "RecolorAllPngs started.");

            foreach (var kv in mappings)
            {
                string[] parts = kv.Value;
                string relativePath = Path.Combine(parts);
                string fullPath = Path.Combine(rootDir, relativePath);

                if (!File.Exists(fullPath))
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"File missing: {relativePath}");
                    continue;
                }

                try
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"Recoloring '{relativePath}'");
                    SafeRecolorImage(fullPath, solidColor, gradient, gradientAngleDeg);
                }
                catch (Exception ex)
                {
                    App.Logger?.WriteLine(LOG_IDENT, $"Error recoloring '{relativePath}': {ex.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(getImageSetDataPath) && File.Exists(getImageSetDataPath))
            {
                App.Logger?.WriteLine(LOG_IDENT, $"Parsing image set data: {getImageSetDataPath}");

                var spriteData = LuaImageSetParser.Parse(getImageSetDataPath);
                foreach (var (sheetPath, sprites) in spriteData)
                {
                    if (!File.Exists(sheetPath)) continue;
                    SafeRecolorSpriteSheet(sheetPath, sprites, solidColor, gradient, gradientAngleDeg);
                }
            }

            // apply custom logo (fixed: check customLogoPath exists instead of getImageSetDataPath)
            if (!string.IsNullOrEmpty(customLogoPath) && File.Exists(customLogoPath) && !string.IsNullOrWhiteSpace(getImageSetDataPath) && File.Exists(getImageSetDataPath))
            {
                App.Logger?.WriteLine(LOG_IDENT, $"Applying custom logo: {customLogoPath}");

                var spriteData = LuaImageSetParser.Parse(getImageSetDataPath);
                foreach (var (sheetPath, sprites) in spriteData)
                {
                    if (!File.Exists(sheetPath)) continue;

                    bool modified = false;
                    string tempPath = sheetPath + ".logo.tmp";
                    using Bitmap customInMemory = LoadBitmapIntoMemory(customLogoPath);

                    using (var sheet = new Bitmap(sheetPath))
                    using (var g = Graphics.FromImage(sheet))
                    {
                        g.CompositingMode = CompositingMode.SourceOver;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;

                        foreach (var sprite in sprites)
                        {
                            if (!string.Equals(sprite.Name, "icons/logo/block", StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (sprite.W <= 0 || sprite.H <= 0) continue;

                            Rectangle targetRect = new Rectangle(sprite.X, sprite.Y, sprite.W, sprite.H);

                            var prevCompositing = g.CompositingMode;
                            g.CompositingMode = CompositingMode.SourceCopy;
                            using (var clearBrush = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
                            {
                                g.FillRectangle(clearBrush, targetRect);
                            }
                            g.CompositingMode = prevCompositing;

                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.CompositingMode = CompositingMode.SourceOver;
                            g.DrawImage(customInMemory, targetRect);

                            modified = true;
                        }

                        if (modified) sheet.Save(tempPath, ImageFormat.Png);
                    }

                    if (modified)
                    {
                        ReplaceFileWithRetry(sheetPath, tempPath);
                        App.Logger?.WriteLine(LOG_IDENT, $"Replaced logo in {sheetPath}");
                    }
                }
            }

            // apply custom spinner (fixed: check customSpinnerPath exists instead of getImageSetDataPath)
            if (!string.IsNullOrEmpty(customSpinnerPath) && File.Exists(customSpinnerPath) && !string.IsNullOrWhiteSpace(getImageSetDataPath) && File.Exists(getImageSetDataPath))
            {
                App.Logger?.WriteLine(LOG_IDENT, $"Applying custom spinner: {customSpinnerPath}");

                var spriteData = LuaImageSetParser.Parse(getImageSetDataPath);
                foreach (var (sheetPath, sprites) in spriteData)
                {
                    if (!File.Exists(sheetPath)) continue;

                    bool modified = false;
                    string tempPath = sheetPath + ".logo.tmp";
                    using Bitmap customInMemory = LoadBitmapIntoMemory(customSpinnerPath);

                    using (var sheet = new Bitmap(sheetPath))
                    using (var g = Graphics.FromImage(sheet))
                    {
                        g.CompositingMode = CompositingMode.SourceOver;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;

                        foreach (var sprite in sprites)
                        {
                            if (!string.Equals(sprite.Name, "icons/graphic/loadingspinner", StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (sprite.W <= 0 || sprite.H <= 0) continue;

                            Rectangle targetRect = new Rectangle(sprite.X, sprite.Y, sprite.W, sprite.H);

                            var prevCompositing = g.CompositingMode;
                            g.CompositingMode = CompositingMode.SourceCopy;
                            using (var clearBrush = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
                            {
                                g.FillRectangle(clearBrush, targetRect);
                            }
                            g.CompositingMode = prevCompositing;

                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.CompositingMode = CompositingMode.SourceOver;
                            g.DrawImage(customInMemory, targetRect);

                            modified = true;
                        }

                        if (modified) sheet.Save(tempPath, ImageFormat.Png);
                    }

                    if (modified)
                    {
                        ReplaceFileWithRetry(sheetPath, tempPath);
                        App.Logger?.WriteLine(LOG_IDENT, $"Replaced spinner in {sheetPath}");
                    }
                }
            }

            void TryRecolorFilesByNames(string[] candidateNames, string? relativeDir = null)
            {
                foreach (var name in candidateNames)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(relativeDir))
                        {
                            string expected = Path.Combine(rootDir, relativeDir, name);
                            if (File.Exists(expected))
                            {
                                SafeRecolorImage(expected, solidColor, gradient, gradientAngleDeg);
                                continue;
                            }
                        }

                        var matches = Directory.EnumerateFiles(rootDir, name, SearchOption.AllDirectories).ToList();
                        if (matches.Count == 0)
                        {
                            matches = Directory.EnumerateFiles(rootDir, "*.png", SearchOption.AllDirectories)
                                .Where(p => p.EndsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
                        }

                        if (matches.Count > 0)
                        {
                            foreach (var m in matches)
                            {
                                try
                                {
                                    SafeRecolorImage(m, solidColor, gradient, gradientAngleDeg);
                                }
                                catch (Exception ex)
                                {
                                    App.Logger?.WriteLine(LOG_IDENT, $"Error recoloring matched file '{m}': {ex.Message}");
                                }
                            }
                            continue;
                        }

                        if (extraSourceDirs != null)
                        {
                            foreach (var srcBase in extraSourceDirs)
                            {
                                if (string.IsNullOrWhiteSpace(srcBase) || !Directory.Exists(srcBase)) continue;

                                var srcMatches = Directory.EnumerateFiles(srcBase, name, SearchOption.AllDirectories).ToList();
                                if (srcMatches.Count == 0)
                                {
                                    srcMatches = Directory.EnumerateFiles(srcBase, "*.png", SearchOption.AllDirectories)
                                        .Where(p => p.EndsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
                                }

                                foreach (var src in srcMatches)
                                {
                                    try
                                    {
                                        string destDir;
                                        if (!string.IsNullOrWhiteSpace(relativeDir))
                                        {
                                            destDir = Path.Combine(rootDir, relativeDir);
                                        }
                                        else
                                        {
                                            var rel = Path.GetRelativePath(srcBase, Path.GetDirectoryName(src) ?? string.Empty);
                                            destDir = Path.Combine(rootDir, rel);
                                        }

                                        Directory.CreateDirectory(destDir);
                                        string destPath = Path.Combine(destDir, name);

                                        File.Copy(src, destPath, overwrite: true);
                                        SafeRecolorImage(destPath, solidColor, gradient, gradientAngleDeg);

                                        App.Logger?.WriteLine(LOG_IDENT, $"Copied + recolored '{src}' → '{destPath}'");
                                    }
                                    catch (Exception ex)
                                    {
                                        App.Logger?.WriteLine(LOG_IDENT, $"Failed copying/recoloring from extraSourceDirs '{src}': {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.WriteLine(LOG_IDENT, $"Exception while trying to handle '{name}': {ex.Message}");
                    }
                }
            }

            void RecolorGrayParts(string[] candidateNames, string? relativeDir = null)
            {
                // target color #4f545f
                const byte targetR = 0x4F;
                const byte targetG = 0x54;
                const byte targetB = 0x5F;

                const int targetDistThreshold = 60;
                const int grayDiffThreshold = 30;
                const float satThreshold = 0.28f;
                const int whiteThreshold = 220;

                int targetDistThresholdSq = targetDistThreshold * targetDistThreshold;

                string sep = Path.DirectorySeparatorChar.ToString();
                string[] angle270Patterns = new[]
                {
                    $"content{sep}textures{sep}ui{sep}VoiceChat",
                    $"content{sep}textures{sep}ui{sep}VoiceChat{sep}New",
                    $"content{sep}textures{sep}ui{sep}VoiceChat{sep}MicLight",
                    $"content{sep}textures{sep}ui{sep}VoiceChat{sep}MicDark"
                };

                string[] angle0Patterns = new[]
                {
                    $"content{sep}textures{sep}ui{sep}VoiceChat{sep}RedSpeakerDark",
                    $"content{sep}textures{sep}ui{sep}VoiceChat{sep}RedSpeakerLight",
                    $"content{sep}textures{sep}ui{sep}VoiceChat{sep}SpeakerDark",
                    $"content{sep}textures{sep}ui{sep}VoiceChat{sep}SpeakerLight",
                    $"content{sep}textures{sep}ui{sep}VoiceChat{sep}SpeakerNew"
                };

                Func<byte, byte, byte, byte, bool> MakeDetector() => (r, g, b, a) =>
                {
                    if (a < 8) return false;

                    int maxc = Math.Max(r, Math.Max(g, b));
                    int minc = Math.Min(r, Math.Min(g, b));
                    int diff = maxc - minc;
                    float sat = (maxc == 0) ? 0f : (diff / (float)maxc);

                    bool isGrayish = diff <= grayDiffThreshold || sat <= satThreshold;
                    bool isWhiteish = maxc >= whiteThreshold;

                    int dr = r - targetR;
                    int dg = g - targetG;
                    int db = b - targetB;
                    int distSq = dr * dr + dg * dg + db * db;
                    bool closeToTarget = distSq <= targetDistThresholdSq;

                    return isGrayish && (closeToTarget || isWhiteish);
                };

                float AngleForRelativePath(string relPath)
                {
                    if (string.IsNullOrWhiteSpace(relPath)) return gradientAngleDeg;

                    string p = relPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar).ToLowerInvariant();

                    foreach (var pat in angle270Patterns)
                    {
                        if (p.StartsWith(pat, StringComparison.OrdinalIgnoreCase) || p.Contains(pat + sep))
                            return 270f;
                    }

                    foreach (var pat in angle0Patterns)
                    {
                        if (p.StartsWith(pat, StringComparison.OrdinalIgnoreCase) || p.Contains(pat + sep))
                            return 0f;
                    }

                    return gradientAngleDeg;
                }

                foreach (var name in candidateNames)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(relativeDir))
                        {
                            string expected = Path.Combine(rootDir, relativeDir, name);
                            if (File.Exists(expected))
                            {
                                string rel = Path.Combine(relativeDir, name);
                                float angleToUse = AngleForRelativePath(rel);
                                SafeRecolorImageSelective(expected, solidColor, gradient, angleToUse, MakeDetector());
                                continue;
                            }
                        }

                        var matches = Directory.EnumerateFiles(rootDir, name, SearchOption.AllDirectories).ToList();
                        if (matches.Count == 0)
                        {
                            matches = Directory.EnumerateFiles(rootDir, "*.png", SearchOption.AllDirectories)
                                .Where(p => p.EndsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
                        }

                        if (matches.Count > 0)
                        {
                            foreach (var m in matches)
                            {
                                try
                                {
                                    string rel = Path.GetRelativePath(rootDir, m);
                                    float angleToUse = AngleForRelativePath(rel);
                                    SafeRecolorImageSelective(m, solidColor, gradient, angleToUse, MakeDetector());
                                }
                                catch (Exception ex)
                                {
                                    App.Logger?.WriteLine(LOG_IDENT, $"Error recoloring matched file '{m}': {ex.Message}");
                                }
                            }
                            continue;
                        }

                        if (extraSourceDirs != null)
                        {
                            foreach (var srcBase in extraSourceDirs)
                            {
                                if (string.IsNullOrWhiteSpace(srcBase) || !Directory.Exists(srcBase)) continue;

                                var srcMatches = Directory.EnumerateFiles(srcBase, name, SearchOption.AllDirectories).ToList();
                                if (srcMatches.Count == 0)
                                {
                                    srcMatches = Directory.EnumerateFiles(srcBase, "*.png", SearchOption.AllDirectories)
                                        .Where(p => p.EndsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
                                }

                                foreach (var src in srcMatches)
                                {
                                    try
                                    {
                                        string destDir;
                                        if (!string.IsNullOrWhiteSpace(relativeDir))
                                        {
                                            destDir = Path.Combine(rootDir, relativeDir);
                                        }
                                        else
                                        {
                                            var rel = Path.GetRelativePath(srcBase, Path.GetDirectoryName(src) ?? string.Empty);
                                            destDir = Path.Combine(rootDir, rel);
                                        }

                                        Directory.CreateDirectory(destDir);
                                        string destPath = Path.Combine(destDir, name);

                                        File.Copy(src, destPath, overwrite: true);

                                        string relForAngle = Path.GetRelativePath(rootDir, destPath);
                                        float angleToUse = AngleForRelativePath(relForAngle);

                                        SafeRecolorImageSelective(destPath, solidColor, gradient, angleToUse, MakeDetector());

                                        App.Logger?.WriteLine(LOG_IDENT, $"Copied + recolored '{src}' → '{destPath}'");
                                    }
                                    catch (Exception ex)
                                    {
                                        App.Logger?.WriteLine(LOG_IDENT, $"Failed copying/recoloring from extraSourceDirs '{src}': {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.WriteLine(LOG_IDENT, $"Exception while trying to handle '{name}': {ex.Message}");
                    }
                }
            }

            if (recolorCursors)
                TryRecolorFilesByNames(
                    new[] { "IBeamCursor.png", "ArrowCursor.png", "ArrowFarCursor.png" },
                    Path.Combine("content", "textures", "Cursors", "KeyboardMouse"));

            if (recolorShiftlock)
                TryRecolorFilesByNames(
                    new[] { "MouseLockedCursor.png" },
                    Path.Combine("content", "textures"));

            if (recolorEmoteWheel)
                TryRecolorFilesByNames(
                    new[]
                    {
                        "SelectedGradient.png", "SelectedGradient@2x.png", "SelectedGradient@3x.png",
                        "SelectedLine.png", "SelectedLine@2x.png", "SelectedLine@3x.png"
                    },
                    Path.Combine("content", "textures", "ui", "Emotes", "Large"));

            if (recolorVoiceChat)
            {
                var voiceChatMappings = new Dictionary<string, (string BaseDir, string[] Files)>
                {
                    ["VoiceChat"] = (
                        @"content\textures\ui\VoiceChat",
                        new[] { "Blank.png", "Blank@2x.png", "Blank@3x.png", "Error.png", "Error@2x.png", "Error@3x.png", "Muted.png", "Muted@2x.png", "Muted@3x.png", "Unmuted0.png", "Unmuted0@2x.png", "Unmuted0@3x.png", "Unmuted20.png", "Unmuted20@2x.png", "Unmuted20@3x.png", "Unmuted40.png", "Unmuted40@2x.png", "Unmuted40@3x.png", "Unmuted60.png", "Unmuted60@2x.png", "Unmuted60@3x.png", "Unmuted80.png", "Unmuted80@2x.png", "Unmuted80@3x.png", "Unmuted100.png", "Unmuted100@2x.png", "Unmuted100@3x.png" }
                    ),
                    ["SpeakerNew"] = (
                        @"content\textures\ui\VoiceChat\SpeakerNew",
                        new[] { "Unmuted60@3x.png", "Unmuted80.png", "Unmuted80@2x.png", "Unmuted80@3x.png", "Unmuted100.png", "Unmuted100@2x.png", "Unmuted100@3x.png", "Error.png", "Error@2x.png", "Error@3x.png", "Muted.png", "Muted@2x.png", "Muted@3x.png", "Unmuted0.png", "Unmuted0@2x.png", "Unmuted0@3x.png", "Unmuted20.png", "Unmuted20@2x.png", "Unmuted20@3x.png", "Unmuted40.png", "Unmuted40@2x.png", "Unmuted40@3x.png", "Unmuted60.png", "Unmuted60@2x.png" }
                    ),
                    ["SpeakerLight"] = (
                        @"content\textures\ui\VoiceChat\SpeakerLight",
                        new[] { "Muted@2x.png", "Muted@3x.png", "Unmuted0.png", "Unmuted0@2x.png", "Unmuted0@3x.png", "Unmuted20.png", "Unmuted20@2x.png", "Unmuted20@3x.png", "Unmuted40.png", "Unmuted40@2x.png", "Unmuted40@3x.png", "Unmuted60.png", "Unmuted60@2x.png", "Unmuted60@3x.png", "Unmuted80.png", "Unmuted80@2x.png", "Unmuted80@3x.png", "Unmuted100.png", "Unmuted100@2x.png", "Unmuted100@3x.png", "Error.png", "Error@2x.png", "Error@3x.png", "Muted.png" }
                    ),
                    ["SpeakerDark"] = (
                        @"content\textures\ui\VoiceChat\SpeakerDark",
                        new[] { "Unmuted40.png", "Unmuted40@2x.png", "Unmuted40@3x.png", "Unmuted60.png", "Unmuted60@2x.png", "Unmuted60@3x.png", "Unmuted80.png", "Unmuted80@2x.png", "Unmuted80@3x.png", "Unmuted100.png", "Unmuted100@2x.png", "Unmuted100@3x.png", "Error.png", "Error@2x.png", "Error@3x.png", "Muted.png", "Muted@2x.png", "Muted@3x.png", "Unmuted0.png", "Unmuted0@2x.png", "Unmuted0@3x.png", "Unmuted20.png", "Unmuted20@2x.png", "Unmuted20@3x.png" }
                    ),
                    ["RedSpeakerLight"] = (
                        @"content\textures\ui\VoiceChat\RedSpeakerLight",
                        new[] { "Unmuted20.png", "Unmuted20@2x.png", "Unmuted20@3x.png", "Unmuted40.png", "Unmuted40@2x.png", "Unmuted40@3x.png", "Unmuted60.png", "Unmuted60@2x.png", "Unmuted60@3x.png", "Unmuted80.png", "Unmuted80@2x.png", "Unmuted80@3x.png", "Unmuted100.png", "Unmuted100@2x.png", "Unmuted100@3x.png", "Unmuted0.png", "Unmuted0@2x.png", "Unmuted0@3x.png" }
                    ),
                    ["RedSpeakerDark"] = (
                        @"content\textures\ui\VoiceChat\RedSpeakerDark",
                        new[] { "Unmuted20.png", "Unmuted20@2x.png", "Unmuted20@3x.png", "Unmuted40.png", "Unmuted40@2x.png", "Unmuted40@3x.png", "Unmuted60.png", "Unmuted60@2x.png", "Unmuted60@3x.png", "Unmuted80.png", "Unmuted80@2x.png", "Unmuted80@3x.png", "Unmuted100.png", "Unmuted100@2x.png", "Unmuted100@3x.png", "Unmuted0.png", "Unmuted0@2x.png", "Unmuted0@3x.png" }
                    ),
                    ["New"] = (
                        @"content\textures\ui\VoiceChat\New",
                        new[] { "Error.png", "Error@2x.png", "Error@3x.png", "Unmuted0.png", "Unmuted0@2x.png", "Unmuted0@3x.png", "Unmuted20.png", "Unmuted20@2x.png", "Unmuted20@3x.png", "Unmuted40.png", "Unmuted40@2x.png", "Unmuted40@3x.png", "Unmuted60.png", "Unmuted60@2x.png", "Unmuted60@3x.png", "Unmuted80.png", "Unmuted80@2x.png", "Unmuted80@3x.png", "Unmuted100.png", "Unmuted100@2x.png", "Unmuted100@3x.png", "Blank.png", "Blank@2x.png", "Blank@3x.png" }
                    ),
                    ["MicLight"] = (
                        @"content\textures\ui\VoiceChat\MicLight",
                        new[] { "Error.png", "Error@2x.png", "Error@3x.png", "Muted.png", "Muted@2x.png", "Muted@3x.png", "Unmuted0.png", "Unmuted0@2x.png", "Unmuted0@3x.png", "Unmuted20.png", "Unmuted20@2x.png", "Unmuted20@3x.png", "Unmuted40.png", "Unmuted40@2x.png", "Unmuted40@3x.png", "Unmuted60.png", "Unmuted60@2x.png", "Unmuted60@3x.png", "Unmuted80.png", "Unmuted80@2x.png", "Unmuted80@3x.png", "Unmuted100.png", "Unmuted100@2x.png", "Unmuted100@3x.png" }
                    ),
                    ["MicDark"] = (
                        @"content\textures\ui\VoiceChat\MicDark",
                        new[] { "Muted.png", "Muted@2x.png", "Muted@3x.png", "Unmuted0.png", "Unmuted0@2x.png", "Unmuted0@3x.png", "Unmuted20.png", "Unmuted20@2x.png", "Unmuted20@3x.png", "Unmuted40.png", "Unmuted40@2x.png", "Unmuted40@3x.png", "Unmuted60.png", "Unmuted60@2x.png", "Unmuted60@3x.png", "Unmuted80.png", "Unmuted80@2x.png", "Unmuted80@3x.png", "Unmuted100.png", "Unmuted100@2x.png", "Unmuted100@3x.png", "Error.png", "Error@2x.png", "Error@3x.png" }
                    )
                };

                foreach (var mapping in voiceChatMappings.Values)
                {
                    RecolorGrayParts(mapping.Files, mapping.BaseDir);
                }
            }

            App.Logger?.WriteLine(LOG_IDENT, "RecolorAllPngs finished.");
        }

        private static Bitmap LoadBitmapIntoMemory(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var bmpFromStream = new Bitmap(fs);
                var copy = new Bitmap(bmpFromStream.Width, bmpFromStream.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(copy))
                {
                    g.CompositingMode = CompositingMode.SourceOver;
                    g.DrawImage(bmpFromStream, new Rectangle(0, 0, copy.Width, copy.Height));
                }
                return copy;
            }
        }

        public static void ZipResult(string sourceDir, string outputZip)
        {
            if (File.Exists(outputZip))
                File.Delete(outputZip);

            ZipFile.CreateFromDirectory(sourceDir, outputZip, CompressionLevel.Optimal, false);
        }

        private static void SafeRecolorImage(string path, Color? solidColor, List<GradientStop>? gradient, float gradientAngleDeg)
        {
            using (var original = new Bitmap(path))
            using (var recolored = ApplyMask(original, solidColor, gradient, gradientAngleDeg))
            {
                string tempPath = path + ".tmp";
                recolored.Save(tempPath, ImageFormat.Png);
            }

            ReplaceFileWithRetry(path, path + ".tmp");
        }

        private static void SafeRecolorSpriteSheet(string sheetPath, List<SpriteDef> sprites, Color? solidColor, List<GradientStop>? gradient, float gradientAngleDeg)
        {
            string tempPath = sheetPath + ".tmp";

            using (var sheet = new Bitmap(sheetPath))
            using (var output = new Bitmap(sheet.Width, sheet.Height, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(output))
            {
                g.Clear(Color.Transparent);

                foreach (var sprite in sprites)
                {
                    if (sprite.W <= 0 || sprite.H <= 0)
                        continue;

                    if (SpriteBlacklistInstance.IsBlacklisted(sprite.Name))
                    {
                        Rectangle rect = new Rectangle(sprite.X, sprite.Y, sprite.W, sprite.H);
                        using var croppedOriginal = sheet.Clone(rect, sheet.PixelFormat);
                        g.DrawImage(croppedOriginal, rect);
                        continue;
                    }

                    Rectangle r = new Rectangle(sprite.X, sprite.Y, sprite.W, sprite.H);
                    // Clone returns Image; cast to Bitmap to pass to ApplyMask
                    using var cropped = (Bitmap)sheet.Clone(r, sheet.PixelFormat);
                    using var recolored = ApplyMask(cropped, solidColor, gradient, gradientAngleDeg);
                    g.DrawImage(recolored, r);
                }

                output.Save(tempPath, ImageFormat.Png);
            }

            ReplaceFileWithRetry(sheetPath, tempPath);
        }

        private static void SafeRecolorImageSelective(string path, Color? solidColor, List<GradientStop>? gradient, float gradientAngleDeg, Func<byte, byte, byte, byte, bool> detector)
        {
            using (var original = new Bitmap(path))
            using (var recolored = ApplyMaskSelective(original, solidColor, gradient, gradientAngleDeg, detector))
            {
                string tempPath = path + ".tmp";
                recolored.Save(tempPath, ImageFormat.Png);
            }

            ReplaceFileWithRetry(path, path + ".tmp");
        }

        private static unsafe Bitmap ApplyMask(Bitmap original, Color? solidColor, List<GradientStop>? gradient, float gradientAngleDeg)
        {
            if (original.Width == 0 || original.Height == 0)
                return new Bitmap(original);

            var recolored = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, original.Width, original.Height);
            BitmapData srcData = original.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = recolored.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            // Angle math
            double theta = gradientAngleDeg * Math.PI / 180.0;
            double cos = Math.Cos(theta);
            double sin = Math.Sin(theta);

            double w = original.Width - 1;
            double h = original.Height - 1;
            double[] projs = new double[]
            {
                0 * cos + 0 * sin,
                w * cos + 0 * sin,
                0 * cos + h * sin,
                w * cos + h * sin
            };
            double minProj = projs.Min();
            double maxProj = projs.Max();
            double denom = maxProj - minProj;
            if (Math.Abs(denom) < 1e-6) denom = 1.0;

            try
            {
                byte* srcPtr = (byte*)srcData.Scan0.ToPointer();
                byte* dstPtr = (byte*)dstData.Scan0.ToPointer();
                int bytesPerPixel = 4;

                for (int y = 0; y < original.Height; y++)
                {
                    for (int x = 0; x < original.Width; x++)
                    {
                        int idx = y * srcData.Stride + x * bytesPerPixel;
                        byte a = srcPtr[idx + 3];

                        if (a == 0)
                        {
                            dstPtr[idx] = 0;
                            dstPtr[idx + 1] = 0;
                            dstPtr[idx + 2] = 0;
                            dstPtr[idx + 3] = 0;
                            continue;
                        }

                        Color overlay;
                        if (gradient != null && gradient.Count > 0)
                        {
                            double proj = x * cos + y * sin;
                            float t = (float)((proj - minProj) / denom);
                            t = Math.Clamp(t, 0f, 1f);
                            overlay = InterpolateGradient(gradient, t);
                        }
                        else
                        {
                            overlay = solidColor ?? Color.White;
                        }

                        dstPtr[idx] = overlay.B;
                        dstPtr[idx + 1] = overlay.G;
                        dstPtr[idx + 2] = overlay.R;
                        dstPtr[idx + 3] = a;
                    }
                }
            }
            finally
            {
                original.UnlockBits(srcData);
                recolored.UnlockBits(dstData);
            }

            return recolored;
        }

        private static unsafe Bitmap ApplyMaskSelective(Bitmap original, Color? solidColor, List<GradientStop>? gradient, float gradientAngleDeg, Func<byte, byte, byte, byte, bool> detector)
        {
            if (original.Width == 0 || original.Height == 0)
                return new Bitmap(original);

            var recolored = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, original.Width, original.Height);
            BitmapData srcData = original.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dstData = recolored.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            double theta = gradientAngleDeg * Math.PI / 180.0;
            double cos = Math.Cos(theta);
            double sin = Math.Sin(theta);

            double w = original.Width - 1;
            double h = original.Height - 1;
            double[] projs = new double[]
            {
                0 * cos + 0 * sin,
                w * cos + 0 * sin,
                0 * cos + h * sin,
                w * cos + h * sin
            };
            double minProj = projs.Min();
            double maxProj = projs.Max();
            double denom = maxProj - minProj;
            if (Math.Abs(denom) < 1e-6) denom = 1.0;

            try
            {
                byte* srcPtr = (byte*)srcData.Scan0.ToPointer();
                byte* dstPtr = (byte*)dstData.Scan0.ToPointer();
                int bytesPerPixel = 4;

                for (int y = 0; y < original.Height; y++)
                {
                    for (int x = 0; x < original.Width; x++)
                    {
                        int idx = y * srcData.Stride + x * bytesPerPixel;
                        byte bSrc = srcPtr[idx + 0];
                        byte gSrc = srcPtr[idx + 1];
                        byte rSrc = srcPtr[idx + 2];
                        byte a = srcPtr[idx + 3];

                        if (a == 0)
                        {
                            dstPtr[idx + 0] = 0;
                            dstPtr[idx + 1] = 0;
                            dstPtr[idx + 2] = 0;
                            dstPtr[idx + 3] = 0;
                            continue;
                        }

                        Color overlay;
                        if (gradient != null && gradient.Count > 0)
                        {
                            double proj = x * cos + y * sin;
                            float t = (float)((proj - minProj) / denom);
                            t = Math.Clamp(t, 0f, 1f);
                            overlay = InterpolateGradient(gradient, t);
                        }
                        else
                        {
                            overlay = solidColor ?? Color.White;
                        }

                        bool shouldMask = detector(rSrc, gSrc, bSrc, a);

                        if (shouldMask)
                        {
                            dstPtr[idx + 0] = overlay.B;
                            dstPtr[idx + 1] = overlay.G;
                            dstPtr[idx + 2] = overlay.R;
                            dstPtr[idx + 3] = a;
                        }
                        else
                        {
                            dstPtr[idx + 0] = bSrc;
                            dstPtr[idx + 1] = gSrc;
                            dstPtr[idx + 2] = rSrc;
                            dstPtr[idx + 3] = a;
                        }
                    }
                }
            }
            finally
            {
                original.UnlockBits(srcData);
                recolored.UnlockBits(dstData);
            }

            return recolored;
        }

        private static Color InterpolateGradient(List<GradientStop> gradient, float t)
        {
            t = Math.Clamp(t, 0f, 1f);

            GradientStop left = gradient[0];
            GradientStop right = gradient[^1];

            for (int i = 0; i < gradient.Count - 1; i++)
            {
                if (t >= gradient[i].Stop && t <= gradient[i + 1].Stop)
                {
                    left = gradient[i];
                    right = gradient[i + 1];
                    break;
                }
            }

            float span = right.Stop - left.Stop;
            float localT = span > 0f ? (t - left.Stop) / span : 0f;

            int r = Math.Clamp((int)(left.Color.R + (right.Color.R - left.Color.R) * localT), 0, 255);
            int g = Math.Clamp((int)(left.Color.G + (right.Color.G - left.Color.G) * localT), 0, 255);
            int b = Math.Clamp((int)(left.Color.B + (right.Color.B - left.Color.B) * localT), 0, 255);
            int a = Math.Clamp((int)(left.Color.A + (right.Color.A - left.Color.A) * localT), 0, 255);

            return Color.FromArgb(a, r, g, b);
        }

        private static void ReplaceFileWithRetry(string originalPath, string tempPath)
        {
            int attempts = 0;
            while (true)
            {
                try
                {
                    if (File.Exists(originalPath))
                        File.Delete(originalPath);
                    File.Move(tempPath, originalPath);
                    break;
                }
                catch (IOException)
                {
                    attempts++;
                    if (attempts > 5)
                        throw;
                    Thread.Sleep(50);
                }
            }
        }

        public record SpriteDef(string Name, int X, int Y, int W, int H);

        private static class LuaImageSetParser
        {
            public static Dictionary<string, List<SpriteDef>> Parse(string luaPath)
            {
                string text = File.ReadAllText(luaPath);
                var result = new Dictionary<string, List<SpriteDef>>();

                var regex = new Regex(@"\['([^']+)'\]\s*=\s*{[^}]*ImageRectOffset\s*=\s*Vector2\.new\((\d+),\s*(\d+)\)[^}]*ImageRectSize\s*=\s*Vector2\.new\((\d+),\s*(\d+)\)[^}]*ImageSet\s*=\s*'([^']+)'", RegexOptions.Compiled);

                foreach (Match match in regex.Matches(text))
                {
                    string name = match.Groups[1].Value;
                    int x = int.Parse(match.Groups[2].Value);
                    int y = int.Parse(match.Groups[3].Value);
                    int w = int.Parse(match.Groups[4].Value);
                    int h = int.Parse(match.Groups[5].Value);
                    string imageSet = match.Groups[6].Value + ".png";

                    string imagePath = Path.Combine(Path.GetDirectoryName(luaPath)!, @"..\SpriteSheets", imageSet);
                    imagePath = Path.GetFullPath(imagePath);

                    if (!result.ContainsKey(imagePath))
                        result[imagePath] = new List<SpriteDef>();

                    result[imagePath].Add(new SpriteDef(name, x, y, w, h));
                }

                return result;
            }
        }
    }
}
