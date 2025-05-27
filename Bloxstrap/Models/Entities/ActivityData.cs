using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        /// <summary>
        /// If the current activity stems from an in-universe teleport, then this will be
        /// set to the activity that corresponds to the initial game join
        /// </summary>
        public ActivityData? RootActivity;

        public long UniverseId
        {
            get => _universeId;
            set
            {
                _universeId = value;
                UniverseDetails.LoadFromCache(value);
            }
        }

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

        public long PlaceId { get; set; } = 0;
        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// This will be empty unless the server joined is a private server
        /// </summary>
        public string AccessCode { get; set; } = string.Empty;

        public long UserId { get; set; } = 0;
        public string MachineAddress { get; set; } = string.Empty;

        public bool MachineAddressValid =>
            !string.IsNullOrEmpty(MachineAddress) && !MachineAddress.StartsWith("10.");

        public bool IsTeleport { get; set; } = false;
        public ServerType ServerType { get; set; } = ServerType.Public;
        public DateTime TimeJoined { get; set; }
        public DateTime? TimeLeft { get; set; }

        /// <summary>
        /// This is intended only for other people to use, i.e. context menu invite link, rich presence joining
        /// </summary>
        public string RPCLaunchData { get; set; } = string.Empty;

        public UniverseDetails? UniverseDetails { get; set; }

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

        public Dictionary<int, UserLog> PlayerLogs { get; internal set; } = new();
        public Dictionary<int, UserMessage> MessageLogs { get; internal set; } = new();

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

                if (string.IsNullOrEmpty(ipInfo.City))
                    throw new InvalidHTTPResponseException("Reported city was blank");

                location = ipInfo.City == ipInfo.Region
                    ? $"{ipInfo.Region}, {ipInfo.Country}"
                    : $"{ipInfo.City}, {ipInfo.Region}, {ipInfo.Country}";

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

        public override string ToString() => $"{PlaceId}/{JobId}";

        private void RejoinServer()
        {
            string playerPath = new RobloxPlayerData().ExecutablePath;
            Process.Start(playerPath, GetInviteDeeplink(false));
        }
    }
}
