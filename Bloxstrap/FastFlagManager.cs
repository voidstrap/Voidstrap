using Hellstrap.Enums.FlagPresets;
using System.Security.Policy;
using System.Windows;

namespace Hellstrap
{
    public class FastFlagManager : JsonManager<Dictionary<string, object>>
    {
        public override string ClassName => nameof(FastFlagManager);

        public override string LOG_IDENT_CLASS => ClassName;

        public override string ProfilesLocation => Path.Combine(Paths.Base, "SavedBackups");

        public override string FileLocation => Path.Combine(Paths.Mods, "ClientSettings\\ClientAppSettings.json");

        public bool Changed => !OriginalProp.SequenceEqual(Prop);

        public static IReadOnlyDictionary<string, string> PresetFlags = new Dictionary<string, string>
        {
            // Activity watcher
            { "Decomposition.Log", "FFlagSimEnableDCD16" },
            { "InertialScrolling.Log", "FFlagUserBetterInertialScrolling" },
            { "FpsFix.Log", "FFlagTaskSchedulerLimitTargetFpsTo2402" },
            { "Perf.Log", "DFFlagDebugPerfMode" },
            { "Players.LogLevel", "FStringDebugLuaLogLevel" },
            { "Players.LogPattern", "FStringDebugLuaLogPattern" },

            // Debug
            { "Debug.FlagState", "FStringDebugShowFlagState"},
            { "Debug.PingBreakdown", "DFFlagDebugPrintDataPingBreakDown" },

            // Presets and stuff
            { "Rendering.Framerate", "DFIntTaskSchedulerTargetFps" },
            { "Rendering.ManualFullscreen", "FFlagHandleAltEnterFullscreenManually" },
            { "Rendering.DisableScaling", "DFFlagDisableDPIScale" },
            { "Rendering.DisableScaling2", "DFFlagDebugOverrideDPIScale" },
            { "Rendering.MSAA", "FIntDebugForceMSAASamples" },
            { "Rendering.DisablePostFX", "FFlagDisablePostFx" },
            { "Rendering.ShadowIntensity", "FIntRenderShadowIntensity" },

            
            // Rendering engines
            { "Rendering.Mode.DisableD3D11", "FFlagDebugGraphicsDisableDirect3D11" },
            { "Rendering.Mode.D3D11", "FFlagDebugGraphicsPreferD3D11" },
            { "Rendering.Mode.Metal", "FFlagDebugGraphicsPreferMetal" },
            { "Rendering.Mode.Vulkan", "FFlagDebugGraphicsPreferVulkan" },
            { "Rendering.Mode.OpenGL", "FFlagDebugGraphicsPreferOpenGL" },
            { "Rendering.Mode.D3D10", "FFlagDebugGraphicsPreferD3D11FL10" },
            { "Rendering.FixHighlights", "FFlagHighlightOutlinesOnMobile"},

            // Lighting technology
            { "Rendering.Lighting.Voxel", "DFFlagDebugRenderForceTechnologyVoxel" },
            { "Rendering.Lighting.ShadowMap", "FFlagDebugForceFutureIsBrightPhase2" },
            { "Rendering.Lighting.Future", "FFlagDebugForceFutureIsBrightPhase3" },
            { "Rendering.Lighting.Unified", "FFlagRenderUnifiedLighting12"},

            // Texture quality
            { "Rendering.TerrainTextureQuality", "FIntTerrainArraySliceSize" },
            { "Rendering.TextureQuality.Level", "FIntDebugTextureManagerSkipMips" },


            // Guis
            { "UI.Hide", "DFIntCanHideGuiGroupId" },
            { "UI.Hide.Toggles", "FFlagUserShowGuiHideToggles"},
            { "UI.FontSize", "FIntFontSizePadding" },
            { "UI.RainBowText", "FStringDebugHighlightSpecificFont" },


    // Telemetry
    { "Telemetry.GraphicsQualityUsage", "DFFlagGraphicsQualityUsageTelemetry" },
    { "Telemetry.GpuVsCpuBound", "DFFlagGpuVsCpuBoundTelemetry" },
    { "Telemetry.RenderFidelity", "DFFlagSendRenderFidelityTelemetry" },
    { "Telemetry.RenderDistance", "DFFlagReportRenderDistanceTelemetry" },
    { "Telemetry.PhysicsSolverPerf", "DFFlagSimSolverSendPerfTelemetryToElasticSearch2" },
    { "Telemetry.BadMoverConstraint", "DFFlagSimEnableBadMoverConstraintTelemetry" },
    { "Telemetry.AudioPlugin", "DFFlagCollectAudioPluginTelemetry" },
    { "Telemetry.FmodErrors", "DFFlagEnableFmodErrorsTelemetry" },
    { "Telemetry.SoundLength", "DFFlagRccLoadSoundLengthTelemetryEnabled" },
    { "Telemetry.AssetRequestV1", "DFFlagReportAssetRequestV1Telemetry" },
    { "Telemetry.SeparateEventPoints", "DFFlagPerformanceControlUseSeparateTelemetryEventsForPointsAndEventIngest_DataCenterFilter" },
    { "Telemetry.DeviceRAM", "DFFlagRobloxTelemetryAddDeviceRAMPointsV2" },
    { "Telemetry.TelemetryFlush", "DFFlagRemoveTelemetryFlushOnJobClose" },
    { "Telemetry.V2FrameRateMetrics", "DFFlagEnableTelemetryV2FRMStats" },
    { "Telemetry.GlobalSkipUpdating", "DFFlagEnableSkipUpdatingGlobalTelemetryInfo2" },
    { "Telemetry.CallbackSafety", "DFFlagEmitSafetyTelemetryInCallbackEnable" },
    { "Telemetry.V2PointEncoding", "DFFlagRobloxTelemetryV2PointEncoding" },
    { "Telemetry.ReplaceSeparator", "DFFlagDSTelemetryV2ReplaceSeparator" },
    { "Telemetry.EpCounter", "FFlagDebugDisableTelemetryEphemeralCounter" },
    { "Telemetry.EpStats", "FFlagDebugDisableTelemetryEphemeralStat" },
    { "Telemetry.Event", "FFlagDebugDisableTelemetryEventIngest" },
    { "Telemetry.V2Counter", "FFlagDebugDisableTelemetryV2Counter" },
    { "Telemetry.V2Event", "FFlagDebugDisableTelemetryV2Event" },
    { "Telemetry.V2Stats", "FFlagDebugDisableTelemetryV2Stat" },
    { "Telemetry.Point", "FFlagDebugDisableTelemetryPoint" },

            // DarkMode
            { "DarkMode.BlueMode", "FFlagLuaAppEnableFoundationColors7"},


            // Clothing
            { "Layered.Clothing", "DFIntLCCageDeformLimit"},

            // Preload
            { "Preload.Preload2", "DFFlagEnableMeshPreloading2"},
            { "Preload.SoundPreload", "DFFlagEnableSoundPreloading"},
            { "Preload.Texture", "DFFlagEnableTexturePreloading"},
            { "Preload.TeleportPreload", "DFFlagTeleportClientAssetPreloadingEnabled9"},
            { "Preload.FontsPreload", "FFlagPreloadAllFonts"},
            { "Preload.ItemPreload", "FFlagPreloadTextureItemsOption4"},
            { "Preload.Teleport2", "DFFlagTeleportPreloadingMetrics5"},
            
            // Fullscreen bar
            { "UI.FullscreenTitlebarDelay", "FIntFullscreenTitleBarTriggerDelayMillis" },

            // useless
            { "UI.Menu.Style.V2Rollout", "FIntNewInGameMenuPercentRollout3" },
            { "UI.Menu.Style.EnableV4.1", "FFlagEnableInGameMenuControls" },
            { "UI.Menu.Style.EnableV4.2", "FFlagEnableInGameMenuModernization" },
            { "UI.Menu.Style.EnableV4Chrome", "FFlagEnableInGameMenuChrome" },
            { "UI.Menu.Style.ReportButtonCutOff", "FFlagFixReportButtonCutOff" },

            // Chrome ui
            { "UI.Menu.ChromeUI", "FFlagEnableInGameMenuChromeABTest4" },

            // Menu stuff
            { "Menu.VRToggles", "FFlagAlwaysShowVRToggleV3" },
            { "Menu.Feedback", "FFlagDisableFeedbackSoothsayerCheck" },
            { "Menu.LanguageSelector", "FIntV1MenuLanguageSelectionFeaturePerMillageRollout" },
            { "Menu.Haptics", "FFlagAddHapticsToggle" },
            { "Menu.Framerate", "FFlagGameBasicSettingsFramerateCap5"},
            { "Menu.ChatTranslation", "FFlagChatTranslationSettingEnabled3" },


            { "UI.Menu.Style.ABTest.1", "FFlagEnableMenuControlsABTest" },
            { "UI.Menu.Style.ABTest.2", "FFlagEnableV3MenuABTest3" },
            { "UI.Menu.Style.ABTest.3", "FFlagEnableInGameMenuChromeABTest3" },
            { "UI.Menu.Style.ABTest.4", "FFlagEnableInGameMenuChromeABTest4" }
        };

