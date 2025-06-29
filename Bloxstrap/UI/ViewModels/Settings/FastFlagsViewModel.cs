using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Voidstrap.Enums.FlagPresets;


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
        public uint dwNumberOfProcessors;
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
            get => App.FastFlags?.GetPreset("Telemetry.TelemetryV2Url") == "0.0.0.0";
            set
            {
                if (App.FastFlags == null) return;

                App.FastFlags.SetPreset("Telemetry.TelemetryV2Url", value ? "0.0.0.0" : null);
                App.FastFlags.SetPreset("Telemetry.Protocol", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.GraphicsQualityUsage", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.GpuVsCpuBound", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.RenderFidelity", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.RenderDistance", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.AudioPlugin", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.FmodErrors", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.SoundLength", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.AssetRequestV1", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.DeviceRAM", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.V2FrameRateMetrics", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.GlobalSkipUpdating", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.CallbackSafety", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.V2PointEncoding", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.ReplaceSeparator", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.OpenTelemetry", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.FLogTelemetry", value ? "0" : null);
                App.FastFlags.SetPreset("Telemetry.TelemetryService", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.PropertiesTelemetry", value ? "False" : null);
            }
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
            set
            {
                // Toggle the main memory probing flag
                App.FastFlags.SetPreset("Memory.Probe", value ? "True" : null);

                if (value)
                {
                    // When enabling memory probing, set all individual memory probe flags.
                    App.FastFlags.SetPreset("Memory.probe2", "0");            // DFIntMemoryUtilityCurveBaseHundrethsPercent: 0
                    App.FastFlags.SetPreset("Memory.probe3", "1");            // DFIntMemoryUtilityCurveFinalDeltaHundredths: 1
                    App.FastFlags.SetPreset("Memory.probe4", "1");            // DFIntMemoryUtilityCurveInitialDeltaHundredths: 1
                    App.FastFlags.SetPreset("Memory.probe5", "3");            // DFIntMemoryUtilityCurveNumSegments: 3
                    App.FastFlags.SetPreset("Memory.probe6", "2000000000");   // DFIntMemoryUtilityCurvePenaltyBuffer: 2000000000
                    App.FastFlags.SetPreset("Memory.probe7", "1");            // DFIntMemoryUtilityCurveSlopeMultiplierHundreths: 1
                    App.FastFlags.SetPreset("Memory.probe8", "102400");       // DFIntMemoryUtilityCurveTotalMemoryReserve: 102400
                }
                else
                {
                    // When disabling memory probing, clear all individual probe flags.
                    App.FastFlags.SetPreset("Memory.probe2", null);
                    App.FastFlags.SetPreset("Memory.probe3", null);
                    App.FastFlags.SetPreset("Memory.probe4", null);
                    App.FastFlags.SetPreset("Memory.probe5", null);
                    App.FastFlags.SetPreset("Memory.probe6", null);
                    App.FastFlags.SetPreset("Memory.probe7", null);
                    App.FastFlags.SetPreset("Memory.probe8", null);
                }
            }
        }


        public bool MoreSensetivityNumbers
        {
            get => App.FastFlags.GetPreset("UI.SensetivityNumbers") == "False";
            set => App.FastFlags.SetPreset("UI.SensetivityNumbers", value ? "False" : null);
        }

        public bool NoGuiBlur
        {
            get => App.FastFlags.GetPreset("UI.NoGuiBlur") == "0";
            set => App.FastFlags.SetPreset("UI.NoGuiBlur", value ? "0" : null);
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
        public bool OptimizeCFrameUpdates
        {
            get => App.FastFlags.GetPreset("OptimizeCFrameUpdates") == "True";
            set
            {
                App.FastFlags.SetPreset("OptimizeCFrameUpdates", value ? "True" : null);
                App.FastFlags.SetPreset("OptimizeCFrameUpdatesIC", value ? "True" : null);
            }
        }

        public bool TextSizeChanger
        {
            get => App.FastFlags.GetPreset("UI.TextSize1") == "True";
            set
            {
                App.FastFlags.SetPreset("UI.TextSize1", value ? "True" : null);
                App.FastFlags.SetPreset("UI.TextSize2", value ? "True" : null);
            }
        }

        public bool TextureRemover
        {
            get => App.FastFlags.GetPreset("Rendering.RemoveTexture1") == "True";
            set
            {
                App.FastFlags.SetPreset("Rendering.RemoveTexture1", value ? "True" : null);
                App.FastFlags.SetPreset("Rendering.RemoveTexture2", value ? "10000" : null);
            }
        }

        public bool Threading
        {
            get => App.FastFlags.GetPreset("Hyper.Threading1") == "True";
            set
            {
                App.FastFlags.SetPreset("Hyper.Threading1", value ? "True" : null);
            }
        }

        public bool LessLagSpikes
        {
            get => App.FastFlags.GetPreset("Network.DefaultBps") == "796850000";
            set
            {
                App.FastFlags.SetPreset("Network.DefaultBps", value ? "796850000" : null);
                App.FastFlags.SetPreset("Network.MaxWorkCatchupMs", value ? "5" : null);
            }
        }

        public bool DisableAds
        {
            get => App.FastFlags.GetPreset("UI.DisableAds1") == "False";
            set
            {
                App.FastFlags.SetPreset("UI.DisableAds1", value ? "False" : null);
                App.FastFlags.SetPreset("UI.DisableAds2", value ? "False" : null);
                App.FastFlags.SetPreset("UI.DisableAds3", value ? "False" : null);
                App.FastFlags.SetPreset("UI.DisableAds4", value ? "False" : null);
                App.FastFlags.SetPreset("UI.DisableAds5", value ? "False" : null);
                App.FastFlags.SetPreset("UI.DisableAds6", value ? "False" : null);
            }
        }

        public bool RobloxCore
        {
            get => App.FastFlags.GetPreset("Network.RCore1") == "20000";
            set
            {
                App.FastFlags.SetPreset("Network.RCore1", value ? "20000" : null);
                App.FastFlags.SetPreset("Network.RCore2", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.RCore3", value ? "10" : null);
                App.FastFlags.SetPreset("Network.RCore4", value ? "3000" : null);
                App.FastFlags.SetPreset("Network.RCore5", value ? "25" : null);
                App.FastFlags.SetPreset("Network.RCore6", value ? "5000" : null);
            }
        }

        public bool NoPayloadLimit
        {
            get => App.FastFlags.GetPreset("Network.Payload1") == "2147483647";
            set
            {
                App.FastFlags.SetPreset("Network.Payload1", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.Payload2", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.Payload3", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.Payload4", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.Payload5", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.Payload6", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.Payload7", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.Payload8", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.Payload9", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.Payload10", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.Payload11", value ? "2147483647" : null);
            }
        }

        public bool ShadersEnabled
        {
            get => App.FastFlags.GetPreset("Rendering.Shaders2") == "21";
            set
            {
                App.FastFlags.SetPreset("Rendering.Shaders2", value ? "21" : "0");
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
                App.FastFlags.SetPreset("Network.EngineModule1", value ? "False" : null);
                App.FastFlags.SetPreset("Network.EngineModule2", value ? "True" : null);
                App.FastFlags.SetPreset("Network.SerializeRead", value ? "True" : null);
                App.FastFlags.SetPreset("Network.SerializeWrite", value ? "True" : null);
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


        public bool ChatBubble
        {
            get => App.FastFlags.GetPreset("UI.Chatbubble") == "False";
            set => App.FastFlags.SetPreset("UI.Chatbubble", value ? "False" : null);
        }

        public bool NoMoreMiddle
        {
            get => App.FastFlags.GetPreset("UI.RemoveMiddle") == "False";
            set => App.FastFlags.SetPreset("UI.RemoveMiddle", value ? "False" : null);
        }

        public bool DisplayFps
        {
            get => App.FastFlags.GetPreset("Rendering.DisplayFps") == "True";
            set => App.FastFlags.SetPreset("Rendering.DisplayFps", value ? "True" : null);
        }

        public bool GrayAvatar
        {
            get => App.FastFlags.GetPreset("Rendering.GrayAvatar") == "0";
            set => App.FastFlags.SetPreset("Rendering.GrayAvatar", value ? "0" : null);
        }

        public bool UseFastFlagManager
        {
            get => App.Settings.Prop.UseFastFlagManager;
            set => App.Settings.Prop.UseFastFlagManager = value;
        }

        public int FramerateLimit
        {
            get => int.TryParse(App.FastFlags.GetPreset("Rendering.Framerate"), out int result) ? result : 0;
            set
            {
                App.FastFlags.SetPreset("Rendering.Framerate", value == 0 ? null : value);
                if (value > 240)
                {
                    Frontend.ShowMessageBox(
                        "Going above 240 FPS is not recommended, as this may cause latency issues.",
                        MessageBoxImage.Warning,
                        MessageBoxButton.OK
                    );
                    App.FastFlags.SetPreset("FpsFix.Log", "False");
                }
                else
                {
                    App.FastFlags.SetPreset("FpsFix.Log", null);
                }
            }
        }

        public int ShadersLimit
        {
            get => int.TryParse(App.FastFlags.GetPreset("Rendering.Shaders"), out int result) ? result : 0;
            set
            {
                App.FastFlags.SetPreset("Rendering.Shaders", value == 0 ? null : value.ToString());

                if (value < -64000000)
                {
                    Frontend.ShowMessageBox(
                        "Going below -64000000 is not recommended for Performance.",
                        MessageBoxImage.Warning,
                        MessageBoxButton.OK
                    );
                }
            }
        }


        public int VolChatLimit
        {
            get => int.TryParse(App.FastFlags.GetPreset("VoiceChat.VoiceChat4"), out int x) ? x : 1000;
            set => App.FastFlags.SetPreset("VoiceChat.VoiceChat4", value > 0 ? value.ToString() : null);
        }



        public int HideGUI
        {
            get => int.TryParse(App.FastFlags.GetPreset("UI.Hide"), out int x) ? x : 0;
            set => App.FastFlags.SetPreset("UI.Hide", value > 0 ? value.ToString() : null);
        }

        public int MtuSize
        {
            get => int.TryParse(App.FastFlags.GetPreset("Network.Mtusize"), out int x) ? x : 0;
            set
            {
                int clamped = Math.Max(0, Math.Min(1500, value));
                App.FastFlags.SetPreset(
                    "Network.Mtusize",
                    clamped >= 576 ? clamped.ToString() : null
                );
            }
        }

        public bool BGRA
        {
            get => App.FastFlags.GetPreset("Rendering.BGRA") == "True";
            set => App.FastFlags.SetPreset("Rendering.BGRA", value ? "True" : null);
        }

        public bool NewFpsSystem
        {
            get => App.FastFlags.GetPreset("Rendering.NewFpsSystem") == "True";
            set => App.FastFlags.SetPreset("Rendering.NewFpsSystem", value ? "True" : null);
        }

        public bool DisableWebview2Telemetry
        {
            get => App.FastFlags?.GetPreset("Telemetry.Webview1") == "www.youtube-nocookie.com";
            set
            {
                App.FastFlags.SetPreset("Telemetry.Webview1", value ? "www.youtube-nocookie.com" : null);
                App.FastFlags.SetPreset("Telemetry.Webview2", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Webview3", value ? "0" : null);
                App.FastFlags.SetPreset("Telemetry.Webview4", value ? "0" : null);
                App.FastFlags.SetPreset("Telemetry.Webview5", value ? "0" : null);
                App.FastFlags.SetPreset("Telemetry.Webview6", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Webview7", value ? "False" : null);
            }
        }


        public bool WorserParticles
        {
            get => App.FastFlags?.GetPreset("Rendering.WorserParticles1") == "False";
            set
            {
                App.FastFlags.SetPreset("Rendering.WorserParticles1", value ? "False" : null);
                App.FastFlags.SetPreset("Rendering.WorserParticles2", value ? "False" : null);
                App.FastFlags.SetPreset("Rendering.WorserParticles3", value ? "False" : null);
                App.FastFlags.SetPreset("Rendering.WorserParticles4", value ? "False" : null);
            }
        }

        public bool LowPolyMeshes
        {
            get => App.FastFlags.GetPreset("Rendering.LowPolyMeshes1") == "0";
            set
            {
                App.FastFlags.SetPreset("Rendering.LowPolyMeshes1", value ? "0" : null);
                App.FastFlags.SetPreset("Rendering.LowPolyMeshes2", value ? "0" : null);
                App.FastFlags.SetPreset("Rendering.LowPolyMeshes3", value ? "0" : null);
                App.FastFlags.SetPreset("Rendering.LowPolyMeshes4", value ? "0" : null);
            }
        }
        public bool CacheSizeImprovement
        {
            get => App.FastFlags.GetPreset("Cache.Increase1") == "True";
            set
            {
                App.FastFlags.SetPreset("Cache.Increase1", value ? "True" : null);
                App.FastFlags.SetPreset("Cache.Increase2", value ? "False" : null);
                App.FastFlags.SetPreset("Cache.Increase3", value ? "True" : null);
                App.FastFlags.SetPreset("Cache.Increase4", value ? "1" : null);
                App.FastFlags.SetPreset("Cache.Increase5", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase6", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase7", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase8", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase9", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase10", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase11", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase12", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase13", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase14", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase15", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Cache.Increase16", value ? "True" : null);
                App.FastFlags.SetPreset("Cache.Increase17", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase18", value ? "1036372536" : null);
                App.FastFlags.SetPreset("Cache.Increase19", value ? "1036372536" : null);
            }
        }


        public bool AndroidVfs
        {
            get => App.FastFlags.GetPreset("Rendering.AndroidVfs") == "{\"and\":[ {\"=\":[\"app_bitness()\",32]}, {\"not\":[ {\"is_any_of\":[\"manufacturer()\",\"samsung\",\"amazon\",\"lge\",\"lg\",\"lg electronics\",\"vivo\"]} ]} ]}";
            set => App.FastFlags.SetPreset("Rendering.AndroidVfs", value ? "{\"and\":[ {\"=\":[\"app_bitness()\",32]}, {\"not\":[ {\"is_any_of\":[\"manufacturer()\",\"samsung\",\"amazon\",\"lge\",\"lg\",\"lg electronics\",\"vivo\"]} ]} ]}" : null);
        }

        public bool FasterLoading
        {
            get => App.FastFlags.GetPreset("Network.MaxAssetPreload") == "2147483647";
            set
            {
                App.FastFlags.SetPreset("Network.MaxAssetPreload", value ? "2147483647" : null);
                App.FastFlags.SetPreset("Network.PlayerImageDefault", value ? "1" : null);
                App.FastFlags.SetPreset("Network.MeshPreloadding", value ? "True" : null);
            }
        }
        
        public bool EnableCustomDisconnectError
        {
            get => App.FastFlags.GetPreset("UI.CustomDisconnectError1") == "True";
            set => App.FastFlags.SetPreset("UI.CustomDisconnectError1", value ? "True" : null);
        }

        public string? CustomDisconnectError
        {
            get => App.FastFlags.GetPreset("UI.CustomDisconnectError2");
            set => App.FastFlags.SetPreset("UI.CustomDisconnectError2", value);
        }

        public string? FakeVerify
        {
            get => App.FastFlags.GetPreset("Fake.Verify");
            set => App.FastFlags.SetPreset("Fake.Verify", value);
        }

        public string? NewCamera
        {
            get => App.FastFlags.GetPreset("Camera.Controls");
            set => App.FastFlags.SetPreset("Camera.Controls", value);
        }

        public string? ChatUI
        {
            get => App.FastFlags.GetPreset("Camera.Chat");
            set => App.FastFlags.SetPreset("Camera.Chat", value);
        }

        public IReadOnlyDictionary<MSAAMode, string?> MSAALevels => FastFlagManager.MSAAModes;

        public MSAAMode SelectedMSAALevel
        {
            get => MSAALevels.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.MSAA1")).Key;
            set
            {
                App.FastFlags.SetPreset("Rendering.MSAA1", MSAALevels[value]);
                App.FastFlags.SetPreset("Rendering.MSAA2", MSAALevels[value]);

            }
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

        public IReadOnlyDictionary<DynamicResolution, string?> DynamicResolutions => FastFlagManager.DynamicResolutions;

        public DynamicResolution SelectedDynamicResolution
        {
            get => DynamicResolutions.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.Dynamic.Resolution")).Key;
            set
            {
                if (value == DynamicResolution.Resolution2)
                {
                    App.FastFlags.SetPreset("Rendering.Dynamic.Resolution", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.Dynamic.Resolution", DynamicResolutions[value]);
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

        public IReadOnlyDictionary<Presents, string?> PresentsLevels => FastFlagManager.PresentsStartMappings;

        public Presents SelectedPresentsLevel
        {
            get => PresentsLevels.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.MSAA")).Key;
            set => App.FastFlags.SetPreset("Rendering.MSAA", PresentsLevels[value]);
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

        public bool TaskSchedulerAvoidingSleep
        {
            get => App.FastFlags.GetPreset("Rendering.AvoidSleep") == "True";
            set => App.FastFlags.SetPreset("Rendering.AvoidSleep", value ? "True" : null);
        }

        public bool AdsToggle
        {
            get => App.FastFlags.GetPreset("UI.Disable.Ads") == "True";
            set => App.FastFlags.SetPreset("UI.Disable.Ads", value ? "True" : null);
        }

        public bool DisablePlayerShadows
        {
            get => App.FastFlags.GetPreset("Rendering.ShadowIntensity") == "0";
            set
            {
                App.FastFlags.SetPreset("Rendering.ShadowIntensity", value ? "0" : null);
                App.FastFlags.SetPreset("Rendering.Pause.Voxelizer", value ? "True" : null);
                App.FastFlags.SetPreset("Rendering.ShadowMapBias", value ? "-1" : null);
            }
        }

        public bool RenderOcclusion
        {
            get => App.FastFlags.GetPreset("Rendering.Occlusion1") == "True";
            set
            {
                App.FastFlags.SetPreset("Rendering.Occlusion1", value ? "True" : null);
                App.FastFlags.SetPreset("Rendering.Occlusion2", value ? "True" : null);
                App.FastFlags.SetPreset("Rendering.Occlusion3", value ? "True" : null);
            }
        }

        public bool EnableGraySky
        {
            get => App.FastFlags.GetPreset("Graphic.GraySky") == "True";
            set => App.FastFlags.SetPreset("Graphic.GraySky", value ? "True" : null);
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

        public bool DisableLayeredClothing
        {
            get => App.FastFlags.GetPreset("UI.DisableLayeredClothing") == "-1";
            set => App.FastFlags.SetPreset("UI.DisableLayeredClothing", value ? "-1" : null);
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

        public string ForceBuggyVulkan
        {
            get => App.FastFlags.GetPreset("Rendering.ForceVulkan") ?? "Automatic";
            set => App.FastFlags.SetPreset("Rendering.ForceVulkan", value == "Automatic" ? null : value);
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
            get => App.FastFlags.GetPreset("UI.Menu.ChromeUI") == "True" && App.FastFlags.GetPreset("UI.Menu.ChromeUI2") == "True";
            set
            {
                App.FastFlags.SetPreset("UI.Menu.ChromeUI", value ? "True" : null);
                App.FastFlags.SetPreset("UI.Menu.ChromeUI2", value ? "True" : null);
            }
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

        public bool ChatTranslation
        {
            get => GetFlagAsBool("Menu.ChatTranslation");
            set => SetFlagFromBool("Menu.ChatTranslation", value);
        }

        public bool FrameRateCap
        {
            get => GetFlagAsBool("Menu.Framerate");
            set => SetFlagFromBool("Menu.Framerate", value);
        }

        public bool DisableVoiceChatTelemetry
        {
            get => App.FastFlags?.GetPreset("Telemetry.Voicechat1") == "False";
            set
            {
                App.FastFlags.SetPreset("Telemetry.Voicechat1", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat2", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat3", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat4", value ? "0" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat5", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat6", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat7", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat8", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat9", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat10", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat11", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat12", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat13", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat14", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat15", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat16", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat17", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat18", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat19", value ? "0" : null);
                App.FastFlags.SetPreset("Telemetry.Voicechat20", value ? "-1" : null);
            }
        }

        public bool OldChromeUI
        {
            get => App.FastFlags?.GetPreset("UI.OldChromeUI1") == "False";
            set
            {
                App.FastFlags.SetPreset("UI.OldChromeUI1", value ? "False" : null);
                App.FastFlags.SetPreset("UI.OldChromeUI2", value ? "False" : null);
                App.FastFlags.SetPreset("UI.OldChromeUI3", value ? "False" : null);
                App.FastFlags.SetPreset("UI.OldChromeUI4", value ? "False" : null);
                App.FastFlags.SetPreset("UI.OldChromeUI5", value ? "False" : null);
                App.FastFlags.SetPreset("UI.OldChromeUI6", value ? "False" : null);
                App.FastFlags.SetPreset("UI.OldChromeUI7", value ? "False" : null);
                App.FastFlags.SetPreset("UI.OldChromeUI8", value ? "True" : null);
                App.FastFlags.SetPreset("UI.OldChromeUI9", value ? "False" : null);
                App.FastFlags.SetPreset("UI.OldChromeUI10", value ? "False" : null);
            }
        }

        public bool BlockTencent
        {
            get => App.FastFlags?.GetPreset("Telemetry.Tencent1") == "/tencent/";
            set
            {
                App.FastFlags.SetPreset("Telemetry.Tencent1", value ? "/tencent/" : null);
                App.FastFlags.SetPreset("Telemetry.Tencent2", value ? "/tencent/" : null);
                App.FastFlags.SetPreset("Telemetry.Tencent3", value ? "https://www.gov.cn" : null);
                App.FastFlags.SetPreset("Telemetry.Tencent4", value ? "https://www.gov.cn" : null);
                App.FastFlags.SetPreset("Telemetry.Tencent5", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Tencent6", value ? "False" : null);
                App.FastFlags.SetPreset("Telemetry.Tencent7", value ? "10000" : null);

            }
        }

        public bool WhiteSky
        {
            get => App.FastFlags.GetPreset("Graphic.WhiteSky") == "True";
            set
            {
                App.FastFlags.SetPreset("Graphic.WhiteSky", value ? "True" : null);
                App.FastFlags.SetPreset("Graphic.GraySky", value ? "True" : null);
            }
        }

        public bool ShowChunks
        {
            get => App.FastFlags.GetPreset("Debug.Chunks") == "True";
            set => App.FastFlags.SetPreset("Debug.Chunks", value ? "True" : null);
        }

        public bool Pseudolocalization
        {
            get => App.FastFlags.GetPreset("UI.Pseudolocalization") == "True";
            set => App.FastFlags.SetPreset("UI.Pseudolocalization", value ? "True" : null);
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

        public int FPSBufferPercentage
        {
            get => int.TryParse(App.FastFlags.GetPreset("Rendering.FrameRateBufferPercentage"), out int x) ? x : 0;
            set
            {
                int clamped = Math.Max(0, Math.Min(100, value));
                App.FastFlags.SetPreset(
                    "Rendering.FrameRateBufferPercentage",
                    clamped >= 1 ? clamped.ToString() : null
                );
            }
        }

        public bool BetterPacketSending
        {
            get => App.FastFlags?.GetPreset("Network.BetterPacketSending1") == "0";
            set
            {
                App.FastFlags.SetPreset("Network.BetterPacketSending1", value ? "0" : null);
                App.FastFlags.SetPreset("Network.BetterPacketSending2", value ? "1" : null);
                App.FastFlags.SetPreset("Network.BetterPacketSending3", value ? "1" : null);
                App.FastFlags.SetPreset("Network.BetterPacketSending4", value ? "1" : null);
                App.FastFlags.SetPreset("Network.BetterPacketSending5", value ? "1" : null);
                App.FastFlags.SetPreset("Network.BetterPacketSending6", value ? "1047483647" : null);
                App.FastFlags.SetPreset("Network.BetterPacketSending7", value ? "5000000" : null);
                App.FastFlags.SetPreset("Network.BetterPacketSending8", value ? "1" : null);
                App.FastFlags.SetPreset("Network.BetterPacketSending9", value ? "1047483647" : null);
            }
        }

        public int BufferArrayLength
        {
            get => int.TryParse(App.FastFlags.GetPreset("Recommended.Buffer"), out int x) ? x : 0;
            set => App.FastFlags.SetPreset("Recommended.Buffer", value == 0 ? null : value);
        }

        public bool MinimalRendering
        {
            get => App.FastFlags.GetPreset("Rendering.MinimalRendering") == "True";
            set => App.FastFlags.SetPreset("Rendering.MinimalRendering", value ? "True" : null);
        }

        public bool DisableSky
        {
            get => App.FastFlags.GetPreset("Rendering.NoFrmBloom") == "False";
            set
            {
                App.FastFlags.SetPreset("Rendering.NoFrmBloom", value ? "False" : null);
                App.FastFlags.SetPreset("Rendering.FRMRefactor", value ? "False" : null);
            }
        }


        public IReadOnlyDictionary<RefreshRate, string?> RefreshRates => FastFlagManager.RefreshRates;
        public RefreshRate SelectedRefreshRate
        {
            get => RefreshRates.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("System.TargetRefreshRate1")).Key;
            set
            {
                if (value == RefreshRate.Default)
                {
                    App.FastFlags.SetPreset("System.TargetRefreshRate1", null);
                    App.FastFlags.SetPreset("System.TargetRefreshRate2", null);
                    App.FastFlags.SetPreset("System.TargetRefreshRate3", null);
                    App.FastFlags.SetPreset("System.TargetRefreshRate4", null);
                }
                else
                {
                    App.FastFlags.SetPreset("System.TargetRefreshRate1", RefreshRates[value]);
                    App.FastFlags.SetPreset("System.TargetRefreshRate2", RefreshRates[value]);
                    App.FastFlags.SetPreset("System.TargetRefreshRate3", RefreshRates[value]);
                    App.FastFlags.SetPreset("System.TargetRefreshRate4", RefreshRates[value]);
                }
            }
        }

        public IReadOnlyDictionary<Shader, string?> Shaders => FastFlagManager.Shaders;

        public Shader SelectedShaderLevel
        {
            get => Shaders.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.Shaders")).Key;
            set
            {
                if (value == Shader.Disabled)
                {
                    App.FastFlags.SetPreset("Rendering.Shaders", null);
                    App.FastFlags.SetPreset("Rendering.Shaders2", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.Shaders", Shaders[value]);
                    App.FastFlags.SetPreset("Rendering.Shaders2", "21");
                }
            }
        }



        public static IReadOnlyDictionary<string, string?> GetCpuThreads()
        {
            const string LOG_IDENT = "FFlagPresets::GetCpuThreads";
            var cpuThreads = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Automatic"] = null
            };

            try
            {
                int logicalProcessorCount = SystemInfo.GetLogicalProcessorCount();

                if (logicalProcessorCount > 0)
                {
                    for (int i = 1; i <= logicalProcessorCount; i++)
                    {
                        string key = i.ToString();
                        cpuThreads[key] = key;
                    }
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "Logical processor count returned 0.");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to get CPU thread count: {ex.Message}");
            }

            return cpuThreads;
        }


        public string BypassVulkan
        {
            get => App.FastFlags.GetPreset("System.BypassVulkan") ?? "Automatic";
            set => App.FastFlags.SetPreset("System.BypassVulkan", value == "Automatic" ? null : value);
        }


        public IReadOnlyDictionary<string, string?>? CpuThreads => GetCpuThreads();
        public KeyValuePair<string, string?> SelectedCpuThreads
        {
            get
            {
                string currentValue = App.FastFlags.GetPreset("System.CpuCore1") ?? "Automatic";
                return CpuThreads?.FirstOrDefault(kvp => kvp.Key == currentValue) ?? default;
            }
            set
            {
                App.FastFlags.SetPreset("System.CpuCore1", value.Value);
                OnPropertyChanged(nameof(SelectedCpuThreads));
                App.FastFlags.SetPreset("System.CpuCore2", value.Value);
                OnPropertyChanged(nameof(SelectedCpuThreads));
                App.FastFlags.SetPreset("System.CpuCore3", value.Value);
                OnPropertyChanged(nameof(SelectedCpuThreads));
                App.FastFlags.SetPreset("System.CpuCore4", value.Value);
                OnPropertyChanged(nameof(SelectedCpuThreads));
                App.FastFlags.SetPreset("System.CpuCore5", value.Value);
                OnPropertyChanged(nameof(SelectedCpuThreads));
                App.FastFlags.SetPreset("System.CpuCore6", value.Value);
                OnPropertyChanged(nameof(SelectedCpuThreads));
                App.FastFlags.SetPreset("System.CpuCore7", value.Value);
                OnPropertyChanged(nameof(SelectedCpuThreads));
                App.FastFlags.SetPreset("System.CpuCore9", value.Value);
                OnPropertyChanged(nameof(SelectedCpuThreads));
                if (value.Value != null && int.TryParse(value.Value, out int parsedValue)) // sets cputhreads to the selected amount minus 1
                {
                    int adjustedValue = Math.Max(parsedValue - 1, 1); // Ensure the value does not go below on one
                    App.FastFlags.SetPreset("System.CpuThreads", adjustedValue.ToString());
                    OnPropertyChanged(nameof(SelectedCpuThreads));
                    App.FastFlags.SetPreset("System.CpuCore8", adjustedValue.ToString());
                    OnPropertyChanged(nameof(SelectedCpuThreads));
                }
                else
                {
                    // Handle the case where value.Value is null or not a valid integer
                    App.FastFlags.SetPreset("System.CpuThreads", null);
                    OnPropertyChanged(nameof(SelectedCpuThreads));
                    App.FastFlags.SetPreset("System.CpuCore8", null);
                    OnPropertyChanged(nameof(SelectedCpuThreads));
                }

            }
        }

        public static IReadOnlyDictionary<string, string?> GetCpuCoreMinThreadCount()
        {
            const string LOG_IDENT = "FFlagPresets::GetCpuCoreMinThreadCount";
            Dictionary<string, string?> cpuThreads = new();

            // Add the "Automatic" option
            cpuThreads.Add("Automatic", null);

            try
            {
                // Use physical core count or logical, whichever you want:
                int coreCount = SystemInfo.GetLogicalProcessorCount();

                // Add options for 1, 2, ..., coreCount
                for (int i = 1; i <= coreCount; i++)
                {
                    cpuThreads.Add(i.ToString(), i.ToString());
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to get CPU thread count: {ex.Message}");
            }

            return cpuThreads;
        }

        public IReadOnlyDictionary<string, string?>? CpuCoreMinThreadCount => GetCpuCoreMinThreadCount();

        public KeyValuePair<string, string?> SelectedCpuCoreMinThreadCount
        {
            get
            {
                string currentValue = App.FastFlags.GetPreset("System.CpuCoreMinThreadCount") ?? "Automatic";
                return CpuThreads?.FirstOrDefault(kvp => kvp.Key == currentValue) ?? default;
            }
            set
            {
                // Save selected value as-is
                App.FastFlags.SetPreset("System.CpuCoreMinThreadCount", value.Value);
                OnPropertyChanged(nameof(SelectedCpuThreads));

                if (value.Value != null && int.TryParse(value.Value, out int parsedValue))
                {
                    // Adjust to at least 0 (not below)
                    int adjustedValue = Math.Max(parsedValue - 1, 1);
                    App.FastFlags.SetPreset("System.CpuCoreMinThreadCount", adjustedValue.ToString());
                }
                else
                {
                    App.FastFlags.SetPreset("System.CpuCoreMinThreadCount", null);
                }
                OnPropertyChanged(nameof(SelectedCpuCoreMinThreadCount));
            }
        }


        // INotifyPropertyChanged implementation
        public new event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
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

        private string selectedProfileMods = string.Empty;

        public string SelectedProfileMods { get => selectedProfileMods; set => SetProperty(ref selectedProfileMods, value); }
    }
}