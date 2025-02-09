using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Hellstrap
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#if QA_BUILD
        public const string ProjectName = "Hellstrap-QA";
#else
        public const string ProjectName = "Hellstrap";
#endif
        public const string ProjectOwner = "Hellstrap";
        public const string ProjectRepository = "https://api.github.com/repos/midaskira/Hellstrap";
        public const string ProjectDownloadLink = "https://github.com/midaskira/Hellstrap/releases";
        public const string ProjectHelpLink = "https://github.com/bloxstraplabs/bloxstrap/wiki";
        public const string ProjectSupportLink = "https://github.com/bloxstraplabs/bloxstrap/issues/new";

        public const string RobloxPlayerAppName = "RobloxPlayerBeta";
        public const string RobloxStudioAppName = "RobloxStudioBeta";

        public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProjectName}";
        public const string ApisKey = $"Software\\{ProjectName}";

        public static LaunchSettings LaunchSettings { get; private set; } = null!;
        public static BuildMetadataAttribute BuildMetadata { get; } = Assembly.GetExecutingAssembly().GetCustomAttribute<BuildMetadataAttribute>()!;
        public static string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version!.ToString();
        public static Bootstrapper? Bootstrapper { get; set; }
        public static bool IsActionBuild => !string.IsNullOrEmpty(BuildMetadata.CommitRef);
        public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);
        public static bool IsStudioVisible => !string.IsNullOrEmpty(State.Prop.Studio.VersionGuid);
        public static readonly MD5 MD5Provider = MD5.Create();
        public static readonly Logger Logger = new();
        public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();
        public static readonly JsonManager<Settings> Settings = new();
        public static readonly JsonManager<State> State = new();
        public static readonly FastFlagManager FastFlags = new();

        public static readonly HttpClient HttpClient = new(
            new HttpClientLoggingHandler(
                new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }
            )
        );

        private static bool _showingExceptionDialog;

        public static void Terminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;
            Logger.WriteLine("App::Terminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");
            Environment.Exit(exitCodeNum);
        }

        public static void SoftTerminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            Logger.WriteLine("App::SoftTerminate", $"Terminating with exit code {(int)exitCode} ({exitCode})");
            Current.Dispatcher.Invoke(() => Current.Shutdown((int)exitCode));
        }

        private void GlobalExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            Logger.WriteLine("App::GlobalExceptionHandler", "An exception occurred");
            HandleException(e.Exception);
        }

        public static void HandleException(Exception ex)
        {
            if (_showingExceptionDialog) return;

            _showingExceptionDialog = true;
            Logger.WriteException("App::HandleException", ex);
            SendLog();

            if (Bootstrapper?.Dialog != null)
            {
                Bootstrapper.Dialog.TaskbarProgressValue = Math.Max(Bootstrapper.Dialog.TaskbarProgressValue, 1);
                Bootstrapper.Dialog.TaskbarProgressState = TaskbarItemProgressState.Error;
            }

            Frontend.ShowExceptionDialog(ex);
            Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
        }

        public static async Task<GithubRelease?> GetLatestRelease()
        {
            try
            {
                var releaseInfo = await Http.GetJson<GithubRelease>($"{ProjectRepository}/releases/latest");
                return releaseInfo?.Assets != null ? releaseInfo : null;
            }
            catch (Exception ex)
            {
                Logger.WriteException("App::GetLatestRelease", ex);
                return null;
            }
        }

        public static void SendStat(string key, string value) { }
        public static void SendLog() { }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string LOG_IDENT = "App::OnStartup";
            Locale.Initialize();
            base.OnStartup(e);
            Logger.WriteLine(LOG_IDENT, $"Starting {ProjectName} v{Version}");

            string userAgent = $"{ProjectName}/{Version}" + (IsActionBuild
                ? $" (Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from commit {BuildMetadata.CommitHash} ({BuildMetadata.CommitRef}))"
                : $" (Build {Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildMetadata.Machine))})");

            Logger.WriteLine(LOG_IDENT, $"Loaded from {Paths.Process}");
            Logger.WriteLine(LOG_IDENT, $"Temp path: {Paths.Temp}");
            Logger.WriteLine(LOG_IDENT, $"Windows Start Menu path: {Paths.WindowsStartMenu}");

            ApplicationConfiguration.Initialize();
            HttpClient.Timeout = TimeSpan.FromSeconds(30);
            HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

            LaunchSettings = new LaunchSettings(e.Args);

            using var uninstallKey = Registry.CurrentUser.OpenSubKey(UninstallKey);
            string? installLocation = uninstallKey?.GetValue("InstallLocation") as string;
            bool fixInstallLocation = false;

            if (!string.IsNullOrEmpty(installLocation) && !Directory.Exists(installLocation))
            {
                var match = Regex.Match(installLocation, @"^[a-zA-Z]:\\Users\\([^\\]+)", RegexOptions.IgnoreCase);
                string newLocation = match.Success ? installLocation.Replace(match.Value, Paths.UserProfile, StringComparison.InvariantCultureIgnoreCase) : "";

                if (!string.IsNullOrEmpty(newLocation) && Directory.Exists(newLocation))
                {
                    installLocation = newLocation;
                    fixInstallLocation = true;
                }
            }

            if (installLocation is null && Directory.GetParent(Paths.Process)?.FullName is string processDir)
            {
                var files = Directory.GetFiles(processDir).Select(Path.GetFileName).ToArray();
                if (files.Length <= 3 && files.Contains("Settings.json") && files.Contains("State.json"))
                {
                    installLocation = processDir;
                    fixInstallLocation = true;
                }
            }

            if (fixInstallLocation && installLocation != null)
            {
                var installer = new Installer { InstallLocation = installLocation, IsImplicitInstall = true };

                if (installer.CheckInstallLocation())
                {
                    Logger.WriteLine(LOG_IDENT, $"Changing install location to '{installLocation}'");
                    installer.DoInstall();
                }
                else installLocation = null;
            }

            if (installLocation is null)
            {
                Logger.Initialize(true);
                LaunchHandler.LaunchInstaller();
            }
            else
            {
                Paths.Initialize(installLocation);
                if (Paths.Process != Paths.Application && !File.Exists(Paths.Application))
                    File.Copy(Paths.Process, Paths.Application);

                Logger.Initialize(LaunchSettings.UninstallFlag.Active);

                if (!Logger.Initialized && !Logger.NoWriteMode)
                {
                    Logger.WriteLine(LOG_IDENT, "Possible duplicate launch detected, terminating.");
                    Terminate();
                }

                Settings.Load();
                State.Load();
                FastFlags.Load();
                Locale.Set(Settings.Prop.Locale);
                if (!LaunchSettings.BypassUpdateCheck) Installer.HandleUpgrade();
                WindowsRegistry.RegisterApis();
                LaunchHandler.ProcessLaunchArgs();
            }
        }
    }
}
