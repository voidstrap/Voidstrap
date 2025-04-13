using Voidstrap.Enums.FlagPresets;
using System.Windows;

namespace Voidstrap
{
    public class FastFlagManager : JsonManager<Dictionary<string, object>>
    {
        public override string ClassName => nameof(FastFlagManager);

        public override string LOG_IDENT_CLASS => ClassName;

        public override string BackupsLocation => Path.Combine(Paths.Base, "SavedBackups");

        public override string FileLocation => Path.Combine(Paths.Mods, "ClientSettings\\ClientAppSettings.json");

        public bool Changed => !OriginalProp.SequenceEqual(Prop);

        public static IReadOnlyDictionary<string, string> PresetFlags = new Dictionary<string, string>
        {
            // Activity watcher
            { "FpsFix.Log", "FFlagTaskSchedulerLimitTargetFpsTo2402" },
            { "Players.LogLevel", "FStringDebugLuaLogLevel" },

            // Hyper Threading
            { "Hyper.Threading1", "FFlagDebugCheckRenderThreading" },
            { "Hyper.Threading2", "FFlagRenderDebugCheckThreading2" },
            { "Hyper.Threading3", "FIntRuntimeMaxNumOfThreads" },
            { "Hyper.Threading4", "FIntTaskSchedulerThreadMin" },

            // Memory Probing
            { "Memory.Probe", "DFFlagPerformanceControlEnableMemoryProbing3" },

            // frm quality level
            { "Rendering.FrmQuality", "DFIntDebugFRMQualityLevelOverride" },

            // Less lag spikes
            { "Network.DefaultBps", "DFIntBandwidthManagerApplicationDefaultBps" },
            { "Network.MaxWorkCatchupMs", "DFIntBandwidthManagerDataSenderMaxWorkCatchupMs" },

            // Load Faster
            { "Network.AssetPreloadding", "DFIntAssetPreloading" },
            { "Network.MeshPreloadding", "DFFlagEnableMeshPreloading2" },
            { "Network.MaxAssetPreload", "DFIntNumAssetsMaxToPreload" },
            { "Network.PlayerImageDefault", "FStringGetPlayerImageDefaultTimeout" },


            //Brighter Visuals
            { "Rendering.BrighterVisual", "FFlagRenderFixFog" },

            // Remove Grass
            { "Rendering.Nograss1", "FIntFRMMinGrassDistance" },
            { "Rendering.Nograss2", "FIntFRMMaxGrassDistance" },
            { "Rendering.Nograss3", "FIntRenderGrassDetailStrands" },

            // Rainbow Text
            { "UI.RainbowText", "FFlagDebugDisplayUnthemedInstances" },

            // Debug
            { "Debug.FlagState", "FStringDebugShowFlagState"},
            { "Debug.PingBreakdown", "DFFlagDebugPrintDataPingBreakDown" },

            // Cpu Threads
            { "Rendering.CpuThreads", "DFIntRuntimeConcurrency"},
            
            // Gray Sky
            { "Rendering.GraySky", "FFlagDebugSkyGray" },

            // Presets and stuff
            { "Rendering.Framerate", "DFIntTaskSchedulerTargetFps" },
            { "Rendering.DisableScaling", "DFFlagDisableDPIScale" },
            { "Rendering.DisableScaling2", "DFFlagDebugOverrideDPIScale" },
            { "Rendering.MSAA", "FIntDebugForceMSAASamples" },
            { "Rendering.DisablePostFX", "FFlagDisablePostFx" },

            //Shadows and lighting
            { "Rendering.ShadowIntensity", "DFFlagDebugPauseVoxelizer" },

            //Chat Bubble
            { "UI.Chatbubble", "FFlagEnableBubbleChatFromChatService" },

            //Light Cullings
            { "Rendering.GpuCulling", "FFlagFastGPULightCulling3" },
            { "Rendering.CpuCulling", "FFlagDebugForceFSMCPULightCulling" },           

            //Unlimited Camera Distance
            { "Rendering.Camerazoom","FIntCameraMaxZoomDistance" },

            //MTU Size
            { "Network.Mtusize","DFIntConnectionMTUSize" },

            //Dynamic Render Resolution
            { "Rendering.Pixel","DFIntDebugDynamicRenderKiloPixels"},

            // Rendering engines
            { "Rendering.Mode.DisableD3D11", "FFlagDebugGraphicsDisableDirect3D11" },
            { "Rendering.Mode.D3D11", "FFlagDebugGraphicsPreferD3D11" },
            { "Rendering.Mode.Metal", "FFlagDebugGraphicsPreferMetal" },
            { "Rendering.Mode.Vulkan", "FFlagDebugGraphicsPreferVulkan" },
            { "Rendering.Mode.OpenGL", "FFlagDebugGraphicsPreferOpenGL" },
            { "Rendering.Mode.D3D10", "FFlagDebugGraphicsPreferD3D11FL10" },

            // Lighting technology
            { "Rendering.Lighting.Voxel", "DFFlagDebugRenderForceTechnologyVoxel" },
            { "Rendering.Lighting.ShadowMap", "FFlagDebugForceFutureIsBrightPhase2" },
            { "Rendering.Lighting.Future", "FFlagDebugForceFutureIsBrightPhase3" },
            { "Rendering.Lighting.Unified", "FFlagRenderUnifiedLighting12"},

            // Texture quality
            { "Rendering.TerrainTextureQuality", "FIntTerrainArraySliceSize" },
            { "Rendering.TextureSkipping.Skips", "FIntDebugTextureManagerSkipMips" },
            { "Rendering.TextureQuality.Level", "DFIntTextureQualityOverride" },
            { "Rendering.TextureQuality.OverrideEnabled", "DFFlagTextureQualityOverrideEnabled" },

            // VoiceChat Google
            { "VoiceChat.VoiceChat1", "FFlagTopBarUseNewBadge" },
            { "VoiceChat.VoiceChat2", "FStringTopBarBadgeLearnMoreLink" },
            { "VoiceChat.VoiceChat3", "FStringVoiceBetaBadgeLearnMoreLink" },

            // VoiceChat Other
            { "VoiceChat.VoiceChat4", "DFIntVoiceChatVolumeThousandths" },
            { "VoiceChat.VoiceChat5", "FFlagEnablePartyVoiceOnlyForUnfilteredThreads" },
            { "VoiceChat.VoiceChat6", "FFlagEnablePartyVoiceOnlyForEligibleUsers" },


            // Guis
            { "UI.Hide", "DFIntCanHideGuiGroupId" },
            { "UI.Hide.Toggles", "FFlagUserShowGuiHideToggles" },
            { "UI.FontSize", "FIntFontSizePadding" },
            { "UI.RedFont", "FStringDebugHighlightSpecificFont" },


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

            // Next Gen Replicator
            { "Network.NextGenReplicatorWrite", "FFlagNextGenReplicatorEnabledWrite2"},
            { "Network.NextGenReplicatorRead", "FFlagNextGenReplicatorEnabledRead2"},
            { "Network.NextGenReplicatorBetterPacket", "DFFlagNextGenRepRollbackOverbudgetPackets"},

            // Next Gen Replicator
            { "Network.EnableLargeReplicator", "FFlagLargeReplicatorEnabled3"},
            { "Network.LargeReplicatorWrite", "FFlagLargeReplicatorWrite2"},
            { "Network.LargeReplicatorRead", "FFlagLargeReplicatorRead2"},

            // Ads
            { "UI.Disable.Ads", "FFlagAdServiceEnabled" },
            
            // Fullscreen bar
            { "UI.FullscreenTitlebarDelay", "FIntFullscreenTitleBarTriggerDelayMillis" },

            // useless
            { "UI.Menu.Style.V2Rollout", "FIntNewInGameMenuPercentRollout3" },
            { "UI.Menu.Style.EnableV4.1", "FFlagEnableInGameMenuControls" },
            { "UI.Menu.Style.EnableV4.2", "FFlagEnableInGameMenuModernization" },
            { "UI.Menu.Style.EnableV4Chrome", "FFlagEnableInGameMenuChrome" },
            { "UI.Menu.Style.ReportButtonCutOff", "FFlagFixReportButtonCutOff" },

            // Display Fps
            { "Rendering.DisplayFps", "FFlagDebugDisplayFPS" },

            // Pause Voxelizer
            { "Rendering.Pause.Voxelizer", "DFFlagDebugPauseVoxelizer" },

            //Distance Rendering
            { "Rendering.Distance.Chunks", "DFIntDebugRestrictGCDistance" },

            //Romark
            { "Rendering.Start.Graphic", "FIntRomarkStartWithGraphicQualityLevel" },

            // Chrome ui
            { "UI.Menu.ChromeUI", "FFlagEnableInGameMenuChromeABTest4" },
            
            // Preferred GPU
            { "Rendering.PreferredGPU", "FStringDebugGraphicsPreferredGPUName"},

            // prerender
            { "Rendering.Prerender", "FFlagMovePrerender" },
            { "Rendering.PrerenderV2", "FFlagMovePrerenderV2" },

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
            { ProfileMode.Voidstrap, "Voidstraps Official" },
            { ProfileMode.Stoof, "Stoofs" },

        };

