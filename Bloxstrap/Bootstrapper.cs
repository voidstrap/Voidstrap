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

using System.ComponentModel;
using System.Data;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Shell;

using Microsoft.Win32;

using ICSharpCode.SharpZipLib.Zip;
using Voidstrap.AppData;
using Voidstrap.RobloxInterfaces;
using Voidstrap.UI.Elements.Bootstrapper.Base;
using Voidstrap;

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

        private string _launchCommandLine = App.LaunchSettings.RobloxLaunchArgs;
        private string _latestVersionGuid = null!;
        private string _latestVersionDirectory = null!;
        private PackageManifest _versionPackageManifest = null!;

        private bool _isInstalling = false;
        private double _progressIncrement;
        private double _taskbarProgressIncrement;
        private double _taskbarProgressMaximum;
        private long _totalDownloadedBytes = 0;

        private bool _mustUpgrade => String.IsNullOrEmpty(AppData.State.VersionGuid) || !File.Exists(AppData.ExecutablePath);
        private bool _noConnection = false;

        private AsyncMutex? _mutex;

        private int _appPid = 0;

        private int totalPackageSize = 0;

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

            // UI progress
            int progressValue = (int)Math.Floor(_progressIncrement * _totalDownloadedBytes);

            // bugcheck: if we're restoring a file from a package, it'll incorrectly increment the progress beyond 100
            // too lazy to fix properly so lol
            progressValue = Math.Clamp(progressValue, 0, ProgressBarMaximum);

            Dialog.ProgressValue = progressValue;

            // taskbar progress
            double taskbarProgressValue = _taskbarProgressIncrement * _totalDownloadedBytes;
            taskbarProgressValue = Math.Clamp(taskbarProgressValue, 0, _taskbarProgressMaximum);

            Dialog.TaskbarProgressValue = taskbarProgressValue;
        }

        private void HandleConnectionError(Exception exception)
        {
            const string LOG_IDENT = "Bootstrapper::HandleConnectionError";

            _noConnection = true;

            App.Logger.WriteLine(LOG_IDENT, "Connectivity check failed");
            App.Logger.WriteException(LOG_IDENT, exception);

            string message = Strings.Dialog_Connectivity_BadConnection;

            if (exception is AggregateException)
                exception = exception.InnerException!;

            // https://gist.github.com/pizzaboxer/4b58303589ee5b14cc64397460a8f386
            if (exception is HttpRequestException && exception.InnerException is null)
                message = String.Format(Strings.Dialog_Connectivity_RobloxDown, "[status.roblox.com](https://status.roblox.com)");

            if (_mustUpgrade)
                message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeNeeded}\n\n{Strings.Dialog_Connectivity_TryAgainLater}";
            else
                message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeSkip}";

            Frontend.ShowConnectivityDialog(
                String.Format(Strings.Dialog_Connectivity_UnableToConnect, "Roblox"),
                message,
                _mustUpgrade ? MessageBoxImage.Error : MessageBoxImage.Warning,
                exception);

            if (_mustUpgrade)
                App.Terminate(ErrorCode.ERROR_CANCELLED);
        }

        public async Task Run()
        {
            const string LOG_IDENT = "Bootstrapper::Run";

            App.Logger.WriteLine(LOG_IDENT, "Running bootstrapper");

            // this is now always enabled as of v1.0.3.6
            if (Dialog is not null)
                Dialog.CancelEnabled = true;

            SetStatus(Strings.Bootstrapper_Status_Connecting);

            var connectionResult = await Deployment.InitializeConnectivity();

            App.Logger.WriteLine(LOG_IDENT, "Connectivity check finished");

            if (connectionResult is not null)
                HandleConnectionError(connectionResult);

#if (!DEBUG || DEBUG_UPDATER) && !QA_BUILD
            if (App.Settings.Prop.CheckForUpdates && !App.LaunchSettings.UpgradeFlag.Active)
            {
                bool updatePresent = await CheckForUpdates();

                if (updatePresent)
                    return;
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
            catch (WaitHandleCannotBeOpenedException)
            {
                // No mutex exists yet
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Unexpected error checking mutex: {ex}");
            }

            // Create and acquire the mutex
            await using var mutex = new AsyncMutex(false, "Voidstrap-Bootstrapper");
            await mutex.AcquireAsync(_cancelTokenSource.Token);
            _mutex = mutex;

            // Reload configs if waiting for other instances
            if (mutexExists)
            {
                App.Settings.Load();
                App.State.Load();
            }

            // Your existing logic
            if (!_noConnection)
            {
                try
                {
                    await GetLatestVersionInfo();
                }
                catch (Exception ex)
                {
                    HandleConnectionError(ex);
                }
            }

            if (!_noConnection)
            {
                if (AppData.State.VersionGuid != _latestVersionGuid || _mustUpgrade)
                    await UpgradeRoblox();

                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                await ApplyModifications();
            }

            if (IsStudioLaunch)
                WindowsRegistry.RegisterStudio();
            else
                WindowsRegistry.RegisterPlayer();

            // Release the mutex only once, here
            await mutex.ReleaseAsync();

            if (!App.LaunchSettings.NoLaunchFlag.Active && !_cancelTokenSource.IsCancellationRequested)
                StartRoblox();

            Dialog?.CloseBootstrapper();
        }

        /// <summary>
        /// Will throw whatever HttpClient can throw
        /// </summary>
        /// <returns></returns>
        private async Task GetLatestVersionInfo()
        {
            const string LOG_IDENT = "Bootstrapper::GetLatestVersionInfo";

            // before we do anything, we need to query our channel
            // if it's set in the launch uri, we need to use it and set the registry key for it
            // else, check if the registry key for it exists, and use it

            using var key = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\ROBLOX Corporation\\Environments\\{AppData.RegistryName}\\Channel");

            var match = Regex.Match(
                App.LaunchSettings.RobloxLaunchArgs,
                "channel:([a-zA-Z0-9-_]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            // CHANNEL CHANGE MODE

            void EnrollChannel()
            {
                if (match.Groups.Count == 2)
                {
                    Deployment.Channel = match.Groups[1].Value.ToLowerInvariant();
                }
                else if (key.GetValue("www.roblox.com") is string value && !String.IsNullOrEmpty(value))
                {
                    Deployment.Channel = value.ToLowerInvariant();
                }
            }

            switch (App.Settings.Prop.ChannelChangeMode)
            {
                case ChannelChangeMode.Automatic:
                    App.Logger.WriteLine(LOG_IDENT, "Enrolling into channel");

                    EnrollChannel();
                    break;
                case ChannelChangeMode.Prompt:
                    App.Logger.WriteLine(LOG_IDENT, "Prompting channel enrollment");

                    if (
                        (!match.Success || match.Groups.Count != 2)
                        &&
                        match.Groups[2].Value != App.Settings.Prop.Channel
                        )
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Channel is either equal or incorrectly formatted");
                        break;
                    }

                    string DisplayChannel = !String.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : Deployment.DefaultChannel;

                    var Result = Frontend.ShowMessageBox(
                    String.Format(Strings.Bootstrapper_Bootstrapper_Dialog_PromptChannelChange,
                    DisplayChannel, App.Settings.Prop.Channel),
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo
                    );

                    if (Result == MessageBoxResult.Yes)
                        EnrollChannel();
                    break;
                case ChannelChangeMode.Ignore:
                    App.Logger.WriteLine(LOG_IDENT, "Ignoring channel enrollment");
                    break;
            }

            if (String.IsNullOrEmpty(Deployment.Channel))
                Deployment.Channel = Deployment.DefaultChannel;

            App.Logger.WriteLine(LOG_IDENT, $"Got channel as {Deployment.DefaultChannel}");

            ClientVersion clientVersion;

            try
            {
                clientVersion = await Deployment.GetInfo(Deployment.Channel);
            }
            catch (InvalidChannelException ex)
            {
                // copied from v2.5.4
                // we are keeping similar logic just updated for newer apis

                // If channel does not exist
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Reverting enrolled channel to {Deployment.DefaultChannel} because a WindowsPlayer build does not exist for {App.Settings.Prop.Channel}");
                }
                // If channel is not available to the user (private/internal release channel)
                else if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Reverting enrolled channel to {Deployment.DefaultChannel} because {App.Settings.Prop.Channel} is restricted for public use.");

                    // Only prompt if user has channel switching mode set to something other than Automatic.
                    if (App.Settings.Prop.ChannelChangeMode != ChannelChangeMode.Automatic)
                    {
                        Frontend.ShowMessageBox(
                            String.Format(
                                Strings.Boostrapper_Dialog_UnauthorizedChannel,
                                Deployment.Channel,
                                Deployment.DefaultChannel
                            ),
                            MessageBoxImage.Information
                        );
                    }
                }
                else
                {
                    throw;
                }

                Deployment.Channel = Deployment.DefaultChannel;
                clientVersion = await Deployment.GetInfo(Deployment.Channel);

                App.Settings.Prop.Channel = Deployment.DefaultChannel;
                App.Settings.Save();
            }

            if (clientVersion.IsBehindDefaultChannel)
            {
                MessageBoxResult action = App.Settings.Prop.ChannelChangeMode switch
                {
                    ChannelChangeMode.Prompt => Frontend.ShowMessageBox(
                        String.Format(Strings.Bootstrapper_Dialog_ChannelOutOfDate, Deployment.Channel, Deployment.DefaultChannel),
                        MessageBoxImage.Warning,
                        MessageBoxButton.YesNo
                    ),
                    ChannelChangeMode.Automatic => MessageBoxResult.Yes,
                    ChannelChangeMode.Ignore => MessageBoxResult.No,
                    _ => MessageBoxResult.None
                };

                if (action == MessageBoxResult.Yes)
                {
                    App.Logger.WriteLine("Bootstrapper::CheckLatestVersion", $"Changed Roblox channel from {App.Settings.Prop.Channel} to {Deployment.DefaultChannel}");

                    App.Settings.Prop.Channel = Deployment.DefaultChannel;
                    clientVersion = await Deployment.GetInfo(Deployment.Channel);
                }

                Deployment.Channel = Deployment.DefaultChannel;
                clientVersion = await Deployment.GetInfo();
            }

            key.SetValueSafe("www.roblox.com", Deployment.IsDefaultChannel ? "" : Deployment.Channel);

            _latestVersionGuid = clientVersion.VersionGuid;
            _latestVersionDirectory = Path.Combine(Paths.Versions, _latestVersionGuid);

            string pkgManifestUrl = Deployment.GetLocation($"/{_latestVersionGuid}-rbxPkgManifest.txt");
            var pkgManifestData = await App.HttpClient.GetStringAsync(pkgManifestUrl);

            _versionPackageManifest = new(pkgManifestData);
        }

        private void StartRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::StartRoblox";

            SetStatus(Strings.Bootstrapper_Status_Starting);

            // Check if we are launching the player and language override is enabled
            if (_launchMode == LaunchMode.Player && App.Settings.Prop.ForceRobloxLanguage)
            {
                // Attempt to extract the game locale from the launch command line
                var match = Regex.Match(_launchCommandLine, @"gameLocale:([a-z_]+)", RegexOptions.CultureInvariant);

                if (match.Success && match.Groups.Count > 1)
                {
                    string detectedLocale = match.Groups[1].Value;

                    // Replace the hardcoded locale with the detected one
                    _launchCommandLine = Regex.Replace(
                        _launchCommandLine,
                        @"robloxLocale:[a-z_]+",
                        $"robloxLocale:{detectedLocale}",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
            }

        var startInfo = new ProcessStartInfo()
            {
                FileName = AppData.ExecutablePath,
                Arguments = _launchCommandLine,
                WorkingDirectory = AppData.Directory
            };

            if (_launchMode == LaunchMode.Player && ShouldRunAsAdmin())
            {
                startInfo.Verb = "runas";
                startInfo.UseShellExecute = true;
            }
            else if (_launchMode == LaunchMode.StudioAuth)
            {
                Process.Start(startInfo);
                return;
            }

            string? logFileName = null;

            string rbxDir = Path.Combine(Paths.LocalAppData, "Roblox");
            if (!Directory.Exists(rbxDir))
                Directory.CreateDirectory(rbxDir);

            string rbxLogDir = Path.Combine(rbxDir, "logs");
            if (!Directory.Exists(rbxLogDir))
                Directory.CreateDirectory(rbxLogDir);

            var logWatcher = new FileSystemWatcher()
            {
                Path = rbxLogDir,
                Filter = "*.log",
                EnableRaisingEvents = true
            };

            var logCreatedEvent = new AutoResetEvent(false);

            logWatcher.Created += (_, e) =>
            {
                logWatcher.EnableRaisingEvents = false;
                logFileName = e.FullPath;
                logCreatedEvent.Set();
            };

            // v2.2.0 - byfron will trip if we keep a process handle open for over a minute, so we're doing this now
            try
            {
                using var process = Process.Start(startInfo)!;
                _appPid = process.Id;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // 1223 = ERROR_CANCELLED, gets thrown if a UAC prompt is cancelled
                return;
            }
            catch (Exception)
            {
                // attempt a reinstall on next launch
                File.Delete(AppData.ExecutablePath);
                throw;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Started Roblox (PID {_appPid}), waiting for log file");

            logCreatedEvent.WaitOne(TimeSpan.FromSeconds(15));

            if (String.IsNullOrEmpty(logFileName))
            {
                App.Logger.WriteLine(LOG_IDENT, "Unable to identify log file");
                Frontend.ShowPlayerErrorDialog();
                return;
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Got log file as {logFileName}");
            }

            _mutex?.ReleaseAsync();

            if (IsStudioLaunch)
                return;

            var autoclosePids = new List<int>();

            // launch custom integrations now
            foreach (var integration in App.Settings.Prop.CustomIntegrations)
            {
                if (!integration.SpecifyGame)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Launching custom integration '{integration.Name}' ({integration.Location} {integration.LaunchArgs} - autoclose is {integration.AutoClose})");


                    int pid = 0;

                    try


                    {
                        var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = integration.Location,
                            Arguments = integration.LaunchArgs.Replace("\r\n", " "),
                            WorkingDirectory = Path.GetDirectoryName(integration.Location),
                            UseShellExecute = true
                        })!;

                        pid = process.Id;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to launch integration '{integration.Name}'!");
                        App.Logger.WriteLine(LOG_IDENT, ex.Message);
                    }

                    if (integration.AutoClose && pid != 0)
                        autoclosePids.Add(pid);
                }
            }
            

            if (App.Settings.Prop.EnableActivityTracking || App.LaunchSettings.TestModeFlag.Active || autoclosePids.Any())
            {
                using var ipl = new InterProcessLock("Watcher", TimeSpan.FromSeconds(5));

                var watcherData = new WatcherData
                {
                    ProcessId = _appPid,
                    LogFile = logFileName,
                    AutoclosePids = autoclosePids
                };

                string watcherDataArg = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(watcherData)));

                string args = $"-watcher \"{watcherDataArg}\"";

                if (App.LaunchSettings.TestModeFlag.Active)
                    args += " -testmode";

                if (ipl.IsAcquired)
                    Process.Start(Paths.Process, args);
            }

            // allow for window to show, since the log is created pretty far beforehand
            Thread.Sleep(1000);
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
                    // clean up install
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

        #region App Install
        private async Task<bool> CheckForUpdates()
        {
            const string LOG_IDENT = "Bootstrapper::CheckForUpdates";

            // Ensure no other instance is running
            if (Process.GetProcessesByName(App.ProjectName).Length > 1)
            {
                App.Logger.WriteLine(LOG_IDENT, $"More than one Voidstrap instance running, aborting update check");
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, "Checking for updates...");

#if !DEBUG_UPDATER
			var releaseInfo = await App.GetLatestRelease();

			if (releaseInfo is null)
			{
				App.Logger.WriteLine(LOG_IDENT, "Failed to fetch release information.");
				return false;
			}

			// Strip leading 'V' or 'v' from versions before comparing
			string currentVersion = App.Version.TrimStart('V', 'v');
			string latestVersion = releaseInfo.TagName.TrimStart('V', 'v');

			var versionComparison = Utilities.CompareVersions(currentVersion, latestVersion);

			// Skip update if current version is equal or newer
			if (App.IsProductionBuild &&
				(versionComparison == VersionComparison.Equal || versionComparison == VersionComparison.GreaterThan))
			{
				App.Logger.WriteLine(LOG_IDENT, "No updates found. Current version is up-to-date.");
				return false;
			}

			if (Dialog is not null)
				Dialog.CancelEnabled = false;

			string version = releaseInfo.TagName;

#else
    string version = App.Version;
#endif

			SetStatus(Strings.Bootstrapper_Status_UpgradingVoidstrap);

			try
			{
#if DEBUG_UPDATER
        string downloadLocation = Path.Combine(Paths.TempUpdates, "Voidstrap.exe");

        Directory.CreateDirectory(Paths.TempUpdates);

        File.Copy(Paths.Process, downloadLocation, true);
#else
                var asset = releaseInfo.Assets?.FirstOrDefault();
                if (asset is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "No assets found in the release information.");
                    return false;
                }

                string downloadLocation = Path.Combine(Paths.TempUpdates, asset.Name);

                Directory.CreateDirectory(Paths.TempUpdates);

                App.Logger.WriteLine(LOG_IDENT, $"Downloading {releaseInfo.TagName}...");

                if (!File.Exists(downloadLocation))
                {
                    var response = await App.HttpClient.GetAsync(asset.BrowserDownloadUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to download update: {response.StatusCode}");
                        return false;
                    }

                    await using var fileStream = new FileStream(downloadLocation, FileMode.Create, FileAccess.Write);
                    await response.Content.CopyToAsync(fileStream);
                }
#endif

                App.Logger.WriteLine(LOG_IDENT, $"Starting {version}...");

                ProcessStartInfo startInfo = new()
                {
                    FileName = downloadLocation,
                };

                startInfo.ArgumentList.Add("-upgrade");

                foreach (string arg in App.LaunchSettings.Args)
                    startInfo.ArgumentList.Add(arg);

                if (_launchMode == LaunchMode.Player && !startInfo.ArgumentList.Contains("-player"))
                    startInfo.ArgumentList.Add("-player");
                else if (_launchMode == LaunchMode.Studio && !startInfo.ArgumentList.Contains("-studio"))
                    startInfo.ArgumentList.Add("-studio");

                App.Settings.Save();

                new InterProcessLock("AutoUpdater");

                Process.Start(startInfo);

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the auto-updater");
                App.Logger.WriteException(LOG_IDENT, ex);

                Frontend.ShowMessageBox(
                    string.Format(Strings.Bootstrapper_AutoUpdateFailed, version),
                    MessageBoxImage.Information
                );

                Utilities.ShellExecute(App.ProjectDownloadLink);
            }

            return false;
        }
        #endregion

        #region Roblox Install
        private void MigrateCompatibilityFlags()
        {
            const string LOG_IDENT = "Bootstrapper::MigrateCompatibilityFlags";

            string oldClientLocation = Path.Combine(Paths.Versions, AppData.State.VersionGuid, AppData.ExecutableName);
            string newClientLocation = Path.Combine(_latestVersionDirectory, AppData.ExecutableName);

            // move old compatibility flags for the old location
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



        private async Task UpgradeRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::UpgradeRoblox";

            bool cancelUpgrade = !App.Settings.Prop.UpdateRoblox;

            if (cancelUpgrade)
            {
                SetStatus(Strings.Bootstrapper_Status_CancelUpgrade);
                App.Logger.WriteLine(LOG_IDENT, "Upgrading disabled, cancelling the upgrade.");
                Thread.Sleep(250);

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

            var cachedPackageHashes = Directory.GetFiles(Paths.Downloads)
                                               .Select(x => Path.GetFileName(x))
                                               .ToList();

            _isInstalling = true;

            if (!IsStudioLaunch)
            {
                await Task.Run(() => KillRobloxPlayers());
            }
            if (Directory.Exists(_latestVersionDirectory))
            {
                try
                {
                    Directory.Delete(_latestVersionDirectory, recursive: true);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to delete the latest version directory");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            Directory.CreateDirectory(_latestVersionDirectory);
            int totalPackedSize = _versionPackageManifest.Sum(p => p.PackedSize);
            int totalUnpackedSize = _versionPackageManifest.Sum(p => p.Size);
            int totalSizeRequired = totalPackedSize + totalUnpackedSize;

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

                _progressIncrement = (double)ProgressBarMaximum / totalPackedSize;

                _taskbarProgressMaximum = Dialog is WinFormsDialogBase
                    ? TaskbarProgressMaximumWinForms
                    : TaskbarProgressMaximumWpf;

                _taskbarProgressIncrement = _taskbarProgressMaximum / totalPackedSize;
            }

            var throttler = new SemaphoreSlim(8);

            var downloadTasks = _versionPackageManifest.Select(async package =>
            {
                await throttler.WaitAsync();
                try
                {
                    await DownloadPackage(package);
                }
                finally
                {
                    throttler.Release();
                }
            }).ToList();

            await Task.WhenAll(downloadTasks);

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            var extractionTasks = _versionPackageManifest.Select(async package =>
            {
                await throttler.WaitAsync();
                try
                {
                    await Task.Run(() => ExtractPackage(package), _cancelTokenSource.Token);
                }
                finally
                {
                    throttler.Release();
                }
            }).ToList();

            await Task.WhenAll(extractionTasks);

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            if (Dialog is not null)
            {
                Dialog.ProgressStyle = ProgressBarStyle.Marquee;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
                SetStatus(Strings.Bootstrapper_Status_Configuring);
            }

            App.Logger.WriteLine(LOG_IDENT, "Writing AppSettings.xml...");
            await File.WriteAllTextAsync(Path.Combine(_latestVersionDirectory, "AppSettings.xml"), AppSettings);

            if (_cancelTokenSource.IsCancellationRequested)
                return;

        MigrateCompatibilityFlags();

            AppData.State.VersionGuid = _latestVersionGuid;

            AppData.State.PackageHashes.Clear();

            foreach (var package in _versionPackageManifest)
                AppData.State.PackageHashes.Add(package.Name, package.Signature);

            CleanupVersionsFolder();

            var allPackageHashes = new List<string>();

            allPackageHashes.AddRange(App.State.Prop.Player.PackageHashes.Values);
            allPackageHashes.AddRange(App.State.Prop.Studio.PackageHashes.Values);

            foreach (string hash in cachedPackageHashes)
            {
                if (!allPackageHashes.Contains(hash))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Deleting unused package {hash}");

                    try
                    {
                        File.Delete(Path.Combine(Paths.Downloads, hash));
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {hash}!");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "Registering approximate program size...");

            int distributionSize = _versionPackageManifest.Sum(x => x.Size + x.PackedSize) / 1024;

            AppData.State.Size = distributionSize;

            int totalSize = App.State.Prop.Player.Size + App.State.Prop.Studio.Size;

            using (var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey))
            {
                uninstallKey.SetValueSafe("EstimatedSize", totalSize);
            }

            App.Logger.WriteLine(LOG_IDENT, $"Registered as {totalSize} KB");

            App.State.Save();

            _isInstalling = false;
        }

        private async Task ApplyModifications()
        {
            const string LOG_IDENT = "Bootstrapper::ApplyModifications";

            SetStatus(Strings.Bootstrapper_Status_ApplyingModifications);

            // handle file mods
            App.Logger.WriteLine(LOG_IDENT, "Checking file mods...");

            // manifest has been moved to State.json
            File.Delete(Path.Combine(Paths.Base, "ModManifest.txt"));

            List<string> modFolderFiles = new();

            Directory.CreateDirectory(Paths.Mods);

            // check custom font mod
            // instead of replacing the fonts themselves, we'll just alter the font family manifests

            string modFontFamiliesFolder = Path.Combine(Paths.Mods, "content\\fonts\\families");

            if (File.Exists(Paths.CustomFont))
            {
                App.Logger.WriteLine(LOG_IDENT, "Begin font check");

                Directory.CreateDirectory(modFontFamiliesFolder);

                const string path = "rbxasset://fonts/CustomFont.ttf";

                // lets make sure the content/fonts/families path exists in the version directory
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

                // get relative directory path
                string relativeFile = file.Substring(Paths.Mods.Length + 1);

                // v1.7.0 - README has been moved to the preferences menu now
                if (relativeFile == "README.txt")
                {
                    File.Delete(file);
                    continue;
                }

                if (!App.Settings.Prop.UseFastFlagManager && String.Equals(relativeFile, "ClientSettings\\ClientAppSettings.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (relativeFile.EndsWith(".lock"))
                    continue;

                modFolderFiles.Add(relativeFile);

                string fileModFolder = Path.Combine(Paths.Mods, relativeFile);
                string fileVersionFolder = Path.Combine(_latestVersionDirectory, relativeFile);

                if (File.Exists(fileVersionFolder) && MD5Hash.FromFile(fileModFolder) == MD5Hash.FromFile(fileVersionFolder))
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

            // the manifest is primarily here to keep track of what files have been
            // deleted from the modifications folder, so that we know when to restore the original files from the downloaded packages
            // now check for files that have been deleted from the mod folder according to the manifest

            var fileRestoreMap = new Dictionary<string, List<string>>();

            foreach (string fileLocation in App.State.Prop.ModManifest)
            {
                if (modFolderFiles.Contains(fileLocation))
                    continue;

                var packageMapEntry = AppData.PackageDirectoryMap.SingleOrDefault(x => !String.IsNullOrEmpty(x.Value) && fileLocation.StartsWith(x.Value));
                string packageName = packageMapEntry.Key;

                // package doesn't exist, likely mistakenly placed file
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
                bool isEuroTrucks = File.Exists(Path.Combine(_latestVersionDirectory, "eurotrucks2.exe")) ? true : false;

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

            App.Logger.WriteLine(LOG_IDENT, $"Finished checking file mods");
        }

        private async Task DownloadPackage(Package package)
        {
            string LOG_IDENT = $"Bootstrapper::DownloadPackage.{package.Name}";

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            Directory.CreateDirectory(Paths.Downloads);

            string packageUrl = Deployment.GetLocation($"/{_latestVersionGuid}-{package.Name}");
            string robloxPackageLocation = Path.Combine(Paths.LocalAppData, "Roblox", "Downloads", package.Signature);

            if (File.Exists(package.DownloadPath))
            {
                var file = new FileInfo(package.DownloadPath);

                string calculatedMD5 = MD5Hash.FromFile(package.DownloadPath);

                if (calculatedMD5 != package.Signature)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package is corrupted ({calculatedMD5} != {package.Signature})! Deleting and re-downloading...");
                    file.Delete();
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package is already downloaded, skipping...");

                    _totalDownloadedBytes += package.PackedSize;
                    UpdateProgressBar();

                    return;
                }
            }
            else if (File.Exists(robloxPackageLocation))
            {
                // let's cheat! if the stock bootstrapper already previously downloaded the file,
                // then we can just copy the one from there

                App.Logger.WriteLine(LOG_IDENT, $"Found existing copy at '{robloxPackageLocation}'! Copying to Downloads folder...");
                File.Copy(robloxPackageLocation, package.DownloadPath);

                _totalDownloadedBytes += package.PackedSize;
                UpdateProgressBar();

                return;
            }

            if (File.Exists(package.DownloadPath))
                return;

            const int maxTries = 5;

            App.Logger.WriteLine(LOG_IDENT, "Downloading...");

            var buffer = new byte[4096];

            for (int i = 1; i <= maxTries; i++)
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                int totalBytesRead = 0;

                try
                {
                    var response = await App.HttpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead, _cancelTokenSource.Token);
                    await using var stream = await response.Content.ReadAsStreamAsync(_cancelTokenSource.Token);
                    await using var fileStream = new FileStream(package.DownloadPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Delete);

                    while (true)
                    {
                        if (_cancelTokenSource.IsCancellationRequested)
                        {
                            stream.Close();
                            fileStream.Close();
                            return;
                        }

                        int bytesRead = await stream.ReadAsync(buffer, _cancelTokenSource.Token);

                        if (bytesRead == 0)
                            break;

                        totalBytesRead += bytesRead;

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cancelTokenSource.Token);

                        _totalDownloadedBytes += bytesRead;
                        UpdateProgressBar();
                    }

                    string hash = MD5Hash.FromStream(fileStream);

                    if (hash != package.Signature)
                        throw new ChecksumFailedException($"Failed to verify download of {packageUrl}\n\nExpected hash: {package.Signature}\nGot hash: {hash}");

                    App.Logger.WriteLine(LOG_IDENT, $"Finished downloading! ({totalBytesRead} bytes total)");
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"An exception occurred after downloading {totalBytesRead} bytes. ({i}/{maxTries})");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    if (ex.GetType() == typeof(ChecksumFailedException))
                    {
                        Frontend.ShowConnectivityDialog(
                            Strings.Dialog_Connectivity_UnableToDownload,
                            String.Format(Strings.Dialog_Connectivity_UnableToDownloadReason, "[https://github.com/Bloxstraplabs/Bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox](https://github.com/Bloxstraplabs/Bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox)"),
                            MessageBoxImage.Error,
                            ex
                        );

                        App.Terminate(ErrorCode.ERROR_CANCELLED);
                    }
                    else if (i >= maxTries)
                        throw;

                    if (File.Exists(package.DownloadPath))
                        File.Delete(package.DownloadPath);

                    _totalDownloadedBytes -= totalBytesRead;
                    UpdateProgressBar();

                    // attempt download over HTTP
                    // this isn't actually that unsafe - signatures were fetched earlier over HTTPS
                    // so we've already established that our signatures are legit, and that there's very likely no MITM anyway
                    if (ex.GetType() == typeof(IOException) && !packageUrl.StartsWith("http://"))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Retrying download over HTTP...");
                        packageUrl = packageUrl.Replace("https://", "http://");
                    }
                }
            }
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

            // for sharpziplib, each file in the filter needs to be a regex
            if (files is not null)
            {
                var regexList = new List<string>();

                foreach (string file in files)
                    regexList.Add("^" + file.Replace("\\", "\\\\") + "$");

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