        public static IReadOnlyDictionary<RenderingMode, string> RenderingModes => new Dictionary<RenderingMode, string>
        {
            { RenderingMode.Default, "None" },
            { RenderingMode.D3D11, "D3D11" },
            { RenderingMode.D3D10, "D3D10" },
            { RenderingMode.Metal, "Metal" },
            { RenderingMode.Vulkan, "Vulkan" },
            { RenderingMode.OpenGL, "OpenGL" },

        };

        public static IReadOnlyDictionary<LightingMode, string> LightingModes => new Dictionary<LightingMode, string>
        {
            { LightingMode.Default, "None" },
            { LightingMode.Voxel, "Voxel" },
            { LightingMode.ShadowMap, "ShadowMap" },
            { LightingMode.Future, "Future" },
            { LightingMode.Unified, "Unified" },
        };

        public static IReadOnlyDictionary<ProfileMode, string> ProfileModes => new Dictionary<ProfileMode, string>
        {
            { ProfileMode.Default, "None" },
            { ProfileMode.Hellstrap, "Hellstraps Official" },
            { ProfileMode.Stoof, "Stoofs" },

        };

        public static IReadOnlyDictionary<MSAAMode, string?> MSAAModes => new Dictionary<MSAAMode, string?>
        {
            { MSAAMode.Default, null },
            { MSAAMode.x1, "1" },
            { MSAAMode.x2, "2" },
            { MSAAMode.x3, "3" },
            { MSAAMode.x4, "4" },
            { MSAAMode.x8, "8" }
        };

