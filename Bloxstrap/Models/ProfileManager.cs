using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Voidstrap.Models;

namespace Voidstrap.Integrations
{
    public static class NvidiaProfileManager
    {
        private const string NVIDIA_INSPECTOR_URL =
            "https://github.com/Orbmu2k/nvidiaProfileInspector/releases/download/2.4.0.30/nvidiaProfileInspector.zip";

        private static readonly string InspectorDir =
            Path.Combine(Paths.Integrations);

        private static readonly string InspectorExe =
            Path.Combine(InspectorDir, "nvidiaProfileInspector.exe");

        private static readonly Encoding Utf16Bom =
            new UnicodeEncoding(false, true);

        public static string EmptyNipTemplate() =>
@"<?xml version=""1.0"" encoding=""utf-16""?>
<ArrayOfProfile>
  <Profile>
    <ProfileName>Voidstrap</ProfileName>
    <Executeables>
      <string>robloxplayerbeta.exe</string>
    </Executeables>
    <Settings>
    </Settings>
  </Profile>
</ArrayOfProfile>";

        public static void SaveToNip(
            string path,
            IEnumerable<NvidiaEditorEntry> entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var unique = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .GroupBy(e => (e.SettingId, e.Name))
                .Select(g => g.Last())
                .ToList();

            var settings = new XElement("Settings");

            foreach (var e in unique)
            {
                if (!TryNormalizeSettingId(e.SettingId, out string fixedId))
                    continue;

                settings.Add(
                    new XElement("ProfileSetting",
                        new XElement("SettingNameInfo", e.Name),
                        new XElement("SettingID", fixedId),
                        new XElement("ValueType", NormalizeValueType(e.ValueType)),
                        new XElement("SettingValue", e.Value ?? "0")
                    )
                );
            }

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-16", null),
                new XElement("ArrayOfProfile",
                    new XElement("Profile",
                        new XElement("ProfileName", "Voidstrap"),
                        new XElement("Executeables",
                            new XElement("string", "robloxplayerbeta.exe")),
                        settings
                    )
                )
            );

            WriteUtf16Xml(path, doc);
        }

        public static List<NvidiaEditorEntry> LoadFromNip(string path)
        {
            var results = new List<NvidiaEditorEntry>();

            if (!File.Exists(path))
                return results;

            XDocument doc;
            try
            {
                doc = XDocument.Load(path);
            }
            catch
            {
                return results;
            }

            foreach (var node in doc.Descendants("ProfileSetting"))
            {
                string name = node.Element("SettingNameInfo")?.Value;
                string id = node.Element("SettingID")?.Value;
                string value = node.Element("SettingValue")?.Value;
                string type = node.Element("ValueType")?.Value;

                if (!TryNormalizeSettingId(id, out string fixedId))
                    continue;

                results.Add(new NvidiaEditorEntry
                {
                    Name = string.IsNullOrWhiteSpace(name)
                        ? $"Setting {fixedId}"
                        : name,
                    SettingId = fixedId,
                    Value = value ?? "0",
                    ValueType = NormalizeValueType(type)
                });
            }

            return results;
        }

        private static bool TryNormalizeSettingId(string? raw, out string result)
        {
            result = null!;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            raw = raw.Trim();

            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!uint.TryParse(
                        raw.AsSpan(2),
                        System.Globalization.NumberStyles.HexNumber,
                        null,
                        out uint hex))
                    return false;

                result = hex.ToString();
                return true;
            }

            if (uint.TryParse(raw, out uint dec))
            {
                result = dec.ToString();
                return true;
            }

            return false;
        }

        private static string NormalizeValueType(string? type)
        {
            return type?.ToLowerInvariant() switch
            {
                "string" => "String",
                "binary" => "Binary",
                "boolean" => "Boolean",
                "hex" => "Hex",
                _ => "Dword"
            };
        }

        private static void WriteUtf16Xml(string path, XDocument doc)
        {
            using var writer = XmlWriter.Create(
                path,
                new XmlWriterSettings
                {
                    Encoding = Utf16Bom,
                    Indent = true,
                    OmitXmlDeclaration = false
                });

            doc.Save(writer);
        }

        public static async Task<bool> ApplyNipFile(string nipPath)
        {
            if (!File.Exists(nipPath))
                return false;

            if (!await EnsureInspectorDownloaded())
                return false;

            if (await DragDropImport(nipPath))
                return true;

            await Task.Delay(1000);
            if (await DragDropImport(nipPath))
                return true;

            await ShowManualDeleteDialog();
            return await DragDropImport(nipPath);
        }

        private static async Task WaitForFileUnlock(string path)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                        return;
                }
                catch (IOException)
                {
                    await Task.Delay(100);
                }
            }
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        private static async Task<bool> EnsureInspectorDownloaded()
        {
            if (File.Exists(InspectorExe))
                return true;

            string zipPath = Path.Combine(InspectorDir, "nvidiaProfileInspector.zip");
            string tempZipPath = Path.Combine(InspectorDir, "nvidiaProfileInspector.tmp.zip");

            try
            {
                Directory.CreateDirectory(InspectorDir);

                SafeDelete(zipPath);
                SafeDelete(tempZipPath);

                using (var response = await App.HttpClient.GetAsync(
                    NVIDIA_INSPECTOR_URL,
                    System.Net.Http.HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    await using var fs = new FileStream(
                        tempZipPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None);

                    await response.Content.CopyToAsync(fs);
                    await fs.FlushAsync(CancellationToken.None);
                }

                await WaitForFileUnlock(tempZipPath);
                ZipFile.ExtractToDirectory(tempZipPath, InspectorDir, true);
                SafeDelete(tempZipPath);

                return File.Exists(InspectorExe);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    "Failed to download NVIDIA Profile Inspector:\n\n" + ex.Message,
                    System.Windows.MessageBoxImage.Error);

                SafeDelete(zipPath);
                SafeDelete(tempZipPath);
                return false;
            }
        }

        private static async Task<bool> DragDropImport(string path)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = InspectorExe,
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                });

                if (p == null)
                    return false;

                for (int i = 0; i < 50; i++)
                {
                    await Task.Delay(100);
                    p.Refresh();

                    if (p.HasExited)
                        return false;

                    if (p.MainWindowHandle != IntPtr.Zero)
                        return true;
                }
            }
            catch { }

            return false;
        }

        private static async Task ShowManualDeleteDialog()
        {
            var driverResult = Frontend.ShowMessageBox(
                "Would you like to install or update to the latest NVIDIA Game Ready Drivers?\n\n" +
                "Recommended for resets on NIP files or fixing bugs with NIP files.\n\n" +
                "If not, click No to continue with the setup.",
                System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxButton.YesNo);

            if (driverResult == System.Windows.MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.nvidia.com/Download/index.aspx",
                    UseShellExecute = true
                });
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = InspectorExe,
                UseShellExecute = true,
                Verb = "runas"
            });

            Frontend.ShowMessageBox(
                "NVIDIA Profile Inspector Opened.\n\n" +
                "• Search for: Roblox VR\n" +
                "• Select the profile\n" +
                "• Click ❌ Delete Profile\n" +
                "• Click Apply Changes\n" +
                "• Close NVIDIA Profile Inspector\n" +
                "• Click OK",
                System.Windows.MessageBoxImage.Warning
            );

            await Task.Delay(1000);
        }
    }
}
