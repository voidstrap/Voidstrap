using Voidstrap.AppData;
using System.ComponentModel;
using System.Security.AccessControl;
using System.Windows;
using Voidstrap;
using Microsoft.VisualBasic.Devices;

namespace Voidstrap
{
    static class Utilities
    {
        public static void ShellExecute(string website)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = website,
                    UseShellExecute = true
                });
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != (int)ErrorCode.CO_E_APPNOTFOUND)
                    throw;

                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32,OpenAs_RunDLL {website}"
                });
            }
        }

        public static Version GetVersionFromString(string version)
        {
            if (version.StartsWith('v'))
                version = version[1..];

            int idx = version.IndexOf('+'); // commit info
            if (idx != -1)
                version = version[..idx];

            return new Version(version);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="versionStr1"></param>
        /// <param name="versionStr2"></param>
        /// <returns>
        /// Result of System.Version.CompareTo <br />
        /// -1: version1 &lt; version2 <br />
        ///  0: version1 == version2 <br />
        ///  1: version1 &gt; version2
        /// </returns>
        public static VersionComparison CompareVersions(string versionStr1, string versionStr2)
        {
            try
            {
                var version1 = GetVersionFromString(versionStr1);
                var version2 = GetVersionFromString(versionStr2);

                return (VersionComparison)version1.CompareTo(version2);
            }
            catch (Exception)
            {
                // temporary diagnostic log for the issue described here:
                // https://github.com/Bloxstraplabs/Bloxstrap/issues/3193
                // the problem is that this happens only on upgrade, so my only hope of catching this is bug reports following the next release

                App.Logger.WriteLine("Utilities::CompareVersions", "An exception occurred when comparing versions");
                App.Logger.WriteLine("Utilities::CompareVersions", $"versionStr1={versionStr1} versionStr2={versionStr2}");

                throw;
            }
        }

        /// <summary>
        /// Parses the input version string and prints if fails
        /// </summary>
        public static Version? ParseVersionSafe(string versionStr)
        {
            const string LOG_IDENT = "Utilities::ParseVersionSafe";

            if (!Version.TryParse(versionStr, out Version? version))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to convert {versionStr} to a valid Version type.");
                return version;
            }

            return version;
        }

        public static string GetRobloxVersion(bool studio)
        {
            IAppData data = studio ? new RobloxStudioData() : new RobloxPlayerData();

            string playerLocation = data.ExecutablePath;

            if (!File.Exists(playerLocation))
                return "";

            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(playerLocation);

            if (versionInfo.ProductVersion is null)
                return "";

            return versionInfo.ProductVersion.Replace(", ", ".");
        }

        public static Process[] GetProcessesSafe()
        {
            const string LOG_IDENT = "Utilities::GetProcessesSafe";

            try
            {
                return Process.GetProcesses();
            }
            catch (ArithmeticException ex) // thanks microsoft
            {
                App.Logger.WriteLine(LOG_IDENT, $"Unable to fetch processes!");
                App.Logger.WriteException(LOG_IDENT, ex);
                return Array.Empty<Process>(); // can we retry?
            }
        }

        public static bool DoesMutexExist(string name)
        {
            try
            {
                Mutex.OpenExisting(name).Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void KillBackgroundUpdater()
        {
            using EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.AutoReset, "Voidstrap-BackgroundUpdaterKillEvent");
            handle.Set();
        }

        public static void RemoveTeleportFix()
        {
            const string LOG_IDENT = "Utilities::RemoveTeleportFix";

            string user = Environment.UserDomainName + "\\" + Environment.UserName;

            try
            {
                FileInfo fileInfo = new FileInfo(App.RobloxCookiesFilePath);
                FileSecurity fileSecurity = fileInfo.GetAccessControl();

                fileSecurity.RemoveAccessRule(new FileSystemAccessRule(user, FileSystemRights.Read, AccessControlType.Deny));
                fileSecurity.RemoveAccessRule(new FileSystemAccessRule(user, FileSystemRights.Write, AccessControlType.Allow));

                fileInfo.SetAccessControl(fileSecurity);

                App.Logger.WriteLine(LOG_IDENT, "Successfully removed teleport fix.");
            }
            catch (Exception ex)
            {
                Frontend.ShowExceptionDialog(ex);
            }
        }

        public static void ApplyTeleportFix()
        {
            const string LOG_IDENT = "Utilities::ApplyTeleportFix";

            string user = Environment.UserDomainName + "\\" + Environment.UserName;

            if (File.Exists(App.RobloxCookiesFilePath))
            {
                if (App.Settings.Prop.FixTeleports)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Attempting to apply teleport fix...");

                    try
                    {
                        FileInfo fileInfo = new FileInfo(App.RobloxCookiesFilePath);
                        FileSecurity fileSecurity = fileInfo.GetAccessControl();

                        fileSecurity.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.Read, AccessControlType.Deny));
                        fileSecurity.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.Write, AccessControlType.Allow));

                        fileInfo.SetAccessControl(fileSecurity);

                        App.Logger.WriteLine(LOG_IDENT, "Successfully made RobloxCookies.dat write-only.");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to make RobloxCookies.dat write-only.");
                        App.Logger.WriteException(LOG_IDENT, ex);
                        Frontend.ShowExceptionDialog(ex);
                    }
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, "Removing teleport fix...");
                    RemoveTeleportFix();
                }
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to find RobloxCookies.dat");
                Frontend.ShowMessageBox($"Failed to find RobloxCookies.dat | Path: {App.RobloxCookiesFilePath}", MessageBoxImage.Error);
            }
        }
    }
}
