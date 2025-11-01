using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Voidstrap
{
    public class GBSEditor
    {
        public XDocument? Document { get; set; }

        public Dictionary<string, string> PresetPaths { get; } = new()
        {
            { "Rendering.FramerateCap", "{UserSettings}/int[@name='FramerateCap']" },
            { "Rendering.SavedQualityLevel", "{UserSettings}/token[@name='SavedQualityLevel']" }, // 0 = automatic
            { "User.MouseSensitivity", "{UserSettings}/float[@name='MouseSensitivity']"},
            { "User.VREnabled", "{UserSettings}/bool[@name='VREnabled']"},
            { "UI.Transparency", "{UserSettings}/float[@name='PreferredTransparency']" },
            { "UI.ReducedMotion", "{UserSettings}/bool[@name='ReducedMotion']" },
            { "UI.FontSize", "{UserSettings}/token[@name='PreferredTextSize']" }
        };

        public Dictionary<string, string> RootPaths { get; } = new()
        {
            { "UserSettings", "//Item[@class='UserGameSettings']/Properties" },
        };

        public bool Loaded { get; private set; } = false;

        // ✅ Fixed invalid interpolated string syntax and environment variable usage
        public string FileLocation => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox",
            "GlobalBasicSettings_13.xml"
        );

        public bool PreviousReadOnlyState { get; private set; }

        public void SetPreset(string prefix, object? value)
        {
            foreach (var pair in PresetPaths.Where(x => x.Key.StartsWith(prefix)))
                SetValue(pair.Value, value);
        }

        public string? GetPreset(string prefix)
        {
            if (!PresetPaths.ContainsKey(prefix))
                return null;

            return GetValue(PresetPaths[prefix]);
        }

        public void SetValue(string path, object? value)
        {
            path = ResolvePath(path);

            XElement? element = Document?.XPathSelectElement(path);
            if (element is null)
                return;

            element.Value = value?.ToString() ?? string.Empty;
        }

        public string? GetValue(string path)
        {
            path = ResolvePath(path);
            return Document?.XPathSelectElement(path)?.Value;
        }

        public void SetReadOnly(bool readOnly, bool preserveState = false)
        {
            const string LOG_IDENT = "GBSEditor::SetReadOnly";

            if (!File.Exists(FileLocation))
                return;

            try
            {
                FileAttributes attributes = File.GetAttributes(FileLocation);

                if (readOnly)
                    attributes |= FileAttributes.ReadOnly;
                else
                    attributes &= ~FileAttributes.ReadOnly;

                File.SetAttributes(FileLocation, attributes);

                if (!preserveState)
                    PreviousReadOnlyState = readOnly;
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine(LOG_IDENT, $"Failed to set read-only on {FileLocation}");
                App.Logger?.WriteException(LOG_IDENT, ex);
            }
        }

        public bool GetReadOnly()
        {
            if (!File.Exists(FileLocation))
                return false;

            return File.GetAttributes(FileLocation).HasFlag(FileAttributes.ReadOnly);
        }

        public void Load()
        {
            const string LOG_IDENT = "GBSEditor::Load";

            App.Logger?.WriteLine(LOG_IDENT, $"Loading from {FileLocation}...");

            if (!File.Exists(FileLocation))
                return;

            try
            {
                Document = XDocument.Load(FileLocation);
                Loaded = true;
                PreviousReadOnlyState = GetReadOnly();
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine(LOG_IDENT, "Failed to load!");
                App.Logger?.WriteException(LOG_IDENT, ex);
            }
        }

        public virtual void Save()
        {
            const string LOG_IDENT = "GBSEditor::Save";

            App.Logger?.WriteLine(LOG_IDENT, $"Saving to {FileLocation}...");

            try
            {
                SetReadOnly(false, true);
                Document?.Save(FileLocation);
                SetReadOnly(PreviousReadOnlyState);
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine(LOG_IDENT, "Failed to save");
                App.Logger?.WriteException(LOG_IDENT, ex);
                return;
            }

            App.Logger?.WriteLine(LOG_IDENT, "Save complete!");
        }

        private string ResolvePath(string rawPath)
        {
            return Regex.Replace(rawPath, @"\{(.+?)\}", match =>
            {
                string key = match.Groups[1].Value;
                return RootPaths.TryGetValue(key, out var value) ? value : match.Value;
            });
        }
    }
}
