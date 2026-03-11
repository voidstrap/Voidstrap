using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using NAudio.Gui;
using NAudio.Midi;
using System;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using Voidstrap;
using Voidstrap.AppData;
using Voidstrap.UI.Elements.Settings.Pages;
using Voidstrap.UI.ViewModels;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using static Voidstrap.UI.Elements.Settings.Pages.ModsPage;

namespace Voidstrap.UI.ViewModels.Settings
{
    public class ModsViewModel : NotifyPropertyChangedViewModel
    {
        private const string GitHubApiBase = "https://api.github.com/repos/KloBraticc/ModsHub-Reworked-/contents"; // fuckass nneda do this after this update ngl..
        public ObservableCollection<ModInfo> AvailableMods { get; set; }
        = new ObservableCollection<ModInfo>();

        public string BrightnessDisplay =>
        Brightness == 50
        ? "(Disabled)"
        : $"{Brightness:0}%";

        public ICommand PickCursorColorCommand { get; }
        public ICommand PickOutlineColorCommand { get; }
        public ICommand GenerateCursorCodeCommand { get; }
        public ICommand ApplyCursorCodeCommand { get; }

        private void OpenModsFolder() => Process.Start("explorer.exe", Paths.Mods);

        private static readonly Dictionary<string, byte[]> FontHeaders = new()
        {
            { "ttf", new byte[] { 0x00, 0x01, 0x00, 0x00 } },
            { "otf", new byte[] { 0x4F, 0x54, 0x54, 0x4F } },
            { "ttc", new byte[] { 0x74, 0x74, 0x63, 0x66 } }
        };

        private void ManageCustomFont()
        {
            if (!string.IsNullOrEmpty(TextFontTask.NewState))
            {
                TextFontTask.NewState = string.Empty;
            }
            else
            {
                var dialog = new Microsoft.Win32.OpenFileDialog { Filter = $"{Strings.Menu_FontFiles}|*.ttf;*.otf;*.ttc" };

                if (dialog.ShowDialog() != true) return;

                string type = Path.GetExtension(dialog.FileName).TrimStart('.').ToLowerInvariant();
                byte[] fileHeader = File.ReadAllBytes(dialog.FileName).Take(4).ToArray();

                if (!FontHeaders.TryGetValue(type, out var expectedHeader) || !expectedHeader.SequenceEqual(fileHeader))
                {
                    Frontend.ShowMessageBox("Custom Font Invalid", MessageBoxImage.Error);
                    return;
                }

                TextFontTask.NewState = dialog.FileName;
            }

            OnPropertyChanged(nameof(ChooseCustomFontVisibility));
            OnPropertyChanged(nameof(DeleteCustomFontVisibility));
        }

        public async Task LoadModsAsync() // was working on this but idk fuc
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("VoidstrapApp");

            var json = await http.GetStringAsync(GitHubApiBase);
            var items = JsonSerializer.Deserialize<List<GitHubContent>>(json);

            var mods = new List<ModInfo>();

            foreach (var item in items)
            {
                if (item.Type == "dir")
                {
                    var mod = new ModInfo
                    {
                        Name = item.Name,
                        FolderPath = item.Path
                    };

                    mod.ImageUrl = await GetPreviewImageUrl(item.Path, http);

                    mods.Add(mod);
                }
            }

            AvailableMods = new ObservableCollection<ModInfo>(mods);
        }

        private async Task<string> GetPreviewImageUrl(string folder, HttpClient http)
        {
            string[] extensions = { "png", "jpg", "jpeg" };

            foreach (var ext in extensions)
            {
                string rawUrl = $"https://raw.githubusercontent.com/KloBraticc/ModsHub-Reworked-/main/{folder}/Preview.{ext}";
                var response = await http.GetAsync(rawUrl);
                if (response.IsSuccessStatusCode)
                    return rawUrl;
            }

            return null;
        }

        public ObservableCollection<SkyboxPack> AvailableSkyboxPacks { get; } = new();

        public class SkyboxPack
        {
            public string Name { get; set; } = "";
            public Uri? DownloadUri { get; set; }

            public override string ToString() => Name;
        }

        private static readonly string RepoRoot = "https://api.github.com/repos/KloBraticc/SkyboxPackV2/contents";
        private readonly HttpClient _http = new HttpClient();

        public async Task LoadSkyboxPacksFromGithub()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("VoidstrapSkyboxClient");

            AvailableSkyboxPacks.Clear();

            var response = await _http.GetFromJsonAsync<JsonElement[]>(RepoRoot);
            if (response == null) return;
            var folders = response
                .Where(e => e.GetProperty("type").GetString() == "dir")
                .Select(e => e.GetProperty("name").GetString()!)
                .ToList();

            if (folders.Contains("Default"))
            {
                AvailableSkyboxPacks.Add(new SkyboxPack
                {
                    Name = "Default",
                    DownloadUri = new Uri("https://github.com/KloBraticc/SkyboxPackV2/archive/refs/heads/main.zip#Default")
                });
            }
            foreach (var name in folders.Where(f => !f.Equals("Default", StringComparison.OrdinalIgnoreCase)))
            {
                AvailableSkyboxPacks.Add(new SkyboxPack
                {
                    Name = name,
                    DownloadUri = new Uri($"https://github.com/KloBraticc/SkyboxPackV2/archive/refs/heads/main.zip#{name}")
                });
            }
            var selected = AvailableSkyboxPacks.FirstOrDefault(s =>
                s.Name.Equals(App.Settings.Prop.SkyboxName, StringComparison.OrdinalIgnoreCase))
                ?? AvailableSkyboxPacks.First();

