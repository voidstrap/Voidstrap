// To debug the automatic updater:
// - Uncomment the definition below
// - Publish the executable
// - Launch the executable (click no when it asks you to upgrade)
// - Launch Roblox (for testing web launches, run it from the command prompt)
// - To re-test the same executable, delete it from the installation folder

// #define DEBUG_UPDATER

#if DEBUG_UPDATER
#warning "Automatic updater debugging is enabled"
#endif

using ICSharpCode.SharpZipLib.Zip;
using Microsoft.VisualBasic.Devices;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Buffers;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Voidstrap.AppData;
using Voidstrap.Integrations;
using Voidstrap.RobloxInterfaces;
using Voidstrap.UI.Elements.Bootstrapper.Base;
using Voidstrap.UI.ViewModels.Settings;
using static Voidstrap.UI.ViewModels.Settings.ModsViewModel;


namespace Voidstrap
{
    public class Bootstrapper
    {
        #region Properties
        private const int ProgressBarMaximum = 10000;

        private const double TaskbarProgressMaximumWpf = 1; // this can not be changed. keep it at 1.
        private const int TaskbarProgressMaximumWinForms = WinFormsDialogBase.TaskbarProgressMaximum;

        private const string AppSettings =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
            "<Settings>\r\n" +
            "	<ContentFolder>content</ContentFolder>\r\n" +
            "	<BaseUrl>http://www.roblox.com</BaseUrl>\r\n" +
            "</Settings>\r\n";

        private readonly FastZipEvents _fastZipEvents = new();
        private readonly CancellationTokenSource _cancelTokenSource = new();

        private readonly IAppData AppData;
        private readonly LaunchMode _launchMode;
        private AggressivePerformanceManager? _gpuPerfManager;
        private string _launchCommandLine = App.LaunchSettings.RobloxLaunchArgs;
        private string _latestVersionGuid = null!;
        private string _latestVersionDirectory = null!;
        private PackageManifest _versionPackageManifest = null!;

        private bool _isInstalling = false;
        private double _progressIncrement;
        private double _taskbarProgressIncrement;
        private double _taskbarProgressMaximum;
        private long _totalDownloadedBytes = 0;
        private System.Timers.Timer? _memoryClearTimer;
        private CancellationTokenSource? _optimizationCts;
        private Process? _fflagRunnerProcess;
        private DispatcherTimer? _memoryCleanerTimer;

        private bool _mustUpgrade => String.IsNullOrEmpty(AppData.State.VersionGuid) || !File.Exists(AppData.ExecutablePath);
        private bool _noConnection = false;

        private AsyncMutex? _mutex;

        private int _appPid = 0;

        public IBootstrapperDialog? Dialog = null;

        public bool IsStudioLaunch => _launchMode != LaunchMode.Player;
        #endregion

        #region Core
        public Bootstrapper(LaunchMode launchMode)
        {
            _launchMode = launchMode;

            // https://github.com/icsharpcode/SharpZipLib/blob/master/src/ICSharpCode.SharpZipLib/Zip/FastZip.cs/#L669-L680
            // exceptions don't get thrown if we define events without actually binding to the failure events. probably a bug. ¯\_(ツ)_/¯
            _fastZipEvents.FileFailure += (_, e) => throw e.Exception;
            _fastZipEvents.DirectoryFailure += (_, e) => throw e.Exception;
            _fastZipEvents.ProcessFile += (_, e) => e.ContinueRunning = !_cancelTokenSource.IsCancellationRequested;

            AppData = IsStudioLaunch ? new RobloxStudioData() : new RobloxPlayerData();
            Deployment.BinaryType = AppData.BinaryType;
        }

        private Process? _robloxProcess;
        private System.Threading.Timer? _gcTimer;
        private System.Threading.Timer? _processOptimizerTimer;


        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr procHandle, int min, int max);

        private void SetStatus(string message)
        {
            App.Logger.WriteLine("Bootstrapper::SetStatus", message);

            message = message.Replace("{product}", AppData.ProductName);

            if (Dialog is not null)
                Dialog.Message = message;
        }

        private void UpdateProgressBar()
        {
            if (Dialog is null)
                return;
            if (_totalDownloadedBytes <= 0 || _progressIncrement <= 0)
            {
                Dialog.ProgressValue = 0;
                Dialog.TaskbarProgressValue = 0;
                return;
            }
            int progressValue = (int)Math.Floor(_progressIncrement * _totalDownloadedBytes);
            progressValue = Math.Clamp(progressValue, 0, ProgressBarMaximum);
            Dialog.ProgressValue = progressValue;
            double taskbarProgressValue = _taskbarProgressIncrement * _totalDownloadedBytes;
            taskbarProgressValue = Math.Clamp(taskbarProgressValue, 0.0, _taskbarProgressMaximum);
            Dialog.TaskbarProgressValue = taskbarProgressValue;
        }

        private async Task HandleConnectionError(Exception exception)
        {
            const string LOG_IDENT = "Bootstrapper::HandleConnectionError";
            if (exception == null)
                return;
            if (exception is AggregateException aggEx)
                exception = aggEx.InnerException ?? aggEx;

            _noConnection = true;
            App.Logger.WriteException(LOG_IDENT, exception);

            if (_isInstalling)
            {
                App.Logger.WriteLine(LOG_IDENT, "Already upgrading skipping retry.");
                return;
            }

            string message = "A network or server issue occurred this is likely a channel problem try switching your default channel!.";

            if (exception is HttpRequestException httpEx)
            {
                switch (httpEx.StatusCode)
                {
                    case HttpStatusCode.Forbidden:
                        App.Logger.WriteLine(LOG_IDENT, "403 Forbidden: switching to default channel.");
                        Deployment.Channel = Deployment.DefaultChannel;
                        _noConnection = false;
                        return;
                    case HttpStatusCode.NotFound:
                        message = "Update file not found on the server.";
                        break;
                }
            }

            Frontend.ShowMessageBox($"{message}\n\nYou can retry later or continue offline.",
                MessageBoxImage.Warning, MessageBoxButton.OK);
        }

        public async Task Run()
        {
            const string LOG_IDENT = "Bootstrapper::Run";
            App.Logger.WriteLine(LOG_IDENT, "Running bootstrapper");

            if (Dialog is not null)
                Dialog.CancelEnabled = true;

            SetStatus(Strings.Bootstrapper_Status_Connecting);

            var connectionResult = await Deployment.InitializeConnectivity();
            App.Logger.WriteLine(LOG_IDENT, "Connectivity check finished");

            if (connectionResult is not null)
                await HandleConnectionError(connectionResult);

#if (!DEBUG || DEBUG_UPDATER) && !QA_BUILD
            if (App.Settings.Prop.CheckForUpdates && !App.LaunchSettings.UpgradeFlag.Active)
            {
                var latestTag = await GithubUpdater.GetLatestVersionTagAsync();
                if (!string.IsNullOrEmpty(latestTag))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Latest GitHub release tag: {latestTag}");
                    string normalizedTag = latestTag.TrimStart('v', 'V');
                    string localVersion = string.IsNullOrWhiteSpace(AppData.State.VersionGuid)
                        ? "0.0.0"
                        : AppData.State.VersionGuid.Trim();

                    App.Version = localVersion;

                    if (IsNewerVersion(localVersion, normalizedTag))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"New version detected! Local: {localVersion}, Remote: {normalizedTag}");
                        SetStatus($"Updating to v{normalizedTag}...");

                        bool success = await GithubUpdater.DownloadAndInstallUpdate(latestTag);
                        if (success)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Update installed successfully — restarting Voidstrap...");
                            RestartApplication();
                            return;
                        }
                        else
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Update failed — continuing without updating.");
                        }
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Already up to date.");
                    }
                }
            }
