using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Voidstrap.Enums.FlagPresets;
using SharpDX.DXGI;

public static class SystemInfo
{
    // Define the SYSTEM_INFO structure
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_INFO
    {
        public ushort wProcessorArchitecture;
        public ushort wReserved;
        public uint dwPageSize;
        public IntPtr lpMinimumApplicationAddress;
        public IntPtr lpMaximumApplicationAddress;
        public IntPtr dwActiveProcessorMask;
        public uint dwNumberOfProcessors; // This field contains the number of logical processors
        public uint dwProcessorType;
        public uint dwAllocationGranularity;
        public ushort wProcessorLevel;
        public ushort wProcessorRevision;
    }

    // Import the GetSystemInfo function from kernel32.dll
    [DllImport("kernel32.dll")]
    private static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    // Method to get the number of logical processors
    public static int GetLogicalProcessorCount()
    {
        // Call the Windows API to get system information
        GetSystemInfo(out SYSTEM_INFO systemInfo);

        // Return the number of logical processors
        return (int)systemInfo.dwNumberOfProcessors;
    }
}

namespace Voidstrap.UI.ViewModels.Settings
{
    public class FastFlagsViewModel : NotifyPropertyChangedViewModel
    {
        private Dictionary<string, object>? _preResetFlags;

        public event EventHandler? RequestPageReloadEvent;
        public event EventHandler? OpenFlagEditorEvent;

        private void OpenFastFlagEditor() => OpenFlagEditorEvent?.Invoke(this, EventArgs.Empty);

        public ICommand OpenFastFlagEditorCommand => new RelayCommand(OpenFastFlagEditor);

        public const string Enabled = "True";
        public const string Disabled = "False";

        public bool DisableTelemetry
        {
            get => App.FastFlags?.GetPreset("Telemetry.EpCounter") == "True";
            set
            {
                if (App.FastFlags == null) return;

                App.FastFlags.SetPreset("Telemetry.EpCounter", value ? Enabled : Disabled);

                var telemetryPresets = new Dictionary<string, string>
                {
                    { "Telemetry.EpCounter", value ? Enabled : Disabled },
                    { "Telemetry.EpStats", value ? Enabled : Disabled },
                    { "Telemetry.Event", value ? Enabled : Disabled },
                    { "Telemetry.Point", value ? Enabled : Disabled },
                    { "Telemetry.GraphicsQualityUsage", value ? Disabled : Enabled },
                    { "Telemetry.GpuVsCpuBound", value ? Disabled : Enabled },
                    { "Telemetry.RenderFidelity", value ? Disabled : Enabled },
                    { "Telemetry.RenderDistance", value ? Disabled : Enabled },
                    { "Telemetry.PhysicsSolverPerf", value ? Disabled : Enabled },
                    { "Telemetry.BadMoverConstraint", value ? Disabled : Enabled },
                    { "Telemetry.AudioPlugin", value ? Disabled : Enabled },
                    { "Telemetry.FmodErrors", value ? Disabled : Enabled },
                    { "Telemetry.SoundLength", value ? Disabled : Enabled },
                    { "Telemetry.AssetRequestV1", value ? Disabled : Enabled },
                    { "Telemetry.SeparateEventPoints", value ? Disabled : Enabled },
                    { "Telemetry.DeviceRAM", value ? Disabled : Enabled },
                    { "Telemetry.TelemetryFlush", value ? Disabled : Enabled },
                    { "Telemetry.V2FrameRateMetrics", value ? Disabled : Enabled },
                    { "Telemetry.GlobalSkipUpdating", value ? Disabled : Enabled },
                    { "Telemetry.CallbackSafety", value ? Disabled : Enabled },
                    { "Telemetry.V2PointEncoding", value ? Disabled : Enabled },
                    { "Telemetry.ReplaceSeparator", value ? Disabled : Enabled }
                };

                foreach (var (key, presetValue) in telemetryPresets)
                {
                    App.FastFlags.SetPreset(key, presetValue);
                }
            }
        }

        public bool ChatBubble
        {
            get => !string.Equals(App.FastFlags.GetPreset("UI.Chatbubble"), "True", StringComparison.OrdinalIgnoreCase);
            set => App.FastFlags.SetPreset("UI.Chatbubble", value ? "False" : "True");
        }

