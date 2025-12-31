using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Voidstrap.Integrations;
using Voidstrap.UI.Elements.Dialogs;
using Voidstrap.UI.ViewModels.Settings;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Voidstrap
{
    public static class LaunchHandler
    {
        public static void ProcessNextAction(NextAction action, bool isUnfinishedInstall = false)
        {
            const string LOG_IDENT = "LaunchHandler::ProcessNextAction";

            switch (action)
            {
                case NextAction.LaunchSettings:
                    App.Logger.WriteLine(LOG_IDENT, "Opening settings");
                    LaunchSettings();
                    break;

                case NextAction.LaunchRoblox:
                    App.Logger.WriteLine(LOG_IDENT, "Opening Roblox");
                    LaunchRoblox(LaunchMode.Player);
                    break;

                case NextAction.LaunchRobloxStudio:
                    App.Logger.WriteLine(LOG_IDENT, "Opening Roblox Studio");
                    LaunchRoblox(LaunchMode.Studio);
                    break;

                default:
                    App.Logger.WriteLine(LOG_IDENT, "Closing");
                    App.Terminate(isUnfinishedInstall ? ErrorCode.ERROR_INSTALL_USEREXIT : ErrorCode.ERROR_SUCCESS);
                    break;
            }
        }

        public static void ProcessLaunchArgs()
        {
            const string LOG_IDENT = "LaunchHandler::ProcessLaunchArgs";
            if (App.LaunchSettings.UninstallFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening uninstaller");
                LaunchUninstaller();
            }
            else if (App.LaunchSettings.MenuFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening settings");
                LaunchSettings();
            }
            else if (App.LaunchSettings.WatcherFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening watcher");
                LaunchWatcher();
            }
            else if (App.LaunchSettings.RobloxLaunchMode != LaunchMode.None)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Opening bootstrapper ({App.LaunchSettings.RobloxLaunchMode})");
                LaunchRoblox(App.LaunchSettings.RobloxLaunchMode);
            }
            else if (App.LaunchSettings.BloxshadeFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening Bloxshade");
                LaunchBloxshadeConfig();
            }
            else if (!App.LaunchSettings.QuietFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening menu");
                LaunchMenu();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Closing - quiet flag active");
                App.Terminate();
            }
        }

        public static void LaunchInstaller()
        {
            var interlock = new InterProcessLock("Installer");

            try
            {
                if (!interlock.IsAcquired)
                {
                    Frontend.ShowMessageBox(Strings.Dialog_AlreadyRunning_Installer, MessageBoxImage.Stop);
                    App.Terminate();
                    return;
                }

                if (App.LaunchSettings.UninstallFlag.Active)
                {
                    Frontend.ShowMessageBox(Strings.Bootstrapper_FirstRunUninstall, MessageBoxImage.Error);
                    App.Terminate(ErrorCode.ERROR_INVALID_FUNCTION);
                    return;
                }

                if (App.LaunchSettings.QuietFlag.Active)
                {
                    var installer = new Installer();

                    if (!installer.CheckInstallLocation())
                        App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);

                    installer.DoInstall();
                    interlock.Dispose();

                    ProcessLaunchArgs();
                }
                else
                {
#if QA_BUILD
                    Frontend.ShowMessageBox(
                        "You are about to install a QA build of Voidstrap. The red window border indicates that this is a QA build.\n\n" +
                        "QA builds are handled completely separately of your standard installation, like a virtual environment.",
                        MessageBoxImage.Information);
#endif

                    new LanguageSelectorDialog().ShowDialog();

                    var installer = new UI.Elements.Installer.MainWindow();
                    installer.ShowDialog();
                    interlock.Dispose();

                    ProcessNextAction(installer.CloseAction, !installer.Finished);
                }
            }
            finally
            {
                interlock.Dispose();
            }
        }

        public static void LaunchUninstaller()
        {
            using var interlock = new InterProcessLock("Uninstaller");

            if (!interlock.IsAcquired)
            {
                Frontend.ShowMessageBox(Strings.Dialog_AlreadyRunning_Uninstaller, MessageBoxImage.Stop);
                App.Terminate();
                return;
            }

            bool confirmed;
            bool keepData = true;

            if (App.LaunchSettings.QuietFlag.Active)
            {
                confirmed = true;
            }
            else
            {
                var dialog = new UninstallerDialog();
                dialog.ShowDialog();

                confirmed = dialog.Confirmed;
                keepData = dialog.KeepData;
            }

            if (!confirmed)
            {
                App.Terminate();
                return;
            }

            Installer.DoUninstall(keepData);

            Frontend.ShowMessageBox(Strings.Bootstrapper_SuccessfullyUninstalled, MessageBoxImage.Information);
            App.Terminate();
        }

        public static void LaunchSettings()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchSettings";

            using var interlock = new InterProcessLock("Settings");

            if (interlock.IsAcquired)
            {
                bool showAlreadyRunningWarning = Process.GetProcessesByName(App.ProjectName).Length > 1;

                var window = new UI.Elements.Settings.MainWindow(showAlreadyRunningWarning);
                window.ShowDialog();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Found an already existing menu window");

                var process = Utilities.GetProcessesSafe()
                    .FirstOrDefault(x => x.MainWindowTitle == Strings.Menu_Title);

                if (process is not null && process.MainWindowHandle != IntPtr.Zero)
                {
                    PInvoke.SetForegroundWindow(new HWND(process.MainWindowHandle));
                }

                App.Terminate();
            }
        }

        public static void LaunchMenu()
        {
            var dialog = new LaunchMenuDialog();
            dialog.ShowDialog();

            ProcessNextAction(dialog.CloseAction);
        }

        public static void LaunchRoblox(LaunchMode launchMode)
        {
            const string LOG_IDENT = "LaunchHandler::LaunchRoblox";
            const string MutexName = "ROBLOX_singletonMutex";

            if (launchMode == LaunchMode.None)
                throw new InvalidOperationException("No Roblox launch mode set");

            if (!File.Exists(Path.Combine(Paths.System, "mfplat.dll")))
            {
                Frontend.ShowMessageBox(Strings.Bootstrapper_WMFNotFound, MessageBoxImage.Error);

                if (!App.LaunchSettings.QuietFlag.Active)
                {
                    Utilities.ShellExecute(
                        "https://support.microsoft.com/en-us/topic/media-feature-pack-list-for-windows-n-editions-c1c6fffa-d052-8338-7a79-a4bb980a700a");
                }

                App.Terminate(ErrorCode.ERROR_FILE_NOT_FOUND);
                return;
            }

            if (App.Settings.Prop.ConfirmLaunches
                && Mutex.TryOpenExisting(MutexName, out _)
                && !App.Settings.Prop.MultiInstanceLaunching
                && !(App.Settings.Prop.IsGameEnabled && !string.IsNullOrWhiteSpace(App.Settings.Prop.LaunchGameID)))
            {
                var result = Frontend.ShowMessageBox(
                    Strings.Bootstrapper_ConfirmLaunch,
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                {
                    App.Terminate();
                    return;
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "Initializing bootstrapper");
            App.Bootstrapper = new Bootstrapper(launchMode);
            
            IBootstrapperDialog? dialog = null;
            if (!App.LaunchSettings.QuietFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Initializing bootstrapper dialog");
                dialog = App.Settings.Prop.BootstrapperStyle.GetNew();
                App.Bootstrapper.Dialog = dialog;
                dialog.Bootstrapper = App.Bootstrapper;
            }

            Mutex? mutex = null;
            if (App.Settings.Prop.MultiInstanceLaunching)
            {
                try
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Creating {MutexName}");
                    mutex = new Mutex(initiallyOwned: true, name: MutexName);
                    App.Logger.WriteLine(LOG_IDENT, $"Created {MutexName}");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to create {MutexName}: {ex}");
                    mutex = null;
                }
            }

            if (App.Settings.Prop.ExclusiveFullscreen)
                Task.Run(() => RobloxFullscreen.WaitAndForceBorderlessFullscreen()); // renamed goooffball for better idfk why did I rename this again... ANT I NEED YOUR HELP BRO


            if (App.Settings.Prop.EnableLuaScripting)
                Task.Run(() => LuaScriptManager.WaitForRobloxAndExecuteScript());

            Task.Run(App.Bootstrapper.Run).ContinueWith(t =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Bootstrapper task has finished");

                try
                {
                    if (t.IsFaulted)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the bootstrapper");

                        if (t.Exception is not null)
                            App.FinalizeExceptionHandling(t.Exception);
                    }

                    if (mutex != null)
                    {
                        string processName = App.RobloxPlayerAppName.Split('.')[0];
                        App.Logger.WriteLine(LOG_IDENT, $"Resolved Roblox name {processName}.exe, running in background.");

                        while (Process.GetProcessesByName(processName).Any())
                            Thread.Sleep(5000);

                        App.Logger.WriteLine(LOG_IDENT, "Every Roblox instance is closed, terminating the process");
                    }
                }
                finally
                {
                    if (mutex != null)
                    {
                        try { mutex.ReleaseMutex(); } catch {}
                        mutex.Dispose();
                    }

                    App.Terminate();
                }
            });

            dialog?.ShowBootstrapper();
            App.Logger.WriteLine(LOG_IDENT, "Exiting");
        }

        public static void LaunchWatcher()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchWatcher";

            var watcher = new Watcher();

            Task.Run(watcher.Run).ContinueWith(t =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher task has finished");

                watcher.Dispose();

                if (t.IsFaulted)
                {
                    App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the watcher");

                    if (t.Exception is not null)
                        App.FinalizeExceptionHandling(t.Exception);
                }

                if (App.Settings.Prop.CleanerOptions != CleanerOptions.Never)
                    Cleaner.DoCleaning();

                App.Terminate();
            });
        }

        public static void LaunchBloxshadeConfig()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchBloxshade";

            App.Logger.WriteLine(LOG_IDENT, "Showing unsupported warning");

            new BloxshadeDialog().ShowDialog();
            App.SoftTerminate();
        }
    }
}
