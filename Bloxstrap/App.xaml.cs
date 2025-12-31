using DiscordRPC;
using DiscordRPC.Logging;
using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Voidstrap.Integrations;
using Voidstrap.UI.Elements.Bootstrapper;
using Voidstrap.UI.ViewModels.ContextMenu;
using Wpf.Ui.Hardware;

namespace Voidstrap
{
    public partial class App : Application
    {
#if QA_BUILD
        public const string ProjectName = "Voidstrap-QA";
#else
        public const string ProjectName = "Voidstrap";
#endif
        public const string ProjectOwner = "Voidstrap";
        public const string ProjectRepository = "/voidstrap/Voidstrap/";
        public const string ProjectDownloadLink = "https://github.com/voidstrap/Voidstrap/releases";
        public const string ProjectHelpLink = "https://github.com/BloxstrapLabs/Bloxstrap/wiki";
        public const string ProjectSupportLink = "https://github.com/voidstrap/Voidstrap/issues/new";

        public const string RobloxPlayerAppName = "RobloxPlayerBeta";
        public const string RobloxStudioAppName = "RobloxStudioBeta";
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

        public static byte[] ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(data);
        }

        public static byte[] ComputeSha256(Stream stream)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(stream);
        }


        public static readonly Logger Logger = new();

        public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();

        public static readonly JsonManager<Models.Persistable.AppSettings> Settings = new();

        public static readonly JsonManager<DownloadStats> DownloadStats = new();

        public static readonly JsonManager<State> State = new();

        public static readonly JsonManager<RobloxState> RobloxState = new();

        public static readonly FastFlagManager FastFlags = new();

        public static readonly GBSEditor GlobalSettings = new();

        private CancellationTokenSource? _memoryTrimCts;

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
                    Bootstrapper.Dialog.TaskbarProgressValue = 1;

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
                var releaseInfo = await Http.GetJson<GithubRelease>($"https://api.github.com/repos/{ProjectRepository}/releases/latest");

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

        protected override async void OnStartup(StartupEventArgs e)
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

                DownloadStats.Load();
                State.Load();
                RobloxState.Load();
                FastFlags.Load();
                Settings.Load();
                TrimTimer();
                var rpcVm = new RPCCustomizerViewModel();
                if (rpcVm.AutoStartRpc && !string.IsNullOrWhiteSpace(rpcVm.ApplicationId))
                {
                    rpcVm.GetType()
                         .GetMethod("SafeStartRpc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                         .Invoke(rpcVm, null);
                }
                Current.Resources["RPCCustomizerVM"] = rpcVm;

                if (Settings?.Prop?.WPFSoftwareRender == true)
                {
                    HardwareAcceleration.DisableAllAnimations();
                    HardwareAcceleration.FreeMemory();
                    HardwareAcceleration.OptimizeVisualRendering();
                    HardwareAcceleration.DisableTransparencyEffects();
                    HardwareAcceleration.MinimizeMemoryFootprint();
                }

                if (!Locale.SupportedLocales.ContainsKey(Settings.Prop.Locale))
                {
                    Settings.Prop.Locale = "nil";
                    Settings.Save();
                }

                Locale.Set(Settings.Prop.Locale);

                if (!LaunchSettings.BypassUpdateCheck)
                    Installer.HandleUpgrade();

                WindowsRegistry.RegisterApis();
                LaunchHandler.ProcessLaunchArgs();
            }
        }

        private void TrimTimer()
        {
            _memoryTrimCts = new CancellationTokenSource();
            CancellationToken token = _memoryTrimCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (DiscordClient != null)
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
#if NET5_0_OR_GREATER
                            System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
                                System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                            GC.Collect();
#endif
                            Logger.WriteLine("App::MemoryTrim", "Memory trimmed successfully (background).");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteException("App::MemoryTrim", ex);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
            }, token);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (Current.MainWindow?.DataContext is RPCCustomizerViewModel rpcVm)
                {
                    rpcVm?.GetType()
                          .GetMethod("SafeStopRpc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                          .Invoke(rpcVm, null);
                }

                DiscordClient?.Dispose();
                DiscordClient = null;
                if (Current.MainWindow?.DataContext is MusicPlayerViewModel musicVm)
                {
                    musicVm.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OnExit] Cleanup error: {ex.Message}");
            }

            base.OnExit(e);
        }
    }
}