#endif

            bool mutexExists = false;

            try
            {
                using (var existingMutex = Mutex.OpenExisting("Voidstrap-Bootstrapper"))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Voidstrap-Bootstrapper mutex exists, waiting...");
                    SetStatus(Strings.Bootstrapper_Status_WaitingOtherInstances);
                    mutexExists = true;
                }
            }
            catch (WaitHandleCannotBeOpenedException) { }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Unexpected error checking mutex: {ex}");
            }

            await using var mutex = new AsyncMutex(false, "Voidstrap-Bootstrapper");
            await mutex.AcquireAsync(_cancelTokenSource.Token);
            _mutex = mutex;

            if (mutexExists)
            {
                App.Settings.Load();
                App.State.Load();
            }

            if (!_noConnection)
            {
                try
                {
                    await GetLatestVersionInfo();
                }
                catch (Exception ex)
                {
                    await HandleConnectionError(ex);
                }
            }

            if (!_noConnection)
            {
                if (AppData.State.VersionGuid != _latestVersionGuid || _mustUpgrade)
                    await UpgradeRoblox();

                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                await ApplyModifications();
                if (App.Settings.Prop.LockDefault)
                {
                    var allowedFlags = new Dictionary<string, string>
                        {
                            { "DFFlagDebugPerfMode", "True" },
                            { "FFlagHandleAltEnterFullscreenManually", "False" }
                        };
                    try
                    {
                        string clientSettingsPath = Path.Combine(_latestVersionDirectory, "ClientSettings\\ClientAppSettings.json");
                        Directory.CreateDirectory(Path.GetDirectoryName(clientSettingsPath)!);
                        File.WriteAllText(clientSettingsPath, JsonSerializer.Serialize(allowedFlags, new JsonSerializerOptions { WriteIndented = true }));

                        App.Logger.WriteLine(LOG_IDENT, "LockDefault is ON: ClientAppSettings.json fully replaced with allowed FastFlags.");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to enforce LockDefault FastFlags in ClientAppSettings.json: " + ex.Message);
                    }
                }
            }

            if (IsStudioLaunch)
                WindowsRegistry.RegisterStudio();
            else
                WindowsRegistry.RegisterPlayer();

            await mutex.ReleaseAsync();

            if (!App.LaunchSettings.NoLaunchFlag.Active && !_cancelTokenSource.IsCancellationRequested)
                await StartRoblox();

            Dialog?.CloseBootstrapper();
        }

        private void RestartApplication()
        {
            try
            {
                string exePath = Environment.ProcessPath!;
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Bootstrapper::Restart", $"Failed to restart: {ex}");
            }
        }

        private static bool IsNewerVersion(string _, string latestVersion)
        {
            if (App.Settings.Prop.CheckForUpdates == false)
                return false;
            string localVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Console.WriteLine("Current app version: " + localVersion);
            latestVersion = latestVersion.TrimStart('v', 'V');
            App.Logger.WriteLine("IsNewerVersion", $"Local: '{localVersion}', GitHub tag: '{latestVersion}'");
            if (Version.TryParse(localVersion, out var localVer) &&
                Version.TryParse(latestVersion, out var latestVer))
            {
                App.Logger.WriteLine("IsNewerVersion", $"Parsed Local: {localVer}, Latest: {latestVer}");
                return latestVer > localVer;
            }
            return string.Compare(latestVersion, localVersion, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private async Task GetLatestVersionInfo()
        {
            const string LOG_IDENT = "Bootstrapper::GetLatestVersionInfo";

            App.Logger.WriteLine(LOG_IDENT, "Initializing GetLatestVersionInfo...");

            try
            {
                App.Logger.WriteLine(LOG_IDENT,
                    $"Fetching client version info for channel: {Deployment.Channel}");

                ClientVersion clientVersion;

                using (var response = await App.HttpClient.GetAsync(
                    Deployment.GetInfoUrl(Deployment.Channel),
                    HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        App.Logger.WriteLine(LOG_IDENT,
                            $"❌ HTTP {(int)response.StatusCode} ({response.StatusCode})");

                        if (response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            App.Logger.WriteLine(LOG_IDENT,
                                "403 Forbidden — switching to default channel.");

                            Deployment.Channel = Deployment.DefaultChannel;
                            clientVersion = await Deployment.GetInfo(Deployment.Channel);
                            goto VersionInfoOK;
                        }

                        throw new HttpRequestException(
                            $"Bad HTTP status: {response.StatusCode}");
                    }

                    var mediaType = response.Content.Headers.ContentType?.MediaType;
                    if (mediaType != "application/json")
                    {
                        var preview = await response.Content.ReadAsStringAsync();
                        App.Logger.WriteLine(LOG_IDENT,
                            $"❌ Expected JSON but got '{mediaType}'. Preview:\n" +
                            preview[..Math.Min(300, preview.Length)]);

                        throw new Exception("Invalid response content-type");
                    }

                    var jsonText = await response.Content.ReadAsStringAsync();

                    clientVersion = JsonSerializer.Deserialize<ClientVersion>(jsonText)
                        ?? throw new Exception("ClientVersion JSON was null.");
                }

            VersionInfoOK:

                _latestVersionGuid = clientVersion.VersionGuid;
                _latestVersionDirectory =
                    Path.Combine(Paths.Versions, _latestVersionGuid);

                var pkgManifestUrl =
                    $"https://setup.rbxcdn.com/{_latestVersionGuid}-rbxPkgManifest.txt";

                App.Logger.WriteLine(LOG_IDENT,
                    $"Downloading manifest from {pkgManifestUrl}");

                using (var manifestResp = await App.HttpClient.GetAsync(
                    pkgManifestUrl,
                    HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!manifestResp.IsSuccessStatusCode)
                    {
                        App.Logger.WriteLine(LOG_IDENT,
                            $"❌ Manifest HTTP {(int)manifestResp.StatusCode} ({manifestResp.StatusCode})");

                        _versionPackageManifest = new("");
                        return;
                    }

                    var manifestText =
                        await manifestResp.Content.ReadAsStringAsync();

                    if (manifestText.TrimStart().StartsWith("<"))
                    {
                        App.Logger.WriteLine(LOG_IDENT,
                            "❌ Manifest returned HTML — skipping parse.");

                        _versionPackageManifest = new("");
                        return;
                    }

                    _versionPackageManifest = new(manifestText);

                    App.Logger.WriteLine(LOG_IDENT,
                        $"Manifest downloaded with {_versionPackageManifest.Count} entries.");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT,
                    $"❌ Critical failure in GetLatestVersionInfo:\n{ex}");

                _versionPackageManifest = new("");
            }
        }

        private void StartMemoryAndProcessOptimizer()
        {
            _processOptimizerTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (_robloxProcess != null && !_robloxProcess.HasExited)
                    {
                        SetProcessWorkingSetSize(_robloxProcess.Handle, -1, -1);
                        _robloxProcess.Refresh();
                        App.Logger.WriteLine("ProcessOptimizer", $"Optimized Roblox PID {_robloxProcess.Id} at {DateTime.Now}");
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("ProcessOptimizer", $"Error optimizing Roblox: {ex}");
                }
            }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }

        private void StopMemoryAndProcessOptimizer()
        {
            _gcTimer?.Dispose();
            _gcTimer = null;

            _processOptimizerTimer?.Dispose();
            _processOptimizerTimer = null;
        }

        private async Task StartRoblox(CancellationToken ct = default)
        {
            const string LOG_IDENT = "Bootstrapper::StartRoblox";
            SetStatus(Strings.Bootstrapper_Status_Starting);
            try
            {
                HandleFullBright();
                StartMemoryAndProcessOptimizer();
                if (_launchMode == LaunchMode.Player && App.Settings.Prop?.ForceRobloxLanguage == true)
                {
                    var match = Regex.Match(_launchCommandLine ?? string.Empty,
                                            @"gameLocale:([a-zA-Z_-]+)",
                                            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

                    if (match.Success && match.Groups.Count > 1)
                    {
                        string detectedLocale = match.Groups[1].Value;
                        _launchCommandLine = Regex.Replace(
                            _launchCommandLine ?? string.Empty,
                            @"robloxLocale:[a-zA-Z_-]+",
                            $"robloxLocale:{detectedLocale}",
                            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
                        );
                    }
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = AppData.ExecutablePath,
                    Arguments = _launchCommandLine ?? string.Empty,
                    WorkingDirectory = AppData.Directory,
                    UseShellExecute = false
                };

                if (_launchMode == LaunchMode.Player && ShouldRunAsAdmin())
                {
                    startInfo.Verb = "runas";
                    startInfo.UseShellExecute = true;
                }

                if (_launchMode == LaunchMode.StudioAuth)
                {
                    _ = Process.Start(startInfo);
                    return;
                }
                string rbxDir = Path.Combine(Paths.LocalAppData, "Roblox");
                string rbxLogDir = Path.Combine(rbxDir, "logs");

                Directory.CreateDirectory(rbxLogDir);

                using var logCreatedEvent = new AutoResetEvent(false);
                using var ctsWatcher = CancellationTokenSource.CreateLinkedTokenSource(ct);

                string? logFileName = null;

                using var logWatcher = new FileSystemWatcher(rbxLogDir, "*.log")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                FileSystemEventHandler onCreated = (_, e) =>
                {
                    Task.Run(async () =>
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            try
                            {
                                if (File.Exists(e.FullPath))
                                {
                                    logFileName = e.FullPath;
                                    logCreatedEvent.Set();
                                    break;
                                }
                            }
                            catch { }
                            await Task.Delay(100, ctsWatcher.Token).ConfigureAwait(false);
                        }
                    }, ctsWatcher.Token);
                };

                RenamedEventHandler onRenamed = (_, e) =>
                {
                    if (Path.GetExtension(e.FullPath).Equals(".log", StringComparison.OrdinalIgnoreCase))
                    {
                        logFileName = e.FullPath;
                        logCreatedEvent.Set();
                    }
                };

                ErrorEventHandler onError = (_, err) =>
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Log watcher error: {err.GetException().Message}");
                    logCreatedEvent.Set();
                };

                logWatcher.Created += onCreated;
                logWatcher.Renamed += onRenamed;
                logWatcher.Error += onError;
                try
                {
                    _robloxProcess = Process.Start(startInfo)
                        ?? throw new InvalidOperationException("Failed to start Roblox process. Please retry.");

                    _appPid = _robloxProcess.Id;
                    _ = Task.Run(() => TryApplyPriorityAsync(_robloxProcess, LOG_IDENT, ct), ct);
                    try
                    {
                        _robloxProcess.WaitForInputIdle(1000);
                    }
                    catch { }

                    if (App.Settings.Prop?.SelectedCpuPriority is not null &&
                        !App.Settings.Prop.SelectedCpuPriority.Equals("Automatic", StringComparison.OrdinalIgnoreCase))
                    {
                        StartCpuLimitWatcher();
                    }

                    RestartMemoryCleanerFromJson();

                    if (App.Settings.Prop?.OptimizeRoblox == true)
                    {
                        ApplyRuntimeOptimizations(_robloxProcess);
                        StartContinuousRobloxOptimization();
                    }


                    if (App.Settings.Prop?.IsBetterServersEnabled == true)
                        ApplyOptimizations(_robloxProcess);


                    if (App.Settings.Prop?.DX12Like == true)
                    {
                        var options = new RobloxDX12Optimizer.RobloxDx12Optimizer.OptimizerOptions
                        {
                            EnableHighResTimer = true,
                            HighResMillis = 1,
                            OverrideAffinity = true,
                            AffinityMask = 0xFFFFFFFFFFFFFFFFUL,
                            WorkingSetMinMB = 128,
                            WorkingSetMaxMB = 2048,
                            BoostThreads = true,
                            ProbeVendorAndDx = true,
                            SafeMode = false
                        };
                        RobloxDX12Optimizer.RobloxDx12Optimizer.Start(intervalMs: 2000, options: options);
                    }

                    if (App.Settings.Prop?.MultiAccount == true)
                        RobloxMemoryCleaner.CleanAllRobloxMemory();
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    return;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Roblox start failed: {ex}");
                    throw;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Started Roblox (PID {_appPid}), waiting for log file");
                using (var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    var signaled = await Task.Run(() => logCreatedEvent.WaitOne(TimeSpan.FromSeconds(35)), delayCts.Token);
                    if (!signaled || string.IsNullOrEmpty(logFileName))
                    {
                        try
                        {
                            logFileName = Directory.EnumerateFiles(rbxLogDir, "*.log")
                                                   .OrderByDescending(File.GetLastWriteTimeUtc)
                                                   .FirstOrDefault();
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Log enumeration failed: {ex.Message}");
                        }
                    }
                }
                logWatcher.EnableRaisingEvents = false;
                ctsWatcher.Cancel();
                logWatcher.Created -= onCreated;
                logWatcher.Renamed -= onRenamed;
                logWatcher.Error -= onError;

                if (string.IsNullOrEmpty(logFileName))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Unable to identify log file");
                    Frontend.ShowPlayerErrorDialog();
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Got log file as {logFileName}");
                _ = _mutex?.ReleaseAsync();

                if (IsStudioLaunch)
                    return;

                var autoclosePids = new System.Collections.Generic.List<int>();
                var integrations = App.Settings.Prop?.CustomIntegrations ?? Enumerable.Empty<CustomIntegration>();

                foreach (var integration in integrations.Where(i => !i.SpecifyGame))
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(integration.Location) || !File.Exists(integration.Location))
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Integration missing: '{integration.Name}' ({integration.Location})");
                            continue;
                        }

                        App.Logger.WriteLine(LOG_IDENT, $"Launching integration '{integration.Name}' ({integration.Location})");

                        var ip = Process.Start(new ProcessStartInfo
                        {
                            FileName = integration.Location,
                            Arguments = (integration.LaunchArgs ?? string.Empty).Replace("\r\n", " "),
                            WorkingDirectory = Path.GetDirectoryName(integration.Location)!,
                            UseShellExecute = true
                        });

                        if (ip != null && integration.AutoClose) autoclosePids.Add(ip.Id);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to launch '{integration.Name}': {ex.Message}");
                    }
                }

                if (App.Settings.Prop.DisableCrash == true)
                {
                    await Task.Delay(800, ct).ConfigureAwait(false);

                    try
                    {
                        var handlers = Process.GetProcessesByName("RobloxCrashHandler");
                        if (handlers.Length == 0)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "No CrashHandler processes found — nothing to disable.");
                        }

                        foreach (var handler in handlers)
                        {
                            try
                            {
                                if (handler.HasExited) continue;
                                App.Logger.WriteLine(LOG_IDENT,
                                    $"Disabling RobloxCrashHandler PID={handler.Id}, Path={handler.MainModule?.FileName}");
                                try
                                {
                                    handler.CloseMainWindow();
                                    if (handler.WaitForExit(1000))
                                    {
                                        App.Logger.WriteLine(LOG_IDENT, $"CrashHandler {handler.Id} closed gracefully.");
                                        continue;
                                    }
                                }
                                catch { }
                                try
                                {
                                    handler.Kill(entireProcessTree: true);
                                    handler.WaitForExit(2000);
                                    App.Logger.WriteLine(LOG_IDENT, $"CrashHandler {handler.Id} terminated.");
                                }
                                catch (Win32Exception ex)
                                {
                                    App.Logger.WriteLine(LOG_IDENT, $"Access denied killing CrashHandler {handler.Id}: {ex.Message}");
                                }
                                catch (InvalidOperationException)
                                {
                                }
                                catch (Exception kex)
                                {
                                    App.Logger.WriteLine(LOG_IDENT, $"Unexpected kill error for CrashHandler {handler.Id}: {kex.Message}");
                                }
                            }
                            finally
                            {
                                try { handler.Dispose(); } catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"CrashHandler disable routine failed: {ex.Message}");
                    }
                }

                if ((App.Settings?.Prop.EnableActivityTracking ?? false) ||
                    App.LaunchSettings.TestModeFlag?.Active == true ||
                    autoclosePids.Any())
                {
                    using var ipl = new InterProcessLock("Watcher", TimeSpan.FromSeconds(5));

                    var watcherData = new WatcherData
                    {
                        ProcessId = _appPid,
                        LogFile = logFileName!,
                        AutoclosePids = autoclosePids
                    };

                    string watcherDataArg = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(watcherData)));

                    var args = new StringBuilder().Append("-watcher \"").Append(watcherDataArg).Append('"');
                    if (App.LaunchSettings.TestModeFlag?.Active == true) args.Append(" -testmode");

                    if (ipl.IsAcquired)
                        _ = Process.Start(Paths.Process, args.ToString());
                }
                await Task.Delay(2500, ct).ConfigureAwait(false);

                if (_robloxProcess != null)
                {
                    _robloxProcess.EnableRaisingEvents = true;
                    _robloxProcess.Exited += (_, __) =>
                    {
                        try { StopContinuousRobloxOptimization(); } catch { }
                    };
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Unexpected error in StartRoblox: {ex}");
                Frontend.ShowPlayerErrorDialog();
            }
            finally
            {
                try { StopMemoryAndProcessOptimizer(); } catch { }
            }
        }

        private void RestartMemoryCleanerFromJson()
        {
            var settings = VoidstrapRobloxSettingsManager.Load();
            int seconds = settings.MemoryCleanerIntervalSeconds;

            _memoryCleanerTimer?.Stop();
            _memoryCleanerTimer = null;

            if (seconds <= 0)
            {
                App.Logger?.WriteLine(
                    "MemoryCleaner",
                    "Memory cleaner disabled (Never)"
                );
                return;
            }

            _memoryCleanerTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(seconds)
            };

            _memoryCleanerTimer.Tick += (_, __) =>
            {
                try
                {
                    RobloxMemoryCleaner.CleanAllRobloxMemory();

                    App.Logger?.WriteLine(
                        "MemoryCleaner",
                        $"Roblox memory cleaned at {DateTime.Now:T}"
                    );
                }
                catch (Exception ex)
                {
                    App.Logger?.WriteLine(
                        "MemoryCleaner",
                        $"Error cleaning memory: {ex}"
                    );
                }
            };

            _memoryCleanerTimer.Start();

            App.Logger?.WriteLine(
                "MemoryCleaner",
                $"Memory cleaner started with interval {seconds}s"
            );
        }

        private void HandleFullBright()
        {
            const string LOG_IDENT = "FullBright";

            try
            {
                if (!Directory.Exists(Paths.Versions))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Versions directory missing.");
                    return;
                }

                string backupDir = Path.Combine(Paths.Base, "FullBrightBackup");
                string metaPath = Path.Combine(backupDir, "brdf.json");

                Directory.CreateDirectory(backupDir);
                if (App.Settings.Prop?.Fullbright == false && File.Exists(metaPath))
                {
                    var meta = JsonSerializer.Deserialize<BrdfBackupInfo>(
                        File.ReadAllText(metaPath));

                    if (meta != null)
                    {
                        string restorePath = Path.Combine(
                            Paths.Versions,
                            meta.RelativePath,
                            meta.FileName
                        );

                        Directory.CreateDirectory(Path.GetDirectoryName(restorePath)!);

                        string backupFile = Path.Combine(backupDir, meta.FileName);
                        if (File.Exists(backupFile))
                        {
                            File.Copy(backupFile, restorePath, overwrite: true);
                            App.Logger.WriteLine(LOG_IDENT, $"Restored {meta.FileName}");
                        }
                    }

                    return;
                }

                foreach (var versionDir in Directory.GetDirectories(Paths.Versions, "version-*"))
                {
                    var texturesRoot = Path.Combine(
                        versionDir,
                        "PlatformContent",
                        "pc",
                        "textures"
                    );

                    if (!Directory.Exists(texturesRoot))
                        continue;

                    var brdf = Directory
                        .EnumerateFiles(texturesRoot, "brdfLUT.*", SearchOption.AllDirectories)
                        .FirstOrDefault();

                    if (brdf == null)
                        continue;

                    if (App.Settings.Prop?.Fullbright == true && File.Exists(brdf))
                    {
                        string relativePath = Path.GetRelativePath(Paths.Versions, Path.GetDirectoryName(brdf)!);
                        string fileName = Path.GetFileName(brdf);
                        string backupFile = Path.Combine(backupDir, fileName);

                        if (!File.Exists(backupFile))
                        {
                            File.Copy(brdf, backupFile);
                            File.WriteAllText(metaPath, JsonSerializer.Serialize(
                                new BrdfBackupInfo
                                {
                                    RelativePath = relativePath,
                                    FileName = fileName
                                }));

                            App.Logger.WriteLine(LOG_IDENT, $"Backed up {fileName}");
                        }

                        File.Delete(brdf);
                        App.Logger.WriteLine(LOG_IDENT, "brdfLUT removed (FullBright ON).");
                    }

                    break;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"FullBright error: {ex}");
            }
        }
        private sealed class BrdfBackupInfo
        {
            public string RelativePath { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
        }

        private CancellationTokenSource _cpuWatcherCts;
        private void StartCpuLimitWatcher()
        {
            _cpuWatcherCts?.Cancel();
            _cpuWatcherCts = new CancellationTokenSource();
            var token = _cpuWatcherCts.Token;

            Task.Run(async () =>
            {
                var seenPids = new HashSet<int>();

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var parts = App.Settings.Prop.SelectedCpuPriority.Split(' ');
                            if (!int.TryParse(parts[0], out int coreCount) || coreCount <= 0)
                            {
                                await Task.Delay(5000, token);
                                continue;
                            }

                            int total = Environment.ProcessorCount;
                            coreCount = Math.Clamp(coreCount, 1, total);
                            long mask = (1L << coreCount) - 1;

                            var procs = Process.GetProcessesByName("RobloxPlayerBeta");
                            foreach (var proc in procs)
                            {
                                try
                                {
                                    if (seenPids.Contains(proc.Id))
                                        continue;

                                    if (!proc.HasExited)
                                    {
                                        proc.ProcessorAffinity = (IntPtr)mask;
                                        App.Logger.WriteLine("Bootstrapper::CPUWatcher",
                                            $"Applied CPU limit to Roblox PID={proc.Id}: {coreCount}/{total} cores (mask 0x{mask:X})");
                                        seenPids.Add(proc.Id);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    App.Logger.WriteLine("Bootstrapper::CPUWatcher",
                                        $"Failed to apply CPU limit to Roblox PID={proc.Id}: {ex.Message}");
                                }
                                finally
                                {
                                    proc.Dispose();
                                }
                            }
                            seenPids.RemoveWhere(pid =>
                            {
                                try { Process.GetProcessById(pid); return false; }
                                catch { return true; }
                            });
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine("Bootstrapper::CPUWatcher", $"Watcher error: {ex.Message}");
                        }

                        await Task.Delay(5000, token);
                    }
                }
                finally
                {
                    _cpuWatcherCts?.Dispose();
                    _cpuWatcherCts = null;
                }

            }, token);
        }

        private static async Task TryApplyPriorityAsync(Process proc, string logIdent, CancellationToken ct)
        {
            try
            {
                await Task.Delay(1100, ct).ConfigureAwait(false);
                proc.Refresh();
                if (proc.HasExited) { App.Logger.WriteLine(logIdent, "Roblox exited before priority could be set."); return; }

                string priority = App.Settings.Prop?.PriorityLimit ?? "Normal";
                var newPriority = priority switch
                {
                    "Realtime" => ProcessPriorityClass.RealTime,
                    "High" => ProcessPriorityClass.High,
                    "Above Normal" => ProcessPriorityClass.AboveNormal,
                    "Below Normal" => ProcessPriorityClass.BelowNormal,
                    "Low" => ProcessPriorityClass.Idle,
                    _ => ProcessPriorityClass.Normal
                };

                try
                {
                    proc.PriorityClass = newPriority;
                    App.Logger.WriteLine(logIdent, $"Applied Roblox CPU priority: {priority}");
                }
                catch (Win32Exception ex)
                {
                    App.Logger.WriteLine(logIdent, $"Access denied while setting {priority} priority: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    App.Logger.WriteLine(logIdent, $"Roblox process invalid: {ex.Message}");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(logIdent, $"Failed to set Roblox process priority: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(logIdent, $"Priority worker crashed: {ex}");
            }
        }

        private IntPtr _originalAffinity;
        private ProcessPriorityClass _originalPriority;

        public void ApplyOptimizations(Process robloxProcess)
        {
            if (robloxProcess == null || robloxProcess.HasExited) return;
            if (!App.Settings.Prop.IsBetterServersEnabled)
            {
                RevertOptimizations(robloxProcess);
                return;
            }

            try
            {
                App.Logger.WriteLine("BetterServers", $"Applying optimizations for PID {robloxProcess.Id}");
                _originalPriority = robloxProcess.PriorityClass;
                _originalAffinity = robloxProcess.ProcessorAffinity;
                int cores = Math.Min(Environment.ProcessorCount, 64);
                ulong affinityMask = 0;
                for (int i = 0; i < cores; i++)
                {
                    unchecked { affinityMask |= 1UL << i; }
                }
                robloxProcess.ProcessorAffinity = (IntPtr)affinityMask;
                robloxProcess.MinWorkingSet = new IntPtr(128L * 1024 * 1024);
                robloxProcess.MaxWorkingSet = new IntPtr(1024L * 1024 * 1024);
                LowerBackgroundProcessesPriority();

                App.Logger.WriteLine("Servers", "Optimizations applied successfully.");
                robloxProcess.EnableRaisingEvents = true;
                robloxProcess.Exited += (_, __) => RevertOptimizations(robloxProcess);
                Task.Run(() => MonitorRoblox(robloxProcess));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Servers", $"Failed to apply optimizations: {ex.Message}");
            }
        }

        private void MonitorRoblox(Process robloxProcess)
        {
            while (!robloxProcess.HasExited)
            {
                Thread.Sleep(3000);

                if (!App.Settings.Prop.IsBetterServersEnabled)
                {
                    RevertOptimizations(robloxProcess);
                    return;
                }

                try
                {
                    int cores = Math.Min(Environment.ProcessorCount, 64);
                    ulong affinityMask = 0;
                    for (int i = 0; i < cores; i++) unchecked { affinityMask |= 1UL << i; }
                    robloxProcess.ProcessorAffinity = (IntPtr)affinityMask;
                    robloxProcess.MinWorkingSet = new IntPtr(128L * 1024 * 1024);
                    robloxProcess.MaxWorkingSet = new IntPtr(1024L * 1024 * 1024);
                }
                catch { }
            }
        }

        public void RevertOptimizations(Process robloxProcess)
        {
            if (robloxProcess == null || robloxProcess.HasExited) return;

            try
            {
                App.Logger.WriteLine("Servers", $"Reverting optimizations for PID {robloxProcess.Id}");

                robloxProcess.ProcessorAffinity = _originalAffinity;
                robloxProcess.PriorityClass = _originalPriority;

                robloxProcess.MinWorkingSet = IntPtr.Zero;
                robloxProcess.MaxWorkingSet = IntPtr.Zero;

                App.Logger.WriteLine("Servers", "All optimizations reverted.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Servers", $"Failed to revert optimizations: {ex.Message}");
            }
        }


        #region Background Process Management

        private void LowerBackgroundProcessesPriority()
        {
            foreach (var proc in Process.GetProcesses().Where(p => p.Id != Process.GetCurrentProcess().Id && p.ProcessName != "RobloxPlayerBeta"))
            {
                try
                {
                    if (proc.PriorityClass > ProcessPriorityClass.Normal)
                        proc.PriorityClass = ProcessPriorityClass.Normal;
                }
                catch { }
            }
        }

        private void ApplyRuntimeOptimizations(Process process)
        {
            void SafeAction(Action action, string description)
            {
                try { action(); }
                catch (Exception ex) { App.Logger.WriteLine("Optimizer", $"Failed to {description}: {ex.Message}"); }
            }

            App.Logger.WriteLine("Optimizer", $"Applying extreme max optimizations to {process.ProcessName} (PID {process.Id})");

            int logicalCores = Environment.ProcessorCount;
            long affinityMask = logicalCores >= 64 ? -1L : (1L << logicalCores) - 1;
            SafeAction(() => process.ProcessorAffinity = (IntPtr)affinityMask, "set CPU affinity");
            ulong totalMemory = new ComputerInfo().TotalPhysicalMemory;
            long maxWorkingSet = totalMemory switch
            {
                var t when t > 64L * 1024 * 1024 * 1024 => 32L * 1024 * 1024 * 1024,
                var t when t > 32L * 1024 * 1024 * 1024 => 16L * 1024 * 1024 * 1024,
                var t when t > 16L * 1024 * 1024 * 1024 => 8L * 1024 * 1024 * 1024,
                var t when t > 8L * 1024 * 1024 * 1024 => 4L * 1024 * 1024 * 1024,
                _ => 2L * 1024 * 1024 * 1024
            };
            SafeAction(() =>
            {
                process.MaxWorkingSet = new IntPtr(Math.Min(maxWorkingSet, (long)IntPtr.MaxValue));
                process.MinWorkingSet = new IntPtr(50 * 1024 * 1024);
            }, "set working set");

            SafeAction(() =>
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    string gpuName = obj["Name"]?.ToString() ?? "Unknown GPU";
                    ulong gpuMemory = (ulong)(obj["AdapterRAM"] ?? 0);
                    App.Logger.WriteLine("Optimizer", $"Detected GPU: {gpuName}, VRAM: {gpuMemory / (1024 * 1024)} MB");
                }
            }, "read GPU info");
            App.Logger.WriteLine("Optimizer", $"Extreme max optimization complete for {process.ProcessName}.");
        }

        private void StartContinuousRobloxOptimization()
        {
            _optimizationCts?.Cancel();
            _optimizationCts = new CancellationTokenSource();

            Task.Run(() => ContinuousRobloxOptimizationLoop(_optimizationCts.Token));
        }

        private async Task ContinuousRobloxOptimizationLoop(CancellationToken token)
        {
            var processNames = new[] { "Roblox", "RobloxPlayerBeta", "Roblox Game Client" };
            var optimizedProcesses = new HashSet<int>();
            var cpuCounters = new Dictionary<int, PerformanceCounter>();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var robloxProcesses = processNames
                        .SelectMany(name => Process.GetProcessesByName(name))
                        .Where(p => !p.HasExited)
                        .GroupBy(p => p.Id)
                        .Select(g => g.First())
                        .ToList();

                    if (robloxProcesses.Count == 0)
                    {
                        await Task.Delay(5000, token);
                        continue;
                    }

                    foreach (var process in robloxProcesses)
                    {
                        try
                        {
                            if (!optimizedProcesses.Contains(process.Id))
                            {
                                ApplyRuntimeOptimizations(process);
                                optimizedProcesses.Add(process.Id);

                                App.Logger.WriteLine("Optimizer",
                                    $"Optimizations applied to {process.ProcessName} (PID {process.Id}) at {DateTime.Now:T}");
                            }

                            await MonitorProcessPerformance(process, cpuCounters);
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine("Optimizer",
                                $"[Error] {process.ProcessName} (PID {process.Id}): {ex.Message}");
                        }
                    }

                    optimizedProcesses.RemoveWhere(pid => robloxProcesses.All(p => p.Id != pid));
                    foreach (var pid in cpuCounters.Keys.Except(robloxProcesses.Select(p => p.Id)).ToList())
                    {
                        cpuCounters[pid].Dispose();
                        cpuCounters.Remove(pid);
                    }

                    await Task.Delay(2000, token);
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    App.Logger.WriteLine("Optimizer",
                        $"[Loop Error] {ex.Message} @ {DateTime.Now:T}");
                }
            }

            App.Logger.WriteLine("Optimizer", "Continuous Roblox optimization stopped.");
        }

        private static readonly float CpuHighThreshold = 80f;
        private static readonly float CpuLowThreshold = 20f;
        private static readonly float MemoryHighThreshold = 0.7f;
        private static readonly int MinWorkingSetMB = 50;

        private async Task MonitorProcessPerformance(Process process, Dictionary<int, PerformanceCounter> cpuCounters)
        {
            try
            {
                process.Refresh();

                if (!cpuCounters.ContainsKey(process.Id))
                {
                    var counter = new PerformanceCounter("Process", "% Processor Time", process.ProcessName, true);
                    counter.NextValue();
                    cpuCounters[process.Id] = counter;
                }

                await Task.Delay(300);

                float cpuUsage = cpuCounters[process.Id].NextValue() / Environment.ProcessorCount;
                if (cpuUsage > CpuHighThreshold)
                    process.PriorityClass = ProcessPriorityClass.AboveNormal;

                try
                {
                    process.PriorityBoostEnabled = true;
                    NativeMethods.SetProcessPriority(process.Handle, NativeMethods.PriorityClass.PROCESS_MODE_BACKGROUND_END);
                }
                catch { }
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT CurrentUsage FROM Win32_VideoController");
                    foreach (var obj in searcher.Get())
                    {
                        uint gpuLoad = (uint)(obj["CurrentUsage"] ?? 0);
                        if (gpuLoad > 85)
                            process.PriorityClass = ProcessPriorityClass.AboveNormal;
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Optimizer", $"[Monitor Error] {process.ProcessName} (PID {process.Id}): {ex.Message}");
            }
        }

        internal static class NativeMethods
        {
            [DllImport("kernel32.dll")]
            internal static extern bool SetProcessPriority(IntPtr handle, PriorityClass priorityClass);

            internal enum PriorityClass : uint
            {
                PROCESS_MODE_BACKGROUND_BEGIN = 0x00100000,
                PROCESS_MODE_BACKGROUND_END = 0x00200000
            }
        }

        private void StopContinuousRobloxOptimization()
        {
            _optimizationCts?.Cancel();
            _optimizationCts = null;
        }

        private bool ShouldRunAsAdmin()
        {
            foreach (var root in WindowsRegistry.Roots)
            {
                using var key = root.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers");

                if (key is null)
                    continue;

                string? flags = (string?)key.GetValue(AppData.ExecutablePath);

                if (flags is not null && flags.Contains("RUNASADMIN", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public void Cancel()
        {
            const string LOG_IDENT = "Bootstrapper::Cancel";

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            App.Logger.WriteLine(LOG_IDENT, "Cancelling launch...");

            _cancelTokenSource.Cancel();

            if (Dialog is not null)
                Dialog.CancelEnabled = false;

            if (_isInstalling)
            {
                try
                {
                    if (Directory.Exists(_latestVersionDirectory))
                        Directory.Delete(_latestVersionDirectory, true);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Could not fully clean up installation!");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
            else if (_appPid != 0)
            {
                try
                {
                    using var process = Process.GetProcessById(_appPid);
                    process.Kill();
                }
                catch (Exception) { }
            }

            Dialog?.CloseBootstrapper();

            App.SoftTerminate(ErrorCode.ERROR_CANCELLED);
        }
        #endregion

        #region Roblox Install
        private void MigrateCompatibilityFlags()
        {
            const string LOG_IDENT = "Bootstrapper::MigrateCompatibilityFlags";

            string oldClientLocation = Path.Combine(Paths.Versions, AppData.State.VersionGuid, AppData.ExecutableName);
            string newClientLocation = Path.Combine(_latestVersionDirectory, AppData.ExecutableName);
            using RegistryKey appFlagsKey = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers");
            string? appFlags = appFlagsKey.GetValue(oldClientLocation) as string;

            if (appFlags is not null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Migrating app compatibility flags from {oldClientLocation} to {newClientLocation}...");
                appFlagsKey.SetValueSafe(newClientLocation, appFlags);
                appFlagsKey.DeleteValueSafe(oldClientLocation);
            }
        }

        private static void KillRobloxPlayers()
        {
            const string LOG_IDENT = "Bootstrapper::KillRobloxPlayers";

            List<Process> processes = new List<Process>();
            processes.AddRange(Process.GetProcessesByName("RobloxPlayerBeta"));
            processes.AddRange(Process.GetProcessesByName("RobloxCrashHandler")); // roblox studio doesnt depend on crash handler being open, so this should be fine

            foreach (Process process in processes)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to close process {process.Id}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }

        private void CleanupVersionsFolder()
        {
            const string LOG_IDENT = "Bootstrapper::CleanupVersionsFolder";

            try
            {
                foreach (string dir in Directory.GetDirectories(Paths.Versions))
                {
                    string dirName = Path.GetFileName(dir);

                    if (dirName != App.State.Prop.Player.VersionGuid && dirName != App.State.Prop.Studio.VersionGuid)
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                            App.Logger.WriteLine(LOG_IDENT, $"Deleted outdated version folder: {dir}");
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {dir}: {ex.Message}");
                            App.Logger.WriteException(LOG_IDENT, ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Unexpected error during CleanupVersionsFolder.");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }
        private static async Task WithRetryAsync(
            Func<Task> action,
            string context,
            int maxAttempts = 5,
            int baseDelayMs = 750,
            Func<Exception, bool>? isTransient = null,
            CancellationToken ct = default)
        {
            isTransient ??= static ex =>
                ex is TaskCanceledException ||
                ex is HttpRequestException ||
                ex is IOException ||
                ex is SocketException;

            var rng = new Random();
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await action().ConfigureAwait(false);
                    return;
                }
                catch (Exception ex) when (isTransient(ex) && attempt < maxAttempts)
                {
                    int delay = baseDelayMs * (1 << (attempt - 1));
                    int jitter = (int)(delay * (0.15 * (rng.NextDouble() * 2 - 1)));
                    delay = Math.Clamp(delay + jitter, 250, 10_000);

                    App.Logger.WriteLine(context, $"Transient error on attempt {attempt}/{maxAttempts}: {ex.Message}. Retrying in {delay}ms...");
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }
        private static async Task SafeDeleteDirectoryAsync(string path, string context, CancellationToken ct)
        {
            if (!Directory.Exists(path)) return;

            await WithRetryAsync(
                action: async () =>
                {
                    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                    }
                    Directory.Delete(path, recursive: true);
                    await Task.CompletedTask;
                },
                context: $"{context}::SafeDeleteDirectory({path})",
                maxAttempts: 5,
                baseDelayMs: 600,
                isTransient: ex => ex is IOException || ex is UnauthorizedAccessException,
                ct: ct
            ).ConfigureAwait(false);
        }

        private async Task UpgradeRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::UpgradeRoblox";
            var ct = _cancelTokenSource.Token;
            if (_isInstalling) { App.Logger.WriteLine(LOG_IDENT, "Upgrade already in progress; skipping."); return; }
            _isInstalling = true;

            try
            {
                if (!App.Settings.Prop.UpdateRoblox)
                {
                    SetStatus(Strings.Bootstrapper_Status_CancelUpgrade);
                    App.Logger.WriteLine(LOG_IDENT, "Upgrading disabled, cancelling the upgrade.");
                    await Task.Delay(250, ct).ConfigureAwait(false);

                    if (!Directory.Exists(_latestVersionDirectory))
                    {
                        Frontend.ShowMessageBox(Strings.Bootstrapper_Dialog_NoUpgradeWithoutClient, MessageBoxImage.Warning, MessageBoxButton.OK);
                    }
                    return;
                }

                SetStatus(string.IsNullOrEmpty(AppData.State.VersionGuid)
                    ? Strings.Bootstrapper_Status_Installing
                    : Strings.Bootstrapper_Status_Upgrading);

                Directory.CreateDirectory(Paths.Base);
                Directory.CreateDirectory(Paths.Downloads);
                Directory.CreateDirectory(Paths.Versions);

                var cachedPackageHashes = Directory.GetFiles(Paths.Downloads).Select(Path.GetFileName).ToList();
                if (!IsStudioLaunch)
                    await Task.Run(KillRobloxPlayers, ct).ConfigureAwait(false);
                if (Directory.Exists(_latestVersionDirectory))
                {
                    try
                    {
                        await SafeDeleteDirectoryAsync(_latestVersionDirectory, LOG_IDENT, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to delete latest version directory");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }
                Directory.CreateDirectory(_latestVersionDirectory);
                if (_versionPackageManifest == null || !_versionPackageManifest.Any())
                {
                    App.Logger.WriteLine(LOG_IDENT, "Warning: _versionPackageManifest is null or empty. Skipping upgrade logic.");
                    return;
                }

                long totalPackedSize = _versionPackageManifest.Sum(p => (long)p.PackedSize);
                long totalUnpackedSize = _versionPackageManifest.Sum(p => (long)p.Size);
                long totalSizeRequired = totalPackedSize + totalUnpackedSize;

                if (Filesystem.GetFreeDiskSpace(Paths.Base) < totalSizeRequired)
                {
                    Frontend.ShowMessageBox(Strings.Bootstrapper_NotEnoughSpace, MessageBoxImage.Error);
                    App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
                    return;
                }
                if (Dialog is not null)
                {
                    Dialog.ProgressStyle = ProgressBarStyle.Continuous;
                    Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;

                    Dialog.ProgressMaximum = ProgressBarMaximum;
                    _progressIncrement = (double)ProgressBarMaximum / Math.Max(1, totalPackedSize);

                    _taskbarProgressMaximum = Dialog is WinFormsDialogBase
                        ? TaskbarProgressMaximumWinForms
                        : TaskbarProgressMaximumWpf;

                    _taskbarProgressIncrement = _taskbarProgressMaximum / Math.Max(1, totalPackedSize);
                }

                var totalPackages = _versionPackageManifest.Count;
                int packagesCompleted = 0;

                long totalBytesToDownload = _versionPackageManifest.Sum(p => (long)p.PackedSize);
                long totalBytesDownloaded = 0;

                using var throttler = new SemaphoreSlim(8);
                var swOverall = Stopwatch.StartNew();

                App.Logger.WriteLine(LOG_IDENT, $"Preparing to download {totalPackages} packages ({totalBytesToDownload / 1048576.0:F2} MB total)");
                var tasks = _versionPackageManifest.Select(async package =>
                {
                    await throttler.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        await WithRetryAsync(
                            async () =>
                            {
                                await DownloadPackage(package).ConfigureAwait(false);
                            },
                            context: $"{LOG_IDENT}::Download({package.Name})",
                            maxAttempts: 4,
                            baseDelayMs: 800,
                            ct: ct
                        ).ConfigureAwait(false);

                        Interlocked.Add(ref totalBytesDownloaded, package.PackedSize);
                        int completed = Interlocked.Increment(ref packagesCompleted);
                        double elapsedSec = Math.Max(0.5, swOverall.Elapsed.TotalSeconds);
                        double speedBytesPerSec = totalBytesDownloaded / elapsedSec;
                        double remainingBytes = totalBytesToDownload - totalBytesDownloaded;
                        double remainingSec = remainingBytes / Math.Max(speedBytesPerSec, 1);
                        string eta = TimeSpan.FromSeconds(remainingSec).ToString(@"hh\:mm\:ss");
                        SetStatus($"Downloading packages... ({completed}/{totalPackages}) | ETA: {eta}");
                        _totalDownloadedBytes = totalBytesDownloaded;
                        UpdateProgressBar();
                        await WithRetryAsync(
                            async () =>
                            {
                                ExtractPackage(package);
                                await Task.CompletedTask;
                            },
                            context: $"{LOG_IDENT}::Extract({package.Name})",
                            maxAttempts: 4,
                            baseDelayMs: 800,
                            isTransient: ex => ex is IOException || ex is UnauthorizedAccessException,
                            ct: ct
                        ).ConfigureAwait(false);
                    }
                    catch (System.Text.Json.JsonException jex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Invalid JSON for package {package.Name}: {jex.Message}");
                        await HandleConnectionError(jex).ConfigureAwait(false);
                    }
                    catch (HttpRequestException httpEx)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"HTTP error downloading {package.Name}: {httpEx.StatusCode} - {httpEx.Message}");
                        await HandleConnectionError(httpEx).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed processing package {package.Name}: {ex}");
                    }
                    finally
                    {
                        throttler.Release();
                    }
                }).ToList();

                await Task.WhenAll(tasks).ConfigureAwait(false);
                swOverall.Stop();

                if (ct.IsCancellationRequested) return;
                if (Dialog is not null)
                {
                    Dialog.ProgressStyle = ProgressBarStyle.Marquee;
                    Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
                    SetStatus(Strings.Bootstrapper_Status_Configuring);
                }

                App.Logger.WriteLine(LOG_IDENT, "Writing AppSettings.xml...");
                await WithRetryAsync(
                    async () =>
                    {
                        await File.WriteAllTextAsync(Path.Combine(_latestVersionDirectory, "AppSettings.xml"), AppSettings, ct).ConfigureAwait(false);
                    },
                    context: $"{LOG_IDENT}::Write(AppSettings.xml)",
                    maxAttempts: 3,
                    baseDelayMs: 600,
                    isTransient: ex => ex is IOException || ex is UnauthorizedAccessException,
                    ct: ct
                ).ConfigureAwait(false);

                if (ct.IsCancellationRequested) return;
                try { MigrateCompatibilityFlags(); }
                catch (Exception ex) { App.Logger.WriteLine(LOG_IDENT, $"MigrateCompatibilityFlags failed: {ex.Message}"); }

                AppData.State.VersionGuid = _latestVersionGuid;
                AppData.State.PackageHashes.Clear();
                foreach (var package in _versionPackageManifest)
                    AppData.State.PackageHashes.Add(package.Name, package.Signature);
                try { CleanupVersionsFolder(); } catch (Exception ex) { App.Logger.WriteLine(LOG_IDENT, $"CleanupVersionsFolder failed: {ex.Message}"); }
                var allPackageHashes = App.State.Prop.Player.PackageHashes.Values
                    .Concat(App.State.Prop.Studio.PackageHashes.Values)
                    .ToHashSet();

                var deleteTasks = cachedPackageHashes
                    .Where(hash => !allPackageHashes.Contains(hash))
                    .Select(async hash =>
                    {
                        string path = Path.Combine(Paths.Downloads, hash);
                        await WithRetryAsync(
                            async () =>
                            {
                                try { if (File.Exists(path)) File.Delete(path); } catch { }
                                await Task.CompletedTask;
                            },
                            context: $"{LOG_IDENT}::DeleteCache({hash})",
                            maxAttempts: 3,
                            baseDelayMs: 500,
                            isTransient: ex => ex is IOException || ex is UnauthorizedAccessException,
                            ct: ct
                        ).ConfigureAwait(false);
                    });

                await Task.WhenAll(deleteTasks).ConfigureAwait(false);
                try
                {
                    App.Logger.WriteLine(LOG_IDENT, "Registering approximate program size...");
                    int bufferSizeKbte = int.Parse(App.Settings.Prop.BufferSizeKbte);
                    int distributionSize =
                        _versionPackageManifest.Sum(x => x.Size + x.PackedSize) / bufferSizeKbte;
                    AppData.State.Size = distributionSize;
                    int totalSize = App.State.Prop.Player.Size + App.State.Prop.Studio.Size;
                    using (var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey))
                        uninstallKey?.SetValueSafe("EstimatedSize", totalSize);

                    App.Logger.WriteLine(LOG_IDENT, $"Registered as {totalSize} KB");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to register size: {ex.Message}");
                }

                App.State.Save();
            }
            catch (TaskCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Upgrade was cancelled.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Unexpected upgrade error: {ex}");
                Frontend.ShowMessageBox($"{Strings.Bootstrapper_Status_Upgrading} failed:\n{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                _isInstalling = false;
            }
        }

        private const string ZipUrl =
            "https://github.com/KloBraticc/SkyboxPackV2/archive/refs/heads/main.zip";

        private const string CommitApiUrl =
            "https://api.github.com/repos/KloBraticc/SkyboxPackV2/commits/main";

        private const string VersionFile = "skybox.commit";
        private static readonly string PackRepoZip = "https://github.com/KloBraticc/SkyboxPackV2/archive/refs/heads/main.zip";
        private static readonly string PackFolder = Path.Combine(Paths.Base, "SkyboxPack");
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        private static readonly Dictionary<string, string> SkyboxPatchFolderMap = new()
    {
        { "a564ec8aeef3614e788d02f0090089d8", "a5" },
        { "7328622d2d509b95dd4dd2c721d1ca8b", "73" },
        { "a50f6563c50ca4d5dcb255ee5cfab097", "a5" },
        { "6c94b9385e52d221f0538aadaceead2d", "6c" },
        { "9244e00ff9fd6cee0bb40a262bb35d31", "92" },
        { "78cb2e93aee0cdbd79b15a866bc93a54", "78" }
    };

        private async Task<string> GetLatestCommitShaAsync()
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, CommitApiUrl);
            req.Headers.UserAgent.ParseAdd("SkyboxInstaller");

            using var res = await Http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            using var stream = await res.Content.ReadAsStreamAsync();
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);

            return doc.RootElement.GetProperty("sha").GetString()!;
        }

        private string? GetLocalCommit()
        {
            string path = Path.Combine(PackFolder, VersionFile);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private void SaveLocalCommit(string sha)
        {
            File.WriteAllText(Path.Combine(PackFolder, VersionFile), sha);
        }

        public async Task EnsureSkyboxPackDownloadedAsync()
        {
            Directory.CreateDirectory(PackFolder);

            string latestCommit = await GetLatestCommitShaAsync();
            string? localCommit = GetLocalCommit();

            if (localCommit == latestCommit &&
                Directory.Exists(PackFolder) &&
                Directory.GetFiles(PackFolder, "*", SearchOption.AllDirectories).Length > 0)
            {
                return;
            }

            SetStatus("Updating Skybox Pack...");

            string tempZipPath = Path.Combine(Path.GetTempPath(), "SkyboxPackV2.zip");

            using (var response = await Http.GetAsync(ZipUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                long totalRead = 0;
                var buffer = new byte[262144];
                var lastUpdate = Stopwatch.StartNew();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true);

                int read;
                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;

                    if (lastUpdate.ElapsedMilliseconds > 200)
                    {
                        SetStatus(totalBytes > 0
                            ? $"Downloading Skybox Data... {totalRead * 100.0 / totalBytes:F1}%"
                            : $"Downloading Skybox Data... {BytesToString(totalRead)}");

                        lastUpdate.Restart();
                    }
                }
            }

            Directory.Delete(PackFolder, true);
            Directory.CreateDirectory(PackFolder);

            using (var zip = System.IO.Compression.ZipFile.OpenRead(tempZipPath))
            {
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var parts = entry.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    var relativePath = Path.Combine(parts.Skip(1).ToArray());
                    var destPath = Path.Combine(PackFolder, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                    using var entryStream = entry.Open();
                    using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write);
                    await entryStream.CopyToAsync(fs);
                }
            }

            SaveLocalCommit(latestCommit);
            File.Delete(tempZipPath);
        }

        private static string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0) return "0B";
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return $"{Math.Sign(byteCount) * num} {suf[place]}";
        }

        public static async Task ApplySkyboxAsync(string skyboxName, string modsFolder)
        {
            string sourceFolder = Path.Combine(PackFolder, skyboxName);
            if (!Directory.Exists(sourceFolder))
                throw new DirectoryNotFoundException($"Skybox '{skyboxName}' not found in PackFolder.");

            string skyboxPath = Path.Combine(modsFolder, "PlatformContent", "pc", "textures", "sky");
            if (Directory.Exists(skyboxPath))
            {
                foreach (var file in Directory.GetFiles(skyboxPath, "*.*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(skyboxPath, true);
            }

            Directory.CreateDirectory(skyboxPath);

            foreach (var file in Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceFolder, file);
                string dest = Path.Combine(skyboxPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, true);
            }

            await Task.CompletedTask;
        }

        public static async Task ApplySkyboxPatchToRobloxStorageAsync()
        {
            string rbxStorage = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "rbx-storage");
            string githubBaseUrl = "https://raw.githubusercontent.com/KloBraticc/SkyboxPatch/main/assets/";

            using HttpClient http = new HttpClient();

            foreach (var kv in SkyboxPatchFolderMap)
            {
                string fileDestFolder = Path.Combine(rbxStorage, kv.Value);
                Directory.CreateDirectory(fileDestFolder);

                string destFile = Path.Combine(fileDestFolder, kv.Key);

                try
                {
                    string fileUrl = githubBaseUrl + kv.Key;
                    byte[] fileData = await http.GetByteArrayAsync(fileUrl);
                    if (File.Exists(destFile))
                        File.SetAttributes(destFile, FileAttributes.Normal);

                    await File.WriteAllBytesAsync(destFile, fileData);
                    File.SetAttributes(destFile, FileAttributes.ReadOnly);

                    Console.WriteLine($"Downloaded and patched: {kv.Key}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to process {kv.Key}: {ex.Message}");
                }
            }
        }

        private async Task ApplyModifications()
        {
            const string LOG_IDENT = "Bootstrapper::ApplyModifications";
            SetStatus(Strings.Bootstrapper_Status_ApplyingModifications);

            App.Logger.WriteLine(LOG_IDENT, "Checking file mods...");
            File.Delete(Path.Combine(Paths.Base, "ModManifest.txt"));

            List<string> modFolderFiles = new();

            Directory.CreateDirectory(Paths.Mods);
            App.Logger.WriteLine("Bootstrapper::ApplyModifications", "Applying SkyboxPatch...");

            string selectedSkybox = App.Settings.Prop.SkyboxName;
            string selectedFont = App.Settings.Prop.FontName;
            string modsFolder = Paths.Mods;

            App.Logger.WriteLine("Bootstrapper::ApplyModifications", "Applying Skybox mod and patch...");

            try
            {
                await ApplySkyboxPatchToRobloxStorageAsync();
                await EnsureSkyboxPackDownloadedAsync();
                await ApplySkyboxAsync(selectedSkybox, modsFolder);
                App.Logger.WriteLine("Bootstrapper::ApplyModifications", "Skybox mod and patch applied successfully!");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Bootstrapper::ApplyModifications", "Failed to apply Skybox mod/patch: " + ex.Message);
            }

            string modFontFamiliesFolder = Path.Combine(Paths.Mods, "content\\fonts\\families");

            if (File.Exists(Paths.CustomFont))
            {
                App.Logger.WriteLine(LOG_IDENT, "Begin font check");

                Directory.CreateDirectory(modFontFamiliesFolder);

                const string path = "rbxasset://fonts/CustomFont.ttf";
                string contentFolder = Path.Combine(_latestVersionDirectory, "content");
                Directory.CreateDirectory(contentFolder);

                string fontsFolder = Path.Combine(contentFolder, "fonts");
                Directory.CreateDirectory(fontsFolder);

                string familiesFolder = Path.Combine(fontsFolder, "families");
                Directory.CreateDirectory(familiesFolder);

                foreach (string jsonFilePath in Directory.GetFiles(familiesFolder))
                {
                    string jsonFilename = Path.GetFileName(jsonFilePath);
                    string modFilepath = Path.Combine(modFontFamiliesFolder, jsonFilename);

                    if (File.Exists(modFilepath))
                        continue;

                    App.Logger.WriteLine(LOG_IDENT, $"Setting font for {jsonFilename}");

                    var fontFamilyData = JsonSerializer.Deserialize<FontFamily>(File.ReadAllText(jsonFilePath));

                    if (fontFamilyData is null)
                        continue;

                    bool shouldWrite = false;

                    foreach (var fontFace in fontFamilyData.Faces)
                    {
                        if (fontFace.AssetId != path)
                        {
                            fontFace.AssetId = path;
                            shouldWrite = true;
                        }
                    }

                    if (shouldWrite)
                        File.WriteAllText(modFilepath, JsonSerializer.Serialize(fontFamilyData, new JsonSerializerOptions { WriteIndented = true }));
                }

                App.Logger.WriteLine(LOG_IDENT, "End font check");
            }
            else if (Directory.Exists(modFontFamiliesFolder))
            {
                Directory.Delete(modFontFamiliesFolder, true);
            }

            foreach (string file in Directory.GetFiles(Paths.Mods, "*.*", SearchOption.AllDirectories))
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                string relativeFile = file.Substring(Paths.Mods.Length + 1);

                if (relativeFile == "README.txt")
                {
                    File.Delete(file);
                    continue;
                }

                if (!App.Settings.Prop.UseFastFlagManager &&
                    String.Equals(relativeFile, "ClientSettings\\ClientAppSettings.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (relativeFile.EndsWith(".lock"))
                    continue;

                modFolderFiles.Add(relativeFile);

                string fileModFolder = Path.Combine(Paths.Mods, relativeFile);
                string fileVersionFolder = Path.Combine(_latestVersionDirectory, relativeFile);

                if (File.Exists(fileVersionFolder) &&
                    MD5Hash.FromFile(fileModFolder) == MD5Hash.FromFile(fileVersionFolder))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{relativeFile} already exists in the version folder, and is a match");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fileVersionFolder)!);

                Filesystem.AssertReadOnly(fileVersionFolder);
                File.Copy(fileModFolder, fileVersionFolder, true);
                Filesystem.AssertReadOnly(fileVersionFolder);

                App.Logger.WriteLine(LOG_IDENT, $"{relativeFile} has been copied to the version folder");
            }

            var fileRestoreMap = new Dictionary<string, List<string>>();

            foreach (string fileLocation in App.State.Prop.ModManifest)
            {
                if (modFolderFiles.Contains(fileLocation))
                    continue;

                var packageMapEntry = AppData.PackageDirectoryMap
                    .SingleOrDefault(x => !String.IsNullOrEmpty(x.Value) && fileLocation.StartsWith(x.Value));
                string packageName = packageMapEntry.Key;
                if (String.IsNullOrEmpty(packageName))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{fileLocation} was removed as a mod but does not belong to a package");

                    string versionFileLocation = Path.Combine(_latestVersionDirectory, fileLocation);

                    if (File.Exists(versionFileLocation))
                        File.Delete(versionFileLocation);

                    continue;
                }

                string fileName = fileLocation.Substring(packageMapEntry.Value.Length);

                if (!fileRestoreMap.ContainsKey(packageName))
                    fileRestoreMap[packageName] = new();

                fileRestoreMap[packageName].Add(fileName);

                App.Logger.WriteLine(LOG_IDENT, $"{fileLocation} was removed as a mod, restoring from {packageName}");
            }

            foreach (var entry in fileRestoreMap)
            {
                var package = _versionPackageManifest.Find(x => x.Name == entry.Key);

                if (package is not null)
                {
                    if (_cancelTokenSource.IsCancellationRequested)
                        return;

                    await DownloadPackage(package);
                    ExtractPackage(package, entry.Value);
                }
            }

            App.State.Prop.ModManifest = modFolderFiles;
            App.State.Save();

            App.Logger.WriteLine(LOG_IDENT, "Checking for eurotrucks2.exe toggle");

            try
            {
                bool isEuroTrucks = File.Exists(Path.Combine(_latestVersionDirectory, "eurotrucks2.exe"));

                if (App.Settings.Prop.RenameClientToEuroTrucks2)
                {
                    if (!isEuroTrucks)
                        File.Move(
                            Path.Combine(_latestVersionDirectory, "RobloxPlayerBeta.exe"),
                            Path.Combine(_latestVersionDirectory, "eurotrucks2.exe")
                        );
                }
                else
                {
                    if (isEuroTrucks)
                        File.Move(
                            Path.Combine(_latestVersionDirectory, "eurotrucks2.exe"),
                            Path.Combine(_latestVersionDirectory, "RobloxPlayerBeta.exe")
                        );
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to update client! " + ex.Message);
            }
        }

        private async Task DownloadPackage(Package package)
        {
            string LOG_IDENT = $"Bootstrapper::DownloadPackage.{package.Name}";
            bool isUpdating = !string.IsNullOrEmpty(AppData.State.VersionGuid);

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            Directory.CreateDirectory(Paths.Downloads);

            string packageUrl = Deployment.GetLocation($"/{_latestVersionGuid}-{package.Name}");
            if (!packageUrl.StartsWith("https://setup.rbxcdn.com", StringComparison.OrdinalIgnoreCase))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Warning: Deployment.GetLocation() returned unexpected URL '{packageUrl}'. Forcing setup.rbxcdn.com as base.");
                packageUrl = $"https://setup.rbxcdn.com/{_latestVersionGuid}-{package.Name}";
            }

            string robloxPackageLocation = Path.Combine(Paths.LocalAppData, "Roblox", "Downloads", package.Signature);
            if (File.Exists(package.DownloadPath))
            {
                string localHash = MD5Hash.FromFile(package.DownloadPath);
                if (localHash == package.Signature)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Already downloaded, skipping...");
                    _totalDownloadedBytes += package.PackedSize;
                    UpdateProgressBar();
                    return;
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "Corrupted file found, deleting...");
                    File.Delete(package.DownloadPath);
                }
            }
            else if (File.Exists(robloxPackageLocation))
            {
                try
                {
                    File.Copy(robloxPackageLocation, package.DownloadPath, true);
                    _totalDownloadedBytes += package.PackedSize;
                    UpdateProgressBar();
                    return;
                }
                catch (Exception copyEx)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to copy from Roblox cache: {copyEx.Message}");
                }
            }

            const int MaxRetries = 10;
            const int MaxParallelSegments = 4;
            const int BufferSize = 1024 * 1024;
            const long MinMultiPartSize = BufferSize * 4;

            var tempFile = package.DownloadPath + ".part";
            if (File.Exists(tempFile))
                File.Delete(tempFile);

            App.Logger.WriteLine(LOG_IDENT, $"Starting download from {packageUrl}");

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                try
                {
                    using var response = await App.HttpClient.GetAsync(
                        packageUrl,
                        HttpCompletionOption.ResponseHeadersRead,
                        _cancelTokenSource.Token
                    );

                    if (!response.IsSuccessStatusCode)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Download failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

                        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Received 403 Forbidden — using setup.rbxcdn.com fallback.");
                            packageUrl = $"https://setup.rbxcdn.com/{_latestVersionGuid}-{package.Name}";
                            continue;
                        }
                        response.EnsureSuccessStatusCode();
                    }

                    var contentLength = response.Content.Headers.ContentLength;
                    bool supportsRanges =
                        contentLength.HasValue &&
                        contentLength.Value >= MinMultiPartSize &&
                        response.Headers.AcceptRanges != null &&
                        response.Headers.AcceptRanges.Contains("bytes");
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);

                    if (supportsRanges)
                    {
                        response.Dispose();
                        await DownloadMultipartAsync(
                            packageUrl,
                            tempFile,
                            contentLength!.Value,
                            BufferSize,
                            MaxParallelSegments,
                            isUpdating,
                            LOG_IDENT,
                            _cancelTokenSource.Token
                        );
                    }
                    else
                    {
                        await DownloadSingleThreadAsync(
                            response,
                            tempFile,
                            BufferSize,
                            isUpdating,
                            LOG_IDENT,
                            _cancelTokenSource.Token
                        );
                    }

                    string hash = MD5Hash.FromFile(tempFile);
                    if (!hash.Equals(package.Signature, StringComparison.OrdinalIgnoreCase))
                        throw new ChecksumFailedException($"Checksum mismatch for {package.Name}: expected {package.Signature}, got {hash}");

                    File.Move(tempFile, package.DownloadPath, true);
                    App.Logger.WriteLine(LOG_IDENT, $"Download complete ({package.Name})");
                    _totalDownloadedBytes += package.PackedSize;
                    if (isUpdating)
                        UpdateProgressBar();

                    return;
                }
                catch (ChecksumFailedException ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Checksum failed ({attempt}/{MaxRetries}): {ex.Message}");
                    if (File.Exists(tempFile)) File.Delete(tempFile);

                    Frontend.ShowConnectivityDialog(
                        Strings.Dialog_Connectivity_UnableToDownload,
                        Strings.Dialog_Connectivity_UnableToDownloadReason.Replace("[link]", "https://github.com/bloxstraplabs/bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox"),
                        MessageBoxImage.Error,
                        ex
                    );

                    App.Terminate(ErrorCode.ERROR_CANCELLED);
                }
                catch (TaskCanceledException)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Download cancelled by user");
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                    return;
                }
                catch (Exception ex)
                {
                    if (ex is AggregateException agg)
                        ex = agg.Flatten().InnerException ?? agg;

                    App.Logger.WriteLine(LOG_IDENT, $"Download failed ({attempt}/{MaxRetries}): {ex.Message}");
                    if (File.Exists(tempFile)) File.Delete(tempFile);

                    if (attempt == MaxRetries)
                        throw;

                    int delay = Math.Min(2000 * attempt, 10000);
                    await Task.Delay(delay, _cancelTokenSource.Token);
                }
            }
        }
        private static async Task DownloadSingleThreadAsync(
            HttpResponseMessage response,
            string tempFile,
            int bufferSize,
            bool isUpdating,
            string logIdent,
            CancellationToken token)
        {
            await using var networkStream = await response.Content.ReadAsStreamAsync(token);
            await using var fileStream = new FileStream(
                tempFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            );

            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

            try
            {
                long totalRead = 0;
                long localDownloaded = 0;
                var sw = Stopwatch.StartNew();
                int read;

                while ((read = await networkStream.ReadAsync(buffer.AsMemory(0, bufferSize), token)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), token);
                    totalRead += read;
                    localDownloaded += read;

                    if (isUpdating && sw.ElapsedMilliseconds >= 400)
                    {
                        App.Current.Dispatcher.Invoke(() => {
                            App.Logger.WriteLine(logIdent, $"Progress: +{localDownloaded:N0} bytes (single-thread)");
                        });

                        localDownloaded = 0;
                        sw.Restart();
                    }
                }

                App.Logger.WriteLine(logIdent, $"Downloaded {totalRead:N0} bytes (single-threaded)");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        private async Task DownloadMultipartAsync(
            string packageUrl,
            string tempFile,
            long contentLength,
            int bufferSize,
            int maxParallelSegments,
            bool isUpdating,
            string logIdent,
            CancellationToken token)
        {
            const long MinSegmentSize = 2L * 1024 * 1024;

            int segmentCount = (int)Math.Min(
                maxParallelSegments,
                Math.Max(1, contentLength / MinSegmentSize)
            );

            if (segmentCount <= 1)
            {
                using var fallbackClient = new HttpClient();
                using var resp = await fallbackClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead, token);
                await DownloadSingleThreadAsync(resp, tempFile, bufferSize, isUpdating, logIdent, token);
                return;
            }

            long baseSegmentSize = contentLength / segmentCount;

            App.Logger.WriteLine(logIdent, $"Using multi-part download: {segmentCount} segments of ~{baseSegmentSize:N0} bytes");
            using var fileStream = new FileStream(
                tempFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.RandomAccess
            );
            fileStream.SetLength(contentLength);

            object fileLock = new object();
            long totalRead = 0;

            var tasks = new List<Task>(segmentCount);
            CancellationTokenSource? progressCts = null;
            Task progressTask = Task.CompletedTask;

            if (isUpdating)
            {
                progressCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                var progressToken = progressCts.Token;

                progressTask = Task.Run(async () =>
                {
                    var sw = Stopwatch.StartNew();
                    while (!progressToken.IsCancellationRequested)
                    {
                        if (sw.ElapsedMilliseconds >= 400)
                        {
                            try
                            {
                                UpdateProgressBar();
                            }
                            catch (Exception ex)
                            {
                                App.Logger.WriteLine(logIdent, $"Progress update failed: {ex.Message}");
                            }

                            sw.Restart();
                        }

                        await Task.Delay(100, progressToken);
                    }
                }, progressToken);
            }

            for (int i = 0; i < segmentCount; i++)
            {
                long start = i * baseSegmentSize;
                long end = (i == segmentCount - 1)
                    ? contentLength - 1
                    : (start + baseSegmentSize - 1);

                tasks.Add(Task.Run(async () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, packageUrl)
                    {
                        Headers = { Range = new RangeHeaderValue(start, end) }
                    };

                    using var response = await App.HttpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        token
                    );

                    response.EnsureSuccessStatusCode();

                    await using var networkStream = await response.Content.ReadAsStreamAsync(token);
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

                    try
                    {
                        long position = start;
                        int read;

                        while ((read = await networkStream.ReadAsync(buffer.AsMemory(0, bufferSize), token)) > 0)
                        {
                            lock (fileLock)
                            {
                                fileStream.Position = position;
                                fileStream.Write(buffer, 0, read);
                            }

                            position += read;

                            Interlocked.Add(ref totalRead, read);
                            Interlocked.Add(ref _totalDownloadedBytes, read);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }, token));
            }

            try
            {
                await Task.WhenAll(tasks);
                await fileStream.FlushAsync(token);
            }
            finally
            {
                if (progressCts != null)
                {
                    progressCts.Cancel();
                    try { await progressTask; } catch { }
                    progressCts.Dispose();
                }
            }

            App.Logger.WriteLine(logIdent, $"Downloaded {totalRead:N0} bytes (multi-part)");
        }

        private void ExtractPackage(Package package, List<string>? files = null)
        {
            const string LOG_IDENT = "Bootstrapper::ExtractPackage";

            string? packageDir = AppData.PackageDirectoryMap.GetValueOrDefault(package.Name);

            if (packageDir is null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"WARNING: {package.Name} was not found in the package map!");
                return;
            }

            string packageFolder = Path.Combine(_latestVersionDirectory, packageDir);
            string? fileFilter = null;
            if (files is not null)
            {
                var regexList = new List<string>();

                foreach (string file in files)
                    regexList.Add("^" + file.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + "$");

                fileFilter = String.Join(';', regexList);
            }

            App.Logger.WriteLine(LOG_IDENT, $"Extracting {package.Name}...");

            var fastZip = new FastZip(_fastZipEvents);

            fastZip.ExtractZip(package.DownloadPath, packageFolder, fileFilter);

            App.Logger.WriteLine(LOG_IDENT, $"Finished extracting {package.Name}");
        }
        #endregion
    }
}
#endregion
