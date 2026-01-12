using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Voidstrap.Integrations;
using Voidstrap.Models;

namespace Voidstrap.UI.ViewModels.Settings
{
    public sealed class NvidiaFastFlagsViewModel : INotifyPropertyChanged
    {
        private static readonly string NipPath =
            Path.Combine(Paths.Base, "NipProfiles", "Voidstrap.nip");

        #region Collections

        public ObservableCollection<string> CplLowLatencyModes { get; } =
            new() { "Off", "On", "Ultra" };

        private static readonly Dictionary<string, int> BenchmarkOverlayMap = new()
        {
            { "Disabled", 0 },
            { "GRAPH_FLIP_FPS - FPS graph, measured on display hw flip", 1 },
            { "GRAPH_PRESENT_FPS - FPS graph, measured when the user mode driver starts processing present", 2 },
            { "GRAPH_APP_PRESENT_FPS - FPS graph, measured on app present", 4 },
            { "DISPLAY_PAGING - Add red paging indicator bars to the GRAPH_PRESENT_FPS graph", 8 },
            { "DISPLAY_APP_THREAD_WAIT - Add app thread wait time indiator bars to the GRAPH_APP_PRESENT_FPS graph", 16 },
            { "Enabled - Enable everything", 511 }
        };

        public ObservableCollection<string> BenchMarkOverlayModes { get; } =
            new(BenchmarkOverlayMap.Keys);

        public ObservableCollection<string> FrlLowLatencyModes { get; } =
            new() { "Off", "On" };

        public ObservableCollection<string> SilkSmoothnessModes { get; } =
            new() { "Off", "Low", "Medium", "High", "Ultra" };

        #endregion

        #region Backing Fields

        private string _cplLatency = "Off";
        private string _benchmarkOverlay = "Disabled";
        private string _frlLatency = "Off";
        private string _selectedSilkSmoothness = "Off";

        private bool _rbar;
        private bool _gamma;
        private bool _dlssSR;
        private bool _dlssFG;
        private bool _mfaa;
        private bool _fxaa;

        private int _textureLodBias;
        private int _frameRateLimit;
        private int _backgroundFrameRateLimit;

        private Dictionary<string, string> _originalValues = new();

        #endregion

        #region Properties

        public string SelectedCplLowLatencyMode
        {
            get => _cplLatency;
            set => Set(ref _cplLatency, value);
        }

        public string BenchMarkOverlayMode
        {
            get => _benchmarkOverlay;
            set => Set(ref _benchmarkOverlay, value);
        }

        public string SelectedFrlLowLatencyMode
        {
            get => _frlLatency;
            set => Set(ref _frlLatency, value);
        }

        public string SelectedSilkSmoothness
        {
            get => _selectedSilkSmoothness;
            set => Set(ref _selectedSilkSmoothness, value);
        }

        public bool EnableRbar
        {
            get => _rbar;
            set => Set(ref _rbar, value);
        }

        public bool EnableGamma
        {
            get => _gamma;
            set => Set(ref _gamma, value);
        }

        public bool EnableDlssSuperResolution
        {
            get => _dlssSR;
            set => Set(ref _dlssSR, value);
        }

        public bool EnableDlssFrameGen
        {
            get => _dlssFG;
            set => Set(ref _dlssFG, value);
        }

        public bool EnableMFAA
        {
            get => _mfaa;
            set => Set(ref _mfaa, value);
        }
        public bool EnableFXAA
        {
            get => _fxaa;
            set => Set(ref _fxaa, value);
        }

        public int FrameRateLimit
        {
            get => _frameRateLimit;
            set => Set(ref _frameRateLimit, Math.Clamp(value, 0, 1000));
        }

        public int BackgroundFrameRateLimit
        {
            get => _backgroundFrameRateLimit;
            set => Set(ref _backgroundFrameRateLimit, Math.Clamp(value, 0, 1000));
        }

        public int TextureLodBias
        {
            get => _textureLodBias;
            set
            {
                Set(ref _textureLodBias, Math.Clamp(value, 0, 120));
                OnPropertyChanged(nameof(TextureLodBiasLabel));
            }
        }

        public string TextureLodBiasLabel =>
            TextureLodBias == 0
                ? "Default (Driver Controlled)"
                : $"LOD Bias Override: {TextureLodBias}";

        #endregion

        #region Constructor

        public NvidiaFastFlagsViewModel()
        {
            EnsureNipExists();
            Load();
        }

        #endregion

        #region Load

        private void Load()
        {
            var entries = NvidiaProfileManager.LoadFromNip(NipPath);
            entries = RemoveDuplicateSettingIds(entries);

            SelectedCplLowLatencyMode = ReadEnum(entries, "390467", CplLowLatencyModes, 0);
            BenchMarkOverlayMode = BenchmarkOverlayMap
                .FirstOrDefault(x => x.Value == ReadInt(entries, "2945366", 0))
                .Key ?? "Disabled";

            SelectedFrlLowLatencyMode = ReadEnum(entries, "277041152", FrlLowLatencyModes, 0);
            SelectedSilkSmoothness = ReadSilk(entries);

            EnableRbar = ReadBool(entries, "549198379");
            EnableDlssSuperResolution = ReadBool(entries, "283385345");
            EnableDlssFrameGen = ReadBool(entries, "283385347");

            FrameRateLimit = ReadInt(entries, "277041154", 0);
            BackgroundFrameRateLimit = ReadInt(entries, "277041157", 0);

            EnableMFAA = ReadBool(entries, "10011052");
            EnableFXAA = (ReadBool(entries, "276089202") && ReadBool(entries, "276757595"));
            EnableGamma = !(ReadBool(entries, "276652957") && ReadBool(entries, "545898348"));

            TextureLodBias = ReadInt(entries, "7573135", 0);
            _originalValues = entries.ToDictionary(x => x.SettingId, x => x.Value);
        }