        public bool GoogleToggle
        {
            get => string.Equals(App.FastFlags.GetPreset("VoiceChat.VoiceChat1"), "False", StringComparison.OrdinalIgnoreCase);
            set
            {
                if (value)
                {
                    App.FastFlags.SetPreset("VoiceChat.VoiceChat1", "False");
                    App.FastFlags.SetPreset("VoiceChat.VoiceChat2", "https://google.com");
                    App.FastFlags.SetPreset("VoiceChat.VoiceChat3", "https://google.com");
                }
                else
                {
                    App.FastFlags.SetPreset("VoiceChat.VoiceChat1", "True");
                    App.FastFlags.SetPreset("VoiceChat.VoiceChat2", null);
                    App.FastFlags.SetPreset("VoiceChat.VoiceChat3", null);
                }
            }
        }

        public bool LightCulling
        {
            get => App.FastFlags.GetPreset("Rendering.GpuCulling") == "True";
            set
            {
                App.FastFlags.SetPreset("Rendering.GpuCulling", value ? "True" : null);
                App.FastFlags.SetPreset("Rendering.CpuCulling", value ? "True" : null);
            }
        }

        public bool RainbowTheme
        {
            get => App.FastFlags.GetPreset("UI.RainbowText") == "True";
            set => App.FastFlags.SetPreset("UI.RainbowText", value ? "True" : null);
        }

        public bool MemoryProbing
        {
            get => App.FastFlags.GetPreset("Memory.Probe") == "True";
            set => App.FastFlags.SetPreset("Memory.Probe", value ? "True" : null);
        }

        public bool Layered
        {
            get => App.FastFlags.GetPreset("Layered.Clothing") == "-1";
            set => App.FastFlags.SetPreset("Layered.Clothing", value ? "-1" : null);
        }

        public bool UnlimitedCameraZoom
        {
            get => App.FastFlags.GetPreset("Rendering.Camerazoom") == "2147483647";
            set => App.FastFlags.SetPreset("Rendering.Camerazoom", value ? "2147483647" : null);
        }

        public bool FpsFix
        {
            get => App.FastFlags.GetPreset("FpsFix.Log")?.Equals("False") ?? false;
            set => App.FastFlags.SetPreset("FpsFix.Log", value ? "False" : null);
        }

        public bool Preload
        {
            get => App.FastFlags.GetPreset("Preload.Preload2") == "True";
            set
            {
                App.FastFlags.SetPreset("Preload.Preload2", value ? "True" : null);
                App.FastFlags.SetPreset("Preload.SoundPreload", value ? "True" : null);
                App.FastFlags.SetPreset("Preload.Texture", value ? "True" : null);
                App.FastFlags.SetPreset("Preload.TeleportPreload", value ? "True" : null);
                App.FastFlags.SetPreset("Preload.FontsPreload", value ? "True" : null);
                App.FastFlags.SetPreset("Preload.ItemPreload", value ? "True" : null);
                App.FastFlags.SetPreset("Preload.Teleport2", value ? "True" : null);
            }
        }