        public static IReadOnlyDictionary<MSAAMode, string?> MSAAModes => new Dictionary<MSAAMode, string?>
        {
            { MSAAMode.Default, null },
            { MSAAMode.x1, "1" },
            { MSAAMode.x2, "2" },
            { MSAAMode.x4, "4" },
            { MSAAMode.x6, "6" },
            { MSAAMode.x8, "8" }
        };

        public static IReadOnlyDictionary<TextureSkipping, string?> TextureSkippingSkips => new Dictionary<TextureSkipping, string?>
        {
            { TextureSkipping.Noskip, null },
            { TextureSkipping.Skip1x, "1" },
            { TextureSkipping.Skip2x, "2" },
            { TextureSkipping.Skip3x, "3" },
            { TextureSkipping.Skip4x, "4" },
            { TextureSkipping.Skip5x, "5" },
            { TextureSkipping.Skip6x, "6" },
            { TextureSkipping.Skip7x, "7" },
            { TextureSkipping.Skip8x, "8" }
        };
        public static IReadOnlyDictionary<DistanceRendering, string?> DistanceRenderings => new Dictionary<DistanceRendering, string?>
        {
            { DistanceRendering.Default, null },
            { DistanceRendering.Chunks1x, "1" },
            { DistanceRendering.Chunks2x, "2" },
            { DistanceRendering.Chunks3x, "3" },
            { DistanceRendering.Chunks4x, "4" },
            { DistanceRendering.Chunks5x, "5" },
            { DistanceRendering.Chunks6x, "6" },
            { DistanceRendering.Chunks7x, "7" },
            { DistanceRendering.Chunks8x, "8" },
            { DistanceRendering.Chunks9x, "9" },
            { DistanceRendering.Chunks10x, "10" },
            { DistanceRendering.Chunks11x, "11" },
            { DistanceRendering.Chunks12x, "12" },
            { DistanceRendering.Chunks13x, "13" },
            { DistanceRendering.Chunks14x, "14" },
            { DistanceRendering.Chunks15x, "15" },
            { DistanceRendering.Chunks16x, "16" }
        };