        #endregion

        #region Apply

        public async Task Apply()
        {
            var entries = NvidiaProfileManager.LoadFromNip(NipPath);
            entries = RemoveDuplicateSettingIds(entries);

            ApplyIfChanged(entries, "Frame Rate Limiter", "277041154", FrameRateLimit.ToString());
            ApplyIfChanged(entries, "Background Application Max Frame Rate", "277041157", BackgroundFrameRateLimit.ToString());

            ApplyIfChanged(entries, "CPL Low Latency Mode", "390467", CplLowLatencyModes.IndexOf(SelectedCplLowLatencyMode).ToString());
            ApplyIfChanged(entries, "Benchmark Overlay", "2945366", BenchmarkOverlayMap.TryGetValue(BenchMarkOverlayMode, out var v) ? v.ToString() : "0");
            ApplyIfChanged(entries, "FRL Low Latency Mode", "277041152", FrlLowLatencyModes.IndexOf(SelectedFrlLowLatencyMode).ToString());
            ApplyIfChanged(entries, "SILK Smoothness", "9990737", SilkToValue(SelectedSilkSmoothness));

            ApplyIfChanged(entries, "Resizable BAR", "549198379", EnableRbar ? "1" : "0");
            ApplyIfChanged(entries, "Enable DLSS-SR override", "283385345", EnableDlssSuperResolution ? "1" : "0");
            ApplyIfChanged(entries, "Enable DLSS-FG override", "283385347", EnableDlssFrameGen ? "1" : "0");

            var gammaValue = EnableGamma ? "0" : "1";
            ApplyIfChanged(entries, "Gamma correction", "276652957", gammaValue);
            ApplyIfChanged(entries, "Line gamma", "545898348", gammaValue);

            var FXAAValue = EnableFXAA ? "1" : "0";
            ApplyIfChanged(entries, "Enable FXAA", "276089202", FXAAValue);
            ApplyIfChanged(entries, "Antialiasing - Mode", "276757595", FXAAValue);

            ApplyIfChanged(entries, "MFAA", "10011052", EnableMFAA ? "1" : "0");

            ApplyIfChanged(entries, "Texture filtering - LOD Bias", "7573135", TextureLodBias.ToString());
            if (TextureLodBias > 0)
            {
                ApplyIfChanged(entries, "Texture filtering - Quality", "13510289", "20");
                ApplyIfChanged(entries, "Anisotropic filtering mode", "282245910", "1");
                ApplyIfChanged(entries, "Antialiasing - Transparency Supersampling", "282364549", "8");
            }

            NvidiaProfileManager.SaveToNip(NipPath, entries);
            _originalValues = entries.ToDictionary(x => x.SettingId, x => x.Value);
            await NvidiaProfileManager.ApplyNipFile(NipPath);
        }

        #endregion

        #region Helpers

        private void ApplyIfChanged(List<NvidiaEditorEntry> entries, string name, string id, string newValue)
        {
            _originalValues.TryGetValue(id, out var oldValue);

            if (oldValue == newValue)
                return;

            if (newValue == "0")
            {
                entries.RemoveAll(x => x.SettingId == id);
                return;
            }

            var entry = entries.FirstOrDefault(x => x.SettingId == id);
            if (entry != null)
                entry.Value = newValue;
            else
                entries.Add(new NvidiaEditorEntry
                {
                    Name = name,
                    SettingId = id,
                    Value = newValue,
                    ValueType = "Dword"
                });
        }

        private static bool ReadBool(List<NvidiaEditorEntry> e, string id)
            => e.FirstOrDefault(x => x.SettingId == id)?.Value == "1";

        private static int ReadInt(List<NvidiaEditorEntry> e, string id, int d)
            => int.TryParse(e.FirstOrDefault(x => x.SettingId == id)?.Value, out var v) ? v : d;

        private static string ReadEnum(List<NvidiaEditorEntry> e, string id, IList<string> v, int d)
            => int.TryParse(e.FirstOrDefault(x => x.SettingId == id)?.Value, out var i) && i < v.Count ? v[i] : v[d];

        private static string ReadSilk(List<NvidiaEditorEntry> e) =>
            e.FirstOrDefault(x => x.SettingId == "9990737")?.Value switch
            {
                "1" => "Low",
                "2" => "Medium",
                "3" => "High",
                "4" => "Ultra",
                _ => "Off"
            };

        private static string SilkToValue(string mode) => mode switch
        {
            "Low" => "1",
            "Medium" => "2",
            "High" => "3",
            "Ultra" => "4",
            _ => "0"
        };

        private static void EnsureNipExists()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(NipPath)!);
            if (!File.Exists(NipPath))
                File.WriteAllText(NipPath, NvidiaProfileManager.EmptyNipTemplate());
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return;
            field = value;
            OnPropertyChanged(name);
        }

        private static List<NvidiaEditorEntry> RemoveDuplicateSettingIds(List<NvidiaEditorEntry> entries)
        {
            return entries
                .GroupBy(x => x.SettingId)
                .Select(g => g.First())
                .ToList();
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion
    }
}