        public bool LoadFaster
        {
            get => App.FastFlags.GetPreset("Network.AssetPreloadding") == "2147483647";
            set
            {
                App.FastFlags.SetPreset("Network.AssetPreloadding", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.MeshPreloadding", value ? "False" : null);
                App.FastFlags.SetPreset("Network.MaxAssetPreload", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.PlayerImageDefault", value ? "1" : null);
            }
        }

        public bool Threading
        {
            get => App.FastFlags.GetPreset("Hyper.Threading1") == "True";
            set
            {
                App.FastFlags.SetPreset("Hyper.Threading1", value ? "True" : null);
                App.FastFlags.SetPreset("Hyper.Threading2", value ? "True" : null);
                App.FastFlags.SetPreset("Hyper.Threading3", value ? "2400" : null);
                App.FastFlags.SetPreset("Hyper.Threading4", value ? "0" : null);
            }
        }

        public bool LessLagSpikes
        {
            get => App.FastFlags.GetPreset("Network.DefaultBps") == "64000";
            set
            {
                App.FastFlags.SetPreset("Network.DefaultBps", value ? "64000" : null);
                App.FastFlags.SetPreset("Network.MaxWorkCatchupMs", value ? "20" : null);
            }
        }

        public bool EnableNextGenReplicator
        {
            get => App.FastFlags.GetPreset("Network.NextGenReplicatorWrite") == "True";
            set
            {
                App.FastFlags.SetPreset("Network.NextGenReplicatorWrite", value ? "True" : null);
                App.FastFlags.SetPreset("Network.NextGenReplicatorRead", value ? "True" : null);
                App.FastFlags.SetPreset("Network.NextGenReplicatorBetterPacket", value ? "True" : null);
            }
        }

        public bool EnableLargeReplicator
        {
            get => App.FastFlags.GetPreset("Network.EnableLargeReplicator") == "True";
            set
            {
                App.FastFlags.SetPreset("Network.EnableLargeReplicator", value ? "True" : null);
                App.FastFlags.SetPreset("Network.LargeReplicatorWrite", value ? "True" : null);
                App.FastFlags.SetPreset("Network.LargeReplicatorRead", value ? "True" : null);
            }
        }

        public bool PingBreakdown
        {
            get => App.FastFlags.GetPreset("Debug.PingBreakdown") == "True";
            set => App.FastFlags.SetPreset("Debug.PingBreakdown", value ? "True" : null);
        }

        public bool EnableDarkMode
        {
            get => App.FastFlags.GetPreset("DarkMode.BlueMode") == "False";
            set => App.FastFlags.SetPreset("DarkMode.BlueMode", value ? "False" : null);
        }

        public bool DisplayFps
        {
            get => App.FastFlags.GetPreset("Rendering.DisplayFps") == "True";
            set => App.FastFlags.SetPreset("Rendering.DisplayFps", value ? "True" : null);
        }

        public bool UseFastFlagManager
        {
            get => App.Settings.Prop.UseFastFlagManager;
            set => App.Settings.Prop.UseFastFlagManager = value;
        }

        public int FramerateLimit
        {
            get => int.TryParse(App.FastFlags.GetPreset("Rendering.Framerate"), out int x) ? x : 0;
            set => App.FastFlags.SetPreset("Rendering.Framerate", value == 0 ? null : value);
        }

        public int VolChatLimit
        {
            get => int.TryParse(App.FastFlags.GetPreset("VoiceChat.VoiceChat4"), out int x) ? x : 1000;
            set => App.FastFlags.SetPreset("VoiceChat.VoiceChat4", value > 0 ? value.ToString() : null);
        }

        public int MtuSize
        {
            get => int.TryParse(App.FastFlags.GetPreset("Network.Mtusize"), out int x) ? x : 0;
            set => App.FastFlags.SetPreset("Network.Mtusize", value > 0 ? value.ToString() : null);
        }

        public int DynamicRenderResolution
        {
            get => int.TryParse(App.FastFlags.GetPreset("Rendering.Pixel"), out int x) ? x : 0;
            set => App.FastFlags.SetPreset("Rendering.Pixel", value == 0 ? null : value);
        }

        public IReadOnlyDictionary<MSAAMode, string?> MSAALevels => FastFlagManager.MSAAModes;

        public MSAAMode SelectedMSAALevel
        {
            get => MSAALevels.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.MSAA")).Key;
            set => App.FastFlags.SetPreset("Rendering.MSAA", MSAALevels[value]);
        }

        public IReadOnlyDictionary<TextureQuality, string?> TextureQualities => FastFlagManager.TextureQualityLevels;

        public TextureQuality SelectedTextureQuality
        {
            get => TextureQualities.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.TextureQuality.Level")).Key;
            set
            {
                if (value == TextureQuality.Default)
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality.OverrideEnabled", "True");
                    App.FastFlags.SetPreset("Rendering.TextureQuality.Level", TextureQualities[value]);
                }
            }
        }

        public IReadOnlyDictionary<RenderingMode, string> RenderingModes => FastFlagManager.RenderingModes;

        public RenderingMode SelectedRenderingMode
        {
            get => App.FastFlags.GetPresetEnum(RenderingModes, "Rendering.Mode", "True");
            set
            {
                RenderingMode[] DisableD3D11 = new RenderingMode[]
                {
                    RenderingMode.Vulkan,
                    RenderingMode.OpenGL
                };

                App.FastFlags.SetPresetEnum("Rendering.Mode", value.ToString(), "True");
                App.FastFlags.SetPreset("Rendering.Mode.DisableD3D11", DisableD3D11.Contains(value) ? "True" : null);
            }
        }

        public bool FixDisplayScaling
        {
            get => App.FastFlags.GetPreset("Rendering.DisableScaling") == "True";
            set => App.FastFlags.SetPreset("Rendering.DisableScaling", value ? "True" : null);
        }

        public bool MoreLighting
        {
            get => App.FastFlags.GetPreset("Rendering.BrighterVisual") == "True";
            set => App.FastFlags.SetPreset("Rendering.BrighterVisual", value ? "True" : null);
        }

        public bool RemoveGrass
        {
            get => App.FastFlags.GetPreset("Rendering.Nograss1") == "0";
            set
            {
                App.FastFlags.SetPreset("Rendering.Nograss1", value ? "0" : null);
                App.FastFlags.SetPreset("Rendering.Nograss2", value ? "0" : null);
                App.FastFlags.SetPreset("Rendering.Nograss3", value ? "0" : null);
            }
        }

        public string? FlagState
        {
            get => App.FastFlags.GetPreset("Debug.FlagState");
            set => App.FastFlags.SetPreset("Debug.FlagState", value);
        }

        public IReadOnlyDictionary<InGameMenuVersion, Dictionary<string, string?>> IGMenuVersions => FastFlagManager.IGMenuVersions;

        public InGameMenuVersion SelectedIGMenuVersion
        {
            get
            {
                foreach (var version in IGMenuVersions)
                {
                    bool flagsMatch = true;

                    foreach (var flag in version.Value)
                    {
                        foreach (var presetFlag in FastFlagManager.PresetFlags.Where(x => x.Key.StartsWith($"UI.Menu.Style.{flag.Key}")))
                        {
                            if (App.FastFlags.GetValue(presetFlag.Value) != flag.Value)
                                flagsMatch = false;
                        }
                    }

                    if (flagsMatch)
                        return version.Key;
                }

                return IGMenuVersions.First().Key;
            }
            set
            {
                foreach (var flag in IGMenuVersions[value])
                    App.FastFlags.SetPreset($"UI.Menu.Style.{flag.Key}", flag.Value);
            }
        }

        public IReadOnlyDictionary<LightingMode, string> LightingModes => FastFlagManager.LightingModes;

        public LightingMode SelectedLightingMode
        {
            get => App.FastFlags.GetPresetEnum(LightingModes, "Rendering.Lighting", "True");
            set => App.FastFlags.SetPresetEnum("Rendering.Lighting", LightingModes[value], "True");
        }

        public bool FullscreenTitlebarDisabled
        {
            get => int.TryParse(App.FastFlags.GetPreset("UI.FullscreenTitlebarDelay"), out int x) && x > 5000;
            set => App.FastFlags.SetPreset("UI.FullscreenTitlebarDelay", value ? "3600000" : null);
        }

        public int GuiHidingId
        {
            get => int.TryParse(App.FastFlags.GetPreset("UI.Hide"), out int x) ? x : 0;
            set
            {
                App.FastFlags.SetPreset("UI.Hide", value == 0 ? null : value);
                App.FastFlags.SetPreset("UI.Hide.Toggles", value != 0 ? "True" : null);
            }
        }

        public IReadOnlyDictionary<TextureSkipping, string?> TextureSkippings => FastFlagManager.TextureSkippingSkips;

        public TextureSkipping SelectedTextureSkipping
        {
            get => TextureSkippings.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.TextureSkipping.Skips")).Key;
            set
            {
                if (value == TextureSkipping.Noskip)
                {
                    App.FastFlags.SetPreset("Rendering.TextureSkipping", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.TextureSkipping.Skips", TextureSkippings[value]);
                }
            }
        }

        public IReadOnlyDictionary<DistanceRendering, string?> DistanceRenderings => FastFlagManager.DistanceRenderings;

        public DistanceRendering SelectedDistanceRendering
        {
            get => DistanceRenderings.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.Distance.Chunks")).Key;
            set
            {
                if (value == DistanceRendering.Chunks1x)
                {
                    App.FastFlags.SetPreset("Rendering.Distance.Chunks", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.Distance.Chunks", DistanceRenderings[value]);
                }
            }
        }

        public IReadOnlyDictionary<RomarkStart, string?> RomarkStartMappings => FastFlagManager.RomarkStartMappings;

        public RomarkStart SelectedRomarkStart
        {
            get => FastFlagManager.RomarkStartMappings.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.Start.Graphic")).Key;
            set
            {
                if (value == RomarkStart.Disabled)
                {
                    App.FastFlags.SetPreset("Rendering.Start.Graphic", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.Start.Graphic", FastFlagManager.RomarkStartMappings[value]);
                }
            }
        }

        public IReadOnlyDictionary<QualityLevel, string?> QualityLevels => FastFlagManager.QualityLevels;

        public QualityLevel SelectedQualityLevel
        {
            get => FastFlagManager.QualityLevels.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.FrmQuality")).Key;
            set
            {
                if (value == QualityLevel.Disabled)
                {
                    App.FastFlags.SetPreset("Rendering.FrmQuality", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.FrmQuality", FastFlagManager.QualityLevels[value]);
                }
            }
        }

        public bool DisablePostFX
        {
            get => App.FastFlags.GetPreset("Rendering.DisablePostFX") == "True";
            set => App.FastFlags.SetPreset("Rendering.DisablePostFX", value ? "True" : null);
        }
        public bool AdsToggle
        {
            get => App.FastFlags.GetPreset("UI.Disable.Ads") == "True";
            set => App.FastFlags.SetPreset("UI.Disable.Ads", value ? "True" : null);
        }

        public bool DisablePlayerShadows
        {
            get => App.FastFlags.GetPreset("Rendering.ShadowIntensity") == "True";
            set => App.FastFlags.SetPreset("Rendering.ShadowIntensity", value ? "True" : null);
        }
        public bool EnableGraySky
        {
            get => App.FastFlags.GetPreset("Rendering.GraySky") == "True";
            set => App.FastFlags.SetPreset("Rendering.GraySky", value ? "True" : null);
        }
        public int? FontSize
        {
            get => int.TryParse(App.FastFlags.GetPreset("UI.FontSize"), out int x) ? x : 1;
            set => App.FastFlags.SetPreset("UI.FontSize", value == 1 ? null : value);
        }

        public bool RedFont
        {
            get => App.FastFlags.GetPreset("UI.RedFont") == "rbxasset://fonts/families/BuilderSans.json";
            set => App.FastFlags.SetPreset("UI.RedFont", value ? "rbxasset://fonts/families/BuilderSans.json" : null);
        }

        public bool DisableTerrainTextures
        {
            get => App.FastFlags.GetPreset("Rendering.TerrainTextureQuality") == "0";
            set
            {
                App.FastFlags.SetPreset("Rendering.TerrainTextureQuality", value ? "0" : null);
            }
        }

        public bool Prerender
        {
            get => App.FastFlags.GetPreset("Rendering.Prerender") == "True" && App.FastFlags.GetPreset("Rendering.PrerenderV2") == "True";
            set
            {
                App.FastFlags.SetPreset("Rendering.Prerender", value ? "True" : null);
                App.FastFlags.SetPreset("Rendering.PrerenderV2", value ? "True" : null);
            }
        }

        public IReadOnlyDictionary<string, string?>? GPUs => GetGPUs();

        public string SelectedGPU
        {
            get => App.FastFlags.GetPreset("Rendering.PreferredGPU") ?? "Automatic";
            set => App.FastFlags.SetPreset("Rendering.PreferredGPU", value == "Automatic" ? null : value);
        }

        public bool PartyToggle
        {
            get => App.FastFlags.GetPreset("VoiceChat.VoiceChat4") == "False";
            set
            {
                string presetValue = value ? "False" : "True";
                App.FastFlags.SetPreset("VoiceChat.VoiceChat4", presetValue);
                App.FastFlags.SetPreset("VoiceChat.VoiceChat5", presetValue);
            }
        }

        public bool GetFlagAsBool(string flagKey, string falseValue = "False")
        {
            return App.FastFlags.GetPreset(flagKey) != falseValue;
        }

        public void SetFlagFromBool(string flagKey, bool value, string falseValue = "False")
        {
            App.FastFlags.SetPreset(flagKey, value ? null : falseValue);
        }

        public bool ChromeUI
        {
            get => GetFlagAsBool("UI.Menu.ChromeUI");
            set => SetFlagFromBool("UI.Menu.ChromeUI", value);
        }

        public bool VRToggle
        {
            get => GetFlagAsBool("Menu.VRToggles");
            set => SetFlagFromBool("Menu.VRToggles", value);
        }

        public bool SoothsayerCheck
        {
            get => GetFlagAsBool("Menu.Feedback");
            set => SetFlagFromBool("Menu.Feedback", value);
        }

        public bool LanguageSelector
        {
            get => App.FastFlags.GetPreset("Menu.LanguageSelector") != "0";
            set => SetFlagFromBool("Menu.LanguageSelector", value, "0");
        }

        public bool Haptics
        {
            get => GetFlagAsBool("Menu.Haptics");
            set => SetFlagFromBool("Menu.Haptics", value);
        }

        public bool Framerate
        {
            get => GetFlagAsBool("Menu.Framerate");
            set => SetFlagFromBool("Menu.Framerate", value);
        }

        public bool ChatTranslation
        {
            get => GetFlagAsBool("Menu.ChatTranslation");
            set => SetFlagFromBool("Menu.ChatTranslation", value);
        }

        public bool ResetConfiguration
        {
            get => _preResetFlags is not null;
            set
            {
                if (value)
                {
                    _preResetFlags = new(App.FastFlags.Prop);
                    App.FastFlags.Prop.Clear();
                }
                else
                {
                    App.FastFlags.Prop = _preResetFlags!;
                    _preResetFlags = null;
                }

                RequestPageReloadEvent?.Invoke(this, EventArgs.Empty);
            }
        }

        public static IReadOnlyDictionary<string, string?> GetGPUs()
        {
            const string LOG_IDENT = "FFlagPresets::GetGPUs";
            Dictionary<string, string?> GPUs = new();

            GPUs.Add("Automatic", null);

            try
            {
                using (var factory = new Factory1())
                {
                    for (int i = 0; i < factory.GetAdapterCount1(); i++)
                    {
                        var GPU = factory.GetAdapter1(i);
                        var Name = GPU.Description;
                        GPUs.Add(Name.Description, Name.Description);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to get GPU names: {ex.Message}");
            }

            return GPUs;
        }

        public static IReadOnlyDictionary<string, string?> GetCpuThreads()
        {
            const string LOG_IDENT = "FFlagPresets::GetCpuThreads";
            Dictionary<string, string?> cpuThreads = new();

            // Add the "Automatic" option
            cpuThreads.Add("Automatic", null);

            try
            {
                // Get the number of logical processors
                int logicalProcessorCount = SystemInfo.GetLogicalProcessorCount();

                // Add options for 1, 2, 3, ..., up to the number of logical processors
                for (int i = 1; i <= logicalProcessorCount; i++)
                {
                    cpuThreads.Add(i.ToString(), i.ToString());
                }
            }
            catch (Exception ex)
            {
                // Log the error if something goes wrong
                App.Logger.WriteLine(LOG_IDENT, $"Failed to get CPU thread count: {ex.Message}");
            }

            return cpuThreads;
        }

        public IReadOnlyDictionary<string, string?>? CpuThreads => GetCpuThreads();

        private KeyValuePair<string, string?> _selectedCpuThreads;
        public KeyValuePair<string, string?> SelectedCpuThreads
        {
            get
            {
                string currentValue = App.FastFlags.GetPreset("Rendering.CpuThreads") ?? "Automatic";
                return CpuThreads?.FirstOrDefault(kvp => kvp.Key == currentValue) ?? default;
            }
            set
            {
                App.FastFlags.SetPreset("Rendering.CpuThreads", value.Value);
                OnPropertyChanged(nameof(SelectedCpuThreads));
            }
        }

        // INotifyPropertyChanged implementation
        public new event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (!Equals(field, newValue))
            {
                field = newValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                return true;
            }

            return false;
        }

        private System.Collections.IEnumerable? profileModes;

        public System.Collections.IEnumerable? ProfileModes { get => profileModes; set => SetProperty(ref profileModes, value); }

        private string selectedProfileMods;

        public string SelectedProfileMods { get => selectedProfileMods; set => SetProperty(ref selectedProfileMods, value); }
    }
}