            SelectedSkyboxPack = selected;
            App.Settings.Prop.SkyboxName = selected.Name;
        }

        private SkyboxPack? _selectedSkyboxPack;
        public SkyboxPack? SelectedSkyboxPack
        {
            get => _selectedSkyboxPack;
            set
            {
                if (_selectedSkyboxPack != value)
                {
                    _selectedSkyboxPack = value;
                    OnPropertyChanged(nameof(SelectedSkyboxPack));

                    if (_selectedSkyboxPack != null)
                    {
                        App.Settings.Prop.SkyboxName = _selectedSkyboxPack.Name;
                    }
                }
            }
        }

        public ICommand OpenModsFolderCommand => new RelayCommand(OpenModsFolder);

        public ICommand AddCustomCursorModCommand => new RelayCommand(AddCustomCursorMod);

        public ICommand RemoveCustomCursorModCommand => new RelayCommand(RemoveCustomCursorMod);

        public ICommand AddCustomShiftlockModCommand => new RelayCommand(AddCustomShiftlockMod);

        public ICommand RemoveCustomShiftlockModCommand => new RelayCommand(RemoveCustomShiftlockMod);
        public ICommand AddCustomDeathSoundCommand => new RelayCommand(AddCustomDeathSound);
        public ICommand RemoveCustomDeathSoundCommand => new RelayCommand(RemoveCustomDeathSound);

        public Visibility ChooseCustomFontVisibility => !String.IsNullOrEmpty(TextFontTask.NewState) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility DeleteCustomFontVisibility => !String.IsNullOrEmpty(TextFontTask.NewState) ? Visibility.Visible : Visibility.Collapsed;

        public ICommand ManageCustomFontCommand => new RelayCommand(ManageCustomFont);

        public ICommand OpenCompatSettingsCommand => new RelayCommand(OpenCompatSettings);

        public ModPresetTask OldDeathSoundTask { get; } = new("OldDeathSound", @"content\sounds\oof.ogg", "Sounds.OldDeath.ogg");

        public ModPresetTask OldAvatarBackgroundTask { get; } = new("OldAvatarBackground", @"ExtraContent\places\Mobile.rbxl", "OldAvatarBackground.rbxl");

        public ModPresetTask OldCharacterSoundsTask { get; } = new("OldCharacterSounds", new()
        {
            { @"content\sounds\action_footsteps_plastic.mp3", "Sounds.OldWalk.mp3"  },
            { @"content\sounds\action_jump.mp3",              "Sounds.OldJump.mp3"  },
            { @"content\sounds\action_get_up.mp3",            "Sounds.OldGetUp.mp3" },
            { @"content\sounds\action_falling.mp3",           "Sounds.Empty.mp3"    },
            { @"content\sounds\action_jump_land.mp3",         "Sounds.Empty.mp3"    },
            { @"content\sounds\action_swim.mp3",              "Sounds.Empty.mp3"    },
            { @"content\sounds\impact_water.mp3",             "Sounds.Empty.mp3"    }
        });

        public EmojiModPresetTask EmojiFontTask { get; } = new();

        public EnumModPresetTask<Enums.CursorType> CursorTypeTask { get; } = new("CursorType", new()
        {
            { Enums.CursorType.DotCursor, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.DotCursor.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.DotCursor.ArrowFarCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursorDecalDrag.png", "Cursor.DotCursor.ArrowCursorDecalDrag.png" }
            }},
                { Enums.CursorType.WhiteDotCursor, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.WhiteDotCursor.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.WhiteDotCursor.ArrowFarCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursorDecalDrag.png", "Cursor.WhiteDotCursor.ArrowCursorDecalDrag.png" }
            }},
                { Enums.CursorType.VerySmallWhiteDot, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.VerySmallWhiteDot.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.VerySmallWhiteDot.ArrowFarCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursorDecalDrag.png", "Cursor.VerySmallWhiteDot.ArrowCursorDecalDrag.png" }
            }},
            { Enums.CursorType.StoofsCursor, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.StoofsCursor.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.StoofsCursor.ArrowFarCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursorDecalDrag.png", "Cursor.StoofsCursor.ArrowCursorDecalDrag.png" }
            }},
            { Enums.CursorType.CleanCursor, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.CleanCursor.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.CleanCursor.ArrowFarCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursorDecalDrag.png", "Cursor.CleanCursor.ArrowCursorDecalDrag.png" }
            }},
            { Enums.CursorType.FPSCursor, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.FPSCursor.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.FPSCursor.ArrowFarCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursorDecalDrag.png", "Cursor.FPSCursor.ArrowCursorDecalDrag.png" }
            }},
            { Enums.CursorType.From2006, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.From2006.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.From2006.ArrowFarCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursorDecalDrag.png", "Cursor.From2006.ArrowCursorDecalDrag.png" }
            }},
            { Enums.CursorType.From2013, new() {
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png", "Cursor.From2013.ArrowCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png", "Cursor.From2013.ArrowFarCursor.png" },
                { "content\\textures\\Cursors\\KeyboardMouse\\ArrowCursorDecalDrag.png", "Cursor.From2013.ArrowCursorDecalDrag.png" }
            }}
        });

        public bool Fullbright
        {
            get => App.Settings.Prop.Fullbright;
            set => App.Settings.Prop.Fullbright = value;
        }

        public bool SkyboxEnabled
        {
            get => App.Settings.Prop.SkyBoxDataSending;
            set => App.Settings.Prop.SkyBoxDataSending = value;
        }

        public bool OverlaysEnabled
        {
            get => App.Settings.Prop.OverlaysEnabled;
            set => App.Settings.Prop.OverlaysEnabled = value;
        }

        public double Brightness
        {
            get => App.Settings.Prop.Brightness;
            set
            {
                double clamped = Math.Clamp(value, 0, 100);

                if (App.Settings.Prop.Brightness != clamped)
                {
                    App.Settings.Prop.Brightness = clamped;

                    OnPropertyChanged(nameof(Brightness));
                    OnPropertyChanged(nameof(BrightnessDisplay));
                }
            }
        }

        public bool ServerDetailsDisplay
        {
            get => App.Settings.Prop.ShowServerDetailsUI;
            set => App.Settings.Prop.ShowServerDetailsUI = value;
        }

        public bool MotionBlurOverlayEnabled
        {
            get => App.Settings.Prop.MotionBlurOverlay;
            set => App.Settings.Prop.MotionBlurOverlay = value;
        }

        public bool Crosshair
        {
            get => App.Settings.Prop.Crosshair;
            set => App.Settings.Prop.Crosshair = value;
        }

        public bool FPSCounter
        {
            get => App.Settings.Prop.FPSCounter;
            set
            {
                App.Settings.Prop.FPSCounter = value;
            }
        }
        public bool CurrentTimeDisplay
        {
            get => App.Settings.Prop.CurrentTimeDisplay;
            set
            {
                App.Settings.Prop.CurrentTimeDisplay = value;
            }
        }

        public FontModPresetTask TextFontTask { get; } = new();

        private void OpenCompatSettings()
        {
            string path = new RobloxPlayerData().ExecutablePath;

            if (File.Exists(path))
                PInvoke.SHObjectProperties(HWND.Null, SHOP_TYPE.SHOP_FILEPATH, path, "Compatibility");
            else
                Frontend.ShowMessageBox(Strings.Common_RobloxNotInstalled, MessageBoxImage.Error);

        }

        private Visibility GetVisibility(string directory, string[] filenames, bool checkExist)
        {
            bool anyExist = filenames.Any(name => File.Exists(Path.Combine(directory, name)));
            return (checkExist ? anyExist : !anyExist) ? Visibility.Visible : Visibility.Collapsed;
        }

        public Visibility ChooseCustomCursorVisibility =>
    GetVisibility(Path.Combine(Paths.Mods, "Content", "textures", "Cursors", "KeyboardMouse"),
                  new[] { "ArrowCursor.png", "ArrowFarCursor.png", "MouseLockedCursor.png" }, checkExist: false);

        public Visibility DeleteCustomCursorVisibility =>
            GetVisibility(Path.Combine(Paths.Mods, "Content", "textures", "Cursors", "KeyboardMouse"),
                          new[] { "ArrowCursor.png", "ArrowFarCursor.png", "MouseLockedCursor.png" }, checkExist: true);

        public Visibility ChooseCustomShiftlockVisibility =>
            GetVisibility(Path.Combine(Paths.Mods, "Content", "textures"),
                          new[] { "MouseLockedCursor.png" }, checkExist: false);

        public Visibility DeleteCustomShiftlockVisibility =>
            GetVisibility(Path.Combine(Paths.Mods, "Content", "textures"),
                          new[] { "MouseLockedCursor.png" }, checkExist: true);

        public Visibility ChooseCustomDeathSoundVisibility =>
            GetVisibility(Path.Combine(Paths.Mods, "Content", "sounds"),
                          new[] { "oof.ogg" }, checkExist: false);

        public Visibility DeleteCustomDeathSoundVisibility =>
            GetVisibility(Path.Combine(Paths.Mods, "Content", "sounds"),
                          new[] { "oof.ogg" }, checkExist: true);

        private void AddCustomFile(string[] targetFiles, string targetDir, string dialogTitle, string filter, string failureText, Action postAction = null!)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Title = dialogTitle
            };

            if (dialog.ShowDialog() != true)
                return;

            string sourcePath = dialog.FileName;
            Directory.CreateDirectory(targetDir);

            try
            {
                foreach (var name in targetFiles)
                {
                    string destPath = Path.Combine(targetDir, name);
                    File.Copy(sourcePath, destPath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to add {failureText}:\n{ex.Message}", MessageBoxImage.Error);
                return;
            }

            postAction?.Invoke();
        }

        private void RemoveCustomFile(string[] targetFiles, string targetDir, string notFoundMessage, Action postAction = null!)
        {
            bool anyDeleted = false;

            foreach (var name in targetFiles)
            {
                string filePath = Path.Combine(targetDir, name);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        anyDeleted = true;
                    }
                    catch (Exception ex)
                    {
                        Frontend.ShowMessageBox($"Failed to remove {name}:\n{ex.Message}", MessageBoxImage.Error);
                    }
                }
            }

            if (!anyDeleted)
            {
                Frontend.ShowMessageBox(notFoundMessage, MessageBoxImage.Information);
            }

            postAction?.Invoke();
        }

        public void AddCustomCursorMod()
        {
            AddCustomFile(
                new[] { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" },
                Path.Combine(Paths.Mods, "Content", "textures", "Cursors", "KeyboardMouse"),
                "Select a PNG Cursor Image",
                "PNG Images (*.png)|*.png",
                "cursors",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
                    OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
                });
        }

        public void RemoveCustomCursorMod()
        {
            RemoveCustomFile(
                new[] { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" },
                Path.Combine(Paths.Mods, "Content", "textures", "Cursors", "KeyboardMouse"),
                "No custom cursors found to remove.",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
                    OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
                });
        }

        public void AddCustomShiftlockMod()
        {
            AddCustomFile(
                new[] { "MouseLockedCursor.png" },
                Path.Combine(Paths.Mods, "Content", "textures"),
                "Select a PNG Shiftlock Image",
                "PNG Images (*.png)|*.png",
                "Shiftlock",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
                    OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
                });
        }

        public void RemoveCustomShiftlockMod()
        {
            RemoveCustomFile(
                new[] { "MouseLockedCursor.png" },
                Path.Combine(Paths.Mods, "Content", "textures"),
                "No custom Shiftlock found to remove.",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
                    OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
                });
        }

        public void AddCustomDeathSound()
        {
            AddCustomFile(
                new[] { "oof.ogg" },
                Path.Combine(Paths.Mods, "Content", "sounds"),
                "Select a Custom Death Sound",
                "OGG Audio (*.ogg)|*.ogg",
                "death sound",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomDeathSoundVisibility));
                    OnPropertyChanged(nameof(DeleteCustomDeathSoundVisibility));
                });
        }

        public void RemoveCustomDeathSound()
        {
            RemoveCustomFile(
                new[] { "oof.ogg" },
                Path.Combine(Paths.Mods, "Content", "sounds"),
                "No custom death sound found to remove.",
                () =>
                {
                    OnPropertyChanged(nameof(ChooseCustomDeathSoundVisibility));
                    OnPropertyChanged(nameof(DeleteCustomDeathSoundVisibility));
                });
        }

        public ObservableCollection<GradientStopViewModel> GradientStops { get; set; } = new();


        #region Custom Cursor Set Related Code
        public ObservableCollection<CustomCursorSet> CustomCursorSets { get; } = new();

        private int _selectedCustomCursorSetIndex;
        public int SelectedCustomCursorSetIndex
        {
            get => _selectedCustomCursorSetIndex;
            set
            {
                if (_selectedCustomCursorSetIndex != value)
                {
                    _selectedCustomCursorSetIndex = value;
                    OnPropertyChanged(nameof(SelectedCustomCursorSetIndex));
                    OnPropertyChanged(nameof(SelectedCustomCursorSet));
                    OnPropertyChanged(nameof(IsCustomCursorSetSelected));
                    SelectedCustomCursorSetName = SelectedCustomCursorSet?.Name ?? "";

                    SelectedCustomCursorSetIndex = value;
                    NotifyCursorVisibilities();
                    LoadCursorPathsForSelectedSet();
                }
            }
        }

        public CustomCursorSet? SelectedCustomCursorSet =>
            SelectedCustomCursorSetIndex >= 0 && SelectedCustomCursorSetIndex < CustomCursorSets.Count
                ? CustomCursorSets[SelectedCustomCursorSetIndex]
                : null;

        public bool IsCustomCursorSetSelected => SelectedCustomCursorSet is not null;

        private string _selectedCustomCursorSetName = string.Empty;
        public string SelectedCustomCursorSetName
        {
            get => _selectedCustomCursorSetName;
            set
            {
                if (_selectedCustomCursorSetName != value)
                {
                    _selectedCustomCursorSetName = value;
                    OnPropertyChanged(nameof(SelectedCustomCursorSetName));
                }
            }
        }

        public ICommand AddCustomCursorSetCommand => new RelayCommand(AddCustomCursorSet);
        public ICommand DeleteCustomCursorSetCommand => new RelayCommand(DeleteCustomCursorSet);
        public ICommand RenameCustomCursorSetCommand => new RelayCommand(RenameCustomCursorSet);
        public ICommand ApplyCursorSetCommand => new RelayCommand(ApplyCursorSet);
        public ICommand GetCurrentCursorSetCommand => new RelayCommand(GetCurrentCursorSet);
        public ICommand ExportCursorSetCommand => new RelayCommand(ExportCursorSet);
        public ICommand ImportCursorSetCommand => new RelayCommand(ImportCursorSet);
        public ICommand AddArrowCursorCommand => new RelayCommand(() => AddCursorImage("ArrowCursor.png", "Select Arrow Cursor PNG"));
        public ICommand AddArrowFarCursorCommand => new RelayCommand(() => AddCursorImage("ArrowFarCursor.png", "Select Arrow Far Cursor PNG"));
        public ICommand AddIBeamCursorCommand => new RelayCommand(() => AddCursorImage("IBeamCursor.png", "Select IBeam Cursor PNG"));
        public ICommand AddShiftlockCursorCommand => new RelayCommand(AddShiftlockCursor);
        public ICommand DeleteArrowCursorCommand => new RelayCommand(() => DeleteCursorImage("ArrowCursor.png"));
        public ICommand DeleteArrowFarCursorCommand => new RelayCommand(() => DeleteCursorImage("ArrowFarCursor.png"));
        public ICommand DeleteIBeamCursorCommand => new RelayCommand(() => DeleteCursorImage("IBeamCursor.png"));
        public ICommand DeleteShiftlockCursorCommand => new RelayCommand(() => DeleteCursorImage("MouseLockedCursor.png"));
        public RelayCommand DownloadCurCommand { get; }
        public RelayCommand DownloadPngCommand { get; }

        private CrosshairShape _selectedShape = CrosshairShape.Cross;
        private string _cursorColorHex = "#00FF00";
        private string _cursorOutlineColorHex = "#000000";
        private int _cursorSize = 20;
        private int _crosshairThickness = 2;
        private int _gap = 4;
        private double _cursorOpacity = 1.0;
        private string _cursorCode;
        private ImageSource _cursorPreview;

        private bool _useImageCrosshair;
        private string _imageUrl;

        private readonly string _dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Voidstrap");
        private readonly string _file;

        public ModsViewModel()
        {
            _file = Path.Combine(_dir, "crosshair.ini");
            Directory.CreateDirectory(_dir);

            LoadIni();

            PickCursorColorCommand = new RelayCommand(() => PickColor(true));
            PickOutlineColorCommand = new RelayCommand(() => PickColor(false));
            GenerateCursorCodeCommand = new RelayCommand(GenerateCode);
            ApplyCursorCodeCommand = new RelayCommand(ApplyCode);
            DownloadCurCommand = new RelayCommand(DownloadCurFile);
            DownloadPngCommand = new RelayCommand(DownloadPngFile);

            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(UpdatePreview));

            _ = LoadSkyboxPacksFromGithub();
            LoadCustomCursorSets();
            LoadCursorPathsForSelectedSet();
            NotifyCursorVisibilities();
        }

        public async Task InitializeAsync()
        {
            await LoadModsAsync();
        }

        public enum CrosshairShape
        {
            Cross,
            Dot,
            Circle,
            Image
        }

        public CrosshairShape[] CrosshairShapes =>
            new[] { CrosshairShape.Cross, CrosshairShape.Dot, CrosshairShape.Circle, CrosshairShape.Image };

        public CrosshairShape SelectedShape
        {
            get => _selectedShape;
            set
            {
                if (!SetProperty(ref _selectedShape, value)) return;
                UseImageCrosshair = value == CrosshairShape.Image;
                SaveIni();
                UpdatePreview();
            }
        }

        public bool UseImageCrosshair
        {
            get => _useImageCrosshair;
            set
            {
                if (!SetProperty(ref _useImageCrosshair, value)) return;
                SaveIni();
                UpdatePreview();
            }
        }

        public string ImageUrl
        {
            get => _imageUrl;
            set
            {
                if (!SetProperty(ref _imageUrl, value)) return;
                SaveIni();
                UpdatePreview();
            }
        }

        public string CursorColorHex
        {
            get => _cursorColorHex;
            set { SetProperty(ref _cursorColorHex, value); SaveIni(); UpdatePreview(); }
        }

        public string CursorOutlineColorHex
        {
            get => _cursorOutlineColorHex;
            set { SetProperty(ref _cursorOutlineColorHex, value); SaveIni(); UpdatePreview(); }
        }

        public int CursorSize
        {
            get => _cursorSize;
            set { SetProperty(ref _cursorSize, value); SaveIni(); UpdatePreview(); }
        }

        public int CrosshairThickness
        {
            get => _crosshairThickness;
            set { SetProperty(ref _crosshairThickness, value); SaveIni(); UpdatePreview(); }
        }

        public int Gap
        {
            get => _gap;
            set { SetProperty(ref _gap, value); SaveIni(); UpdatePreview(); }
        }

        public double CursorOpacity
        {
            get => _cursorOpacity;
            set { SetProperty(ref _cursorOpacity, value); SaveIni(); UpdatePreview(); }
        }

        public string CursorCode
        {
            get => _cursorCode;
            set => SetProperty(ref _cursorCode, value);
        }

        public ImageSource CursorPreview
        {
            get => _cursorPreview;
            set => SetProperty(ref _cursorPreview, value);
        }

        private void PickColor(bool main)
        {
            using var dlg = new ColorDialog();
            if (dlg.ShowDialog() != DialogResult.OK) return;

            var hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
            if (main) CursorColorHex = hex;
            else CursorOutlineColorHex = hex;
        }

        private ImageSource LoadImageFromUrl(string url)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(url, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private void DownloadCurFile() // goofass someone made this - bratic did
        {
            try
            {
                const int size = 64; // should I make this customizable HELL NOO
                double center = size / 2.0;
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size));

                    if (SelectedShape == CrosshairShape.Image && !string.IsNullOrWhiteSpace(ImageUrl))
                    {
                        var img = LoadImageFromUrl(ImageUrl) as BitmapSource;
                        if (img != null)
                            dc.DrawImage(img, new Rect(0, 0, size, size));
                    }
                    else
                    {
                        var mainColor = (Color)ColorConverter.ConvertFromString(CursorColorHex);
                        var outlineColor = (Color)ColorConverter.ConvertFromString(CursorOutlineColorHex);

                        var mainBrush = new SolidColorBrush(mainColor) { Opacity = CursorOpacity };
                        var outlineBrush = new SolidColorBrush(outlineColor) { Opacity = CursorOpacity };
                        mainBrush.Freeze();
                        outlineBrush.Freeze();

                        double scale = 1.0;
                        double drawSize = CursorSize * scale;
                        double gap = Gap * scale;
                        double thickness = Math.Max(1, CrosshairThickness * scale);

                        var mainPen = new Pen(mainBrush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                        var outlinePen = new Pen(outlineBrush, thickness + 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                        mainPen.Freeze(); outlinePen.Freeze();

                        switch (SelectedShape)
                        {
                            case CrosshairShape.Cross:
                                dc.DrawLine(outlinePen, new Point(center - drawSize, center), new Point(center - gap, center));
                                dc.DrawLine(outlinePen, new Point(center + gap, center), new Point(center + drawSize, center));
                                dc.DrawLine(outlinePen, new Point(center, center - drawSize), new Point(center, center - gap));
                                dc.DrawLine(outlinePen, new Point(center, center + gap), new Point(center, center + drawSize));

                                dc.DrawLine(mainPen, new Point(center - drawSize, center), new Point(center - gap, center));
                                dc.DrawLine(mainPen, new Point(center + gap, center), new Point(center + drawSize, center));
                                dc.DrawLine(mainPen, new Point(center, center - drawSize), new Point(center, center - gap));
                                dc.DrawLine(mainPen, new Point(center, center + gap), new Point(center, center + drawSize));
                                break;

                            case CrosshairShape.Dot:
                                double r = drawSize / 3;
                                dc.DrawEllipse(outlineBrush, null, new Point(center, center), r + 2, r + 2);
                                dc.DrawEllipse(mainBrush, null, new Point(center, center), r, r);
                                break;

                            case CrosshairShape.Circle:
                                double rc = drawSize / 2;
                                dc.DrawEllipse(null, outlinePen, new Point(center, center), rc, rc);
                                dc.DrawEllipse(null, mainPen, new Point(center, center), rc - 2, rc - 2);
                                break;
                        }
                    }
                }

                var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(visual);

                int stride = size * 4;
                byte[] pixels = new byte[stride * size];
                bmp.CopyPixels(pixels, stride, 0);

                byte[] flipped = new byte[pixels.Length];
                for (int y = 0; y < size; y++)
                {
                    Array.Copy(pixels, y * stride, flipped, (size - y - 1) * stride, stride);
                }

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Cursor File (*.cur)|*.cur",
                    FileName = "crosshair.cur"
                };

                if (saveDialog.ShowDialog() != true)
                    return;

                using var fs = new FileStream(saveDialog.FileName, FileMode.Create);
                using var bw = new BinaryWriter(fs);

                bw.Write((ushort)0);
                bw.Write((ushort)2);
                bw.Write((ushort)1);

                bw.Write((byte)size);
                bw.Write((byte)size);
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)(size / 2));
                bw.Write((ushort)(size / 2));

                int bmpDataSize = 40 + flipped.Length + (size * ((size + 7) / 8));
                bw.Write((uint)bmpDataSize);
                bw.Write((uint)22);

                bw.Write(40);
                bw.Write(size);
                bw.Write(size * 2);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write(0);
                bw.Write(flipped.Length);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);

                bw.Write(flipped);

                int maskBytes = size * ((size + 7) / 8);
                bw.Write(new byte[maskBytes]);

                bw.Flush();
                Frontend.ShowMessageBox("Crosshair CUR Saved");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox("Failed to generate cursor:\n" + ex.Message);
            }
        }

        private void DownloadPngFile()
        {
            try
            {
                const int size = 128;
                double center = size / 2.0;

                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size));

                    if (SelectedShape == CrosshairShape.Image && !string.IsNullOrWhiteSpace(ImageUrl))
                    {
                        var img = LoadImageFromUrl(ImageUrl) as BitmapSource;
                        if (img != null)
                            dc.DrawImage(img, new Rect(0, 0, size, size));
                    }
                    else
                    {
                        var mainColor = (Color)ColorConverter.ConvertFromString(CursorColorHex);
                        var outlineColor = (Color)ColorConverter.ConvertFromString(CursorOutlineColorHex);

                        var mainBrush = new SolidColorBrush(mainColor) { Opacity = CursorOpacity };
                        var outlineBrush = new SolidColorBrush(outlineColor) { Opacity = CursorOpacity };
                        mainBrush.Freeze(); outlineBrush.Freeze();

                        double scale = 1.0;
                        double drawSize = CursorSize * scale;
                        double gap = Gap * scale;
                        double thickness = Math.Max(1, CrosshairThickness * scale);

                        var mainPen = new Pen(mainBrush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                        var outlinePen = new Pen(outlineBrush, thickness + 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                        mainPen.Freeze(); outlinePen.Freeze();

                        switch (SelectedShape)
                        {
                            case CrosshairShape.Cross:
                                dc.DrawLine(outlinePen, new Point(center - drawSize, center), new Point(center - gap, center));
                                dc.DrawLine(outlinePen, new Point(center + gap, center), new Point(center + drawSize, center));
                                dc.DrawLine(outlinePen, new Point(center, center - drawSize), new Point(center, center - gap));
                                dc.DrawLine(outlinePen, new Point(center, center + gap), new Point(center, center + drawSize));

                                dc.DrawLine(mainPen, new Point(center - drawSize, center), new Point(center - gap, center));
                                dc.DrawLine(mainPen, new Point(center + gap, center), new Point(center + drawSize, center));
                                dc.DrawLine(mainPen, new Point(center, center - drawSize), new Point(center, center - gap));
                                dc.DrawLine(mainPen, new Point(center, center + gap), new Point(center, center + drawSize));
                                break;

                            case CrosshairShape.Dot:
                                double r = drawSize / 3;
                                dc.DrawEllipse(outlineBrush, null, new Point(center, center), r + 2, r + 2);
                                dc.DrawEllipse(mainBrush, null, new Point(center, center), r, r);
                                break;

                            case CrosshairShape.Circle:
                                double rc = drawSize / 2;
                                dc.DrawEllipse(null, outlinePen, new Point(center, center), rc, rc);
                                dc.DrawEllipse(null, mainPen, new Point(center, center), rc - 2, rc - 2);
                                break;
                        }
                    }
                }

                var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(visual);

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG Image (*.png)|*.png",
                    FileName = "crosshair.png"
                };
                if (saveDialog.ShowDialog() != true)
                    return;

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using var fs = new FileStream(saveDialog.FileName, FileMode.Create);
                encoder.Save(fs);

                Frontend.ShowMessageBox("Crosshair PNG Saved");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox("Failed to save PNG:\n" + ex.Message);
            }
        }

        public void GenerateCode()
        {
            if (SelectedShape == CrosshairShape.Image && !string.IsNullOrWhiteSpace(ImageUrl))
            {
                CursorCode = $"VXH:IMAGE|{ImageUrl}|{CursorSize}|{CursorOpacity}";
                return;
            }

            CursorCode =
                $"VXH:{SelectedShape}|{CursorColorHex}|{CursorOutlineColorHex}|{CursorSize}|{CrosshairThickness}|{Gap}|{CursorOpacity}";
        }

        private void ApplyCode()
        {
            if (string.IsNullOrWhiteSpace(CursorCode) || !CursorCode.StartsWith("VXH:"))
                return;

            var parts = CursorCode.Substring(4).Split('|');
            try
            {
                if (parts[0] == "IMAGE")
                {
                    SelectedShape = CrosshairShape.Image;
                    ImageUrl = parts.Length > 1 ? parts[1] : "";
                    CursorSize = (parts.Length > 2 && int.TryParse(parts[2], out var size)) ? size : 20;
                    CursorOpacity = (parts.Length > 3 && double.TryParse(parts[3], out var opacity)) ? opacity : 1.0;
                    return;
                }

                if (!Enum.TryParse<CrosshairShape>(parts[0], true, out var shape))
                    shape = CrosshairShape.Cross;
                SelectedShape = shape;

                CursorColorHex = parts.Length > 1 ? parts[1] : "#00FF00";
                CursorOutlineColorHex = parts.Length > 2 ? parts[2] : "#000000";
                CursorSize = (parts.Length > 3 && int.TryParse(parts[3], out var sizeVal)) ? sizeVal : 20;
                CrosshairThickness = (parts.Length > 4 && int.TryParse(parts[4], out var thickVal)) ? thickVal : 2;
                Gap = (parts.Length > 5 && int.TryParse(parts[5], out var gapVal)) ? gapVal : 4;
                CursorOpacity = (parts.Length > 6 && double.TryParse(parts[6], out var opVal)) ? opVal : 1.0;
            }
            catch
            {
                // dont catch anything... UWU we catch UWU
            }
        }

        private void UpdatePreview()
        {
            if (System.Windows.Application.Current == null)
                return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    const int sizePx = 128;
                    double center = sizePx / 2.0;

                    if (SelectedShape == CrosshairShape.Image && !string.IsNullOrWhiteSpace(ImageUrl))
                    {
                        var img = LoadImageFromUrl(ImageUrl);
                        if (img != null)
                        {
                            CursorPreview = img;
                            return;
                        }
                    }

                    var visual = new DrawingVisual();

                    using (var dc = visual.RenderOpen())
                    {
                        dc.DrawRectangle(
                            Brushes.Transparent,
                            null,
                            new Rect(0, 0, sizePx, sizePx)
                        );

                        var mainColor = (Color)ColorConverter.ConvertFromString(CursorColorHex);
                        var outlineColor = (Color)ColorConverter.ConvertFromString(CursorOutlineColorHex);

                        var mainBrush = new SolidColorBrush(mainColor)
                        {
                            Opacity = CursorOpacity
                        };
                        mainBrush.Freeze();

                        var outlineBrush = new SolidColorBrush(outlineColor)
                        {
                            Opacity = CursorOpacity
                        };
                        outlineBrush.Freeze();

                        double scale = 0.75;
                        double size = CursorSize * scale;
                        double gap = Gap * scale;
                        double thickness = Math.Max(1, CrosshairThickness * scale);

                        var mainPen = new Pen(mainBrush, thickness)
                        {
                            StartLineCap = PenLineCap.Round,
                            EndLineCap = PenLineCap.Round,
                            LineJoin = PenLineJoin.Round
                        };
                        mainPen.Freeze();

                        var outlinePen = new Pen(outlineBrush, thickness + 2)
                        {
                            StartLineCap = PenLineCap.Round,
                            EndLineCap = PenLineCap.Round,
                            LineJoin = PenLineJoin.Round
                        };
                        outlinePen.Freeze();

                        switch (SelectedShape)
                        {
                            case CrosshairShape.Cross:
                                {
                                    dc.DrawLine(outlinePen,
                                        new Point(center - size, center),
                                        new Point(center - gap, center));

                                    dc.DrawLine(outlinePen,
                                        new Point(center + gap, center),
                                        new Point(center + size, center));

                                    dc.DrawLine(mainPen,
                                        new Point(center - size, center),
                                        new Point(center - gap, center));

                                    dc.DrawLine(mainPen,
                                        new Point(center + gap, center),
                                        new Point(center + size, center));

                                    dc.DrawLine(outlinePen,
                                        new Point(center, center - size),
                                        new Point(center, center - gap));

                                    dc.DrawLine(outlinePen,
                                        new Point(center, center + gap),
                                        new Point(center, center + size));

                                    dc.DrawLine(mainPen,
                                        new Point(center, center - size),
                                        new Point(center, center - gap));

                                    dc.DrawLine(mainPen,
                                        new Point(center, center + gap),
                                        new Point(center, center + size));
                                    break;
                                }

                            case CrosshairShape.Dot:
                                {
                                    double r = size / 3;
                                    dc.DrawEllipse(outlineBrush, null,
                                        new Point(center, center), r + 2, r + 2);
                                    dc.DrawEllipse(mainBrush, null,
                                        new Point(center, center), r, r);
                                    break;
                                }

                            case CrosshairShape.Circle:
                                {
                                    double r = size / 2;
                                    dc.DrawEllipse(null, outlinePen,
                                        new Point(center, center), r, r);
                                    dc.DrawEllipse(null, mainPen,
                                        new Point(center, center), r - 2, r - 2);
                                    break;
                                }
                        }
                    }

                    var bmp = new RenderTargetBitmap(
                        sizePx,
                        sizePx,
                        96,
                        96,
                        PixelFormats.Pbgra32);

                    bmp.Render(visual);
                    bmp.Freeze();

                    CursorPreview = bmp;
                }
                catch
                {
                    CursorPreview = null;
                }
            });
        }
        private void SaveIni()
        {
            IniFile.Write(_file, new()
            {
                ["Shape"] = SelectedShape.ToString(),
                ["Color"] = CursorColorHex,
                ["Outline"] = CursorOutlineColorHex,
                ["Size"] = CursorSize.ToString(),
                ["Thickness"] = CrosshairThickness.ToString(),
                ["Gap"] = Gap.ToString(),
                ["Opacity"] = CursorOpacity.ToString(),
                ["ImageUrl"] = ImageUrl ?? ""
            });
        }

        private void LoadIni()
        {
            if (!File.Exists(_file)) return;

            var ini = IniFile.Read(_file);
            string shapeStr = ini.GetValueOrDefault("Shape", "Cross");
            if (!Enum.TryParse<CrosshairShape>(shapeStr, true, out var parsedShape))
            {
                parsedShape = CrosshairShape.Cross;
            }
            SelectedShape = parsedShape;
            CursorColorHex = ini.GetValueOrDefault("Color", "#00FF00");
            CursorOutlineColorHex = ini.GetValueOrDefault("Outline", "#000000");

            if (!int.TryParse(ini.GetValueOrDefault("Size", "20"), out int size))
                size = 20;
            CursorSize = size;

            if (!int.TryParse(ini.GetValueOrDefault("Thickness", "2"), out int thickness))
                thickness = 2;
            CrosshairThickness = thickness;

            if (!int.TryParse(ini.GetValueOrDefault("Gap", "4"), out int gap))
                gap = 4;
            Gap = gap;

            if (!double.TryParse(ini.GetValueOrDefault("Opacity", "1.0"), out double opacity))
                opacity = 1.0;
            CursorOpacity = opacity;

            ImageUrl = ini.GetValueOrDefault("ImageUrl", "");
        }

        #endregion
        #region Button Logic
        private void LoadCustomCursorSets()
        {
            CustomCursorSets.Clear();

            if (!Directory.Exists(Paths.CustomCursors))
                Directory.CreateDirectory(Paths.CustomCursors);

            foreach (var dir in Directory.GetDirectories(Paths.CustomCursors))
            {
                var name = Path.GetFileName(dir);

                CustomCursorSets.Add(new CustomCursorSet
                {
                    Name = name,
                    FolderPath = dir
                });
            }

            if (CustomCursorSets.Any())
                SelectedCustomCursorSetIndex = 0;

            OnPropertyChanged(nameof(IsCustomCursorSetSelected));
        }

        private void AddCustomCursorSet()
        {
            string basePath = Paths.CustomCursors;
            int index = 1;
            string newFolderPath;

            do
            {
                string folderName = $"Custom Cursor Set {index}";
                newFolderPath = Path.Combine(basePath, folderName);
                index++;
            }
            while (Directory.Exists(newFolderPath));

            try
            {
                Directory.CreateDirectory(newFolderPath);

                var newSet = new CustomCursorSet
                {
                    Name = Path.GetFileName(newFolderPath),
                    FolderPath = newFolderPath
                };

                CustomCursorSets.Add(newSet);
                SelectedCustomCursorSetIndex = CustomCursorSets.Count - 1;
                OnPropertyChanged(nameof(IsCustomCursorSetSelected));
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::AddCustomCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to create cursor set:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        private void DeleteCustomCursorSet()
        {
            if (SelectedCustomCursorSet is null)
                return;

            try
            {
                if (Directory.Exists(SelectedCustomCursorSet.FolderPath))
                    Directory.Delete(SelectedCustomCursorSet.FolderPath, true);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::DeleteCustomCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to delete cursor set:\n{ex.Message}", MessageBoxImage.Error);
                return;
            }

            CustomCursorSets.Remove(SelectedCustomCursorSet);

            if (CustomCursorSets.Any())
            {
                SelectedCustomCursorSetIndex = CustomCursorSets.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomCursorSet));
            }

            OnPropertyChanged(nameof(IsCustomCursorSetSelected));
        }

        private void RenameCustomCursorSetStructure(string oldName, string newName)
        {
            string oldDir = Path.Combine(Paths.CustomCursors, oldName);
            string newDir = Path.Combine(Paths.CustomCursors, newName);

            if (Directory.Exists(newDir))
                throw new IOException("A folder with the new name already exists.");

            Directory.Move(oldDir, newDir);
        }

        private void RenameCustomCursorSet()
        {
            const string LOG_IDENT = "ModsViewModel::RenameCustomCursorSet";

            if (SelectedCustomCursorSet is null || SelectedCustomCursorSet.Name == SelectedCustomCursorSetName)
                return;

            if (string.IsNullOrWhiteSpace(SelectedCustomCursorSetName))
            {
                Frontend.ShowMessageBox("Name cannot be empty.", MessageBoxImage.Error);
                return;
            }

            var validationResult = PathValidator.IsFileNameValid(SelectedCustomCursorSetName);

            if (validationResult != PathValidator.ValidationResult.Ok)
            {
                string msg = validationResult switch
                {
                    PathValidator.ValidationResult.IllegalCharacter => "Name contains illegal characters.",
                    PathValidator.ValidationResult.ReservedFileName => "Name is reserved.",
                    _ => "Unknown validation error."
                };

                App.Logger.WriteLine(LOG_IDENT, $"Validation result: {validationResult}");
                Frontend.ShowMessageBox(msg, MessageBoxImage.Error);
                return;
            }

            try
            {
                RenameCustomCursorSetStructure(SelectedCustomCursorSet.Name, SelectedCustomCursorSetName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                Frontend.ShowMessageBox($"Failed to rename:\n{ex.Message}", MessageBoxImage.Error);
                return;
            }

            int idx = CustomCursorSets.IndexOf(SelectedCustomCursorSet);
            CustomCursorSets[idx] = new CustomCursorSet
            {
                Name = SelectedCustomCursorSetName,
                FolderPath = Path.Combine(Paths.CustomCursors, SelectedCustomCursorSetName)
            };

            SelectedCustomCursorSetIndex = idx;
            OnPropertyChanged(nameof(SelectedCustomCursorSetIndex));
        }

        private void ApplyCursorSet()
        {
            if (SelectedCustomCursorSet is null)
            {
                Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            string sourceDir = SelectedCustomCursorSet.FolderPath;
            string targetDir = Path.Combine(Paths.Mods, "content", "textures");
            string targetKeyboardMouse = Path.Combine(targetDir, "Cursors", "KeyboardMouse");

            try
            {
                if (!Directory.Exists(sourceDir))
                {
                    Frontend.ShowMessageBox("Selected cursor set folder does not exist.", MessageBoxImage.Error);
                    return;
                }

                Directory.CreateDirectory(targetDir);
                Directory.CreateDirectory(targetKeyboardMouse);

                var filesToDelete = new[]
                {
                    Path.Combine(targetDir, "MouseLockedCursor.png"),
                    Path.Combine(targetKeyboardMouse, "ArrowCursor.png"),
                    Path.Combine(targetKeyboardMouse, "ArrowFarCursor.png"),
                    Path.Combine(targetKeyboardMouse, "IBeamCursor.png")
                };

                foreach (var file in filesToDelete)
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }

                foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(sourceDir, file);
                    string destPath = Path.Combine(targetDir, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    File.Copy(file, destPath, overwrite: true);
                }

                Frontend.ShowMessageBox($"Cursor set '{SelectedCustomCursorSet.Name}' applied successfully!", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::ApplyCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to apply cursor set:\n{ex.Message}", MessageBoxImage.Error);
            }

            LoadCursorPathsForSelectedSet();

            OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
            OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
            OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
            OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
        }

        private void GetCurrentCursorSet()
        {
            if (SelectedCustomCursorSet is null)
            {
                Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            string sourceMouseLocked = Path.Combine(Paths.Mods, "content", "textures", "MouseLockedCursor.png");
            string sourceKeyboardMouse = Path.Combine(Paths.Mods, "content", "textures", "Cursors", "KeyboardMouse");

            string targetBase = SelectedCustomCursorSet.FolderPath;
            string targetMouseLocked = Path.Combine(targetBase, "MouseLockedCursor.png");
            string targetKeyboardMouse = Path.Combine(targetBase, "Cursors", "KeyboardMouse");

            try
            {
                Directory.CreateDirectory(targetBase);
                Directory.CreateDirectory(targetKeyboardMouse);

                var filesToDelete = new[]
                {
                    targetMouseLocked,
                    Path.Combine(targetKeyboardMouse, "ArrowCursor.png"),
                    Path.Combine(targetKeyboardMouse, "ArrowFarCursor.png"),
                    Path.Combine(targetKeyboardMouse, "IBeamCursor.png")
                };

                foreach (var file in filesToDelete)
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }

                if (File.Exists(sourceMouseLocked))
                    File.Copy(sourceMouseLocked, targetMouseLocked, overwrite: true);

                if (Directory.Exists(sourceKeyboardMouse))
                {
                    foreach (var fileName in new[] { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" })
                    {
                        string source = Path.Combine(sourceKeyboardMouse, fileName);
                        string dest = Path.Combine(targetKeyboardMouse, fileName);

                        if (File.Exists(source))
                            File.Copy(source, dest, overwrite: true);
                    }
                }

                Frontend.ShowMessageBox("Current cursor set copied into selected folder.", MessageBoxImage.Information);
                NotifyCursorVisibilities();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::GetCurrentCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to get current cursor set:\n{ex.Message}", MessageBoxImage.Error);
            }

            LoadCursorPathsForSelectedSet();
            NotifyCursorVisibilities();
        }

        private void ExportCursorSet()
        {
            if (SelectedCustomCursorSet is null)
                return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"{SelectedCustomCursorSet.Name}.zip",
                Filter = $"{Strings.FileTypes_ZipArchive}|*.zip"
            };

            if (dialog.ShowDialog() != true)
                return;

            string cursorDir = SelectedCustomCursorSet.FolderPath;

            try
            {
                using var memStream = new MemoryStream();
                using var zipStream = new ZipOutputStream(memStream);

                foreach (var filePath in Directory.EnumerateFiles(cursorDir, "*.*", SearchOption.AllDirectories))
                {
                    string relativePath = filePath[(cursorDir.Length + 1)..].Replace('\\', '/');

                    var entry = new ZipEntry(relativePath)
                    {
                        DateTime = DateTime.Now,
                        Size = new FileInfo(filePath).Length
                    };

                    zipStream.PutNextEntry(entry);

                    using var fileStream = File.OpenRead(filePath);
                    fileStream.CopyTo(zipStream);

                    zipStream.CloseEntry();
                }

                zipStream.Finish();
                memStream.Position = 0;

                using var outputStream = File.OpenWrite(dialog.FileName);
                memStream.CopyTo(outputStream);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::ExportCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to export cursor set:\n{ex.Message}", MessageBoxImage.Error);
                return;
            }

            Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
        }

        private void ImportCursorSet()
        {
            if (SelectedCustomCursorSet is null)
            {
                Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Cursor Set",
                Filter = $"{Strings.FileTypes_ZipArchive}|*.zip",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);

                ExtractZipToDirectory(dialog.FileName, tempPath);

                string mouseLockedDest = Path.Combine(SelectedCustomCursorSet.FolderPath, "MouseLockedCursor.png");
                string destKeyboardMouseFolder = Path.Combine(SelectedCustomCursorSet.FolderPath, "Cursors", "KeyboardMouse");

                if (File.Exists(mouseLockedDest))
                    File.Delete(mouseLockedDest);

                foreach (var fileName in new[] { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" })
                {
                    string filePath = Path.Combine(destKeyboardMouseFolder, fileName);
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }

                string? mouseLockedSource = Directory.GetFiles(tempPath, "MouseLockedCursor.png", SearchOption.AllDirectories).FirstOrDefault();

                if (mouseLockedSource != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(mouseLockedDest)!);
                    File.Copy(mouseLockedSource, mouseLockedDest, overwrite: true);
                }

                Directory.CreateDirectory(destKeyboardMouseFolder);

                foreach (var fileName in new[] { "ArrowCursor.png", "ArrowFarCursor.png", "IBeamCursor.png" })
                {
                    string? sourceFile = Directory.GetFiles(tempPath, fileName, SearchOption.AllDirectories).FirstOrDefault();
                    if (sourceFile != null)
                    {
                        string destFile = Path.Combine(destKeyboardMouseFolder, fileName);
                        File.Copy(sourceFile, destFile, overwrite: true);
                    }
                }

                Directory.Delete(tempPath, recursive: true);

                Frontend.ShowMessageBox("Cursor set imported successfully.", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ModsViewModel::ImportCursorSet", ex);
                Frontend.ShowMessageBox($"Failed to import cursor set:\n{ex.Message}", MessageBoxImage.Error);
            }

            LoadCursorPathsForSelectedSet();
        }

        private void ExtractZipToDirectory(string zipFilePath, string extractPath)
        {
            using var zipInputStream = new ZipInputStream(File.OpenRead(zipFilePath));

            ZipEntry? entry;
            while ((entry = zipInputStream.GetNextEntry()) != null)
            {
                if (entry.IsDirectory)
                    continue;

                string filePath = Path.Combine(extractPath, entry.Name);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                using var outputStream = File.Create(filePath);
                zipInputStream.CopyTo(outputStream);
            }
        }

        private string? GetCursorTargetPath(string fileName)
        {
            if (SelectedCustomCursorSet is null)
                return null;

            string dir = fileName == "MouseLockedCursor.png"
                ? SelectedCustomCursorSet.FolderPath
                : Path.Combine(SelectedCustomCursorSet.FolderPath, "Cursors", "KeyboardMouse");

            Directory.CreateDirectory(dir);
            return Path.Combine(dir, fileName);
        }

        private void DeleteCursorImage(string fileName)
        {
            string? destPath = GetCursorTargetPath(fileName);
            if (destPath is null || !File.Exists(destPath))
                return;

            try
            {
                File.Delete(destPath);

                UpdateCursorPathProperty(fileName, "");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"ModsViewModel::Delete{fileName}", ex);
                Frontend.ShowMessageBox($"Failed to delete {fileName}:\n{ex.Message}", MessageBoxImage.Error);
            }

            LoadCursorPathsForSelectedSet();
            NotifyCursorVisibilities();

            OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
            OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
            OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
            OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
        }

        private void AddShiftlockCursor()
        {
            AddCursorImage("MouseLockedCursor.png", "Select Shiftlock PNG");
            OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
            OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
            OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
            OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
        }

        private void AddCursorImage(string fileName, string dialogTitle)
        {
            if (SelectedCustomCursorSet is null)
            {
                Frontend.ShowMessageBox("Please select a cursor set first.", MessageBoxImage.Warning);
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = dialogTitle,
                Filter = "PNG files (*.png)|*.png",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            string? destPath = GetCursorTargetPath(fileName);
            if (destPath is null)
                return;

            try
            {
                if (File.Exists(destPath))
                    File.Delete(destPath);

                File.Copy(dialog.FileName, destPath);
                UpdateCursorPathAndPreview(fileName, dialog.FileName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException($"ModsViewModel::Add{fileName}", ex);
                Frontend.ShowMessageBox($"Failed to add {fileName}:\n{ex.Message}", MessageBoxImage.Error);
            }

            LoadCursorPathsForSelectedSet();
            NotifyCursorVisibilities();

            OnPropertyChanged(nameof(ChooseCustomShiftlockVisibility));
            OnPropertyChanged(nameof(DeleteCustomShiftlockVisibility));
            OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
            OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
        }
        #endregion

        #region Preview Images
        private void UpdateCursorPathProperty(string fileName, string path)
        {
            switch (fileName)
            {
                case "MouseLockedCursor.png":
                    ShiftlockCursorSelectedPath = path;
                    break;
                case "ArrowCursor.png":
                    ArrowCursorSelectedPath = path;
                    break;
                case "ArrowFarCursor.png":
                    ArrowFarCursorSelectedPath = path;
                    break;
                case "IBeamCursor.png":
                    IBeamCursorSelectedPath = path;
                    break;
            }
        }

        private void UpdateCursorPathAndPreview(string fileName, string fullPath)
        {
            if (!File.Exists(fullPath))
                fullPath = "";

            ImageSource? image = LoadImageSafely(fullPath);

            switch (fileName)
            {
                case "MouseLockedCursor.png":
                    ShiftlockCursorSelectedPath = fullPath;
                    ShiftlockCursorPreview = image;
                    App.Settings.Prop.ShiftlockCursorSelectedPath = fullPath;
                    break;

                case "ArrowCursor.png":
                    ArrowCursorSelectedPath = fullPath;
                    ArrowCursorPreview = image;
                    App.Settings.Prop.ArrowCursorSelectedPath = fullPath;
                    break;

                case "ArrowFarCursor.png":
                    ArrowFarCursorSelectedPath = fullPath;
                    ArrowFarCursorPreview = image;
                    App.Settings.Prop.ArrowFarCursorSelectedPath = fullPath;
                    break;

                case "IBeamCursor.png":
                    IBeamCursorSelectedPath = fullPath;
                    IBeamCursorPreview = image;
                    App.Settings.Prop.IBeamCursorSelectedPath = fullPath;
                    break;
            }

            App.Settings.Save();
        }

        private void LoadCursorPathsForSelectedSet()
        {
            if (SelectedCustomCursorSet == null)
            {
                UpdateCursorPathAndPreview("MouseLockedCursor.png", "");
                UpdateCursorPathAndPreview("ArrowCursor.png", "");
                UpdateCursorPathAndPreview("ArrowFarCursor.png", "");
                UpdateCursorPathAndPreview("IBeamCursor.png", "");
                return;
            }

            string baseDir = SelectedCustomCursorSet.FolderPath;
            string kbMouseDir = Path.Combine(baseDir, "Cursors", "KeyboardMouse");

            UpdateCursorPathAndPreview("MouseLockedCursor.png", Path.Combine(baseDir, "MouseLockedCursor.png"));
            UpdateCursorPathAndPreview("ArrowCursor.png", Path.Combine(kbMouseDir, "ArrowCursor.png"));
            UpdateCursorPathAndPreview("ArrowFarCursor.png", Path.Combine(kbMouseDir, "ArrowFarCursor.png"));
            UpdateCursorPathAndPreview("IBeamCursor.png", Path.Combine(kbMouseDir, "IBeamCursor.png"));
        }

        private string _shiftlockCursorSelectedPath = "";
        public string ShiftlockCursorSelectedPath
        {
            get => _shiftlockCursorSelectedPath;
            set
            {
                if (_shiftlockCursorSelectedPath != value)
                {
                    _shiftlockCursorSelectedPath = value;
                    OnPropertyChanged(nameof(ShiftlockCursorSelectedPath));
                }
            }
        }

        private string _arrowCursorSelectedPath = "";
        public string ArrowCursorSelectedPath
        {
            get => _arrowCursorSelectedPath;
            set
            {
                if (_arrowCursorSelectedPath != value)
                {
                    _arrowCursorSelectedPath = value;
                    OnPropertyChanged(nameof(ArrowCursorSelectedPath));
                }
            }
        }

        private string _arrowFarCursorSelectedPath = "";
        public string ArrowFarCursorSelectedPath
        {
            get => _arrowFarCursorSelectedPath;
            set
            {
                if (_arrowFarCursorSelectedPath != value)
                {
                    _arrowFarCursorSelectedPath = value;
                    OnPropertyChanged(nameof(ArrowFarCursorSelectedPath));
                }
            }
        }

        private string _iBeamCursorSelectedPath = "";
        public string IBeamCursorSelectedPath
        {
            get => _iBeamCursorSelectedPath;
            set
            {
                if (_iBeamCursorSelectedPath != value)
                {
                    _iBeamCursorSelectedPath = value;
                    OnPropertyChanged(nameof(IBeamCursorSelectedPath));
                }
            }
        }

        private ImageSource? _shiftlockCursorPreview;
        public ImageSource? ShiftlockCursorPreview
        {
            get => _shiftlockCursorPreview;
            set { _shiftlockCursorPreview = value; OnPropertyChanged(nameof(ShiftlockCursorPreview)); }
        }

        private ImageSource? _arrowCursorPreview;
        public ImageSource? ArrowCursorPreview
        {
            get => _arrowCursorPreview;
            set { _arrowCursorPreview = value; OnPropertyChanged(nameof(ArrowCursorPreview)); }
        }

        private ImageSource? _arrowFarCursorPreview;
        public ImageSource? ArrowFarCursorPreview
        {
            get => _arrowFarCursorPreview;
            set { _arrowFarCursorPreview = value; OnPropertyChanged(nameof(ArrowFarCursorPreview)); }
        }

        private ImageSource? _iBeamCursorPreview;
        public ImageSource? IBeamCursorPreview
        {
            get => _iBeamCursorPreview;
            set { _iBeamCursorPreview = value; OnPropertyChanged(nameof(IBeamCursorPreview)); }
        }

        private static BitmapImage LoadImageSafely(string path)
        {
            if (!File.Exists(path))
                return null!;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null!;
            }
        }
        #endregion

        #region Button Visibility
        public Visibility AddShiftlockCursorVisibility => GetCursorAddVisibility("MouseLockedCursor.png");
        public Visibility DeleteShiftlockCursorVisibility => GetCursorDeleteVisibility("MouseLockedCursor.png");
        public Visibility AddArrowCursorVisibility => GetCursorAddVisibility("ArrowCursor.png");
        public Visibility DeleteArrowCursorVisibility => GetCursorDeleteVisibility("ArrowCursor.png");
        public Visibility AddArrowFarCursorVisibility => GetCursorAddVisibility("ArrowFarCursor.png");
        public Visibility DeleteArrowFarCursorVisibility => GetCursorDeleteVisibility("ArrowFarCursor.png");
        public Visibility AddIBeamCursorVisibility => GetCursorAddVisibility("IBeamCursor.png");
        public Visibility DeleteIBeamCursorVisibility => GetCursorDeleteVisibility("IBeamCursor.png");

        private Visibility GetCursorAddVisibility(string fileName)
        {
            string? path = GetCursorTargetPath(fileName);
            return path is not null && File.Exists(path) ? Visibility.Collapsed : Visibility.Visible;
        }

        private Visibility GetCursorDeleteVisibility(string fileName)
        {
            string? path = GetCursorTargetPath(fileName);
            return path is not null && File.Exists(path) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NotifyCursorVisibilities()
        {
            OnPropertyChanged(nameof(AddShiftlockCursorVisibility));
            OnPropertyChanged(nameof(DeleteShiftlockCursorVisibility));
            OnPropertyChanged(nameof(AddArrowCursorVisibility));
            OnPropertyChanged(nameof(DeleteArrowCursorVisibility));
            OnPropertyChanged(nameof(AddArrowFarCursorVisibility));
            OnPropertyChanged(nameof(DeleteArrowFarCursorVisibility));
            OnPropertyChanged(nameof(AddIBeamCursorVisibility));
            OnPropertyChanged(nameof(DeleteIBeamCursorVisibility));
        }
        #endregion
    }
}