        public static IReadOnlyDictionary<TextureQuality, string?> TextureQualityLevels => new Dictionary<TextureQuality, string?>
        {
            { TextureQuality.Default, null },
            { TextureQuality.Level1, "1" },
            { TextureQuality.Level2, "2" },
            { TextureQuality.Level3, "3" },
            { TextureQuality.Level4, "4" },
            { TextureQuality.Level5, "5" },
            { TextureQuality.Level6, "6" },
            { TextureQuality.Level7, "7" },
            { TextureQuality.Level8, "8" }
        };



        public static IReadOnlyDictionary<InGameMenuVersion, Dictionary<string, string?>> IGMenuVersions => new Dictionary<InGameMenuVersion, Dictionary<string, string?>>
        {
           {
               InGameMenuVersion.Default,
                new Dictionary<string, string?>
                {
                    { "V2Rollout", null },
                    { "EnableV4", null },
                    { "EnableV4Chrome", null },
                   { "ABTest", null },
                    { "ReportButtonCutOff", null }
                }
            },

            {
                InGameMenuVersion.V2,
                new Dictionary<string, string?>
                {
                    { "V2Rollout", "100" },
                    { "EnableV4", "False" },
                    { "EnableV4Chrome", "False" },
                   { "ABTest", "False" },
                    { "ReportButtonCutOff", null }
                }
            },

            {
                InGameMenuVersion.V4,
                new Dictionary<string, string?>
                {
                    { "V2Rollout", "0" },
                    { "EnableV4", "True" },
                    { "EnableV4Chrome", "False" },
                   { "ABTest", "False" },
                    { "ReportButtonCutOff", null }
                }
            },

            {
                InGameMenuVersion.V4Chrome,
                new Dictionary<string, string?>
                {
                    { "V2Rollout", "0" },
                   { "EnableV4", "True" },
                    { "EnableV4Chrome", "True" },
                   { "ABTest", "False" },
                    { "ReportButtonCutOff", null }
                }
            }
        };

