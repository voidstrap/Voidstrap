using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;
using DiscordRPC;
using DiscordRPC.Logging;
using Wpf.Ui.Hardware;
using System.Windows.Media.Animation;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace Voidstrap
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#if QA_BUILD
        public const string ProjectName = "Voidstrap-QA";
#else
        public const string ProjectName = "Voidstrap";
#endif
        public const string ProjectOwner = "Voidstrap";
        public const string ProjectRepository = "Voidstrap/Voidstrap";
        public const string ProjectDownloadLink = "https://github.com/Voidstrap/Voidstrap/releases";
        public const string ProjectHelpLink = "https://github.com/BloxstrapLabs/Bloxstrap/wiki";
        public const string ProjectSupportLink = "https://github.com/Voidstrap/Voidstrap/issues/new";

        public const string RobloxPlayerAppName = "RobloxPlayerBeta";
        public const string RobloxStudioAppName = "RobloxStudioBeta";

        // simple shorthand for extremely frequently used and long string - this goes under HKCU
        public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProjectName}";

        public const string ApisKey = $"Software\\{ProjectName}";

        public static LaunchSettings LaunchSettings { get; private set; } = null!;

        public static readonly string RobloxCookiesFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Roblox\LocalStorage\RobloxCookies.dat");

        public static BuildMetadataAttribute BuildMetadata = Assembly.GetExecutingAssembly().GetCustomAttribute<BuildMetadataAttribute>()!;

        public static string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString();

        public static Bootstrapper? Bootstrapper { get; set; } = null!;

        public static bool IsActionBuild => !String.IsNullOrEmpty(BuildMetadata.CommitRef);

        public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);

        public static bool IsStudioVisible => !String.IsNullOrEmpty(App.State.Prop.Studio.VersionGuid);

        public static readonly MD5 MD5Provider = MD5.Create();

        public static readonly Logger Logger = new();

        public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();

        public static readonly JsonManager<Models.Persistable.AppSettings> Settings = new();

        public static readonly JsonManager<DownloadStats> DownloadStats = new();

        public static readonly JsonManager<State> State = new();

        public static readonly JsonManager<RobloxState> RobloxState = new();

        public static readonly FastFlagManager FastFlags = new();
        private static readonly MD5 mD5 = MD5.Create();

        // IDisposable fields to dispose on exit
        private static MD5? _md5Provider = mD5;

        private static HttpClient? _httpClient;
        public static HttpClient HttpClient => _httpClient ??= new HttpClient(
            new HttpClientLoggingHandler(
                new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }
            )
        );

        private static bool _showingExceptionDialog = false;
        public static DiscordRpcClient? DiscordClient;

        public static void Terminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::Terminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Environment.Exit(exitCodeNum);
        }

        public static void SoftTerminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::SoftTerminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Current.Dispatcher.Invoke(() => Current.Shutdown(exitCodeNum));
        }

        void GlobalExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Logger.WriteLine("App::GlobalExceptionHandler", "An exception occurred");

            FinalizeExceptionHandling(e.Exception);
        }

        public static void FinalizeExceptionHandling(AggregateException ex)
        {
            foreach (var innerEx in ex.InnerExceptions)
                Logger.WriteException("App::FinalizeExceptionHandling", innerEx);

            FinalizeExceptionHandling(ex.GetBaseException(), false);
        }

        public static void FinalizeExceptionHandling(Exception ex, bool log = true)
        {
            if (log)
                Logger.WriteException("App::FinalizeExceptionHandling", ex);

            if (_showingExceptionDialog)
                return;

            _showingExceptionDialog = true;

            SendLog();

            if (Bootstrapper?.Dialog != null)
            {
                if (Bootstrapper.Dialog.TaskbarProgressValue == 0)
                    Bootstrapper.Dialog.TaskbarProgressValue = 1; // make sure it's visible

                Bootstrapper.Dialog.TaskbarProgressState = TaskbarItemProgressState.Error;
            }

            Frontend.ShowExceptionDialog(ex);

            Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
        }

        public static async Task<GithubRelease?> GetLatestRelease()
        {
            const string LOG_IDENT = "App::GetLatestRelease";

            try
            {
                var releaseInfo = await Http.GetJson<GithubRelease>($""); //Glitchy so removed for now Adding back soon sowy

                if (releaseInfo is null || releaseInfo.Assets is null)
                {
                    Logger.WriteLine(LOG_IDENT, "Encountered invalid data");
                    return null;
                }

                return releaseInfo;
            }
            catch (Exception ex)
            {
                Logger.WriteException(LOG_IDENT, ex);
            }

            return null;
        }
        public static void SendStat(string key, string value)
        {

        }

        public static void SendLog()
        {

        }

        public static void AssertWindowsOSVersion()
        {
            const string LOG_IDENT = "App::AssertWindowsOSVersion";

            int major = Environment.OSVersion.Version.Major;
            if (major < 7)
            {
                Logger.WriteLine(LOG_IDENT, $"Detected unsupported Windows version ({Environment.OSVersion.Version}).");

                if (!LaunchSettings.QuietFlag.Active)
                    Frontend.ShowMessageBox("Your Windows Version is not supported with Voidstrap!", MessageBoxImage.Error);

                Terminate(ErrorCode.ERROR_INVALID_FUNCTION);
            }
        }
        private void InitializeDiscordRPC()
        {
            const string discordAppId = "1375529225230094507";

            DiscordClient = new DiscordRpcClient(discordAppId)
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning }
            };

            if (Settings.Prop.VoidstrapRPCReal)
            {
                DiscordClient.Initialize();

                // Set presence only after initialization
                DiscordClient.SetPresence(new DiscordRPC.RichPresence()
                {
                    State = "Using Voidstrap",
                    Assets = new DiscordRPC.Assets()
                    {
                        LargeImageKey = "large_image",
                        LargeImageText = "Voidstrap"
                    }
                });
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string LOG_IDENT = "App::OnStartup";

            Locale.Initialize();
            base.OnStartup(e);



        Logger.WriteLine(LOG_IDENT, $"Starting {ProjectName} v{Version}");

            string userAgent = $"{ProjectName}/{Version}";

            if (IsActionBuild)
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from commit {BuildMetadata.CommitHash} ({BuildMetadata.CommitRef})");

                if (IsProductionBuild)
                    userAgent += $" (Production)";
                else
                    userAgent += $" (Artifact {BuildMetadata.CommitHash}, {BuildMetadata.CommitRef})";
            }
            else
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from {BuildMetadata.Machine}");

