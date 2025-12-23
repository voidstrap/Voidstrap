using Microsoft.Win32;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Voidstrap.Integrations;
using Voidstrap.RobloxInterfaces;
using Voidstrap.UI.ViewModels.Settings;
using Wpf.Ui.Controls;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for ModsPage.xaml
    /// </summary>
    public partial class ModsPage
    {
        private string? CustomLogoPath = null;
        private string? CustomSpinnerPath = null;

        private ModsViewModel ViewModel;


        public ModsPage()
        {
            InitializeComponent();
            InitializePreview();
            ViewModel = new ModsViewModel();
            DataContext = ViewModel;

            GradientAngleTextBox.Text = "0.0";
            IncludeModificationsCheckBox.IsChecked = true;

            ViewModel.GradientStops.Add(new GradientStopViewModel { Offset = 0, ColorHex = "#FFFFFF" });

            // Load Lua script from file
            LoadLuaScript();
        }

        private void LoadLuaScript()
        {
            try
            {
                string luaScriptPath = Path.Combine(Paths.Base, "autoexecute.lua");
                if (File.Exists(luaScriptPath))
                {
                    string luaScript = File.ReadAllText(luaScriptPath);
                    LuaScriptEditor.Text = luaScript;
                }
                else
                {
                    // Set default example script
                    LuaScriptEditor.Text = "-- Lua Script Example\n-- This script executes when Roblox launches\n\n-- Call the example function\nlocal result = ExampleFunction()\nprint(\"ExampleFunction returned: \" .. tostring(result))";
                }
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine("ModsPage::LoadLuaScript", $"Error loading Lua script: {ex.Message}");
            }
        }

        private void SaveLuaScript_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string luaScriptPath = Path.Combine(Paths.Base, "autoexecute.lua");
                string luaScript = LuaScriptEditor.Text;
                
                File.WriteAllText(luaScriptPath, luaScript);
                
                App.Logger?.WriteLine("ModsPage::SaveLuaScript", $"Lua script saved to {luaScriptPath}");
            }
            catch (Exception ex)
            {
                App.Logger?.WriteLine("ModsPage::SaveLuaScript", $"Error saving Lua script: {ex.Message}");
                Frontend.ShowMessageBox($"Error saving Lua script: {ex.Message}", MessageBoxImage.Error);
            }
        }

        public void SaveCurrentLuaScript()
        {
            SaveLuaScript_Click(null!, null!);
        }


        private async void ModGenerator_Click(object sender, RoutedEventArgs e)
        {
            const string LOG_IDENT = "UI::ModGenerator";
            var overallSw = Stopwatch.StartNew();

            GenerateModButton.IsEnabled = false;
            AddStopButton.IsEnabled = false;

            DownloadStatusText.Text = "Starting mod generation...";
            App.Logger?.WriteLine(LOG_IDENT, "Mod generation started.");

            try
            {

                var (luaPackagesZip, extraTexturesZip, contentTexturesZip, versionHash, version) =
                    await Deployment.DownloadForModGenerator();

                App.Logger?.WriteLine(LOG_IDENT, $"DownloadForModGenerator returned. Version: {version} ({versionHash})");

                string VoidstrapTemp = Path.Combine(Path.GetTempPath(), "Voidstrap");

                string luaPackagesDir = Path.Combine(VoidstrapTemp, "ExtraContent", "LuaPackages");
                string extraTexturesDir = Path.Combine(VoidstrapTemp, "ExtraContent", "textures");
                string contentTexturesDir = Path.Combine(VoidstrapTemp, "content", "textures");

                void SafeExtract(string zipPath, string targetDir)
                {
                    if (Directory.Exists(targetDir))
                    {
                        try { Directory.Delete(targetDir, true); }
                        catch (Exception ex)
                        {
                            App.Logger?.WriteException(LOG_IDENT, ex);
                            throw;
                        }
                    }

                    Directory.CreateDirectory(targetDir);

                    using (var archive = ZipFile.OpenRead(zipPath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.FullName) || entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                                continue;

                            string destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));

                            if (!destinationPath.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                                throw new IOException($"Entry {entry.FullName} is trying to extract outside of {targetDir}");

                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }
                    }
                }

                DownloadStatusText.Text = "Extracting ZIPs...";
                App.Logger?.WriteLine(LOG_IDENT, "Extracting downloaded ZIPs...");

                SafeExtract(luaPackagesZip, luaPackagesDir);
                SafeExtract(extraTexturesZip, extraTexturesDir);
                SafeExtract(contentTexturesZip, contentTexturesDir);

                App.Logger?.WriteLine(LOG_IDENT, "Extraction complete.");

                var assembly = Assembly.GetExecutingAssembly();
                Dictionary<string, string[]> mappings;
                using (var stream = assembly.GetManifestResourceStream("Voidstrap.Resources.mappings.json"))
                using (var reader = new StreamReader(stream!))
                {
                    string json = await reader.ReadToEndAsync();
                    mappings = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json)!;
                }
                App.Logger?.WriteLine(LOG_IDENT, $"Loaded mappings.json with {mappings.Count} top-level entries.");

                string foundationImagesDir = Path.Combine(VoidstrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\FoundationImages\FoundationImages");
                string? getImageSetDataPath = Directory.EnumerateFiles(foundationImagesDir, "GetImageSetData.lua", SearchOption.AllDirectories).FirstOrDefault();

                if (getImageSetDataPath != null)
                    App.Logger?.WriteLine(LOG_IDENT, $"Found GetImageSetData.lua at {getImageSetDataPath}");
                else
                    App.Logger?.WriteLine(LOG_IDENT, $"No GetImageSetData.lua found under {foundationImagesDir}");

                DownloadStatusText.Text = "Recoloring images...";

                Color? solidColor = null;
                List<ModGenerator.GradientStop>? gradient = null;

                if (ViewModel.GradientStops.Count == 1)
                {
                    solidColor = ViewModel.GradientStops[0].Color;
                    App.Logger?.WriteLine(LOG_IDENT, $"Using solid color for recolor: {solidColor}");
                }
                else
                {
                    gradient = ViewModel.GradientStops.Select(s => new ModGenerator.GradientStop(s.Offset, s.Color)).ToList();
                    App.Logger?.WriteLine(LOG_IDENT, $"Using gradient with {gradient.Count} stops for recolor.");
                }

                bool colorCursors = CursorsCheckBox?.IsChecked == true;
                bool colorShiftlock = ShiftlockCheckBox?.IsChecked == true;
                bool colorEmoteWheel = EmoteWheelCheckBox?.IsChecked == true;
                bool colorVoiceChat = VoiceChatCheckBox?.IsChecked == true;

                App.Logger?.WriteLine(LOG_IDENT, "Starting RecolorAllPngs...");
                ModGenerator.RecolorAllPngs(VoidstrapTemp, solidColor, gradient, getImageSetDataPath ?? string.Empty, CustomLogoPath, CustomSpinnerPath, (float)_gradientAngle, colorCursors, colorShiftlock, colorEmoteWheel, colorVoiceChat);
                App.Logger?.WriteLine(LOG_IDENT, "RecolorAllPngs finished.");

                DownloadStatusText.Text = "Cleaning up unnecessary files...";
                var preservePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    Path.Combine(VoidstrapTemp, @"ExtraContent\LuaPackages\Packages\_Index\FoundationImages\FoundationImages\SpriteSheets")
                };

                foreach (var entry in mappings.Values)
                {
                    string fullPath = Path.Combine(VoidstrapTemp, Path.Combine(entry));
                    preservePaths.Add(fullPath);
                }

                if (getImageSetDataPath != null)
                    preservePaths.Add(getImageSetDataPath);

                if (colorCursors)
                {
                    preservePaths.Add(Path.Combine(VoidstrapTemp, @"content\textures\Cursors\KeyboardMouse\IBeamCursor.png"));
                    preservePaths.Add(Path.Combine(VoidstrapTemp, @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png"));
                    preservePaths.Add(Path.Combine(VoidstrapTemp, @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png"));
                }

                if (colorShiftlock)
                {
                    preservePaths.Add(Path.Combine(VoidstrapTemp, @"content\textures\MouseLockedCursor.png"));
                }

                if (colorEmoteWheel)
                {
                    string emotesDir = Path.Combine(VoidstrapTemp, @"content\textures\ui\Emotes\Large");
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedGradient.png"));
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedGradient@2x.png"));
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedGradient@3x.png"));
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedLine.png"));
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedLine@2x.png"));
                    preservePaths.Add(Path.Combine(emotesDir, "SelectedLine@3x.png"));
                }

                if (colorVoiceChat)
                {
                    var voiceChatMappings = new Dictionary<string, (string BaseDir, string[] Files)>
                    {
                        ["VoiceChat"] = (
                            @"content\textures\ui\VoiceChat",
                            new[]
                            {"Blank.png","Blank@2x.png","Blank@3x.png","Error.png","Error@2x.png","Error@3x.png","Muted.png","Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png"}
                        ),
                        ["SpeakerNew"] = (
                            @"content\textures\ui\VoiceChat\SpeakerNew",
                            new[]
                            {"Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Error.png","Error@2x.png","Error@3x.png","Muted.png","Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png"}
                        ),
                        ["SpeakerLight"] = (
                            @"content\textures\ui\VoiceChat\SpeakerLight",
                            new[]
                            {"Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Error.png","Error@2x.png","Error@3x.png","Muted.png"}
                        ),
                        ["SpeakerDark"] = (
                            @"content\textures\ui\VoiceChat\SpeakerDark",
                            new[]
                            {"Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Error.png","Error@2x.png","Error@3x.png","Muted.png","Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png"}
                        ),
                        ["RedSpeakerLight"] = (
                            @"content\textures\ui\VoiceChat\RedSpeakerLight",
                            new[]
                            {"Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png"}
                        ),
                        ["RedSpeakerDark"] = (
                            @"content\textures\ui\VoiceChat\RedSpeakerDark",
                            new[]
                            {"Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png"}
                        ),
                        ["New"] = (
                            @"content\textures\ui\VoiceChat\New",
                            new[]
                            {
                        "Error.png","Error@2x.png","Error@3x.png",
                        "Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Blank.png","Blank@2x.png","Blank@3x.png"}
                        ),
                        ["MicLight"] = (
                            @"content\textures\ui\VoiceChat\MicLight",
                            new[]
                            {"Error.png","Error@2x.png","Error@3x.png","Muted.png","Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png"}
                        ),
                        ["MicDark"] = (
                            @"content\textures\ui\VoiceChat\MicDark",
                            new[]
                            {"Muted.png","Muted@2x.png","Muted@3x.png","Unmuted0.png","Unmuted0@2x.png","Unmuted0@3x.png","Unmuted20.png","Unmuted20@2x.png","Unmuted20@3x.png","Unmuted40.png","Unmuted40@2x.png","Unmuted40@3x.png","Unmuted60.png","Unmuted60@2x.png","Unmuted60@3x.png","Unmuted80.png","Unmuted80@2x.png","Unmuted80@3x.png","Unmuted100.png","Unmuted100@2x.png","Unmuted100@3x.png","Error.png","Error@2x.png","Error@3x.png"}
                        )
                    };

                    foreach (var mapping in voiceChatMappings.Values)
                    {
                        string baseDir = Path.Combine(VoidstrapTemp, mapping.BaseDir);
                        foreach (var file in mapping.Files)
                        {
                            preservePaths.Add(Path.Combine(baseDir, file));
                        }
                    }
                }

                void DeleteExcept(string dir)
                {
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        if (!preservePaths.Contains(file))
                        {
                            try { File.Delete(file); } catch { /* ignore */ }
                        }
                    }

                    foreach (var subDir in Directory.GetDirectories(dir))
                    {
                        if (!preservePaths.Contains(subDir))
                        {
                            try
                            {
                                DeleteExcept(subDir);
                                if (Directory.Exists(subDir) && !Directory.EnumerateFileSystemEntries(subDir).Any())
                                    Directory.Delete(subDir);
                            }
                            catch { /* ignore */ }
                        }
                    }
                }

                if (Directory.Exists(luaPackagesDir)) DeleteExcept(luaPackagesDir);
                if (Directory.Exists(extraTexturesDir)) DeleteExcept(extraTexturesDir);
                if (Directory.Exists(contentTexturesDir)) DeleteExcept(contentTexturesDir);

                string infoPath = Path.Combine(VoidstrapTemp, "info.json");
                object? colorInfo;
                if (solidColor.HasValue)
                {
                    colorInfo = new
                    {
                        SolidColor = $"#{solidColor.Value.R:X2}{solidColor.Value.G:X2}{solidColor.Value.B:X2}"
                    };
                }
                else if (gradient != null && gradient.Count > 0)
                {
                    colorInfo = gradient.Select(g => new
                    {
                        Stop = g.Stop,
                        Color = $"#{g.Color.R:X2}{g.Color.G:X2}{g.Color.B:X2}"
                    }).ToArray();
                }
                else
                {
                    colorInfo = null;
                }

                var infoData = new
                {
                    VoidstrapVersion = App.Version,
                    CreatedUsing = "Voidstrap",
                    RobloxVersion = version,
                    RobloxVersionHash = versionHash,
                    OptionsUsed = new
                    {
                        ColorCursors = colorCursors,
                        ColorShiftlock = colorShiftlock,
                        ColorVoicechat = colorVoiceChat,
                        ColorEmoteWheel = colorEmoteWheel,
                        GradientAngle = Math.Round(_gradientAngle, 2)
                    },
                    ColorsUsed = colorInfo
                };

                string infoJson = JsonSerializer.Serialize(infoData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(infoPath, infoJson);

                if (IncludeModificationsCheckBox.IsChecked == true)
                {
                    if (!Directory.Exists(Paths.Mods))
                        Directory.CreateDirectory(Paths.Mods);

                    int copiedFiles = 0;
                    foreach (var file in Directory.GetFiles(VoidstrapTemp, "*", SearchOption.AllDirectories))
                    {
                        if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string relativePath = Path.GetRelativePath(VoidstrapTemp, file);
                        string destPath = Path.Combine(Paths.Mods, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        File.Copy(file, destPath, overwrite: true);
                        copiedFiles++;
                    }

                    DownloadStatusText.Text = $"Mod files copied to {Paths.Mods}";
                    App.Logger?.WriteLine(LOG_IDENT, $"Copied {copiedFiles} files to {Paths.Mods}");
                }
                else
                {
                    var saveDialog = new SaveFileDialog
                    {
                        FileName = "VoidstrapMod.zip",
                        Filter = "ZIP Archives (*.zip)|*.zip",
                        Title = "VoidstrapMod"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        ModGenerator.ZipResult(VoidstrapTemp, saveDialog.FileName);
                        DownloadStatusText.Text = $"Mod generated successfully! Saved to: {saveDialog.FileName}";
                        App.Logger?.WriteLine(LOG_IDENT, $"Mod zip created at {saveDialog.FileName}");
                    }
                    else
                    {
                        DownloadStatusText.Text = "Save cancelled by user.";
                        App.Logger?.WriteLine(LOG_IDENT, "User cancelled save dialog.");
                    }
                }

                overallSw.Stop();
                App.Logger?.WriteLine(LOG_IDENT, $"Mod generation completed successfully in {overallSw.ElapsedMilliseconds} ms.");
            }
            catch (Exception ex)
            {
                DownloadStatusText.Text = $"Error: {ex.Message}";
                App.Logger?.WriteException(LOG_IDENT, ex);
            }
            finally
            {
                GenerateModButton.IsEnabled = true;
                AddStopButton.IsEnabled = true;
            }
        }

        private void OnSelectCustomLogo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "Select Custom Roblox Logo"
            };

            if (dlg.ShowDialog() == true)
            {
                CustomLogoPath = dlg.FileName;

                _ = UpdatePreviewAsync();
            }
        }

        private void OnClearCustomLogo_Click(object sender, RoutedEventArgs e)
        {
            CustomLogoPath = null;

            _ = UpdatePreviewAsync();
        }

        private void OnSelectCustomSpinner_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "Select Custom Spinner"
            };

            if (dlg.ShowDialog() == true)
            {
                CustomSpinnerPath = dlg.FileName;
            }
        }

        private void OnClearCustomSpinner_Click(object sender, RoutedEventArgs e)
        {
            CustomSpinnerPath = null;
        }

        private Bitmap? _sheetOriginalBitmap = null;
        private CancellationTokenSource? _previewCts;
        private readonly List<ModGenerator.SpriteDef> _previewSprites = ParseHardcodedSpriteList();

        private void InitializePreview()
        {
            LoadEmbeddedPreviewSheet();
            PopulateSpriteSelector();
            _ = UpdatePreviewAsync();
        }

        private void LoadEmbeddedPreviewSheet()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string? resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && n.Contains("Voidstrap.Resources"));
                if (resName == null)
                    resName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
                if (resName == null) return;
                using var rs = asm.GetManifestResourceStream(resName);
                if (rs == null) return;
                using var src = new Bitmap(rs);
                _sheetOriginalBitmap?.Dispose();
                _sheetOriginalBitmap = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(_sheetOriginalBitmap)) g.DrawImage(src, 0, 0, src.Width, src.Height);
            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ModsPage::LoadEmbeddedPreviewSheet", ex);
            }
        }

        private static List<ModGenerator.SpriteDef> ParseHardcodedSpriteList()
        {
            string[] lines = new[]
            {
                "like_off 0x0 72x72","rocket_off 74x0 72x72","heart_on 148x0 72x72","trophy_off 222x0 72x72","heart_off 296x0 72x72","report_off 370x0 72x72",
                "dislike_off 0x74 72x72","music 74x74 72x72","player_count 148x74 72x72","selfview_on 222x74 72x72","notification_off 296x74 72x72","send 370x74 72x72",
                "like_on 0x148 72x72","robux 74x148 72x72","backpack_on 148x148 72x72","report_on 222x148 72x72","search 296x148 72x72","notification_on 370x148 72x72",
                "dislike_on 0x222 72x72","chat_on 74x222 72x72","backpack_off 148x222 72x72","fingerprint 222x222 72x72","roblox 296x222 72x72","roblox_studio 370x222 72x72",
                "notepad 0x296 72x72","chat_off 74x296 72x72","close 148x296 72x72","add 222x296 72x72","sync 296x296 72x72","pin 370x296 72x72",
                "picture 0x370 72x72","enlarge 74x370 72x72","headset_locked 148x370 72x72","friends_off 222x370 72x72","friends_on 296x370 72x72","person_camera 370x370 72x72"
            };

            var list = new List<ModGenerator.SpriteDef>();
            foreach (var l in lines)
            {
                var parts = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;
                string name = parts[0];
                var coords = parts[1].Split('x');
                int x = int.Parse(coords[0]);
                int y = int.Parse(coords[1]);
                var size = parts[2].Split('x');
                int w = int.Parse(size[0]);
                int h = int.Parse(size[1]);
                list.Add(new ModGenerator.SpriteDef(name, x, y, w, h));
            }
            return list;
        }

        private void PopulateSpriteSelector()
        {
            SpriteSelector.ItemsSource = _previewSprites.Select(s => s.Name).ToList();
            if (SpriteSelector.Items.Count > 0) SpriteSelector.SelectedIndex = 0;
        }

        private async Task UpdatePreviewAsync()
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var ct = _previewCts.Token;

            try
            {
                Color? solidColor = null;
                List<ModGenerator.GradientStop>? gradient = null;
                if (ViewModel.GradientStops.Count == 1)
                    solidColor = ViewModel.GradientStops[0].Color;
                else
                    gradient = ViewModel.GradientStops.Select(s => new ModGenerator.GradientStop(s.Offset, s.Color)).ToList();

                string? customRobloxPath = string.IsNullOrEmpty(CustomLogoPath) ? null : CustomLogoPath;

                if (_sheetOriginalBitmap == null) return;
                byte[] sheetBytes;
                using (var ms = new MemoryStream())
                {
                    _sheetOriginalBitmap.Save(ms, ImageFormat.Png);
                    sheetBytes = ms.ToArray();
                }

                var bmp = await Task.Run(() =>
                {
                    if (ct.IsCancellationRequested) return (Bitmap?)null;

                    try
                    {
                        using var ms2 = new MemoryStream(sheetBytes);
                        using var sheetCopy = new Bitmap(ms2);
                        float angleDeg = (float)_gradientAngle;
                        var result = RenderPreviewSheet(sheetCopy, solidColor, gradient, customRobloxPath, angleDeg);

                        return result;
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.WriteException("ModsPage::UpdatePreviewAsync(Task)", ex);
                        return (Bitmap?)null;
                    }
                }, ct).ConfigureAwait(false);

                if (bmp == null) return;

                await Dispatcher.InvokeAsync(() =>
                {
                    SheetPreview.Source = BitmapToImageSource(bmp);
                    UpdateSpritePreviewFromBitmap(bmp);
                });

                bmp.Dispose();
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                App.Logger?.WriteException("ModsPage::UpdatePreviewAsync", ex);
            }
        }

        private Bitmap RenderPreviewSheet(Bitmap sheetBmp, Color? solidColor, List<ModGenerator.GradientStop>? gradient, string? customRobloxPath, float gradientAngleDeg)
        {
            if (sheetBmp == null) throw new InvalidOperationException("sheetBmp is null.");

            var output = new Bitmap(sheetBmp.Width, sheetBmp.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(output))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(sheetBmp, 0, 0);
            }

            Bitmap? customRoblox = null;
            if (!string.IsNullOrEmpty(customRobloxPath) && File.Exists(customRobloxPath))
            {
                using var fs = new FileStream(customRobloxPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var tmp = new Bitmap(fs);
                customRoblox = new Bitmap(tmp.Width, tmp.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(customRoblox)) g.DrawImage(tmp, 0, 0, tmp.Width, tmp.Height);
            }

            try
            {
                foreach (var def in _previewSprites)
                {
                    if (def.W <= 0 || def.H <= 0) continue;
                    var rect = new Rectangle(def.X, def.Y, def.W, def.H);

                    if (string.Equals(def.Name, "roblox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (customRoblox != null)
                        {
                            using (var g = Graphics.FromImage(output))
                            {
                                g.CompositingMode = CompositingMode.SourceCopy;
                                using (var clearBrush = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
                                    g.FillRectangle(clearBrush, rect);

                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.CompositingMode = CompositingMode.SourceOver;
                                g.DrawImage(customRoblox, rect);
                            }
                        }
                        else
                        {
                            using (var cropped = sheetBmp.Clone(rect, PixelFormat.Format32bppArgb))
                            using (var recolored = ApplyMaskPreview(cropped, solidColor, gradient, gradientAngleDeg))
                            using (var g = Graphics.FromImage(output))
                            {
                                g.CompositingMode = CompositingMode.SourceOver;
                                g.DrawImage(recolored, rect);
                            }
                        }

                        continue;
                    }

                    using (var cropped = sheetBmp.Clone(rect, PixelFormat.Format32bppArgb))
                    using (var recolored = ApplyMaskPreview(cropped, solidColor, gradient, gradientAngleDeg))
                    using (var g = Graphics.FromImage(output))
                    {
                        g.CompositingMode = CompositingMode.SourceOver;
                        g.DrawImage(recolored, rect);
                    }
                }
            }
            finally
            {
                customRoblox?.Dispose();
            }

            return output;
        }

        private Bitmap ApplyMaskPreview(Bitmap original, Color? solidColor, List<ModGenerator.GradientStop>? gradient, float gradientAngleDeg)
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

            unsafe
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;
                int bytesPerPixel = 4;

                for (int y = 0; y < original.Height; y++)
                {
                    for (int x = 0; x < original.Width; x++)
                    {
                        int idx = y * srcData.Stride + x * bytesPerPixel;
                        byte a = srcPtr[idx + 3];

                        if (a <= 5)
                        {
                            dstPtr[idx] = 0;
                            dstPtr[idx + 1] = 0;
                            dstPtr[idx + 2] = 0;
                            dstPtr[idx + 3] = 0;
                            continue;
                        }

                        Color applyColor;
                        if (gradient != null && gradient.Count > 0)
                        {
                            double proj = x * cos + y * sin;
                            float t = (float)((proj - minProj) / denom);
                            t = Math.Clamp(t, 0f, 1f);
                            applyColor = InterpolateGradientPreview(gradient, t);
                        }
                        else
                        {
                            applyColor = solidColor ?? Color.White;
                        }

                        float alphaFactor = a / 255f;
                        dstPtr[idx] = (byte)(applyColor.B * alphaFactor);
                        dstPtr[idx + 1] = (byte)(applyColor.G * alphaFactor);
                        dstPtr[idx + 2] = (byte)(applyColor.R * alphaFactor);
                        dstPtr[idx + 3] = a;
                    }
                }
            }

            original.UnlockBits(srcData);
            recolored.UnlockBits(dstData);
            return recolored;
        }

        private static Color InterpolateGradientPreview(List<ModGenerator.GradientStop> gradient, float t)
        {
            if (gradient == null || gradient.Count == 0)
                return Color.White;

            var stops = gradient.OrderBy(s => s.Stop).ToList();

            if (t <= stops[0].Stop) return stops[0].Color;
            if (t >= stops[^1].Stop) return stops[^1].Color;

            ModGenerator.GradientStop left = stops[0];
            ModGenerator.GradientStop right = stops[^1];
            for (int i = 0; i < stops.Count - 1; i++)
            {
                if (t >= stops[i].Stop && t <= stops[i + 1].Stop)
                {
                    left = stops[i];
                    right = stops[i + 1];
                    break;
                }
            }

            float span = right.Stop - left.Stop;
            float localT = span > 0 ? (t - left.Stop) / span : 0f;
            localT = Math.Clamp(localT, 0f, 1f);

            int r = (int)Math.Round(left.Color.R + (right.Color.R - left.Color.R) * localT);
            int g = (int)Math.Round(left.Color.G + (right.Color.G - left.Color.G) * localT);
            int b = (int)Math.Round(left.Color.B + (right.Color.B - left.Color.B) * localT);

            r = Math.Clamp(r, 0, 255);
            g = Math.Clamp(g, 0, 255);
            b = Math.Clamp(b, 0, 255);

            return Color.FromArgb(r, g, b);
        }

        private void UpdateSpritePreviewFromBitmap(Bitmap sheetBmp)
        {
            if (SpriteSelector.SelectedItem == null) { SpritePreview.Source = null; return; }

            string sel = SpriteSelector.SelectedItem.ToString()!;
            var def = _previewSprites.FirstOrDefault(s => string.Equals(s.Name, sel, StringComparison.OrdinalIgnoreCase));
            if (def == null) { SpritePreview.Source = null; return; }

            using (var cropped = new Bitmap(def.W, def.H, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(cropped))
                {
                    g.DrawImage(sheetBmp, new Rectangle(0, 0, def.W, def.H), new Rectangle(def.X, def.Y, def.W, def.H), GraphicsUnit.Pixel);
                }

                SpritePreview.Source = BitmapToImageSource(cropped);
            }
        }

        private BitmapImage BitmapToImageSource(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        private void OnSpriteSelectorChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SheetPreview.Source is BitmapImage bi)
            {
                _ = UpdatePreviewAsync();
            }
            else
            {
                _ = UpdatePreviewAsync();
            }
        }

        private double _gradientAngle = 0.0;

        private void ApplyGradientAngleFromTextBox()
        {
            if (GradientAngleTextBox == null) return;

            string txt = GradientAngleTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(txt))
            {
                _gradientAngle = 0.0;
            }
            else
            {
                if (!double.TryParse(txt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    GradientAngleTextBox.Text = Math.Round(_gradientAngle).ToString(CultureInfo.InvariantCulture);
                    return;
                }

                val = Math.Max(0.0, Math.Min(360.0, val));
                _gradientAngle = val;
            }

            GradientAngleTextBox.Text = Math.Round(_gradientAngle).ToString(CultureInfo.InvariantCulture);

            _ = UpdatePreviewAsync();
        }

        private static readonly Regex _angleInputRegex = new Regex("^[0-9]*\\.?[0-9]*$");

        private void GradientAngleTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var tb = sender as System.Windows.Controls.TextBox;
            if (tb == null) { e.Handled = true; return; }

            string full = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength)
                .Insert(tb.SelectionStart, e.Text);

            e.Handled = !_angleInputRegex.IsMatch(full);
        }

        private void GradientAngleTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string paste = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
                if (!_angleInputRegex.IsMatch(paste))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void GradientAngleTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyGradientAngleFromTextBox();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void GradientAngleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyGradientAngleFromTextBox();
        }

        #region Gradient Color Stuff
        public class GradientStopViewModel : INotifyPropertyChanged
        {
            private float offset;
            private string colorHex = "#FFFFFF";

            public float Offset
            {
                get => offset;
                set { offset = value; OnPropertyChanged(); }
            }

            public string ColorHex
            {
                get => colorHex;
                set { colorHex = value; OnPropertyChanged(); }
            }

            public Color Color
            {
                get
                {
                    try { return ColorTranslator.FromHtml(colorHex); }
                    catch { return Color.White; }
                }
                set { ColorHex = $"#{value.R:X2}{value.G:X2}{value.B:X2}"; }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void OnAddGradientStop_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.GradientStops.Add(new GradientStopViewModel { Offset = 1f, ColorHex = "#FFFFFF" });
            _ = UpdatePreviewAsync();
        }

        private void OnRemoveGradientStop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is GradientStopViewModel stop)
            {
                ViewModel.GradientStops.Remove(stop);
                _ = UpdatePreviewAsync();
            }
        }

        private void OnMoveUpGradientStop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is GradientStopViewModel stop)
            {
                int idx = ViewModel.GradientStops.IndexOf(stop);
                if (idx > 0)
                    ViewModel.GradientStops.Move(idx, idx - 1);
                _ = UpdatePreviewAsync();
            }
        }

        private void OnMoveDownGradientStop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is GradientStopViewModel stop)
            {
                int idx = ViewModel.GradientStops.IndexOf(stop);
                if (idx < ViewModel.GradientStops.Count - 1)
                    ViewModel.GradientStops.Move(idx, idx + 1);
                _ = UpdatePreviewAsync();
            }
        }

        private void OnGradientOffsetChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Two-way binding handles the value change; refresh preview so it updates immediately.
            _ = UpdatePreviewAsync();
        }

        private void OnGradientColorHexChanged(object sender, TextChangedEventArgs e)
        {
            // Two-way binding updates the ColorHex; refresh preview so it updates immediately.
            _ = UpdatePreviewAsync();
        }

        private void OnChangeGradientColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is GradientStopViewModel stop)
            {
                var dlg = new System.Windows.Forms.ColorDialog
                {
                    AllowFullOpen = true,
                    FullOpen = true,
                    Color = ColorTranslator.FromHtml(stop.ColorHex)
                };

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    stop.ColorHex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                    _ = UpdatePreviewAsync();
                }
            }
        }
        #endregion
    }
}