        public static IReadOnlyDictionary<TextureQuality, string?> TextureQualityLevels => new Dictionary<TextureQuality, string?>
        {
            { TextureQuality.Default, null },
            { TextureQuality.Lowest, "0" },
            { TextureQuality.Low, "1" },
            { TextureQuality.Medium, "2" },
            { TextureQuality.High, "3" },
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
        public static IReadOnlyDictionary<RomarkStart, string?> RomarkStartMappings => new Dictionary<RomarkStart, string?>
        {
            { RomarkStart.Disabled, null },
            { RomarkStart.One, "1" },
            { RomarkStart.Two, "2" },
            { RomarkStart.Three, "3" },
            { RomarkStart.Four, "4" },
            { RomarkStart.Five, "5" },
            { RomarkStart.Six, "6" },
            { RomarkStart.Seven, "7" },
            { RomarkStart.Eight, "8" },
            { RomarkStart.Nine, "9" },
            { RomarkStart.Ten, "10" }
        };

        public static IReadOnlyDictionary<QualityLevel, string?> QualityLevels => new Dictionary<QualityLevel, string?>
        {
            { QualityLevel.Disabled, null },
            { QualityLevel.One, "1" },
            { QualityLevel.Two, "2" },
            { QualityLevel.Three, "3" },
            { QualityLevel.Four, "4" },
            { QualityLevel.Five, "5" },
            { QualityLevel.Six, "6" },
            { QualityLevel.Seven, "7" },
            { QualityLevel.Eight, "8" },
            { QualityLevel.Nine, "9" },
            { QualityLevel.Ten, "10" }
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
            // Check if the preset is already set
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
            OriginalProp = Prop.ToDictionary(pair => pair.Key, pair => (object)(pair.Value?.ToString() ?? string.Empty));

            if (!HasFastFlags())
            {
                var presets = new Dictionary<string, string>
        {
            { "DFFlagDebugPerfMode", "True" },
            { "FFlagHandleAltEnterFullscreenManually", "False" },  // Yeh I fixed it W
        };

                foreach (var (key, value) in presets)
                {
                    if (!Prop.ContainsKey(key))
                    {
                        Prop[key] = value;
                    }
                }
            }
        }

        private bool HasFastFlags()
        {
            return Prop.Keys.Any(key => key.StartsWith("FastFlag"));
        }



        public void DeleteBackup(string Backup)
        {
            if (string.IsNullOrWhiteSpace(Backup))
                return; // Exit early if the profile name is invalid

            try
            {
                string backupsDirectory = Path.Combine(Paths.Base, Paths.SavedBackups);
                Directory.CreateDirectory(backupsDirectory); // Ensures the directory exists

                string BackupPath = Path.Combine(backupsDirectory, Path.GetFileName(Backup)); // Prevents path traversal

                if (File.Exists(BackupPath))
                {
                    File.Delete(BackupPath);
                }
            }
            catch (Exception ex)
            {
                // Optionally log the exception here
                Frontend.ShowMessageBox($"Error deleting backup: {ex.Message}", MessageBoxImage.Error);
            }
        }
    }
}