#if QA_BUILD
                userAgent += " (QA)";
#else
                userAgent += $" (Build {Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildMetadata.Machine))})";
#endif
            }

            Logger.WriteLine(LOG_IDENT, $"Loaded from {Paths.Process}");
            Logger.WriteLine(LOG_IDENT, $"Temp path is {Paths.Temp}");
            Logger.WriteLine(LOG_IDENT, $"WindowsStartMenu path is {Paths.WindowsStartMenu}");

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.


            HttpClient.Timeout = TimeSpan.FromSeconds(30);
            HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

            LaunchSettings = new LaunchSettings(e.Args);

            // installation check begins here
            using var uninstallKey = Registry.CurrentUser.OpenSubKey(UninstallKey);
            string? installLocation = null;
            bool fixInstallLocation = false;

            if (uninstallKey?.GetValue("InstallLocation") is string value)
            {
                if (Directory.Exists(value))
                {
                    installLocation = value;
                }
                else
                {
                    var match = Regex.Match(value, @"^[a-zA-Z]:\\Users\\([^\\]+)", RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        string newLocation = value.Replace(match.Value, Paths.UserProfile, StringComparison.InvariantCultureIgnoreCase);

                        if (Directory.Exists(newLocation))
                        {
                            installLocation = newLocation;
                            fixInstallLocation = true;
                        }
                    }
                }
            }

            // silently change install location if we detect a portable run
            if (installLocation is null && Directory.GetParent(Paths.Process)?.FullName is string processDir)
            {
                var files = Directory.GetFiles(processDir).Select(x => Path.GetFileName(x)).ToArray();

                // check if settings.json and state.json are the only files in the folder
                if (files.Length <= 3 && files.Contains("Settings.json") && files.Contains("State.json") && files.Contains("DownloadStats.json"))
                {
                    installLocation = processDir;
                    fixInstallLocation = true;
                }
            }

            if (fixInstallLocation && installLocation is not null)
            {
                var installer = new Installer
                {
                    InstallLocation = installLocation,
                    IsImplicitInstall = true
                };

                if (installer.CheckInstallLocation())
                {
                    Logger.WriteLine(LOG_IDENT, $"Changing install location to '{installLocation}'");
                    installer.DoInstall();
                }
                else
                {
                    // force reinstall
                    installLocation = null;
                }
            }

            if (installLocation is null)
            {
                Logger.Initialize(true);
                LaunchHandler.LaunchInstaller();
            }
            else
            {
                Paths.Initialize(installLocation);

                // ensure executable is in the install directory
                if (Paths.Process != Paths.Application && !File.Exists(Paths.Application))
                    File.Copy(Paths.Process, Paths.Application);

                Logger.Initialize(LaunchSettings.UninstallFlag.Active);

                if (!Logger.Initialized && !Logger.NoWriteMode)
                {
                    Logger.WriteLine(LOG_IDENT, "Possible duplicate launch detected, terminating.");
                    Terminate();
                }

                // Removed some Code SO MEDDSAM DONT STEAL

                DownloadStats.Load();
                State.Load();
                RobloxState.Load();
                FastFlags.Load();

                Settings.Load();
                if (!Locale.SupportedLocales.ContainsKey(Settings.Prop.Locale))
                {
                    Settings.Prop.Locale = "nil";
                    Settings.Save();
                }

                Locale.Set(Settings.Prop.Locale);

                if (!LaunchSettings.BypassUpdateCheck)
                    Installer.HandleUpgrade();

                WindowsRegistry.RegisterApis(); // we want to register those early on
                                                // so we wont have any issues with bloxshade

                LaunchHandler.ProcessLaunchArgs();
            }
        }

            // you must *explicitly* call terminate when everything is done, it won't be called implicitly


        protected override void OnExit(ExitEventArgs e)
        {
            DiscordClient?.Dispose();
            DiscordClient = null;

            base.OnExit(e);
        }
    }
}
