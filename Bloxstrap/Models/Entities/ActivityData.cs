using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Voidstrap.AppData;
using Voidstrap.Models.APIs;

namespace Voidstrap.Models.Entities
{
    public class ActivityData
    {
        private long _universeId = 0;
        public ActivityData? RootActivity;

        public long UniverseId
        {
            get => _universeId;
            set
            {
                _universeId = value;
                UniverseDetails = UniverseDetails.LoadFromCache(value);
            }
        }

        #region Display Properties
        public string DisplayTimeJoined { get; private set; } = "Unknown";
        public string DisplayTimeLeft { get; private set; } = "Unknown";
        public string ServerStatus { get; private set; } = "Offline";

        public void ComputeDisplayTimes()
        {
            DisplayTimeJoined = TimeJoined != default
                ? TimeJoined.ToString("yyyy-MM-dd HH:mm:ss")
                : "Unknown";

            bool online = !TimeLeft.HasValue || (DateTime.Now - TimeLeft.Value).TotalHours < 24;
            DisplayTimeLeft = TimeLeft.HasValue
                ? TimeLeft.Value.ToString("yyyy-MM-dd HH:mm:ss")
                : "Still Online";
            ServerStatus = online ? "Online" : "Offline";
        }
        #endregion

        #region Existing Classes
        public class UserLog
        {
            public string UserId { get; set; } = "Unknown";
            public string Username { get; set; } = "Unknown";
            public string Type { get; set; } = "Unknown";
            public DateTime Time { get; set; } = DateTime.Now;
        }

        public class UserMessage
        {
            public string Message { get; set; } = "Unknown";
            public DateTime Time { get; set; } = DateTime.Now;
        }
        #endregion

        #region Core Properties
        public long PlaceId { get; set; } = 0;
        public string JobId { get; set; } = string.Empty;
        public string AccessCode { get; set; } = string.Empty;
        public long UserId { get; set; } = 0;
        public string MachineAddress { get; set; } = string.Empty;
        public bool MachineAddressValid =>
            !string.IsNullOrEmpty(MachineAddress) && !MachineAddress.StartsWith("10.");
        public bool IsTeleport { get; set; } = false;
        public ServerType ServerType { get; set; } = ServerType.Public;
        public DateTime TimeJoined { get; set; }
        public DateTime? TimeLeft { get; set; }
        public string RPCLaunchData { get; set; } = string.Empty;
        public UniverseDetails? UniverseDetails { get; set; }
        public Dictionary<int, UserLog> PlayerLogs { get; internal set; } = new();
        public Dictionary<int, UserMessage> MessageLogs { get; internal set; } = new();
        #endregion

        #region Derived Properties & Commands
        public string GameHistoryDescription
        {
            get
            {
                string desc = string.Format(
                    "{0} • {1} {2} {3}",
                    UniverseDetails?.Data.Creator.Name ?? "Unknown",
                    TimeJoined.ToString("t"),
                    Locale.CurrentCulture.Name.StartsWith("ja") ? '~' : '-',
                    TimeLeft?.ToString("t") ?? "?"
                );

                if (ServerType != ServerType.Public)
                    desc += " • " + ServerType.ToTranslatedString();

                return desc;
            }
        }

        public ICommand RejoinServerCommand => new RelayCommand(RejoinServer);
        #endregion

        #region Server Methods
        private SemaphoreSlim serverQuerySemaphore = new(1, 1);

        public string GetInviteDeeplink(bool launchData = true)
        {
            string deeplink = $"https://www.roblox.com/games/start?placeId={PlaceId}";
            if (ServerType == ServerType.Private)
                deeplink += "&accessCode=" + AccessCode;
            else
                deeplink += "&gameInstanceId=" + JobId;

            if (launchData && !string.IsNullOrEmpty(RPCLaunchData))
                deeplink += "&launchData=" + HttpUtility.UrlEncode(RPCLaunchData);

            return deeplink;
        }

        public async Task<string?> QueryServerLocation()
        {
            const string LOG_IDENT = "ActivityData::QueryServerLocation";

            if (!MachineAddressValid)
                throw new InvalidOperationException($"Machine address is invalid ({MachineAddress})");

            await serverQuerySemaphore.WaitAsync();
            if (GlobalCache.ServerLocation.TryGetValue(MachineAddress, out string? cachedLocation))
            {
                serverQuerySemaphore.Release();
                return cachedLocation;
            }

            string? location = null;
            try
            {
                var ipInfo = await Http.GetJson<IPInfoResponse>($"https://ipinfo.io/{MachineAddress}/json");
                if (string.IsNullOrEmpty(ipInfo.Country))
                    throw new InvalidHTTPResponseException("Reported country was blank");

                string flag = CountryCodeToFlagEmoji(ipInfo.Country);

                location = !string.IsNullOrEmpty(ipInfo.City)
                    ? (ipInfo.City == ipInfo.Region ? $"{ipInfo.Region}, {flag}" : $"{ipInfo.City}, {ipInfo.Region}, {flag}")
                    : $"{ipInfo.Country} {flag}";

                GlobalCache.ServerLocation[MachineAddress] = location;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to get server location for {MachineAddress}");
                App.Logger.WriteException(LOG_IDENT, ex);
                GlobalCache.ServerLocation[MachineAddress] = location;

                Frontend.ShowConnectivityDialog(
                    string.Format(Strings.Dialog_Connectivity_UnableToConnect, "ipinfo.io"),
                    Strings.ActivityWatcher_LocationQueryFailed,
                    MessageBoxImage.Warning,
                    ex
                );
            }
            finally
            {
                serverQuerySemaphore.Release();
            }

            return location;
        }

        private static string CountryCodeToFlagEmoji(string countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
                return string.Empty;

            int offset = 0x1F1E6 - 'A';
            return string.Concat(countryCode.ToUpper().Select(c => char.ConvertFromUtf32(c + offset)));
        }

        public override string ToString() => $"{PlaceId}/{JobId}";

        private void RejoinServer()
        {
            string playerPath = new RobloxPlayerData().ExecutablePath;
            Process.Start(playerPath, GetInviteDeeplink(false));
        }
        #endregion
    }
}
