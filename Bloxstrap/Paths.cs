namespace Hellstrap
{
    static class Paths
    {
        // Directories that are not dependent on the base directory
        public static string Temp => Path.Combine(Path.GetTempPath(), App.ProjectName);
        public static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        public static string WindowsStartMenu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        public static string System => Environment.GetFolderPath(Environment.SpecialFolder.System);
        public static string Process => Environment.ProcessPath!;

        public static string TempUpdates => Path.Combine(Temp, "Updates");
        public static string TempLogs => Path.Combine(Temp, "Logs");

        // Base directory-dependent paths
        public static string Base { get; private set; } = string.Empty;
        public static string Downloads { get; private set; } = string.Empty;
        public static string SavedFlagProfiles { get; private set; } = string.Empty;
        public static string Logs { get; private set; } = string.Empty;
        public static string Integrations { get; private set; } = string.Empty;
        public static string Versions { get; private set; } = string.Empty;
        public static string Modifications { get; private set; } = string.Empty;
        public static string Roblox { get; private set; } = string.Empty;
        public static string CustomThemes { get; private set; } = string.Empty;
        public static string Application { get; private set; } = string.Empty;

        public static string CustomFont => Path.Combine(Modifications, "content", "fonts", "CustomFont.ttf");

        public static bool Initialized => !string.IsNullOrWhiteSpace(Base);

        public static void Initialize(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory cannot be null or empty.", nameof(baseDirectory));

            Base = baseDirectory;
            Downloads = Path.Combine(Base, "Downloads");
            SavedFlagProfiles = Path.Combine(Base, "SavedFlagProfiles");
            Logs = Path.Combine(Base, "Logs");
            Integrations = Path.Combine(Base, "Integrations");
            Versions = Path.Combine(Base, "Versions");
            Modifications = Path.Combine(Base, "Modifications");
            Roblox = Path.Combine(Base, "Roblox");
            CustomThemes = Path.Combine(Base, "CustomThemes");

            Application = Path.Combine(Base, $"{App.ProjectName}.exe");
        }
    }
}
