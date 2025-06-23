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
            { "Players.LogPattern", "FStringDebugLuaLogPattern" },

            // Hyper Threading
            { "Hyper.Threading1", "FFlagDebugCheckRenderThreading" },

            // Memory Probing
            { "Memory.Probe", "DFFlagPerformanceControlEnableMemoryProbing3" },

            //Optimize charcater frame
           { "OptimizeCFrameUpdates", "FFlagOptimizeCFrameUpdates4" },
           { "OptimizeCFrameUpdatesIC", "FFlagOptimizeCFrameUpdatesIC4" },

            // frm quality level
            { "Rendering.FrmQuality", "DFIntDebugFRMQualityLevelOverride" },

            // No Texture
            { "Rendering.RemoveTexture1", "FFlagTextureUseACR3" },
            { "Rendering.RemoveTexture2", "FIntTextureUseACRHundredthPercent" },

            // More Sensetivity Numbers
            { "UI.SensetivityNumbers", "FFlagFixSensitivityTextPrecision" },

            // Remove GUI Blur
            { "UI.NoGuiBlur", "FIntRobloxGuiBlurIntensity" },

            // Custom Disconnect Error
            { "UI.CustomDisconnectError1", "FFlagReconnectDisabled" },
            { "UI.CustomDisconnectError2", "FStringReconnectDisabledReason" },

            // Less lag spikes
            { "Network.DefaultBps", "DFIntBandwidthManagerApplicationDefaultBps" },
            { "Network.MaxWorkCatchupMs", "DFIntBandwidthManagerDataSenderMaxWorkCatchupMs" },

            // Load Faster
            { "Network.MeshPreloadding", "DFFlagEnableMeshPreloading2" },
            { "Network.MaxAssetPreload", "DFIntNumAssetsMaxToPreload" },
            { "Network.PlayerImageDefault", "FStringGetPlayerImageDefaultTimeout" },

            // Payload Limit
            { "Network.Payload1", "DFIntRccMaxPayloadSnd" },
            { "Network.Payload2", "DFIntCliMaxPayloadRcv" },
            { "Network.Payload3", "DFIntCliMaxPayloadSnd" },
            { "Network.Payload4", "DFIntRccMaxPayloadRcv" },
            { "Network.Payload5", "DFIntCliTcMaxPayloadRcv" },
            { "Network.Payload6", "DFIntRccTcMaxPayloadRcv" },
            { "Network.Payload7", "DFIntCliTcMaxPayloadSnd" },
            { "Network.Payload8", "DFIntRccTcMaxPayloadSnd" },
            { "Network.Payload9", "DFIntMaxDataPayloadSize" },
            { "Network.Payload10", "DFIntMaxUREPayloadSingleLimit" },
            { "Network.Payload11", "DFIntTotalRepPayloadLimit" },

            // Allow vulkan on older gpus
            { "Rendering.ForceVulkan", "FStringBuggyRenderpassList2" },

            // Brighter Visuals
            { "Rendering.BrighterVisual", "FFlagRenderFixFog" },

            // Remove Grass
            { "Rendering.Nograss1", "FIntFRMMinGrassDistance" },
            { "Rendering.Nograss2", "FIntFRMMaxGrassDistance" },
            { "Rendering.Nograss3", "FIntRenderGrassDetailStrands" },

            // Text Size option
            { "UI.TextSize1", "FFlagEnablePreferredTextSizeScale"},
            { "UI.TextSize2", "FFlagEnablePreferredTextSizeSettingInMenus2" },

            // Debug
            { "Debug.FlagState", "FStringDebugShowFlagState" },
            { "Debug.PingBreakdown", "DFFlagDebugPrintDataPingBreakDown" },
            { "Debug.Chunks", "FFlagDebugLightGridShowChunks" },

            // Rainbow Text
            { "UI.RainbowText", "FFlagDebugDisplayUnthemedInstances" },

            // Cpu Threads
            { "Rendering.CpuThreads", "DFIntRuntimeConcurrency"},

            // Sky
            { "Graphic.GraySky", "FFlagDebugSkyGray" },
            { "Graphic.WhiteSky", "FFlagSkyUseRGBEEncoding" },

            // Fake Verify Icon!
            { "Fake.Verify", "FStringWhitelistVerifiedUserId" },

            { "Camera.Controls", "FFlagNewCameraControls" },
            { "Camera.Chat", "FFlagDebugForceChatDisabled" },

            // Pseudolocalization
            { "UI.Pseudolocalization", "FFlagDebugEnablePseudolocalization" },

            { "Rendering.Shaders", "DFIntRenderClampRoughnessMax" },
            { "Rendering.Shaders2", "DFIntDebugFRMQualityLevelOverride" },

            // Webview2 telemetry
            { "Telemetry.Webview1", "DFStringWebviewUrlAllowlist" },
            { "Telemetry.Webview2", "DFFlagWindowsWebViewTelemetryEnabled" },
            { "Telemetry.Webview3", "DFIntMacWebViewTelemetryThrottleHundredthsPercent" },
            { "Telemetry.Webview4", "DFIntWindowsWebViewTelemetryThrottleHundredthsPercent" },
            { "Telemetry.Webview5", "FIntStudioWebView2TelemetryHundredthsPercent" },
            { "Telemetry.Webview6", "FFlagSyncWebViewCookieToEngine2" },
            { "Telemetry.Webview7", "FFlagUpdateHTTPCookieStorageFromWKWebView" },

            // Refresh Rate
            { "System.TargetRefreshRate1", "DFIntGraphicsOptimizationModeFRMFrameRateTarget" },
            { "System.TargetRefreshRate2", "DFIntGraphicsOptimizationModeMaxFrameTimeTargetMs" },
            { "System.TargetRefreshRate3", "DFIntGraphicsOptimizationModeMinFrameTimeTargetMs" },

            // Presets and stuff
            { "Rendering.LimitFramerate", "FFlagTaskSchedulerLimitTargetFpsTo2402" },
            { "Rendering.Framerate", "DFIntTaskSchedulerTargetFps" },
            { "Rendering.DisableScaling", "DFFlagDisableDPIScale" },
            { "Rendering.MSAA1", "FIntDebugForceMSAASamples" },
            { "Rendering.MSAA2", "FIntDebugFRMOptionalMSAALevelOverride" },
            { "Rendering.DisablePostFX", "FFlagDisablePostFx" },

            // Force Logical Processors
            { "System.CpuCore1", "DFIntInterpolationNumParallelTasks" },
            { "System.CpuCore2", "DFIntMegaReplicatorNumParallelTasks" },
            { "System.CpuCore3", "DFIntNetworkClusterPacketCacheNumParallelTasks" },
            { "System.CpuCore4", "DFIntReplicationDataCacheNumParallelTasks" },
            { "System.CpuCore5", "FIntLuaGcParallelMinMultiTasks" },
            { "System.CpuCore6", "FIntSmoothClusterTaskQueueMaxParallelTasks" },
            { "System.CpuCore7", "DFIntPhysicsReceiveNumParallelTasks" },
            { "System.CpuCore8", "FIntTaskSchedulerAutoThreadLimit" },
            { "System.CpuCore9", "FIntSimWorldTaskQueueParallelTasks" },
            { "System.CpuThreads", "DFIntRuntimeConcurrency"},

            // Cpu cores
            { "System.CpuCoreMinThreadCount", "FIntTaskSchedulerAsyncTasksMinimumThreadCount"},

            // Chat Bubble
            { "UI.Chatbubble", "FFlagEnableBubbleChatFromChatService" },

            // Light Cullings
            { "Rendering.GpuCulling", "FFlagFastGPULightCulling3" },
            { "Rendering.CpuCulling", "FFlagDebugForceFSMCPULightCulling" },           

            // Unlimited Camera Distance
            { "Rendering.Camerazoom","FIntCameraMaxZoomDistance" },

            // Remove Sky/Clouds
            { "Rendering.NoFrmBloom", "FFlagRenderNoLowFrmBloom"},
            { "Rendering.FRMRefactor", "FFlagFRMRefactor"},

            // Minimal Rendering
            { "Rendering.MinimalRendering", "FFlagDebugRenderingSetDeterministic"},

            // MTU Size
            { "Network.Mtusize","DFIntConnectionMTUSize" },

            // Dynamic Render Resolution
            { "Rendering.Dynamic.Resolution","DFIntDebugDynamicRenderKiloPixels"},

            // Rendering engines
            { "Rendering.Mode.DisableD3D11", "FFlagDebugGraphicsDisableDirect3D11" },
            { "Rendering.Mode.D3D11", "FFlagDebugGraphicsPreferD3D11" },
            { "Rendering.Mode.Metal", "FFlagDebugGraphicsPreferMetal" },
            { "Rendering.Mode.Vulkan", "FFlagDebugGraphicsPreferVulkan" },
            { "Rendering.Mode.OpenGL", "FFlagDebugGraphicsPreferOpenGL" },
            { "Rendering.Mode.D3D10", "FFlagDebugGraphicsPreferD3D11FL10" },

            // Task Scheduler Avoid sleep
            { "Rendering.AvoidSleep", "DFFlagTaskSchedulerAvoidSleep" },

            // Task Scheduler Avoid sleep
            { "Rendering.GrayAvatar", "DFIntTextureCompositorActiveJobs" },

            // Lighting technology
            { "Rendering.Lighting.Voxel", "DFFlagDebugRenderForceTechnologyVoxel" },
            { "Rendering.Lighting.ShadowMap", "FFlagDebugForceFutureIsBrightPhase2" },
            { "Rendering.Lighting.Future", "FFlagDebugForceFutureIsBrightPhase3" },
            { "Rendering.Lighting.Unified", "FFlagRenderUnifiedLighting13"},

            // Worser Particles
            { "Rendering.WorserParticles1", "FFlagFixOutdatedParticles2" },
            { "Rendering.WorserParticles2", "FFlagFixOutdatedTimeScaleParticles" },
            { "Rendering.WorserParticles3", "FFlagFixParticleAttachmentCulling" },
            { "Rendering.WorserParticles4", "FFlagFixParticleEmissionBias2" },

            // Low Poly Meshes
            { "Rendering.LowPolyMeshes1", "DFIntCSGLevelOfDetailSwitchingDistance" },
            { "Rendering.LowPolyMeshes2", "DFIntCSGLevelOfDetailSwitchingDistanceL12" },
            { "Rendering.LowPolyMeshes3", "DFIntCSGLevelOfDetailSwitchingDistanceL23" },
            { "Rendering.LowPolyMeshes4", "DFIntCSGLevelOfDetailSwitchingDistanceL34" },

            // Low Quality on Low-End Devices
            { "Rendering.AndroidVfs", "FStringAndroidVfsLowspecHwCondition" },

            // BGRA
            { "Rendering.BGRA", "FFlagD3D11SupportBGRA" },

            // Texture quality
            { "Rendering.TerrainTextureQuality", "FIntTerrainArraySliceSize" },
            { "Rendering.TextureSkipping.Skips", "FIntDebugTextureManagerSkipMips" },
            { "Rendering.TextureQuality.Level", "DFIntTextureQualityOverride" },
            { "Rendering.TextureQuality.OverrideEnabled", "DFFlagTextureQualityOverrideEnabled" },


            // Guis
            { "UI.Hide", "DFIntCanHideGuiGroupId" },
            { "UI.Hide.Toggles", "FFlagUserShowGuiHideToggles" },
            { "UI.FontSize", "FIntFontSizePadding" },
            { "UI.RedFont", "FStringDebugHighlightSpecificFont" },

            // New Fps System
            { "Rendering.NewFpsSystem", "FFlagEnableFPSAndFrameTime"},
            { "Rendering.FrameRateBufferPercentage", "FIntMaquettesFrameRateBufferPercentage"},

            
            // Better Packet Sending
            { "Network.BetterPacketSending1", "DFIntNetworkStopProducingPacketsToProcessThresholdMs" },
            { "Network.BetterPacketSending2", "DFIntMaxWaitTimeBeforeForcePacketProcessMS" },
            { "Network.BetterPacketSending3", "DFIntClientPacketMaxDelayMs" },
            { "Network.BetterPacketSending4", "DFIntClientPacketMinMicroseconds" },
            { "Network.BetterPacketSending5", "DFIntClientPacketExcessMicroseconds" },
            { "Network.BetterPacketSending6", "DFIntClientPacketMaxFrameMicroseconds" },
            { "Network.BetterPacketSending7", "DFIntMaxProcessPacketsJobScaling" },
            { "Network.BetterPacketSending8", "DFIntMaxProcessPacketsStepsAccumulated" },
            { "Network.BetterPacketSending9", "DFIntMaxProcessPacketsStepsPerCyclic" },

            // Recommended Buffering
            { "Recommended.Buffer", "FIntRakNetResendBufferArrayLength" },

            // Voicechat Telemetry
            { "Telemetry.Voicechat1", "DFFlagVoiceChatCullingRecordEventIngestTelemetry" },
            { "Telemetry.Voicechat2", "DFFlagVoiceChatJoinProfilingUsingTelemetryStat_RCC" },
            { "Telemetry.Voicechat3", "DFFlagVoiceChatPossibleDuplicateSubscriptionsTelemetry" },
            { "Telemetry.Voicechat4", "DFIntVoiceChatTaskStatsTelemetryThrottleHundrethsPercent" },
            { "Telemetry.Voicechat5", "FFlagEnableLuaVoiceChatAnalyticsV2" },
            { "Telemetry.Voicechat6", "FFlagLuaVoiceChatAnalyticsBanMessage" },
            { "Telemetry.Voicechat7", "FFlagLuaVoiceChatAnalyticsUseCounterV2" },
            { "Telemetry.Voicechat8", "FFlagLuaVoiceChatAnalyticsUseEventsV2" },
            { "Telemetry.Voicechat9", "FFlagLuaVoiceChatAnalyticsUsePointsV2" },
            { "Telemetry.Voicechat10", "FFlagVoiceChatCullingEnableMutedSubsTelemetry" },
            { "Telemetry.Voicechat11", "FFlagVoiceChatCullingEnableStaleSubsTelemetry" },
            { "Telemetry.Voicechat12", "FFlagVoiceChatCustomAudioDeviceEnableNeedMorePlayoutTelemetry" },
            { "Telemetry.Voicechat13", "FFlagVoiceChatCustomAudioDeviceEnableNeedMorePlayoutTelemetry3" },
            { "Telemetry.Voicechat14", "FFlagVoiceChatCustomAudioMixerEnableUpdateSourcesTelemetry2" },
            { "Telemetry.Voicechat15", "FFlagVoiceChatDontSendTelemetryForPubIceTrickle" },
            { "Telemetry.Voicechat16", "FFlagVoiceChatPeerConnectionTelemetryDetails" },
            { "Telemetry.Voicechat17", "FFlagVoiceChatRobloxAudioDeviceUpdateRecordedBufferTelemetryEnabled" },
            { "Telemetry.Voicechat18", "FFlagVoiceChatSubscriptionsDroppedTelemetry" },
            { "Telemetry.Voicechat19", "FIntLuaVoiceChatAnalyticsPointsThrottle" },
            { "Telemetry.Voicechat20", "FIntVoiceChatPerfSensitiveTelemetryIntervalSeconds" },

            // Telemetry
            { "Telemetry.GraphicsQualityUsage", "DFFlagGraphicsQualityUsageTelemetry" },
            { "Telemetry.GpuVsCpuBound", "DFFlagGpuVsCpuBoundTelemetry" },
            { "Telemetry.RenderFidelity", "DFFlagSendRenderFidelityTelemetry" },
            { "Telemetry.RenderDistance", "DFFlagReportRenderDistanceTelemetry" },
            { "Telemetry.AudioPlugin", "DFFlagCollectAudioPluginTelemetry" },
            { "Telemetry.FmodErrors", "DFFlagEnableFmodErrorsTelemetry" },
            { "Telemetry.SoundLength", "DFFlagRccLoadSoundLengthTelemetryEnabled" },
            { "Telemetry.AssetRequestV1", "DFFlagReportAssetRequestV1Telemetry" },
            { "Telemetry.DeviceRAM", "DFFlagRobloxTelemetryAddDeviceRAMPointsV2" },
            { "Telemetry.V2FrameRateMetrics", "DFFlagEnableTelemetryV2FRMStats" },
            { "Telemetry.GlobalSkipUpdating", "DFFlagEnableSkipUpdatingGlobalTelemetryInfo2" },
            { "Telemetry.CallbackSafety", "DFFlagEmitSafetyTelemetryInCallbackEnable" },
            { "Telemetry.V2PointEncoding", "DFFlagRobloxTelemetryV2PointEncoding" },
            { "Telemetry.ReplaceSeparator", "DFFlagDSTelemetryV2ReplaceSeparator" },
            { "Telemetry.TelemetryV2Url", "DFStringTelemetryV2Url" },
            { "Telemetry.Protocol", "FFlagEnableTelemetryProtocol" },
            { "Telemetry.TelemetryService", "FFlagEnableTelemetryService1" },
            { "Telemetry.PropertiesTelemetry", "FFlagPropertiesEnableTelemetry" },
            { "Telemetry.OpenTelemetry", "FFlagOpenTelemetryEnabled" },
            { "Telemetry.FLogTelemetry", "FLogRobloxTelemetry" },

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

            // R core
            { "Network.RCore1", "DFIntSignalRCoreServerTimeoutMs"},
            { "Network.RCore2", "DFIntSignalRCoreRpcQueueSize"},
            { "Network.RCore3", "DFIntSignalRCoreHubBaseRetryMs"},
            { "Network.RCore4", "DFIntSignalRCoreHandshakeTimeoutMs"},
            { "Network.RCore5", "DFIntSignalRCoreKeepAlivePingPeriodMs"},
            { "Network.RCore6", "DFIntSignalRCoreHubMaxBackoffMs"},

            // Enable Large Replicator
            { "Network.EnableLargeReplicator", "FFlagLargeReplicatorEnabled6"},
            { "Network.LargeReplicatorWrite", "FFlagLargeReplicatorWrite5"},
            { "Network.LargeReplicatorRead", "FFlagLargeReplicatorRead5"},
            { "Network.EngineModule1", "FFlagGlobalSettingsEngineModule3"},
            { "Network.EngineModule2", "DFFlagLargeReplicatorEngineModule"},
            { "Network.SerializeRead", "FFlagLargeReplicatorSerializeRead2"},
            { "Network.SerializeWrite", "FFlagLargeReplicatorSerializeWrite2"},


            // Turn Off Ads
            { "UI.DisableAds1", "FFlagAdServiceEnabled" },
            { "UI.DisableAds2", "FFlagEnableSponsoredAdsGameCarouselTooltip3" },
            { "UI.DisableAds3", "FFlagEnableSponsoredAdsPerTileTooltipExperienceFooter" },
            { "UI.DisableAds4", "FFlagEnableSponsoredAdsSeeAllGamesListTooltip" },
            { "UI.DisableAds5", "FFlagEnableSponsoredTooltipForAvatarCatalog2" },
            { "UI.DisableAds6", "FFlagLuaAppSponsoredGridTiles" },
            
            // Fullscreen bar
            { "UI.FullscreenTitlebarDelay", "FIntFullscreenTitleBarTriggerDelayMillis" },

            // Useless
            { "UI.Menu.Style.V2Rollout", "FIntNewInGameMenuPercentRollout3" },
            { "UI.Menu.Style.EnableV4.1", "FFlagEnableInGameMenuControls" },
            { "UI.Menu.Style.EnableV4.2", "FFlagEnableInGameMenuModernization" },
            { "UI.Menu.Style.EnableV4Chrome", "FFlagEnableInGameMenuChrome" },
            { "UI.Menu.Style.ReportButtonCutOff", "FFlagFixReportButtonCutOff" },

            // Display Fps
            { "Rendering.DisplayFps", "FFlagDebugDisplayFPS" },

            // No Shadows
            { "Rendering.Pause.Voxelizer", "DFFlagDebugPauseVoxelizer" },
            { "Rendering.ShadowIntensity", "FIntRenderShadowIntensity" },
            { "Rendering.ShadowMapBias", "FIntRenderShadowmapBias" },

            // Render Occlusion
            { "Rendering.Occlusion1", "DFFlagUseVisBugChecks" },
            { "Rendering.Occlusion2", "FFlagEnableVisBugChecks27" },
            { "Rendering.Occlusion3", "FFlagVisBugChecksThreadYield" },

            // No More Middle
            { "UI.RemoveMiddle", "FFlagUIBloxMoveDetailsPageToLuaApps" },

            // Distance Rendering
            { "Rendering.Distance.Chunks", "DFIntDebugRestrictGCDistance" },

            // Romark
            { "Rendering.Start.Graphic", "FIntRomarkStartWithGraphicQualityLevel" },

            // Chrome ui
            { "UI.Menu.ChromeUI", "FFlagEnableInGameMenuChromeABTest4" },
            { "UI.Menu.ChromeUI2", "FFlagEnableInGameMenuChrome" },

            // Preferred GPU
            { "System.PreferredGPU", "FStringDebugGraphicsPreferredGPUName"},
            { "System.DXT", "FStringGraphicsDisableUnalignedDxtGPUNameBlacklist"},
            { "System.BypassVulkan", "FStringVulkanBuggyRenderpassList2"},

            // Prerender
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
            { "UI.Menu.Style.ABTest.4", "FFlagEnableInGameMenuChromeABTest4" },

            
            // Old ChromeUI
            { "UI.OldChromeUI1", "FFlagEnableHamburgerIcon"},
            { "UI.OldChromeUI2", "FFlagEnableUnibarV4IA"},
            { "UI.OldChromeUI3", "FFlagEnableAlwaysOpenUnibar2"},
            { "UI.OldChromeUI4", "FFlagUseNewUnibarIcon"},
            { "UI.OldChromeUI5", "FFlagUseSelfieViewFlatIcon"},
            { "UI.OldChromeUI6", "FFlagUnibarRespawn"},
            { "UI.OldChromeUI7", "FFlagEnableChromePinIntegrations2"},
            { "UI.OldChromeUI8", "FFlagEnableUnibarMaxDefaultOpen"},
            { "UI.OldChromeUI9", "FFlagUpdateHealthBar"},
            { "UI.OldChromeUI10", "FFlagUseNewPinIcon"},

            // Cache Size Improvement
            { "Cache.Increase1",  "FFlagClearCacheableContentProviderOnGameLaunch" },
            { "Cache.Increase2",  "DFFlagAlwaysSkipDiskCache" },
            { "Cache.Increase3",  "FFlagUseCachedAudibilityMeasurements" },
            { "Cache.Increase4",  "DFIntCachedPatchLoadDelayMilliseconds" },
            { "Cache.Increase5",  "DFIntHttpCacheCleanScheduleAfterMs" },
            { "Cache.Increase6",  "DFIntHttpCacheCleanUpToAvailableSpaceMiB" },
            { "Cache.Increase7",  "DFIntHttpCacheAsyncWriterMaxPendingSize" },
            { "Cache.Increase8",  "DFIntHttpCacheEvictionExemptionMapMaxSize" },
            { "Cache.Increase9",  "DFIntHttpCacheReportSlowWritesMinDuration" },
            { "Cache.Increase10", "DFIntMemCacheMaxCapacityMB" },
            { "Cache.Increase11", "DFIntFileCacheReserveSize" },
            { "Cache.Increase12", "DFIntThirdPartyInMemoryCacheCapacity" },
            { "Cache.Increase13", "DFIntSoundServiceCacheCleanupMaxAgeDays" },
            { "Cache.Increase14", "DFIntUserIdPlayerNameCacheLifetimeSeconds" },
            { "Cache.Increase15", "DFIntAssetCacheErrorLogHundredthsPercent" },
            { "Cache.Increase16", "DFFlagHttpTrackSyncWriteCachePhase" },
            { "Cache.Increase17", "DFIntHttpCachePerfSamplingRate" },
            { "Cache.Increase18", "DFIntHttpCachePerfHundredthsPercent" },
            { "Cache.Increase19", "DFIntReportCacheDirSizesHundredthsPercent" },

            // Block Tencent
            { "Telemetry.Tencent1", "FStringTencentAuthPath" },
            { "Telemetry.Tencent2", "FLogTencentAuthPath" },
            { "Telemetry.Tencent3", "FStringXboxExperienceGuidelinesUrl" },
            { "Telemetry.Tencent4", "FStringExperienceGuidelinesExplainedPageUrl" },
            { "Telemetry.Tencent5", "DFFlagPolicyServiceReportIsNotSubjectToChinaPolicies" },
            { "Telemetry.Tencent6", "DFFlagPolicyServiceReportDetailIsNotSubjectToChinaPolicies" },
            { "Telemetry.Tencent7", "DFIntPolicyServiceReportDetailIsNotSubjectToChinaPoliciesHundredthsPercentage" },
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
            { MSAAMode.x0, "0" },
            { MSAAMode.x1, "1" },
            { MSAAMode.x2, "2" },
            { MSAAMode.x4, "4" },
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

        public static IReadOnlyDictionary<DynamicResolution, string?> DynamicResolutions => new Dictionary<DynamicResolution, string?>
        {
            { DynamicResolution.Default, null },
            { DynamicResolution.Resolution2, "77" },
            { DynamicResolution.Resolution3, "230" },
            { DynamicResolution.Resolution4, "410" },
            { DynamicResolution.Resolution5, "922" },
            { DynamicResolution.Resolution6, "2074" },
            { DynamicResolution.Resolution7, "3686" },
            { DynamicResolution.Resolution8, "8294" },
            { DynamicResolution.Resolution9, "33178 " },
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
            { RomarkStart.Bar1, "1" },
            { RomarkStart.Bar2, "2" },
            { RomarkStart.Bar3, "3" },
            { RomarkStart.Bar4, "4" },
            { RomarkStart.Bar5, "5" },
            { RomarkStart.Bar6, "6" },
            { RomarkStart.Bar7, "7" },
            { RomarkStart.Bar8, "8" },
            { RomarkStart.Bar9, "9" },
            { RomarkStart.Bar10, "10" }
        };

        public static IReadOnlyDictionary<Presents, string?> PresentsStartMappings => new Dictionary<Presents, string?>
        {
            { Presents.Default, null },
            { Presents.Stoofs, "1" }
        };

        public static IReadOnlyDictionary<QualityLevel, string?> QualityLevels => new Dictionary<QualityLevel, string?>
        {
            { QualityLevel.Disabled, null },
            { QualityLevel.Level1, "1" },
            { QualityLevel.Level2, "2" },
            { QualityLevel.Level3, "3" },
            { QualityLevel.Level4, "4" },
            { QualityLevel.Level5, "5" },
            { QualityLevel.Level6, "6" },
            { QualityLevel.Level7, "7" },
            { QualityLevel.Level8, "8" },
            { QualityLevel.Level9, "9" },
            { QualityLevel.Level10, "10" },
            { QualityLevel.Level11, "11" },
            { QualityLevel.Level12, "12" },
            { QualityLevel.Level13, "13" },
            { QualityLevel.Level14, "14" },
            { QualityLevel.Level15, "15" },
            { QualityLevel.Level16, "16" },
            { QualityLevel.Level17, "17" },
            { QualityLevel.Level18, "18" },
            { QualityLevel.Level19, "19" },
            { QualityLevel.Level20, "20" },
            { QualityLevel.Level21, "21" }
        };

public static IReadOnlyDictionary<RefreshRate, string?> RefreshRates => new Dictionary<RefreshRate, string?>
{
    { RefreshRate.Default, null },
    { RefreshRate.RefreshRate75, "75" },
    { RefreshRate.RefreshRate85, "80" },
    { RefreshRate.RefreshRate90, "90" },
    { RefreshRate.RefreshRate100, "100" },
    { RefreshRate.RefreshRate120, "120" },
    { RefreshRate.RefreshRate144, "144" },
    { RefreshRate.RefreshRate165, "165" },
    { RefreshRate.RefreshRate180, "180" },
    { RefreshRate.RefreshRate200, "200" },
    { RefreshRate.RefreshRate240, "240" },
    { RefreshRate.RefreshRate360, "360" },
};


        public static IReadOnlyDictionary<Shader, string?> Shaders => new Dictionary<Shader, string?>
        {
            { Shader.Disabled, null },
            { Shader.x1, "-140000000" },
            { Shader.x2, "-340000000" },
            { Shader.x3, "-640000000" }
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
                // Removed Debug.Assert to prevent crash in Release mode
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
            { "FFlagHandleAltEnterFullscreenManually", "False" },
        }
            ;

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
