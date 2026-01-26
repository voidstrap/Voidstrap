using System.Collections.ObjectModel;
using System.IO;
using Voidstrap.Enums;

namespace Voidstrap.Models.Persistable
{
    /// <summary>
    /// Represents configuration settings for Voidstrap.
    /// </summary>
    public class AppSettings
    {
        // General Configuration
        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.FluentAeroDialog;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconVoidstrap;
        public CleanerOptions CleanerOptions { get; set; } = CleanerOptions.Never;
        public List<string> CleanerDirectories { get; set; } = new List<string>();
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = "";
        public Theme Theme2 { get; set; } = Theme.Dark;
        public string? SelectedCustomTheme { get; set; } = null;
        public bool CheckForUpdates { get; set; } = true;
        public string SelectedCpuPriority { get; set; } = "Automatic";
        public int MaxCpuCores { get; set; } = Environment.ProcessorCount;
        public int TotalLogicalCores { get; set; } = Environment.ProcessorCount;
        public int TotalPhysicalCores { get; set; } = Environment.ProcessorCount;
        public bool IsChannelEnabled { get; set; } = false;
        public bool UpdateRoblox { get; set; } = true;

        public int CleanRobloxNumber = 0;
        public bool DisableCrash { get; set; } = false;
        public int CpuCoreLimit { get; set; } = Environment.ProcessorCount;
        public string ShiftlockCursorSelectedPath { get; set; } = "";
        public string UseCustomIcon { get; set; } = "";
        public string CustomGameName { get; set; } = "";
        public string PriorityLimit { get; set; } = "Normal";
        public string SelectedStatus { get; set; } = "Gray";
        public string ArrowCursorSelectedPath { get; set; } = "";
        public string ArrowFarCursorSelectedPath { get; set; } = "";
        public string IBeamCursorSelectedPath { get; set; } = "";

        public bool DisableSplashScreen { get; set; } = true;
        public bool EnableAnalytics { get; set; } = true;
        public bool ShouldExportConfig { get; set; } = true;
        public bool ShouldExportLogs { get; set; } = true;
        public bool UseFastFlagManager { get; set; } = true;
        public bool WPFSoftwareRender { get; set; } = false;
        public bool FixTeleports { get; set; } = false;
        public bool ConfirmLaunches { get; set; } = true;
        public bool HasLaunchedGame { get; set; } = false;
        public bool BackgroundWindow { get; set; } = true;
        public bool UsePlaceId { get; set; } = false;
        public string PlaceId { get; set; } = "";
        public bool OptimizeRoblox { get; set; } = false;
        public bool BackgroundUpdatesEnabled { get; set; } = true;
        public bool VoidNotify { get; set; } = true;
        public bool MultiInstanceLaunching { get; set; } = false;
        public bool ServerPingCounter { get; set; } = false;
        public bool ShowServerDetailsUI { get; set; } = false;
        public bool EnableCustomStatusDisplay { get; set; } = true;
        public bool RenameClientToEuroTrucks2 { get; set; } = false;
        public bool SnowWOWSOCOOLWpfSnowbtw { get; set; } = false;

        public bool MotionBlurOverlay { get; set; } = false;

        public bool MotionBlurOverlay2 { get; set; } = false;

        public string ClientPath { get; set; } = Path.Combine(Paths.Base, "Roblox", "Player");

        public string Locale { get; set; } = "nil";
        public string BufferSizeKbte { get; set; } = "1024";
        public string BufferSizeKbtes { get; set; } = "2048";
        public string SkyboxName { get; set; } = "Default";
        public string FontName { get; set; } = "Default";
        public string LastServerSave { get; set; } = "112757576021097";
        public bool SkyBoxDataSending { get; set; } = false;

        public bool FFlagRPCDisplayer { get; set; } = true;

        public bool FPSCounter { get; set; } = false;

        public bool CurrentTimeDisplay { get; set; } = false;
        public bool ExclusiveFullscreen { get; set; } = false;
        public bool Crosshair { get; set; } = false;
        public bool LockDefault { get; set; } = false;
        public bool GameWIP { get; set; } = false;
        public bool ForceRobloxLanguage { get; set; } = true;

        // Analytics & Tracking
        public bool DarkTextures { get; set; } = false;
        public bool EnableActivityTracking { get; set; } = true;
        public bool OverClockCPU { get; set; } = false;
        public bool exitondissy { get; set; } = false;
        public bool ServerUptimeBetterBLOXcuzitsbetterXD { get; set; } = true;

        public string DownloadingStringFormat { get; set; } = Strings.Bootstrapper_Status_Downloading + " {0} - {1}MB / {2}MB";
        public bool ConnectCloset { get; set; } = false;

        public bool Fullbright { get; set; } = false;

        public bool GameIconChecked { get; set; } = true;
        public bool ServerLocationGame { get; set; } = false;
        public bool GameNameChecked { get; set; } = true;
        public bool GameCreatorChecked { get; set; } = true;
        public bool GameStatusChecked { get; set; } = true;

        public bool DX12Like { get; set; } = false;

        // Rich Presence (Discord Integration)
        public bool UseDiscordRichPresence { get; set; } = true;
        public bool HideRPCButtons { get; set; } = true;
        public bool ShowAccountOnRichPresence { get; set; } = true;
        public bool MultiAccount { get; set; } = false;
        public bool ShowServerDetails { get; set; } = true;

        // Mod Settings
        public string CustomFontLocation { get; set; } = string.Empty;
        public CursorType CursorType { get; set; } = CursorType.Default;

        // Custom Integrations
        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = new();

        // Mod Preset Configuration
        public bool UseDisableAppPatch { get; set; } = false;

        // Roblox Deployment Settings
        public string Channel { get; set; } = RobloxInterfaces.Deployment.DefaultChannel;
        public string ChannelHash { get; set; } = "";

        public string LaunchGameID { get; set; } = "";
        public bool IsGameEnabled { get; set; } = false;
        public bool MatchUniverseId { get; set; } = true;
        public long? TargetUniverseId { get; set; }
        public bool IsBetterServersEnabled { get; set; } = false;
        public bool OverClockGPU { get; set; } = false;
        public bool GRADmentFR { get; set; } = false;
        public bool VoidRPC { get; set; } = true;

        public bool AntiAFK { get; set; } = false;

        public ResolutionSetting? InGameResolution { get; set; }

        public class ResolutionSetting
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int RefreshRate { get; set; }
        }
    }
}