        public void SetValue(string key, object? value)
        {
            const string LOG_IDENT = "FastFlagManager::SetValue";

            if (value is null)
            {
                if (Prop.ContainsKey(key))
                    App.Logger.WriteLine(LOG_IDENT, $"Deletion of '{key}' is pending");

                Prop.Remove(key);
            }
            else
            {
                if (Prop.ContainsKey(key))
                {
                    if (key == Prop[key].ToString())
                        return;

                    App.Logger.WriteLine(LOG_IDENT, $"Changing of '{key}' from '{Prop[key]}' to '{value}' is pending");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Setting of '{key}' to '{value}' is pending");
                }

                Prop[key] = value.ToString()!;
            }
        }

        // this returns null if the fflag doesn't exist
        public string? GetValue(string key)
        {
            // check if we have an updated change for it pushed first
            if (Prop.TryGetValue(key, out object? value) && value is not null)
                return value.ToString();

            return null;
        }

        public void SetPreset(string prefix, object? value)
        {
            foreach (var pair in PresetFlags.Where(x => x.Key.StartsWith(prefix)))
                SetValue(pair.Value, value);
        }

        public void SetPresetEnum(string prefix, string target, object? value)
        {
            foreach (var pair in PresetFlags.Where(x => x.Key.StartsWith(prefix)))
            {
                if (pair.Key.StartsWith($"{prefix}.{target}"))
                    SetValue(pair.Value, value);
                else
                    SetValue(pair.Value, null);
            }
        }

        public string? GetPreset(string name)
        {
            if (!PresetFlags.ContainsKey(name))
            {
                App.Logger.WriteLine("FastFlagManager::GetPreset", $"Could not find preset {name}");
                Debug.Assert(false, $"Could not find preset {name}");
                return null;
            }

            // Retrieve the list of flags associated with the preset
            var flags = PresetFlags[name];

            return GetValue(PresetFlags[name]);
        }


        public T GetPresetEnum<T>(IReadOnlyDictionary<T, string> mapping, string prefix, string value) where T : Enum
        {
            foreach (var pair in mapping)
            {
                if (pair.Value == "None")
                    continue;

                if (GetPreset($"{prefix}.{pair.Value}") == value)
                    return pair.Key;
            }

            return mapping.First().Key;
        }

        public override void Save()
        {
            // convert all flag values to strings before saving

            foreach (var pair in Prop)
                Prop[pair.Key] = pair.Value.ToString()!;

            base.Save();

            // clone the dictionary
            OriginalProp = new(Prop);
        }



        public override void Load(bool alertFailure = false)
        {
            base.Load(alertFailure);

            // Clone the dictionary
            OriginalProp = new(Prop);

            // Presets to be checked and set
            var presets = new Dictionary<string, string>
    {
        { "InertialScrolling.Log", "True" },
        { "Decomposition.Log", "True" },
        { "Perf.Log", "True" },
        { "Rendering.ManualFullscreen", "True" },  // dx and vulkan alt enter fix
        { "Rendering.FixHighlights", "True" }
    };

            // Check and set the presets
            foreach (var preset in presets)
            {
                if (GetPreset(preset.Key) != preset.Value)
                {
                    SetPreset(preset.Key, preset.Value);
                }
            }

        }


        public void DeleteProfile(string profile)
        {
            if (string.IsNullOrWhiteSpace(profile))
                return; // Exit early if the profile name is invalid

            try
            {
                string profilesDirectory = Path.Combine(Paths.Base, Paths.SavedBackups);
                Directory.CreateDirectory(profilesDirectory); // Ensures the directory exists

                string profilePath = Path.Combine(profilesDirectory, Path.GetFileName(profile)); // Prevents path traversal

                if (File.Exists(profilePath))
                {
                    File.Delete(profilePath);
                }
            }
            catch (Exception ex)
            {
                // Optionally log the exception here
                Frontend.ShowMessageBox($"Error deleting profile: {ex.Message}", MessageBoxImage.Error);
            }
        }
    }
}
