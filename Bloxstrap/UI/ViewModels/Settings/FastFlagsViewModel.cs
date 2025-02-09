using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Hellstrap.Enums.FlagPresets;
using Hellstrap.UI.Elements.Settings.Pages;
using Wpf.Ui.Mvvm.Contracts;

namespace Hellstrap.UI.ViewModels.Settings
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
            { "Telemetry.V2Counter", value ? Enabled : Disabled },
            { "Telemetry.V2Event", value ? Enabled : Disabled },
            { "Telemetry.V2Stats", value ? Enabled : Disabled },
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





        public bool EnableDarkMode
        {
            get => string.Equals(App.FastFlags.GetPreset("DarkMode.BlueMode"), "True", StringComparison.OrdinalIgnoreCase);

            set => App.FastFlags.SetPreset("DarkMode.BlueMode", value ? "True" : "False");
        }


        public bool Layered
        {
            get => App.FastFlags.GetPreset("Layered.Clothing") == "-1"; // we use this fflag to determine if preset is enabled
            set
            {

                App.FastFlags.SetPreset("Layered.Clothing", value ? "-1" : null);
            }
        }

        public bool FpsFix
        {
            get => App.FastFlags.GetPreset("FpsFix.Log") == "False"; // we use this fflag to determine if preset is enabled
            set
            {

                App.FastFlags.SetPreset("FpsFix.Log", value ? "False" : null);
            }
        }

        public bool Preload
        {
            get => App.FastFlags.GetPreset("Preload.Preload2") == "True"; // we use this fflag to determine if preset is enabled
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
        

        public bool PingBreakdown
        {
            get => App.FastFlags.GetPreset("Debug.PingBreakdown") == "True";
            set => App.FastFlags.SetPreset("Debug.PingBreakdown", value ? "True" : null);
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

        public IReadOnlyDictionary<MSAAMode, string?> MSAALevels => FastFlagManager.MSAAModes;

        public MSAAMode SelectedMSAALevel
        {
            get => MSAALevels.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.MSAA")).Key;
            set
            {
                App.FastFlags.SetPreset("Rendering.MSAA", MSAALevels[value]);
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
                // yeah this kinda sucks
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
            set {
                App.FastFlags.SetPreset("UI.Hide", value == 0 ? null : value);
                if (value != 0)
                {
                    App.FastFlags.SetPreset("UI.Hide.Toggles", true);
                } else
                {
                    App.FastFlags.SetPreset("UI.Hide.Toggles", null);
                }
            }
        }

        public IReadOnlyDictionary<TextureQuality, string?> TextureQualities => FastFlagManager.TextureQualityLevels;

        public TextureQuality SelectedTextureQuality
        {
            get => TextureQualities.Where(x => x.Value == App.FastFlags.GetPreset("Rendering.TextureQuality.Level")).FirstOrDefault().Key;
            set
            {
                if (value == TextureQuality.Default)
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality.Level", TextureQualities[value]);
                }
            }
        }


        public bool DisablePostFX
        {
            get => App.FastFlags.GetPreset("Rendering.DisablePostFX") == "True";
            set => App.FastFlags.SetPreset("Rendering.DisablePostFX", value ? "True" : null);
        }

        public bool DisablePlayerShadows
        {
            get => App.FastFlags.GetPreset("Rendering.ShadowIntensity") == "0";
            set
            {
                App.FastFlags.SetPreset("Rendering.ShadowIntensity", value ? "0" : null);
            }
        }
        public int? FontSize
        {
            get => int.TryParse(App.FastFlags.GetPreset("UI.FontSize"), out int x) ? x : 1;
            set => App.FastFlags.SetPreset("UI.FontSize", value == 1 ? null : value);
        }

        public bool RainBowText
        {
            get
            {
                // Return true if the preset is set to "True"
                return App.FastFlags.GetPreset("UI.RainBowText") == "rbxasset://fonts/families/BuilderSans.json";
            }
            set
            {
                // Set the preset to "True" if value is true, otherwise set it to null
                App.FastFlags.SetPreset("UI.RainBowText", value ? "rbxasset://fonts/families/BuilderSans.json" : null);
            }
        }


        public bool DisableTerrainTextures
        {
            get => App.FastFlags.GetPreset("Rendering.TerrainTextureQuality") == "0";
            set
            {
                App.FastFlags.SetPreset("Rendering.TextureQuality", value ? " " : null);
                App.FastFlags.SetPreset("Rendering.TextureQuality2", value ? " " : null);
                App.FastFlags.SetPreset("Rendering.TerrainTextureQuality", value ? "0" : null);
            }
        }





        public bool ChromeUI
        {
            get => App.FastFlags.GetPreset("UI.Menu.ChromeUI") != "False"; // its on by default so we have to do that
            set => App.FastFlags.SetPreset("UI.Menu.ChromeUI", value);
        }

        public bool VRToggle
        {
            get => App.FastFlags.GetPreset("Menu.VRToggles") != "False";
            set => App.FastFlags.SetPreset("Menu.VRToggles", value);
        }

        public bool SoothsayerCheck
        {
            get => App.FastFlags.GetPreset("Menu.Feedback") != "False";
            set => App.FastFlags.SetPreset("Menu.Feedback", value);
        }

        public bool LanguageSelector
        {
            get => App.FastFlags.GetPreset("Menu.LanguageSelector") != "0";
            set => App.FastFlags.SetPreset("Menu.LanguageSelector", value ? null : "0");
        }

        public bool Haptics
        {
            get => App.FastFlags.GetPreset("Menu.Haptics") != "False";
            set => App.FastFlags.SetPreset("Menu.Haptics", value);
        }

        public bool Framerate
        {
            get => App.FastFlags.GetPreset("Menu.Framerate") != "False";
            set => App.FastFlags.SetPreset("Menu.Framerate", value);
        }

        public bool ChatTranslation
        {
            get => App.FastFlags.GetPreset("Menu.ChatTranslation") != "False";
            set => App.FastFlags.SetPreset("Menu.ChatTranslation", value);
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
    }
}