using Markdig.Extensions.CustomContainers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Voidstrap.Integrations;
using Voidstrap.UI.ViewModels.Settings;
using Wpf.Ui.Mvvm.Contracts;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    public partial class FastFlagsPage // meowr
    {
        private bool _initialLoad = false;
        private bool _isLoading = true;
        public ObservableCollection<FFlagItem> FFlags { get; } = new();
        public ObservableCollection<NvidiaFFlag> CustomFFlags { get; } = new();

        private FastFlagsViewModel _viewModel = null!;
        private static readonly string SavedFilePath =
            Path.Combine(Paths.Base, "Settings.ini");

        private string _editorBaseProfile = "Default Settings";
        private bool _editorModified;

        public class NvidiaFFlag : INotifyPropertyChanged, IDataErrorInfo
        {
            private string _name = string.Empty;
            private string _value = string.Empty;

            public string Name
            {
                get => _name;
                set
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }

            public string Value
            {
                get => _value;
                set
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }

            public string Error => null;

            public string this[string columnName]
            {
                get
                {
                    if (columnName == nameof(Name) && string.IsNullOrWhiteSpace(Name))
                        return "Flag name is required";

                    if (columnName == nameof(Value) && string.IsNullOrWhiteSpace(Value))
                        return "Value is required";

                    return null;
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string prop)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public FastFlagsPage()
        {
            SetupViewModel();
            InitializeComponent();
            Loaded += FastFlagsPage_Loaded;
            Loaded += async (_, _) => await LoadFFlagsAsync();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private async Task LoadFFlagsAsync()
        {
            const string url =
                "https://raw.githubusercontent.com/LeventGameing/allowlist/main/allowlist.json";

            try
            {
                string json = await _httpClient.GetStringAsync(url).ConfigureAwait(false);

                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (dict == null)
                    throw new InvalidOperationException("FFlags JSON returned null.");

                await Dispatcher.InvokeAsync(() =>
                {
                    FFlags.Clear();

                    foreach (var kv in dict)
                    {
                        FFlags.Add(new FFlagItem
                        {
                            Name = kv.Key,
                            Value = kv.Value.ValueKind switch
                            {
                                JsonValueKind.String => kv.Value.GetString(),
                                JsonValueKind.Number => kv.Value.GetRawText(),
                                JsonValueKind.True => "true",
                                JsonValueKind.False => "false",
                                _ => kv.Value.GetRawText()
                            }
                        });
                    }

                    DataGrid.ItemsSource = null;
                    DataGrid.ItemsSource = FFlags;
                });
            }
            catch (HttpRequestException ex)
            {
                Frontend.ShowMessageBox(
                    $"Network error while loading FFlags:\n{ex.Message}");
            }
            catch (JsonException ex)
            {
                Frontend.ShowMessageBox(
                    $"Invalid FFlags JSON format:\n{ex.Message}");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(
                    $"Failed to load FFlags:\n{ex}");
            }
        }

        public class FFlagItem
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        private void FastFlagsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
        }

        private void OpenCustomEditor_Click(object sender, RoutedEventArgs e)
        {
            if (IsVulkanSelected())
            {
                Frontend.ShowMessageBox(
                    "Some FFlags may not work while Vulkan Rendering Mode is enabled in the NVIDIA FFlags Editor.\n\n" +
                    "Please switch to DirectX/Direct3D Rendering Mode."
                );
            }

            string basePreset = "Default Settings";

            if (ProfileComboBox.SelectedItem is ComboBoxItem item)
                basePreset = item.Content?.ToString() ?? basePreset;

            NavigationService.Navigate(new NvidiaFFlagEditorPage());
        }

        private bool IsVulkanSelected()
        {
            if (_viewModel?.SelectedRenderingMode == null)
                return false;

            string modeText = _viewModel.SelectedRenderingMode.ToString();
            return modeText.Contains("Vulkan", StringComparison.OrdinalIgnoreCase);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (ProfileComboBox.Items.Count > 0)
            {
                ProfileComboBox.SelectedIndex = 0;
            }
            if (!_initialLoad)
            {
                _initialLoad = true;
                return;
            }

            SetupViewModel();
        }

        private void SetupViewModel()
        {
            _viewModel = new FastFlagsViewModel();

            _viewModel.OpenFlagEditorEvent += OpenFlagEditor;
            _viewModel.RequestPageReloadEvent += (_, _) => SetupViewModel();
            DataContext = _viewModel;
        }

        private void ValidateIntInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            e.Handled = !Regex.IsMatch(newText, @"^[\+\-]?[0-9]*$");
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(ProfileTextBox.Text))
            {
                Clipboard.SetText(ProfileTextBox.Text);
            }
        }

        private void OpenFlagEditor(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is INavigationWindow window)
            {
                window.Navigate(typeof(FastFlagEditorPage));
            }
        }

        private void ValidateInt32(object sender, TextCompositionEventArgs e) => e.Handled = e.Text != "-" && !Int32.TryParse(e.Text, out int _);

        private void ValidateUInt32(object sender, TextCompositionEventArgs e) => e.Handled = !UInt32.TryParse(e.Text, out uint _);

        private void ToggleSwitch_Checked(object sender, RoutedEventArgs e)
        {
        }

        private void ToggleSwitch_Checked_1(object sender, RoutedEventArgs e)
        {
        }

        private void OptionControl_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void OptionControl_Loaded_1(object sender, RoutedEventArgs e)
        {
        }

        private async void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileComboBox?.SelectedItem == null) return;
            if (ProfileTextBox == null || ProfileImage == null) return;

            if (ProfileComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selected = selectedItem.Content.ToString();
                string imageUrl = "";
                string profileFlags = "";
                switch (selected)
                {
                    case "Allfortavion List":
                        imageUrl = "https://media.discordapp.net/attachments/1419822748582023360/1419822749651828787/image.png?ex=68ec34fa&is=68eae37a&hm=d94a9a944f822c1f03aea25b43aee877e65fc506d6cee2eb8394f95799a2dc54&=&format=webp&quality=lossless";
                        profileFlags = @"{
  ""DFIntCSGLevelOfDetailSwitchingDistance"": ""0"",
  ""FFlagDebugDisableTelemetryEventIngest"": ""True"",
  ""FFlagBetaBadgeLearnMoreLinkFormview"": ""False"",
  ""DFIntGraphicsOptimizationModeMinFrameTimeTargetMs"": ""17"",
  ""DFIntGraphicsOptimizationModeFRMFrameRateTarget"": ""75"",
  ""DFIntCSGLevelOfDetailSwitchingDistanceL23"": ""0"",
  ""DFIntTaskSchedulerTargetFps"": ""9999"",
  ""DFIntAnimationLodFacsDistanceMax"": ""0"",
  ""FFlagHandleAltEnterFullscreenManually"": ""False"",
  ""DFIntGraphicsOptimizationModeMaxFrameTimeTargetMs"": ""14"",
  ""DFIntAnimationLodFacsDistanceMin"": ""0"",
  ""FFlagDebugDisableTelemetryV2Counter"": ""True"",
  ""DFIntCSGLevelOfDetailSwitchingDistanceL12"": ""0"",
  ""DFIntMaxFrameBufferSize"": ""4"",
  ""DFIntCSGLevelOfDetailSwitchingDistanceL34"": ""0"",
  ""DFIntDebugFRMQualityLevelOverride"": ""1"",
  ""DFIntAnimationLodFacsVisibilityDenominator"": ""0"",
  ""DFFlagRenderHighlightManagerPrepare"": ""True"",
  ""DFFlagTextureQualityOverrideEnabled"": ""True"",
  ""FFlagTaskSchedulerLimitTargetFpsTo2402"": ""False"",
  ""FFlagDebugDisableTelemetryEphemeralCounter"": ""True"",
  ""FFlagDebugDisableTelemetryEphemeralStat"": ""True"",
  ""DFIntTextureQualityOverride"": ""0"",
  ""DFIntNumAssetsMaxToPreload"": ""9999999"",
  ""DFIntAssetPreloading"": ""9999999"",
  ""DFFlagDebugPauseVoxelizer"": ""True"",
  ""DFIntPerformanceControlTextureQualityBestUtility"": ""-1"",
  ""FFlagRenderDynamicResolutionScale9"": ""True"",
  ""FFlagDebugRenderingSetDeterministic"": ""True"",
  ""FFlagDebugDisableTelemetryV2Event"": ""True"",
  ""FFlagDebugDisableTelemetryV2Stat"": ""True"",
  ""FFlagRenderDebugCheckThreading2"": ""True"",
  ""FFlagDebugDisableTelemetryPoint"": ""True"",
  ""FFlagDebugCheckRenderThreading"": ""True"",
  ""FFlagControlBetaBadgeWithGuac"": ""False"",
  ""FFlagVideoFixSoundVolumeRange"": ""True"",
  ""FFlagRenderUnifiedLighting16"": ""False"",
  ""FFlagUserShowGuiHideToggles"": ""True"",
  ""FFlagEnableQuickGameLaunch"": ""True"",
  ""FFlagFastGPULightCulling3"": ""True"",
  ""FFlagRenderNoLowFrmBloom"": ""False"",
  ""FFlagTopBarUseNewBadge"": ""False"",
  ""FFlagAdServiceEnabled"": ""False"",
  ""FFlagVoiceBetaBadge"": ""False"",
  ""FFlagDisablePostFx"": ""True"",
  ""FFlagEnableFPSAndFrameTime"": ""True"",
  ""FFlagDebugSkyGray"": ""True"",
  ""FFlagRenderFixFog"": ""True"",
  ""FIntFullscreenTitleBarTriggerDelayMillis"": ""3600000"",
  ""FIntRomarkStartWithGraphicQualityLevel"": ""1"",
  ""FIntGrassMovementReducedMotionFactor"": ""0"",
  ""FIntDebugTextureManagerSkipMips"": ""10"",
  ""FIntRenderLocalLightUpdatesMin"": ""6"",
  ""FIntRenderLocalLightUpdatesMax"": ""8"",
  ""FIntRenderGrassDetailStrands"": ""0"",
  ""FIntCameraMaxZoomDistance"": ""9999"",
  ""FIntRenderLocalLightFadeInMs"": ""0"",
  ""FIntTerrainArraySliceSize"": ""0"",
  ""FIntRenderShadowIntensity"": ""0"",
  ""FIntDebugForceMSAASamples"": ""0"",
  ""FIntFRMMaxGrassDistance"": ""0"",
  ""FIntFRMMinGrassDistance"": ""0"",
  ""FStringVoiceBetaBadgeLearnMoreLink"": ""null"",
  ""GuiHidingApiSupport2"": ""True"",
  ""DFIntGraphicsOptimizationModeMinFrameTimeTargetMs"": ""13"",
  ""FFlagGameBasicSettingsFramerateCap5"": ""False"",
  ""FFlagAlwaysShowVRToggleV3"": ""False""
}";
                        break;

                    case "YNR Niffty":
                        imageUrl = "https://media.discordapp.net/attachments/1400473539789983815/1400970608631611563/fast_flag.PNG?ex=68ec2d8d&is=68eadc0d&hm=f21a4d5a3041b1ab598ef4472b82f9cd87242017b5827e18b70ba892178dfbc5&=&format=webp&quality=lossless";
                        profileFlags = @"{
    ""DFFlagRenderHighlightManagerPrepare"": ""True"",
    ""FFlagRenderDynamicResolutionScale9"": ""True"",
    ""FFlagMovePrerender"": ""True"",
    ""FIntGrassMovementReducedMotionFactor"": ""0"",
    ""FFlagRenderNoLowFrmBloom"": ""False"",
    ""FFlagRenderFixFog"": ""True"",
    ""FFlagDebugCheckRenderThreading"": ""True"",
    ""FFlagRenderDebugCheckThreading2"": ""True"",
    ""FFlagDebugRenderingSetDeterministic"": ""True"",
    ""FIntRomarkStartWithGraphicQualityLevel"": ""1"",
    ""FIntRenderShadowIntensity"": ""0"",
    ""DFIntDebugFRMQualityLevelOverride"": ""1"",
    ""FIntRenderLocalLightUpdatesMax"": ""8"",
    ""DFIntS2PhysicsSenderRate"": ""6"",
    ""FIntRenderLocalLightUpdatesMin"": ""6"",
    ""FIntRenderLocalLightFadeInMs"": ""0"",
    ""FFlagDisablePostFx"": ""True"",
    ""DFFlagDebugPauseVoxelizer"": ""True"",
    ""FFlagDebugSkyGray"": ""True"",
    ""DFIntCSGLevelOfDetailSwitchingDistance"": ""0"",
    ""DFIntCSGLevelOfDetailSwitchingDistanceL12"": ""0"",
    ""DFIntCSGLevelOfDetailSwitchingDistanceL23"": ""0"",
    ""DFIntCSGLevelOfDetailSwitchingDistanceL34"": ""0"",
    ""FFlagFastGPULightCulling3"": ""True"",
    ""DFIntMaxFrameBufferSize"": ""4"",
    ""FIntTerrainArraySliceSize"": ""4"",
    ""DFFlagTextureQualityOverrideEnabled"": ""True"",
    ""DFIntTextureQualityOverride"": ""0"",
    ""DFIntPerformanceControlTextureQualityBestUtility"": ""-1"",
    ""DFIntTextureCompositorActiveJobs"": ""0"",
    ""FIntDebugTextureManagerSkipMips"": ""8"",
    ""FIntFRMMinGrassDistance"": ""0"",
    ""FIntFRMMaxGrassDistance"": ""0"",
    ""FIntRenderGrassDetailStrands"": ""0"",
    ""FIntDebugForceMSAASamples"": ""0"",
    ""FFlagTaskSchedulerLimitTargetFpsTo2402"": ""False"",
    ""DFIntTaskSchedulerTargetFps"": ""9999"",
    ""FIntFullscreenTitleBarTriggerDelayMillis"": ""3600000"",
    ""FFlagUserShowGuiHideToggles"": ""True"",
    ""GuiHidingApiSupport2"": ""True"",
    ""FFlagAdServiceEnabled"": ""False"",
    ""FFlagDebugDisableTelemetryEphemeralCounter"": ""True"",
    ""FFlagDebugDisableTelemetryEphemeralStat"": ""True"",
    ""FFlagDebugDisableTelemetryEventIngest"": ""True"",
    ""FFlagDebugDisableTelemetryPoint"": ""True"",
    ""FFlagDebugDisableTelemetryV2Counter"": ""True"",
    ""FFlagDebugDisableTelemetryV2Event"": ""True"",
    ""FFlagDebugDisableTelemetryV2Stat"": ""True"",
    ""FFlagEnableQuickGameLaunch"": ""True"",
    ""DFIntNumAssetsMaxToPreload"": ""9999999"",
    ""DFIntAssetPreloading"": ""9999999"",
    ""DFIntAnimationLodFacsDistanceMin"": ""0"",
    ""DFIntAnimationLodFacsDistanceMax"": ""0"",
    ""DFIntAnimationLodFacsVisibilityDenominator"": ""0"",
    ""FIntCameraMaxZoomDistance"": ""9999"",
    ""FFlagVoiceBetaBadge"": ""False"",
    ""FFlagTopBarUseNewBadge"": ""False"",
    ""FFlagBetaBadgeLearnMoreLinkFormview"": ""False"",
    ""FFlagControlBetaBadgeWithGuac"": ""False"",
    ""FStringVoiceBetaBadgeLearnMoreLink"": ""null""
}";
                        break;

                    case "Vitor List":
                        imageUrl = "https://media.discordapp.net/attachments/1415660795282329711/1415660795613810749/RobloxScreenShot20250911_073307693.png?ex=68ec3a1b&is=68eae89b&hm=e0ae55efeb3a37563fa1199f01955ffcd494b5ff4d0b514ffafc7ef788f84cea&=&format=webp&quality=lossless";
                        profileFlags = @"{
    ""DFFlagVoiceChatPossibleDuplicateSubscriptionsTelemetry"": ""False"",
    ""DFFlagTeleportClientAssetPreloadingDoingExperiment2"": ""True"",
    ""DFFlagAcceleratorUpdateOnPropsAndValueTimeChange"": ""True"",
    ""DFFlagTeleportClientAssetPreloadingEnabledIXP2"": ""True"",
    ""DFFlagEnableSkipUpdatingGlobalTelemetryInfo2"": ""False"",
    ""DFFlagPerformanceControlEnableMemoryProbing3"": ""True"",
    ""DFFlagTeleportClientAssetPreloadingEnabled9"": ""True"",
    ""DFFlagSimOptimizeGeometryChangedAssemblies3"": ""True"",
    ""DFFlagRobloxTelemetryAddDeviceRAMPointsV2"": ""False"",
    ""DFFlagEmitSafetyTelemetryInCallbackEnable"": ""False"",
    ""DFFlagRccLoadSoundLengthTelemetryEnabled"": ""False"",
    ""DFFlagReplicatorCheckReadTableCollisions"": ""True"",
    ""DFFlagWindowsWebViewTelemetryEnabled"": ""False"",
    ""DFFlagReplicatorSeparateVarThresholds"": ""True"",
    ""DFFlagGraphicsQualityUsageTelemetry"": ""False"",
    ""DFFlagReportRenderDistanceTelemetry"": ""False"",
    ""DFFlagReportAssetRequestV1Telemetry"": ""False"",
    ""DFFlagSimSmoothedRunningController2"": ""True"",
    ""DFFlagCollectAudioPluginTelemetry"": ""False"",
    ""DFFlagRobloxTelemetryAddDeviceRAM"": ""False"",
    ""DFFlagHumanoidReplicateSimulated2"": ""True"",
    ""DFFlagClampIncomingReplicationLag"": ""True"",
    ""DFFlagEnableTelemetryV2FRMStats"": ""False"",
    ""DFFlagSolverStateReplicatedOnly2"": ""True"",
    ""DFFlagSampleAndRefreshRakPing"": ""True"",
    ""DFFlagReplicateCreateToPlayer"": ""True"",
    ""DFFlagDebugOverrideDPIScale"": ""False"",
    ""DFFlagDebugSkipMeshVoxelizer"": ""True"",
    ""DFFlagReplicatorDisKickSize"": ""True"",
    ""DFFlagNewPackageAnalytics"": ""False"",
    ""DFFlagLightGridSimdNew3"": ""True"",
    ""DFFlagRakNetEnablePoll"": ""True"",
    ""DFFlagUseVisBugChecks"": ""True"",
    ""DFFlagDebugPerfMode"": ""True"",

    ""DFIntPolicyServiceReportDetailIsNotSubjectToChinaPoliciesHundredthsPercentage"": ""10000"",
    ""DFIntContentProviderPreloadHangTelemetryHundredthsPercentage"": ""0"",
    ""DFIntRakNetApplicationFeedbackScaleUpFactorHundredthPercent"": ""0"",
    ""DFIntTeleportClientAssetPreloadingHundredthsPercentage2"": ""1000"",
    ""DFIntVoiceChatTaskStatsTelemetryThrottleHundrethsPercent"": ""0"",
    ""DFIntWindowsWebViewTelemetryThrottleHundredthsPercent"": ""0"",
    ""DFIntRakNetApplicationFeedbackScaleUpThresholdPercent"": ""0"",
    ""DFIntNetworkStopProducingPacketsToProcessThresholdMs"": ""2"",
    ""DFIntJoinDataItemEstimatedCompressionRatioHundreths"": ""0"",
    ""DFIntGraphicsOptimizationModeMaxFrameTimeTargetMs"": ""18"",
    ""DFIntGraphicsOptimizationModeMinFrameTimeTargetMs"": ""17"",
    ""DFIntMacWebViewTelemetryThrottleHundredthsPercent"": ""0"",
    ""DFIntGraphicsOptimizationModeFRMFrameRateTarget"": ""60"",
    ""DFIntMaxReceiveToDeserializeLatencyMilliseconds"": ""10"",
    ""DFIntNetworkClusterPacketCacheNumParallelTasks"": ""12"",
    ""DFIntMemoryUtilityCurveBaseHundrethsPercent"": ""10000"",
    ""DFIntClusterEstimatedCompressionRatioHundredths"": ""0"",
    ""DFIntMegaReplicatorNetworkQualityProcessorUnit"": ""10"",
    ""DFIntTimestepArbiterHumanoidTurningVelThreshold"": ""1"",
    ""DFIntTrackCountryRegionAPIHundredthsPercent"": ""10000"",
    ""DFIntTimestepArbiterHumanoidLinearVelThreshold"": ""1"",
    ""DFIntClientNetworkInfluxHundredthsPercentage"": ""0"",
    ""DFIntPerformanceControlReportingPeriodInMs"": ""700"",
    ""DFIntPerformanceControlFrameTimeMaxUtility"": ""-1"",
    ""DFIntMaxWaitTimeBeforeForcePacketProcessMS"": ""5"",
    ""DFIntAnimationLodFacsVisibilityDenominator"": ""0"",
    ""DFIntClientPacketHealthyAllocationPercent"": ""20"",
    ""DFIntNetworkInProcessLimitGameplayMsClient"": ""0"",
    ""DFIntReplicationDataCacheNumParallelTasks"": ""12"",
    ""DFIntRaknetBandwidthPingSendEveryXSeconds"": ""1"",
    ""DFIntCSGLevelOfDetailSwitchingDistanceL23"": ""0"",
    ""DFIntMemoryUtilityCurveTotalMemoryReserve"": ""0"",
    ""DFIntCSGLevelOfDetailSwitchingDistanceL34"": ""0"",
    ""DFIntCSGLevelOfDetailSwitchingDistanceL12"": ""0"",
    ""DFIntInitialAccelerationLatencyMultTenths"": ""1"",
    ""DFIntMaxProcessPacketsStepsPerCyclic"": ""5000"",
    ""DFIntOcclusionFresnelConsensusNumerator"": ""2"",
    ""DFIntNetworkQualityResponderMaxWaitTime"": ""1"",
    ""DFIntClientPacketMaxFrameMicroseconds"": ""200"",
    ""DFIntClientPacketExcessMicroseconds"": ""1000"",
    ""DFIntMaxProcessPacketsStepsAccumulated"": ""0"",
    ""DFIntAnalyticsServiceMonitoringPeriod"": ""0"",
    ""DFIntDebugPerformanceControlFrameTime"": ""2"",
    ""DFIntWaitOnUpdateNetworkLoopEndedMS"": ""100"",
    ""DFIntPhysicsReceiveNumParallelTasks"": ""12"",
    ""DFIntMegaReplicatorNumParallelTasks"": ""12"",
    ""DFIntLargePacketQueueSizeCutoffMB"": ""1000"",
    ""DFIntMemoryUtilityCurveNumSegments"": ""100"",
    ""DFIntMaxProcessPacketsJobScaling"": ""10000"",
    ""DFIntBatchThumbnailResultsSizeCap"": ""200"",
    ""DFIntTaskSchedulerJobInGameThreads"": ""12"",
    ""DFIntPerformanceControlFrameTimeMax"": ""4"",
    ""DFIntInterpolationNumParallelTasks"": ""12"",
    ""DFIntSoundVelocitySmoothingOldRatio"": ""5"",
    ""DFIntSoundVelocitySmoothingNewRatio"": ""2"",
    ""DFIntDebugFRMQualityLevelOverride"": ""10"",
    ""DFIntTargetTimeDelayFacctorTenths"": ""15"",
    ""DFIntOcclusionShelfScalarNumerator"": ""2"",
    ""DFIntNetworkSchemaCompressionRatio"": ""0"",
    ""DFIntClientPacketMinMicroseconds"": ""50"",
    ""DFIntNetworkQualityResponderUnit"": ""10"",
    ""DFIntTaskSchedulerJobInitThreads"": ""12"",
    ""DFIntOcclusionGainScalarNumerator"": ""2"",
    ""DFIntAnimationLodFacsDistanceMax"": ""0"",
    ""DFIntAnimationLodFacsDistanceMin"": ""0"",
    ""DFIntOcclusionFresnelEllipsoids"": ""6"",
    ""DFIntWaitOnRecvFromLoopEndedMS"": ""10"",
    ""DFIntHttpBatchApi_cacheDelayMs"": ""15"",
    ""DFIntRenderClampRoughnessMax"": ""225"",
    ""DFIntCodecMaxOutgoingFrames"": ""1000"",
    ""DFIntMaxDataPacketPerSend"": ""100000"",
    ""DFIntCodecMaxIncomingPackets"": ""100"",
    ""DFIntMaxAcceptableUpdateDelay"": ""1"",
    ""DFIntJoinDataCompressionLevel"": ""0"",
    ""DFIntServerFramesBetweenJoins"": ""1"",
    ""DFIntHttpAnalyticsMaxHistory"": ""0"",
    ""DFIntHttpBatchApi_maxWaitMs"": ""40"",
    ""DFIntRakNetResendRttMultiple"": ""1"",
    ""DFIntHttpBatchApi_minWaitMs"": ""5"",
    ""DFIntBufferCompressionLevel"": ""0"",
    ""DFIntRakNetNakResendDelayMs"": ""1"",
    ""DFIntClientPacketMaxDelayMs"": ""1"",
    ""DFIntRakNetSelectTimeoutMs"": ""1"",
    ""DFIntS2PhysicsSenderRate"": ""260"",
    ""DFIntConnectionMTUSize"": ""1478"",
    ""DFIntMaxFrameBufferSize"": ""3"",
    ""DFIntDataSenderRate"": ""38760"",
    ""DFIntCharacterLoadTime"": ""1"",
    ""DFIntRakNetLoopMs"": ""1"",

    ""DFStringRobloxAnalyticsURL"": ""null"",
    ""DFStringTelemetryV2Url"": ""0.0.0.0"",

    ""FFlagRenderInstanceClusterRetryPartInvalidationWhenMeshNotReady4"": ""False"",
    ""FFlagEnableTexasU18VPCForInExperienceBundleRobuxUpsellFlow"": ""False"",
    ""FFlagEnableTexasU18VPCForInExperienceRobuxUpsellFlowV2"": ""False"",
    ""FFlagEnableVpcForInExperienceSubscriptionPurchase"": ""False"",
    ""FFlagDebugNextGenReplicatorEnabledWriteCFrameColor"": ""True"",
    ""FFlagPreComputeAcceleratorArrayForSharingTimeCurve"": ""True"",
    ""FFlagFixTextureCompositorFramebufferManagement2"": ""True"",
    ""FFlagEnablePartyVoiceOnlyForUnfilteredThreads"": ""False"",
    ""FFlagVngLogoutGlobalAppSessionsOnConversion"": ""False"",
    ""FFlagEnableVpcForInExperiencePremiumUpsell"": ""False"",
    ""FFlagUpdateHTTPCookieStorageFromWKWebView"": ""False"",
    ""FFlagUserCameraControlLastInputTypeUpdate"": ""False"",
    ""FFlagEnablePartyVoiceOnlyForEligibleUsers"": ""False"",
    ""FFlagContentProviderPreloadHangTelemetry"": ""False"",
    ""FFlagVideoServiceAddHardwareCodecMetrics"": ""True"",
    ""FFlagChatTranslationEnableSystemMessage"": ""False"",
    ""FFlagNewCameraControls_SpeedAdjustEnum"": ""False"",
    ""FFlagHandleAltEnterFullscreenManually"": ""False"",
    ""FFlagLuaAppLegacyInputSettingRefactor"": ""True"",
    ""FFlagEnableRewardedVideoInAdService15"": ""True"",
    ""FFlagUseDecomposedVPCForChargebacks"": ""False"",
    ""FFlagBetaBadgeLearnMoreLinkFormview"": ""False"",
    ""FFlagEnableInGameMenuDurationLogger"": ""False"",
    ""FFlagReportGpuLimitedToPerfControl"": ""False"",
    ""FFlagDebugRenderCollectGpuCounters"": ""True"",
    ""FFlagEnableAmpVPCUpsellSupportV3"": ""False"",
    ""FFlagDebugForceFSMCPULightCulling"": ""True"",
    ""FFlagEnableZstdForClientSettings"": ""False"",
    ""FFlagRenderSkipReadingShaderData"": ""False"",
    ""FFlagSyncWebViewCookieToEngine2"": ""False"",
    ""FFlagUserBetterInertialScrolling"": ""True"",
    ""FFlagPropertiesEnableTelemetry"": ""False"",
    ""FFlagImproveShiftLockTransition"": ""True"",
    ""FFlagMessageBusCallOptimization"": ""True"",
    ""FFlagGraphicsEnableD3D10Compute"": ""True"",
    ""FFlagStylingFasterTagProcessing"": ""True"",
    ""FFlagDebugCheckRenderThreading"": ""True"",
    ""FFlagControlBetaBadgeWithGuac"": ""False"",
    ""FFlagEnableTelemetryProtocol"": ""False"",
    ""FFlagEnableTelemetryService1"": ""False"",
    ""FFlagQuaternionPoseCorrection"": ""True"",
    ""FFlagEnableNudgeAnalyticsV2"": ""False"",
    ""FFlagLuaAppHomeVngAppUpsell"": ""False"",
    ""FFlagLuaMenuPerfImprovements"": ""True"",
    ""FFlagRobloxInputUsesRuntime2"": ""True"",
    ""FFlagEnableChromeAnalytics"": ""False"",
    ""FFlagPushFrameTimeToHarmony"": ""True"",
    ""FFlagOpenTelemetryEnabled"": ""False"",
    ""FFlagVngTOSRevisedEnabled"": ""False"",
    ""FFlagEnableFPSAndFrameTime"": ""True"",
    ""FFlagRenderFixGrassPrepass"": ""True"",
    ""FFlagFastGPULightCulling3"": ""True"",
    ""FFlagRenderNoLowFrmBloom"": ""False"",
    ""FFlagNewLightAttenuation"": ""True"",
    ""FFlagShoeSkipRenderMesh"": ""False"",
    ""FFlagSmoothInputOffset"": ""True"",
    ""FFlagLuaAppVpcUpsell3"": ""False"",
    ""FFlagMovePrerenderV2"": ""False"",
    ""FFlagDebugDisplayFPS"": ""False"",
    ""FFlagVoiceBetaBadge"": ""False"",
    ""FFlagMovePrerender"": ""False"",
    ""FFlagDebugSSAOForce"": ""True"",
    ""FFlagDebugSkyGray"": ""False"",
    ""FFlagAddDMLogging"": ""False"",
    ""FFlagLuauCodegen"": ""True"",

    ""FIntTaskSchedulerMaxTempArenaSizeBytes"": ""268435456"",
    ""FIntRenderMaxShadowAtlasUsageBeforeDownscale"": ""250"",
    ""FIntInterpolationAwareTargetTimeLerpHundredth"": ""40"",
    ""FIntFullscreenTitleBarTriggerDelayMillis"": ""3600000"",
    ""FIntStudioWebView2TelemetryHundredthsPercent"": ""0"",
    ""FIntSmoothClusterTaskQueueMaxParallelTasks"": ""12"",
    ""FIntSmoothMouseSpringFrequencyTenths"": ""100"",
    ""FIntLuaAnalyticsReleasePeriod"": ""2147483647"",
    ""FIntDirectionalAttenuationMaxPoints"": ""100"",
    ""FIntDebugFRMOptionalMSAALevelOverride"": ""4"",
    ""FIntGrassMovementReducedMotionFactor"": ""0"",
    ""FIntSimSolverResponsiveness"": ""2147483647"",
    ""FIntRakNetResendBufferArrayLength"": ""128"",
    ""FIntRuntimeMaxNumOfConditions"": ""1000000"",
    ""FIntRuntimeMaxNumOfSemaphores"": ""1000000"",
    ""FIntRuntimeMaxNumOfSchedulers"": ""1000000"",
    ""FIntVertexSmoothingGroupTolerance"": ""250"",
    ""FIntRenderMeshOptimizeVertexBuffer"": ""1"",
    ""FIntTaskSchedulerAutoThreadLimit"": ""12"",
    ""FIntActivatedCountTimerMSKeyboard"": ""0"",
    ""FIntRuntimeMaxNumOfThreads"": ""1000000"",
    ""FIntRuntimeMaxNumOfMutexes"": ""1000000"",
    ""FIntRuntimeMaxNumOfLatches"": ""1000000"",
    ""FIntLuaGcParallelMinMultiTasks"": ""12"",
    ""FIntInterpolationMaxDelayMSec"": ""100"",
    ""FIntUnifiedLightingBlendZone"": ""100"",
    ""FIntActivatedCountTimerMSMouse"": ""0"",
    ""FIntRenderLocalLightUpdatesMin"": ""1"",
    ""FIntRenderLocalLightUpdatesMax"": ""1"",
    ""FIntRenderGrassDetailStrands"": ""0"",
    ""FIntRenderLocalLightFadeInMs"": ""0"",
    ""FIntUITextureMaxUpdateDepth"": ""1"",
    ""FIntDebugForceMSAASamples"": ""4"",
    ""FIntRenderShadowmapBias"": ""50"",
    ""FIntRuntimeMaxNumOfDPCs"": ""64"",
    ""FIntFRMMinGrassDistance"": ""0"",
    ""FIntFRMMaxGrassDistance"": ""0"",
    ""FIntDefaultJitterN"": ""0"",
    ""FIntSSAOMipLevels"": ""1"",
    ""FIntCLI20390_2"": ""0"",
    ""FIntSSAO"": ""1""
}";
                        break;

                    case "Luci Potato - Allow List":
                        imageUrl = "none";
                        profileFlags = @"{
  ""DFFlagDebugPauseVoxelizer"": ""True"",
  ""DFFlagDisableDPIScale"": true,
  ""DFFlagTextureQualityOverrideEnabled"": ""True"",
  ""DFIntCSGLevelOfDetailSwitchingDistance"": ""0"",
  ""DFIntCSGLevelOfDetailSwitchingDistanceL12"": ""0"",
  ""DFIntCSGLevelOfDetailSwitchingDistanceL23"": ""0"",
  ""DFIntCSGLevelOfDetailSwitchingDistanceL34"": ""0"",
  ""DFIntDebugFRMQualityLevelOverride"": ""1"",
  ""DFIntTextureQualityOverride"": ""0"",
  ""FFlagDebugGraphicsPreferD3D11"": ""True"",
  ""FFlagDebugSkyGray"": ""True"",
  ""FIntDebugForceMSAASamples"": ""1"",
  ""FIntFRMMaxGrassDistance"": ""0"",
  ""FIntFRMMinGrassDistance"": ""0"",
  ""FFlagHandleAltEnterFullscreenManually"": false,
  ""FIntGrassMovementReducedMotionFactor"": ""0""
}";
                        break;

                    case "VulgoM3":
                        imageUrl = "https://media.discordapp.net/attachments/1422065296289824980/1422067259299332287/image.png?ex=68ec7657&is=68eb24d7&hm=5398bf434a17228c1962e4d4ba4a46c13c8bbf213b4ae24ca9fdedbffc56c14f&=&format=webp&quality=lossless";
                        profileFlags = @"{
    ""DFFlagClientLightingTechnologyChangedTelemetryTrackTimeSpent"": false,
    ""DFFlagAvatarChatTelemetryAddTrackingTimeToSessionTracking2"": false,
    ""DFFlagAvatarChatTelemetryAddTrackingTimeToSessionTracking"": false,
    ""DFFlagFFlagRolloutDuplicateRobloxTelemetryCountersEnabled"": false,
    ""DFFlagFixRobloxTelemetryEphemeralCountersAndStatsCategory"": false,
    ""DFFlagPerformanceControlIXPAllowCustomTelemetryThrottles"": false,
    ""DFFlagSimEnvironmentAddMinimumDensityRolloutTelemetry"": false,
    ""DFFlagAvatarChatServiceTelemetryIncludeServerFeatures"": false,
    ""DFFlagFaceAnimatorServiceTelemetryIncludeTrackerMode"": false,
    ""DFFlagRakNetUnblockSelectOnShutdownByWritingToSocket"": true,
    ""DFFlagAddPlaySessionIdToSimConstraintTelemetryEvent"": false,
    ""DFFlagFFlagRolloutDuplicateTelemetryCountersEnabled"": false,
    ""DFFlagAllowRegistrationOfAnimationClipInCoreScripts"": true,
    ""DFFlagTeleportClientAssetPreloadingDoingExperiment2"": true,
    ""DFFlagTeleportClientAssetPreloadingDoingExperiment"": true,
    ""DFFlagEnableGlobalFeatureTrackingInTelemetryEvent"": false,
    ""DFFlagOptimizeNoCollisionPrimitiveInMidphaseCrash"": true,
    ""DFFlagFixHumanoidStateTypeNameNullTelemetryCrash"": false,
    ""DFFlagPhysicsSkipNonRealTimeHumanoidForceCalc2"": false,
    ""DFFlagTeleportClientAssetPreloadingEnabledIXP2"": true,
    ""DFFlagVoicePublishConnectionStateTelemetryFix"": false,
    ""DFFlagSimSimulationRadiusTelemetryV2Migration"": false,
    ""DFFlagDebugEnableRomarkMicroprofilerTelemetry"": false,
    ""DFFlagRakNetDecoupleRecvAndUpdateLoopShutdown"": true,
    ""DFFlagTeleportClientAssetPreloadingEnabledIXP"": true,
    ""DFFlagDebugVisualizerTrackRotationPredictions"": true,
    ""DFFlagLoadCharacterLayeredClothingProperty2"": false,
    ""DFFlagDebugLargeReplicatorDisableCompression"": true,
    ""DFFlagPerformanceControlEnableMemoryProbing3"": true,
    ""DFFlagReportOutputDeviceWithRobloxTelemetry"": false,
    ""DFFlagAudioEnableVolumetricPanningForMeshes"": true,
    ""DFFlagTeleportClientAssetPreloadingEnabled9"": true,
    ""DFFlagAudioEnableVolumetricPanningForPolys"": true,
    ""DFFlagTrackerPerfTelemetryIncludePerfData"": false,
    ""DFFlagNextGenRepRollbackOverbudgetPackets"": true,
    ""DFFlagRakNetCalculateApplicationFeedback2"": true,
    ""DFFlagKeyRingUsingDynamicConfigTelemetry"": false,
    ""DFFlagEnableRemoteSaveValidatorTelemetry"": false,
    ""DFFlagPhysicsMechanismCacheOptimizeAlloc"": true,
    ""DFFlagRobloxTelemetryLogStringListField"": false,
    ""DFFlagDebugEnableInterpolationVisualizer"": true,
    ""DFFlagBrowserTrackerIdTelemetryEnabled"": false,
    ""DFFlagDebugLargeReplicatorForceFullSend"": true,
    ""DFFlagSessionTrackingRecordHasLocation"": false,
    ""DFFlagHasRuppHeaderFromServerTelemetry"": false,
    ""DFFlagDebugLargeReplicatorDisableDelta"": true,
    ""DFFlagAudioWiringEventIngestTelemetry"": false,
    ""DFFlagEnableExtraUnreachableTelemetry"": false,
    ""DFFlagNoRuppHeaderFromServerTelemetry"": false,
    ""DFFlagDebugVisualizeAllPropertyChanges"": true,
    ""DFFlagDebugRenderForceTechnologyVoxel"": true,
    ""DFFlagAnimatorFixReplicationASANError"": true,
    ""DFFlagEnablePerfDataGatherTelemetry2"": false,
    ""DFFlagRakNetDetectRecvThreadOverload"": true,
    ""DFFlagEnableRequestAsyncCompression"": false,
    ""DFFlagSimSolverTelemetryV2Migration"": false,
    ""DFFlagDebugDisableTelemetryAfterTest"": true,
    ""DFFlagDebugVisualizationImprovements"": true,
    ""DFFlagSimHumanoidTimestepModelUpdate"": true,
    ""DFFlagTextureQualityOverrideEnabled"": true,
    ""DFFlagJointIrregularityOptimization"": true,
    ""DFFlagRakNetDisconnectNotification"": true,
    ""DFFlagRobloxTelemetryAddDeviceRAM"": false,
    ""DFFlagClientRolloutPhaseTelemetry"": false,
    ""DFFlagDebugInterpolationVisualiser"": true,
    ""DFFlagAddUserIdToSessionTracking"": false,
    ""DFFlagEnableDynamicHeadByDefault"": false,
    ""DFFlagReportServerConnectionLost"": false,
    ""DFFlagTeleportMenuOpenTelemetry2"": false,
    ""DFFlagEnableRobloxTelemetryV2POC"": false,
    ""DFFlagEnableArbiterTimeTelemetry"": false,
    ""DFFlagRakNetDetectNetUnreachable"": true,
    ""DFFlagEnableLightstepReporting2"": false,
    ""DFFlagAddPlaySessionIdTelemetry"": false,
    ""DFFlagTeleportPreloadingMetrics5"": true,
    ""DFFlagEnableTelemetryV2FRMStats"": false,
    ""DFFlagLuauHeapProfilerTelemetry"": false,
    ""DFFlagEnableFmodErrorsTelemetry"": false,
    ""DFFlagLuauCodeGenIssueTelemetry"": false,
    ""DFFlagEnablePercentileTelemetry"": false,
    ""DFFlagOptimizeClusterCacheAlloc"": true,
    ""DFFlagAudioUseVolumetricPanning"": true,
    ""DFFlagEnablePreloadAvatarAssets"": true,
    ""DFFlagMeshCompressionTelemetry"": false,
    ""DFFlagReportTokenWithTelemetry"": false,
    ""DFFlagAllowPropertyDefaultSkip"": true,
    ""DFFlagCLI46794SendToTelemetry"": false,
    ""DFFlagRakNetUseSlidingWindow4"": true,
    ""DFFlagEnableTexturePreloading"": true,
    ""DFFlagSampleAndRefreshRakPing"": true,
    ""DFFlagDisableFastLogTelemetry"": true,
    ""DFFlagGpuVsCpuBoundTelemetry"": false,
    ""DFFlagDebugOverrideDPIScale"": false,
    ""DFFlagDebugSkipMeshVoxelizer"": true,
    ""DFFlagSessionTelemetryUnify"": false,
    ""DFFlagFileMeshDataTelemetry"": false,
    ""DFFlagAudioDeviceTelemetry"": false,
    ""DFFlagEnableMeshPreloading2"": true,
    ""DFFlagEnableSoundPreloading"": true,
    ""DFFlagDebugAssertTelemetry"": false,
    ""DFFlagRakNetFixBwCollapse"": false,
    ""DFFlagDebugPauseVoxelizer"": true,
    ""DFFlagBaseNetworkMetrics"": false,
    ""DFFlagSimOptimizeSetSize"": true,
    ""DFFlagRakNetEnablePoll"": true,
    ""DFFlagDisableDPIScale"": true,
    ""DFFlagDebugPerfMode"": false,

    ""DFIntPerformanceControlEventBasedTelemetryEffectPredictionEventNumReportsPerSecond"": 0,
    ""DFIntAssetPermissionsApiPostUniversesPermissionscopyIntoTelemetryHundredthsPercent"": 0,
    ""DFIntPerformanceControlEventBasedTelemetryTunableChangeEventNumReportsPerSecond"": 0,
    ""DFIntPerformanceControlEventBasedTelemetryEffectPredictionEventRateEventIngest"": 0,
    ""DFIntRCCServiceGetMachineStates_IncludeGlobalStateTelemetryHundredthsPercent"": 0,
    ""DFIntAssetPermissionsApiPostAssetsCheckPermissionsTelemetryHundredthsPercent"": 0,
    ""DFIntPerformanceControlEventBasedTelemetryTunableChangeEventRateEventIngest"": 0,
    ""DFIntRCCServiceGetMachineStates_IncludeDataModelTelemetryHundredthsPercent"": 0,
    ""DFIntFFlagRolloutDuplicateRobloxTelemetryCountersThrottleHundredthsPercent"": 0,
    ""DFIntPerformanceControlMemoryCategoriesTelemetryEnabledHundrethPercentage"": 0,
    ""DFIntPerformanceControlEventBasedTelemetryEffectPredictionEventRatePoints"": 0,
    ""DFIntAssetPermissionsApiPatchAssetsPermissions0TelemetryHundredthsPercent"": 0,
    ""DFIntAvatarFacechatLODCameraDisableTelemetryThrottleHundrethsPercent"": 10000,
    ""DFIntAvatarFacechatPipelinePerformanceTelemetryThrottleHundrethsPercent"": 0,
    ""DFIntAssetPermissionsApiGetAssetsPermissionsTelemetryHundredthsPercent"": 0,
    ""DFIntAvatarFacechatReplicationOverRCCTelemetryThrottleHundrethsPercent"": 0,
    ""DFIntPerformanceControlEventBasedTelemetryTunableChangeEventRatePoints"": 0,
    ""DFIntFFlagRolloutDuplicateTelemetryCountersThrottleHundredthsPercent"": 0,
    ""DFIntPerformanceControlEventBasedTelemetryDefaultSamplingRatePoints"": 0,
    ""DFIntPerformanceControlEventBasedTelemetryRateLimiterDefaultRegen"": 0,
    ""DFIntPhysicsSolverNumericalExplosionTelemetryHundrethsPercentage"": 0,
    ""DFIntAvatarFacechatPipelineLodTelemetryThrottleHundrethsPercent"": 0,
    ""DFIntAssetPermissionsApiGetOperationsTelemetryHundredthsPercent"": 0,
    ""DFIntClientLightingTechnologyChangedTelemetryHundredthsPercent"": 0,
    ""DFIntClientLightingEnvmapPlacementTelemetryHundredthsPercent"": 100,
    ""DFIntLuauRefinementTelemetryInfluxPriorityHundredthsPercentage"": 0,
    ""DFIntCurveMarkerCheckerTelemetryEventsThrottleHundrethsPercent"": 0,
    ""DFIntSignalRCoreHubConnectionDisconnectInfoHundredthsPercent"": 10,
    ""DFIntContentProviderPreloadHangTelemetryHundredthsPercentage"": 0,
    ""DFIntTeleportClientAssetPreloadingHundredthsPercentage2"": 100000,
    ""DFIntRCCServiceUpdateMachineStatesTelemetryHundredthsPercent"": 0,
    ""DFIntLoadStreamAnimationFailureTelemetryHundredthsPercentage"": 0,
    ""DFIntTeleportClientAssetPreloadingHundredthsPercentage"": 100000,
    ""DFIntCullFactorPixelThresholdShadowMapHighQuality"": 2147483647,
    ""DFIntCullFactorPixelThresholdShadowMapLowQuality"": 2147483647,
    ""DFIntRCCServiceGetMachineStatesTelemetryHundredthsPercent"": 0,
    ""DFIntOAuth2RefreshTokenStorageTelemetryHundredthsPercent"": 0,
    ""DFIntAppConfigurationTelemetryThrottleHundredthsPercent"": 0,
    ""DFIntKeyRingUsingDynamicConfigTelemetryInfluxHundredths"": 0,
    ""DFIntBrowserTrackerIdTelemetryThrottleHundredthsPercent"": 0,
    ""DFIntLuauRefinementTelemetryInfluxHundredthsPercentage"": 0,
    ""DFIntVoicePublishConnectionStateTelemetryFixReportRate"": 0,
    ""DFIntLongAvatarAssetTelemetryThrottleHundredthsPercent"": 0,
    ""DFIntOAuth2TokenHttpRequestTelemetryHundredthsPercent"": 0,
    ""DFIntIkControlTelemetryEventsThrottleHundrethsPercent"": 0,
    ""DFIntRccWorkerConsoleOutputTelemetryHundredthsPercent"": 0,
    ""DFIntInterpolationFrameRotVelocityThresholdMillionth"": 1,
    ""DFIntVoicePublishConnectionStateTelemetryFixThrottle"": 0,
    ""DFIntRaknetBandwidthInfluxHundredthsPercentageV2"": 10000,
    ""DFIntAvatarFacechatReplOverRCCTelemetryEventRateSec"": 0,
    ""DFIntAMPVerifiedTelemetryPointsHundredthsPercentage"": 0,
    ""DFIntRakNetClockDriftAdjustmentPerPingMillisecond"": 100,
    ""DFIntCLI46794SendInputTelemetryHundredthsPercentage"": 0,
    ""DFIntGraphicsOptimizationModeMaxFrameTimeTargetMs"": 18,
    ""DFIntGraphicsOptimizationModeMinFrameTimeTargetMs"": 17,
    ""DFIntLuauHeapProfilerTelemetryHundredthsPercentage"": 0,
    ""DFIntInterpolationFrameVelocityThresholdMillionth"": 1,
    ""DFIntPerformanceControlTextureQualityBestUtility"": -1,
    ""DFIntLuauCodeGenIssueTelemetryHundrethsPercentage"": 0,
    ""DFIntRccServerMetricsFilterTelemetryInfluxPercent"": 0,
    ""DFIntGraphicsOptimizationModeFRMFrameRateTarget"": 60,
    ""DFIntSignalRHubConnectionHeartbeatTimerRateMs"": 1000,
    ""DFIntReportServerConnectionLostHundredthsPercent"": 0,
    ""DFIntNewCameraControls_TelemetryPerFrameThrottle"": 0,
    ""DFIntMegaReplicatorNetworkQualityProcessorUnit"": 10,
    ""DFIntMemoryUtilityCurveBaseHundrethsPercent"": 10000,
    ""DFIntTrackCountryRegionAPIHundredthsPercent"": 10000,
    ""DFIntPhysicsMemoryTelemetryHundredthsPercentage"": 0,
    ""DFIntQueryPerformanceTelemetryConfigMaxRequests"": 0,
    ""DFIntNetworkClusterPacketCacheNumParallelTasks"": 2,
    ""DFIntPhysicsAnalyticsHighFrequencyIntervalSec"": 20,
    ""DFIntLightstepHTTPTransportHundredthsPercent2"": 0,
    ""DFIntAMPVerifiedTelemetryHundredthsPercentage"": 0,
    ""DFIntSignalRHubConnectionConnectTimeoutMs"": 7000,
    ""DFIntHACDPointSampleDistApartTenths"": 2147483647,
    ""DFIntPercentApiRequestsRecordGoogleAnalytics"": 0,
    ""DFIntPerformanceControlFrameTimeMaxUtility"": -1,
    ""DFIntSignalRHubConnectionMaxRetryTimeMs"": 1000,
    ""DFIntMaxInterpolationRecursionsBeforeCheck"": 1,
    ""DFIntReplicationDataCacheNumParallelTasks"": 20,
    ""DFIntAnimationLodFacsVisibilityDenominator"": 0,
    ""DFIntVisibilityCheckRayCastLimitPerFrame"": 10,
    ""DFIntSignalRHubConnectionBaseRetryTimeMs"": 50,
    ""DFIntInitialAccelerationLatencyMultTenths"": 1,
    ""DFIntCSGLevelOfDetailSwitchingDistanceL23"": 0,
    ""DFIntCSGLevelOfDetailSwitchingDistanceL34"": 0,
    ""DFIntCSGLevelOfDetailSwitchingDistanceL12"": 0,
    ""DFIntMemoryUtilityCurveTotalMemoryReserve"": 0,
    ""DFIntRaknetBandwidthPingSendEveryXSeconds"": 1,
    ""DFIntInterpolationNumMechanismsBatchSize"": 1,
    ""DFIntSignalRCoreKeepAlivePingPeriodMs"": 1000,
    ""DFIntDebugSimPrimalPreconditionerMinExp"": 69,
    ""DFIntMaxProcessPacketsStepsPerCyclic"": 5000,
    ""DFIntDebugSimPrimalWarmstartVelocity"": -350,
    ""DFIntGameNetLocalSpaceMaxSendIndex"": 100000,
    ""DFIntMaxProcessPacketsStepsAccumulated"": 0,
    ""DFIntInterpolationNumMechanismsPerTask"": 5,
    ""DFIntNumFramesToKeepAfterInterpolation"": 1,
    ""DFIntPercentileTelemetryHundredPercent"": 0,
    ""DFIntCSGLevelOfDetailSwitchingDistance"": 0,
    ""DFIntSignalRCoreHandshakeTimeoutMs"": 1000,
    ""DFIntPhysicsDecompForceUpgradeVersion"": 4,
    ""DFIntWaitOnUpdateNetworkLoopEndedMS"": 100,
    ""DFIntNetworkSchemaCompressionRatio"": 100,
    ""DFIntMaxProcessPacketsJobScaling"": 10000,
    ""DFIntSignalRHeartbeatIntervalSeconds"": 1,
    ""DFIntAnimatorThrottleMaxFramesToSkip"": 1,
    ""DFIntLargePacketQueueSizeCutoffMB"": 1000,
    ""DFIntPhysicsReceiveNumParallelTasks"": 20,
    ""DFIntMegaReplicatorNumParallelTasks"": 20,
    ""DFIntMemoryUtilityCurveNumSegments"": 100,
    ""DFIntProductUpdateTelemetryEventRate"": 0,
    ""DFIntAnimatorTelemetryCollectionRate"": 0,
    ""DFIntDataSenderMaxJoinBandwidthBps"": 222,
    ""DFIntDebugSimPrimalWarmstartForce"": -885,
    ""DFIntNumFramesAllowedToBeAboveError"": 1,
    ""DFIntPerformanceControlFrameTimeMax"": 1,
    ""DFIntDebugDynamicRenderKiloPixels"": 150,
    ""DFIntNumAssetsMaxToPreload"": 2147483647,
    ""DFIntSignalRCoreServerTimeoutMs"": 5000,
    ""DFIntInterpolationMinAssemblyCount"": 1,
    ""DFIntSignalRCoreHubMaxElapsedMs"": 5000,
    ""DFIntInterpolationNumParallelTasks"": 5,
    ""DFIntPlayerNetworkUpdateQueueSize"": 20,
    ""DFIntVoiceChatVolumeThousandths"": 6000,
    ""DFIntDebugSimPrimalPreconditioner"": 69,
    ""DFIntSignalRCoreHubMaxBackoffMs"": 500,
    ""DFIntBufferCompressionThreshold"": 100,
    ""DFIntDebugFRMQualityLevelOverride"": 1,
    ""DFIntPreloadAvatarAssets"": 2147483647,
    ""DFIntTaskSchedulerTargetFps"": 999999,
    ""DFIntInterpolationDtLimitForLod"": 10,
    ""DFIntAnimationLodFacsDistanceMax"": 0,
    ""DFIntAnimationLodFacsDistanceMin"": 0,
    ""DFIntDataSenderMaxBandwidthBps"": 555,
    ""DFIntWaitOnRecvFromLoopEndedMS"": 100,
    ""DFIntTextureCompositorActiveJobs"": 0,
    ""DFIntSignalRCoreHubBaseRetryMs"": 50,
    ""DFIntSignalRCoreRpcQueueSize"": 4096,
    ""DFIntRenderingThrottleDelayInMS"": 1,
    ""DFIntCodecMaxOutgoingFrames"": 10000,
    ""DFIntDebugSimPrimalToleranceInv"": 1,
    ""DFIntSignalRCoreNetworkHandler"": 1,
    ""DFIntCodecMaxIncomingPackets"": 100,
    ""DFIntRunningBaseOrientationP"": 115,
    ""DFIntCanHideGuiGroupId"": 32380007,
    ""DFIntAssetPreloading"": 2147483647,
    ""DFIntServerPhysicsUpdateRate"": 60,
    ""DFIntPlayerNetworkUpdateRate"": 60,
    ""DFIntRakNetMtuValue1InBytes"": 900,
    ""DFIntNewRunningBaseAltitudeD"": 45,
    ""DFIntDebugRestrictGCDistance"": 1,
    ""DFIntRakNetResendRttMultiple"": 1,
    ""DFIntNetworkLatencyTolerance"": 1,
    ""DFIntUseFmodTelemetryPercent"": 0,
    ""DFIntDebugSimPrimalNewtonIts"": 1,
    ""DFIntTextureQualityOverride"": 0,
    ""DFIntBufferCompressionLevel"": 0,
    ""DFIntOptimizePingThreshold"": 50,
    ""DFIntS2PhysicsSenderRate"": 250,
    ""DFIntSignalRCoreTimerMs"": 50,
    ""DFIntMaxFrameBufferSize"": 10,
    ""DFIntConnectionMTUSize"": 900,
    ""DFIntNetworkPrediction"": 120,
    ""DFIntLCCageDeformLimit"": -1,
    ""DFIntCharacterLoadTime"": 1,
    ""DFIntSignalRCoreError"": 1,
    ""DFIntServerTickRate"": 60,
    ""DFIntNetworkCluster"": 1,
    ""DFIntDataSenderRate"": 4,
    ""DFIntRakNetLoopMs"": 1,
    ""DFIntSignalRCore"": 1,

    ""DFIntplacesUpdateUniverseConfigurationTelemetryHundredthsPercent"": 0,
    ""DFIntplacesCreatePlaceVersionUserAuthTelemetryHundredthsPercent"": 0,
    ""DFIntplacesGetUniverseContainingPlaceTelemetryHundredthsPercent"": 0,
    ""DFIntplacesPatchUniverseConfigurationTelemetryHundredthsPercent"": 0,
    ""DFIntplacesCreatePlaceVersionApiKeyTelemetryHundredthsPercent"": 0,
    ""DFIntplacesUpdateUniverseRootPlaceTelemetryHundredthsPercent"": 0,
    ""DFIntplacesPatchUniverseSecurityV2TelemetryHundredthsPercent"": 0,
    ""DFIntplacesUpdateUniverseSecurityTelemetryHundredthsPercent"": 0,
    ""DFIntplacesSetUniverseSecurityV2TelemetryHundredthsPercent"": 0,
    ""DFIntplacesPatchUniverseSecurityTelemetryHundredthsPercent"": 0,
    ""DFIntplacesGetUniverseSecurityV2TelemetryHundredthsPercent"": 0,
    ""DFIntplacesCreatePlaceFromPlaceTelemetryHundredthsPercent"": 0,
    ""DFIntplacesGetUniverseRootPlaceTelemetryHundredthsPercent"": 0,
    ""DFIntplacesSetUniverseRootPlaceTelemetryHundredthsPercent"": 0,
    ""DFIntplacesCreatePlaceUserAuthTelemetryHundredthsPercent"": 0,
    ""DFIntplacesGetJoinRestrictionsTelemetryHundredthsPercent"": 0,
    ""DFIntplacesGetUniverseSecurityTelemetryHundredthsPercent"": 0,
    ""DFIntplacesSetJoinRestrictionsTelemetryHundredthsPercent"": 0,
    ""DFIntplacesSetUniverseSecurityTelemetryHundredthsPercent"": 0,
    ""DFIntplacesAddPlaceToUniverseTelemetryHundredthsPercent"": 0,
    ""DFIntplacesUpdatePlaceVersionTelemetryHundredthsPercent"": 0,
    ""DFIntplacesCreatePlaceApiKeyTelemetryHundredthsPercent"": 0,
    ""DFIntplacesCreateUniverseTelemetryHundredthsPercent"": 0,
    ""DFIntplacesPatchRootPlaceTelemetryHundredthsPercent"": 0,
    ""DFIntplacesUpdatePlaceTelemetryHundredthsPercent"": 0,

    ""DFStringCrashUploadToBacktraceWindowsPlayerToken"": ""null"",
    ""DFStringCrashUploadToBacktraceMacPlayerToken"": ""null"",
    ""DFStringLightstepHTTPTransportUrlPath"": ""null"",
    ""DFStringLightstepHTTPTransportUrlHost"": ""null"",
    ""DFStringCrashUploadToBacktraceBaseUrl"": ""null"",
    ""DFStringAltTelegrafHTTPTransportUrl"": ""null"",
    ""DFStringTelegrafHTTPTransportUrl"": ""null"",
    ""DFStringAltHttpPointsReporterUrl"": ""null"",
    ""DFStringHttpPointsReporterUrl"": ""null"",
    ""DFStringRobloxAnalyticsURL"": ""null"",
    ""DFStringTelemetryV2Url"": ""null"",
    ""DFStringLightstepToken"": ""null"",

    ""FFlagEnableAccessibilitySettingsEffectsInExperienceChat"": true,
    ""FFlagEnableAccessibilitySettingsEffectsInCoreScripts2"": true,
    ""FFlagDebugNextGenReplicatorEnabledWriteCFrameColor"": true,
    ""FFlagEnablePreferredTextSizeStyleFixesInAppShell3"": true,
    ""FFlagEnablePreferredTextSizeStyleFixesInAvatarExp"": true,
    ""FFlagNewOptimizeNoCollisionPrimitiveInMidphase660"": true,
    ""FFlagEnableAccessibilitySettingsInExperienceMenu2"": true,
    ""FFlagEnablePartyVoiceOnlyForUnfilteredThreads"": false,
    ""FFlagUserHideCharacterParticlesInFirstPerson"": true,
    ""FFlagUISUseLastFrameTimeInUpdateInputSignal"": true,
    ""FFlagEnablePreferredTextSizeSettingInMenus2"": true,
    ""FFlagDebugDisableTelemetryEphemeralCounter"": true,
    ""FFlagEnablePartyVoiceOnlyForEligibleUsers"": false,
    ""FFlagUserCameraControlLastInputTypeUpdate"": false,
    ""FFlagContentProviderPreloadHangTelemetry"": false,
    ""FFlagVideoServiceAddHardwareCodecMetrics"": true,
    ""FFlagChatTranslationEnableSystemMessage"": false,
    ""FFlagTaskSchedulerLimitTargetFpsTo2402"": false,
    ""FFlagDebugDisableTelemetryEphemeralStat"": true,
    ""FFlagRenderLegacyShadowsQualityRefactor"": true,
    ""FFlagHandleAltEnterFullscreenManually"": false,
    ""FFlagEnablePreferredTextSizeGuiService"": true,
    ""FFlagEnableClickToMoveUsageTelemetry2"": false,
    ""FFlagEnableTerrainFoliageOptimizations"": true,
    ""FFlagDebugDisableTelemetryEventIngest"": true,
    ""FFlagDebugEnableDirectAudioOcclusion2"": true,
    ""FFlagLuaAppLegacyInputSettingRefactor"": true,
    ""FFlagDebugDisableTelemetryEventingest"": true,
    ""FFlagEnableAccessibilitySettingsAPIV2"": true,
    ""FFlagEnableInGameMenuDurationLogger"": false,
    ""FFlagBetaBadgeLearnMoreLinkFormview"": false,
    ""FFlagDisableFeedbackSoothsayerCheck"": false,
    ""FFlagInGameMenuV1FullScreenTitleBar"": false,
    ""FFlagEnableMenuModernizationABTest2"": false,
    ""FFlagSimAdaptiveTimesteppingDefault2"": true,
    ""FFlagDebugGraphicsDisableDirect3D11"": true,
    ""FFlagDebugForceFutureIsBrightPhase3"": true,
    ""FFlagDebugForceFutureIsBrightPhase2"": true,
    ""FFlagGameBasicSettingsFramerateCap5"": true,
    ""FFlagDebugDisableTelemetryV2Counter"": true,
    ""FFlagFixOutdatedTimeScaleParticles"": false,
    ""FFlagNextGenReplicatorEnabledWrite2"": true,
    ""FFlagRenderDynamicResolutionScale11"": true,
    ""FFlagUserSoundsUseRelativeVelocity2"": true,
    ""FFlagEnableInGameMenuChromeABTest4"": false,
    ""FFlagEnableMenuModernizationABTest"": false,
    ""FFlagEnableDropdownButtonTelemetry"": false,
    ""FFlagDebugDisableTelemetryEphemeral"": true,
    ""FFlagEnableInGameMenuChromeABTest3"": false,
    ""FFlagDebugRenderingSetDeterministic"": true,
    ""FFlagNextGenReplicatorEnabledWrite"": true,
    ""FFlagSimAdaptiveMinorOptimizations"": true,
    ""FFlagFixParticleAttachmentCulling"": false,
    ""FFlagNextGenReplicatorEnabledRead2"": true,
    ""FFlagEnableInGameMenuModernization"": true,
    ""FFlagEnableNetworkChangeTelemtry2"": false,
    ""FFlagEnableHumanoidLuaSideCaching"": false,
    ""FFlagNextGenReplicatorEnabledRead"": true,
    ""FFlagFixSensitivityTextPrecision"": false,
    ""FFlagTrackPlaceIdForCrashEnabled"": false,
    ""FFlagDebugDeterministicParticles"": false,
    ""FFlagDebugDisableTelemetryV2Event"": true,
    ""FFlagDebugForceFSMCPULightCulling"": true,
    ""FFlagEnablePreferredTextSizeScale"": true,
    ""FFlagRenderSkipReadingShaderData"": false,
    ""FFlagCoreGuiTypeSelfViewPresent"": false,
    ""FFlagWindowsLaunchTypeAnalytics"": false,
    ""FFlagEnableBetaFacialAnimation2"": false,
    ""FFlagDebugDisableTelemetryV2Stat"": true,
    ""FFlagDebugLargeReplicatorEnabled"": true,
    ""FFlagUserBetterInertialScrolling"": true,
    ""FFlagDebugSimDefaultPrimalSolver"": true,
    ""FFlagEnableCommandAutocomplete"": false,
    ""FFlagCommitToGraphicsQualityFix"": true,
    ""FFlagDebugDisableTelemetryPoint"": true,
    ""FFlagEnableAudioPannerFiltering"": true,
    ""FFlagImproveShiftLockTransition"": true,
    ""FFlagMessageBusCallOptimization"": true,
    ""FFlagPreloadTextureItemsOption4"": true,
    ""FFlagGraphicsEnableD3D10Compute"": true,
    ""FFlagRenderDebugCheckThreading2"": true,
    ""FFlagLanguageFeaturesTelemetry"": false,
    ""FFlagEnableTerrainOptimizations"": true,
    ""FFlagControlBetaBadgeWithGuac"": false,
    ""FFlagDebugCheckRenderThreading"": true,
    ""FFlagDebugLargeReplicatorWrite"": true,
    ""FFlagFixParticleEmissionBias2"": false,
    ""FFlagHighlightOutlinesOnMobile"": true,
    ""FFlagEnableMenuControlsABTest"": false,
    ""FFlagFixScalingModelRendering"": false,
    ""FFlagDebugGraphicsPreferMetal"": true,
    ""FFlagFacialAnimationSupport1"": false,
    ""FFlagDebugLargeReplicatorRead"": true,
    ""FFlagQuaternionPoseCorrection"": true,
    ""FFlagOptimizeNetworkTransport"": true,
    ""FFlagInGameMenuV1LeaveToHome"": false,
    ""FFlagEnableInGameMenuControls"": true,
    ""FFlagUseUnifiedRenderStepped"": false,
    ""FFlagEnableLightAttachToPart"": false,
    ""FFlagBetterTrackpadScrolling"": true,
    ""FFlagLuaMenuPerfImprovements"": true,
    ""FFlagShaderLightingRefactor"": false,
    ""FFlagEnableInGameMenuChrome"": false,
    ""FFlagEnableNewHeapSnapshots"": false,
    ""FFlagPushFrameTimeToHarmony"": true,
    ""FFlagFixOutdatedParticles2"": false,
    ""FFlagLoginPageOptimizedPngs"": true,
    ""FFlagUserShowGuiHideToggles"": true,
    ""FFlagOptimizeNetworkRouting"": true,
    ""FFlagOptimizeServerTickRate"": true,
    ""FFlagUseNewAnimationSystem"": false,
    ""FFlagEnableQuickGameLaunch"": false,
    ""FFlagRenderEnableHalfsPVR"": false,
    ""FFlagDebugForceGenerateHSR"": true,
    ""FFlagAlwaysShowVRToggleV3"": false,
    ""FFlagInGameMenuV1ExitModal"": true,
    ""FFlagEnableSoundTelemetry"": false,
    ""FFlagRenderCheckThreading6"": true,
    ""FFlagSimIslandizerManager"": false,
    ""FFlagGlobalWindRendering"": false,
    ""FFlagGlobalWindActivated"": false,
    ""FFlagFastGPULightCulling3"": true,
    ""FFlagFixIGMTabTransitions"": true,
    ""FFlagRenderNoLowFrmBloom"": false,
    ""FFlagEnableV3MenuABTest3"": false,
    ""FFlagCloudsReflectOnWater"": true,
    ""FFlagNewLightAttenuation"": true,
    ""FFlagShoeSkipRenderMesh"": false,
    ""FFlagUseDeferredContext"": false,
    ""FFlagFixMeshPartScaling"": false,
    ""FFlagFixGraphicsQuality"": true,
    ""FFlagAssetPreloadingIXP"": true,
    ""FFlagDisableNewIGMinDUA"": true,
    ""FFlagTweenOptimizations"": true,
    ""FFlagDebugCrashReports"": false,
    ""FFlagRenderCBRefactor2"": true,
    ""FFlagAddHapticsToggle"": false,
    ""FFlagAdServiceEnabled"": false,
    ""FFlagPreloadAllFonts"": false,
    ""FFlagLuaAppSystemBar"": false,
    ""FFlagDebugSSAOForce"": false,
    ""FFlagVoiceBetaBadge"": false,
    ""FFlagOptimizeNetwork"": true,
    ""FFlagDebugDisplayFPS"": true,
    ""FFlagAnimatePhysics"": false,
    ""FFlagUseParticlesV2"": false,
    ""FFlagOptimizeEmotes"": false,
    ""FFlagSimEnableDCD10"": true,
    ""FFlagSimEnableDCD16"": true,
    ""FFlagNewNetworking"": false,
    ""FFlagEnableNewInput"": true,
    ""FFlagUseDynamicSun"": false,
    ""FFlagDisablePostFx"": true,
    ""FFlagRenderFixFog"": true,
    ""FFlagFRMRefactor"": false,
    ""FFlagMSRefactor5"": false,
    ""FFlagLuauCodegen"": true,

    ""FIntMockClientLightingTechnologyIxpExperimentQualityLevel"": 7,
    ""FIntPreferredTextSizeSettingBetaFeatureRolloutPercent"": 100,
    ""FIntMockClientLightingTechnologyIxpExperimentMode"": 0,
    ""FIntOverrideISRReplicatorStepBandwidthBytes"": 131072,
    ""FIntFullscreenTitleBarTriggerDelayMillis"": 18000000,
    ""FIntRenderMaxShadowAtlasUsageBeforeDownscale"": 0,
    ""FIntSmoothClusterTaskQueueMaxParallelTasks"": 20,
    ""FIntCAP1544DataSharingUserRolloutPercentage"": 0,
    ""FIntEnableVisBugChecksHundredthPercent27"": 100,
    ""FIntHSRClusterSymmetryDistancePercent"": 10000,
    ""FIntRomarkStartWithGraphicQualityLevel"": 10,
    ""FIntDebugFRMOptionalMSAALevelOverride"": 0,
    ""FIntGrassMovementReducedMotionFactor"": 0,
    ""FIntRuntimeMaxNumOfConditions"": 1000000,
    ""FIntSimWorldTaskQueueParallelTasks"": 20,
    ""FIntDirectionalAttenuationMaxPoints"": 0,
    ""FIntRakNetResendBufferArrayLength"": 128,
    ""FIntCameraMaxZoomDistance"": 2147483647,
    ""FIntRenderMeshOptimizeVertexBuffer"": 1,
    ""FIntVertexSmoothingGroupTolerance"": 0,
    ""FIntRuntimeMaxNumOfLatches"": 1000000,
    ""FIntCAP1209DataSharingTOSVersion"": 0,
    ""FIntNewInGameMenuPercentRollout3"": 0,
    ""FIntDebugTextureManagerSkipMips"": 8,
    ""FIntInterpolationMaxDelayMSec"": 75,
    ""FIntRenderLocalLightUpdatesMax"": 1,
    ""FIntRenderLocalLightUpdatesMin"": 1,
    ""FIntRuntimeMaxNumOfThreads"": 2400,
    ""FIntRenderGrassDetailStrands"": 0,
    ""FIntRenderLocalLightFadeInMs"": 0,
    ""FIntUnifiedLightingBlendZone"": 0,
    ""FIntRenderGrassHeightScaler"": 0,
    ""FIntRenderGrassHeightScalar"": 0,
    ""FIntUITextureMaxUpdateDepth"": 1,
    ""FIntRobloxGuiBlurIntensity"": 0,
    ""FIntTaskSchedulerThreadMin"": 3,
    ""FIntTerrainArraySliceSize"": 0,
    ""FIntRenderShadowIntensity"": 0,
    ""FIntDebugForceMSAASamples"": 1,
    ""FIntRuntimeMaxNumOfDPCs"": 64,
    ""FIntMaxSpeedDeltaMillis"": 1,
    ""FIntFRMMinGrassDistance"": 0,
    ""FIntFRMMaxGrassDistance"": 0,
    ""FIntRenderShadowmapBias"": 0,
    ""FIntFontSizePadding"": 3,
    ""FIntSSAOMipLevels"": 0,
    ""FIntSSAO"": 0,

    ""FLogNetwork"": 7,

    ""FStringPartTexturePackTable2022"": ""{\""foil\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[238,238,238,255]},\""asphalt\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[227,227,228,234]},\""basalt\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[160,160,158,238]},\""brick\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[229,214,205,227]},\""cobblestone\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[218,219,219,243]},\""concrete\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[225,225,224,255]},\""crackedlava\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[76,79,81,156]},\""diamondplate\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[210,210,210,255]},\""fabric\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[221,221,221,255]},\""glacier\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[225,229,229,243]},\""glass\"":{\""ids\"":[\""rbxassetid://9873284556\"",\""rbxassetid://9438453972\""],\""color\"":[254,254,254,7]},\""granite\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[210,206,200,255]},\""grass\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[196,196,189,241]},\""ground\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[165,165,160,240]},\""ice\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[235,239,241,248]},\""leafygrass\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[182,178,175,234]},\""limestone\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[250,248,243,250]},\""marble\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[181,183,193,249]},\""metal\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[226,226,226,255]},\""mud\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[193,192,193,252]},\""pavement\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[218,218,219,236]},\""pebble\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[204,203,201,234]},\""plastic\"":{\""ids\"":[\""\"",\""rbxassetid://0\""],\""color\"":[255,255,255,255]},\""rock\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[211,211,210,248]},\""corrodedmetal\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[206,177,163,180]},\""salt\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[249,249,249,255]},\""sand\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[218,216,210,240]},\""sandstone\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[241,234,230,246]},\""slate\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[235,234,235,254]},\""snow\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[239,240,240,255]},\""wood\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[217,209,208,255]},\""woodplanks\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[207,208,206,254]}}"",
    ""FStringPartTexturePackTablePre2022"": ""{\""foil\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[255,255,255,255]},\""brick\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[204,201,200,232]},\""cobblestone\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[212,200,187,250]},\""concrete\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[208,208,208,255]},\""diamondplate\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[170,170,170,255]},\""fabric\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[105,104,102,244]},\""glass\"":{\""ids\"":[\""rbxassetid://7547304948\"",\""rbxassetid://7546645118\""],\""color\"":[254,254,254,7]},\""granite\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[113,113,113,255]},\""grass\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[165,165,159,255]},\""ice\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[255,255,255,255]},\""marble\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[199,199,199,255]},\""metal\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[199,199,199,255]},\""pebble\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[208,208,208,255]},\""corrodedmetal\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[159,119,95,200]},\""sand\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[220,220,220,255]},\""slate\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[193,193,193,255]},\""wood\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[227,227,227,255]},\""woodplanks\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[212,209,203,255]},\""asphalt\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[123,123,123,234]},\""basalt\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[154,154,153,238]},\""crackedlava\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[74,78,80,156]},\""glacier\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[226,229,229,243]},\""ground\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[114,114,112,240]},\""leafygrass\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[121,117,113,234]},\""limestone\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[235,234,230,250]},\""mud\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[130,130,130,252]},\""pavement\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[142,142,144,236]},\""rock\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[154,154,154,248]},\""salt\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[220,220,221,255]},\""sandstone\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[174,171,169,246]},\""snow\"":{\""ids\"":[\""rbxassetid://0\"",\""rbxassetid://0\""],\""color\"":[218,218,218,255]}}"",
    ""FStringCoreScriptBacktraceErrorUploadToken"": ""null"",
    ""FStringVoiceBetaBadgeLearnMoreLink"": ""null"",
    ""FStringGetPlayerImageDefaultTimeout"": 1,
    ""FStringTerrainMaterialTablePre2022"": """",
    ""FStringTerrainMaterialTable2022"": """",
    ""FStringGamesUrlPath"": ""/games/""
}";
                        break;

                }

                ProfileTextBox.Text = profileFlags;
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            byte[] bytes = await wc.DownloadDataTaskAsync(imageUrl);
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = new System.IO.MemoryStream(bytes);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            ProfileImage.Source = bitmap;
                        }
                    }
                    catch
                    {
                        ProfileImage.Source = null;
                    }
                }
            }
        }

        private async void CheckSystemButton_Click(object sender, RoutedEventArgs e)
        {
            CheckSystemButton.IsEnabled = false;
            SystemCheckProgress.Visibility = Visibility.Visible;
            SystemCheckProgress.Value = 0;

            string logFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SystemCheckLog.txt");

            string configFolder = Path.Combine(Paths.Base, "VoidstrapMods", "ClientSettings");
            string configFile = Path.Combine(configFolder, "ClientAppSettings.json");
            Directory.CreateDirectory(configFolder);

            var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
            double totalRamGb = Math.Round(ci.TotalPhysicalMemory / (1024.0 * 1024.0 * 1024.0), 2);
            int cpuCores = Environment.ProcessorCount;

            SystemCheckProgress.Value = 20;
            await Task.Delay(200);

            var tierFlags = new Dictionary<string, Dictionary<string, string>>
            {
                ["Low"] = new Dictionary<string, string> { ["DFFlagDebugPerfMode"] = "True", ["FFlagHandleAltEnterFullscreenManually"] = "False", ["DFFlagDisableDPIScale"] = "True", ["FFlagDebugGraphicsPreferVulkan"] = "True", ["FFlagDebugGraphicsDisableDirect3D11"] = "True" },
                ["Mid"] = new Dictionary<string, string> { ["FFlagRenderDisableShadows"] = "False", ["FFlagGraphicsTextureQuality"] = "2", ["FFlagTerrainEnable"] = "True", ["FIntRenderShadowmapBias"] = "-1", ["DFFlagDebugPauseVoxelizer"] = "True", ["FIntRenderShadowIntensity"] = "0", ["DFFlagDebugRenderForceTechnologyVoxel"] = "True", ["FIntFullscreenTitleBarTriggerDelayMillis"] = "3600000", ["FFlagDisablePostFx"] = "True", ["DFIntTextureQualityOverride"] = "2", ["DFFlagTextureQualityOverrideEnabled"] = "True", ["DFFlagDisableDPIScale"] = "True" },
                ["High"] = new Dictionary<string, string> { ["FFlagRenderDisableShadows"] = "False", ["FFlagGraphicsTextureQuality"] = "3", ["FFlagTerrainEnable"] = "True", ["DFIntTextureQualityOverride"] = "2", ["DFFlagTextureQualityOverrideEnabled"] = "True", ["FFlagDisablePostFx"] = "True", ["FIntFullscreenTitleBarTriggerDelayMillis"] = "3600000", ["FFlagDebugForceFutureIsBrightPhase2"] = "True", ["DFFlagDisableDPIScale"] = "True" },
                ["Ultra"] = new Dictionary<string, string> { ["FFlagRenderDisableShadows"] = "False", ["FFlagGraphicsTextureQuality"] = "3", ["FFlagTerrainEnable"] = "True", ["FFlagDisablePostFx"] = "True", ["FIntFullscreenTitleBarTriggerDelayMillis"] = "3600000", ["DFFlagDisableDPIScale"] = "True" }
            };

            SystemCheckProgress.Value = 40;
            await Task.Delay(200);

            string tier = "Mid";
            if (totalRamGb < 4 || cpuCores <= 2)
                tier = "Low";
            else if (totalRamGb >= 16 && cpuCores >= 12)
                tier = "Ultra";
            else if (totalRamGb >= 12 && cpuCores >= 8)
                tier = "High";

            SystemCheckProgress.Value = 60;
            await Task.Delay(200);

            var flags = tierFlags.ContainsKey(tier) ? new Dictionary<string, string>(tierFlags[tier]) : new Dictionary<string, string>();

            SystemCheckProgress.Value = 80;
            await Task.Delay(200);

            string json = System.Text.Json.JsonSerializer.Serialize(flags, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configFile, json);

            SystemCheckProgress.Value = 90;
            await Task.Delay(200);

            using (StreamWriter writer = new StreamWriter(logFile, false))
            {
                await writer.WriteLineAsync($"System Check Log - {DateTime.Now}");
                await writer.WriteLineAsync($"Machine: {Environment.MachineName}");
                await writer.WriteLineAsync($"OS: {Environment.OSVersion}");
                await writer.WriteLineAsync($"CPU Cores: {cpuCores}");
                await writer.WriteLineAsync($"RAM: {totalRamGb} GB");
                await writer.WriteLineAsync($"Chosen Tier: {tier}");
                await writer.WriteLineAsync($"FFlags written to: {configFile}");
            }

            SystemCheckProgress.Value = 100;
            await Task.Delay(200);
            Frontend.ShowMessageBox($"System check complete! Flags applied for {tier}-tier.\nConfig saved to: {configFile}\nLog saved to Documents.\nApp Restart is needed!", MessageBoxImage.Information);

            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
    }
}
