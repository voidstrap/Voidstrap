using System.Collections.ObjectModel;
using System.IO;

namespace Hellstrap.Models.Persistable
{
    /// <summary>
    /// Represents configuration settings for Hellstrap.
    /// </summary>
    public class Settings
    {
        // General Configuration
        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.FluentAeroDialog;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconHellstrap;
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = string.Empty;
        public Theme Theme { get; set; } = Theme.Default;
        public string? SelectedCustomTheme { get; set; }

        // Update and Performance Settings
        public bool CheckForUpdates { get; set; } = true;
        public bool UseFastFlagManager { get; set; } = true;
        public bool WPFSoftwareRender { get; set; } = false;

        // Launch Configuration
        public bool ConfirmLaunches { get; set; } = true;
        public bool MultiInstanceLaunching { get; set; } = false;
        public bool RenameClientToEuroTrucks2 { get; set; } = false;
        public string ClientPath { get; set; } = Path.Combine(Paths.Base, "Roblox", "Player");

        // Localization
        public string Locale { get; set; } = "nil";
        public bool ForceRobloxLanguage { get; set; } = true;

        // Analytics & Tracking
        public bool EnableAnalytics { get; set; } = false;
        public bool EnableActivityTracking { get; set; } = true;

        // Rich Presence (Discord Integration)
        public bool UseDiscordRichPresence { get; set; } = true;
        public bool HideRPCButtons { get; set; } = true;
        public bool ShowAccountOnRichPresence { get; set; } = false;
        public bool ShowServerDetails { get; set; } = true;


        // Custom Integrations
        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = new();

        // Mod Preset Configuration
        public bool UseDisableAppPatch { get; set; } = false;

        // Roblox Deployment Settings
        public string Channel { get; set; } = Hellstrap.RobloxInterfaces.Deployment.DefaultChannel;
        public string ChannelHash { get; set; } = string.Empty;
        public string DownloadingStringFormat { get; set; } = $"{Strings.Bootstrapper_Status_Downloading} {{0}} - {{1}}MB / {{2}}MB";
    }
}
