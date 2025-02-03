using System.DirectoryServices.ActiveDirectory;
using System.Runtime.InteropServices;
using System.Web;
using System.Windows;
using System.Windows.Input;
using Hellstrap.AppData;
using Hellstrap.Models.APIs;
using CommunityToolkit.Mvvm.Input;
using System.Net.Http;

namespace Hellstrap.Models.Entities
{
    public class ActivityData
    {
        private long _universeId = 0;

        public ActivityData? RootActivity { get; set; }

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
            public DateTime Time { get; set; } = DateTime.UtcNow;
        }

        public class UserMessage
        {
            public string Message { get; set; } = "Unknown";
            public DateTime Time { get; set; } = DateTime.UtcNow;
        }

        public long PlaceId { get; set; } = 0;
        public string JobId { get; set; } = string.Empty;
        public string AccessCode { get; set; } = string.Empty;
        public long UserId { get; set; } = 0;
        public string MachineAddress { get; set; } = string.Empty;

        public bool MachineAddressValid => !string.IsNullOrEmpty(MachineAddress) && !MachineAddress.StartsWith("10.");
        public bool IsTeleport { get; set; } = false;
        public ServerType ServerType { get; set; } = ServerType.Public;
        public DateTime TimeJoined { get; set; } = DateTime.UtcNow;
        public DateTime? TimeLeft { get; set; }

        public string RPCLaunchData { get; set; } = string.Empty;
        public UniverseDetails? UniverseDetails { get; set; }

        public string GameHistoryDescription
        {
            get
            {
                var desc = $"{UniverseDetails?.Data.Creator.Name} • {TimeJoined:t} " +
                           (Locale.CurrentCulture.Name.StartsWith("ja") ? '~' : '-') +
                           $"{TimeLeft?.ToString("t") ?? "Ongoing"}";

                if (ServerType != ServerType.Public)
                    desc += $" • {ServerType.ToTranslatedString()}";

                return desc;
            }
        }

        public ICommand RejoinServerCommand { get; }

        public Dictionary<int, UserLog> PlayerLogs { get; internal set; } = new();
        public Dictionary<int, UserMessage> MessageLogs { get; internal set; } = new();

        private SemaphoreSlim serverQuerySemaphore = new(1, 1);

        public ActivityData()
        {
            RejoinServerCommand = new RelayCommand(RejoinServer);
        }

        public string GetInviteDeeplink(bool launchData = true)
        {
            var deeplink = $"https://www.roblox.com/games/start?placeId={PlaceId}";
            deeplink += ServerType == ServerType.Private ? $"&accessCode={AccessCode}" : $"&gameInstanceId={JobId}";

            if (launchData && !string.IsNullOrEmpty(RPCLaunchData))
                deeplink += $"&launchData={HttpUtility.UrlEncode(RPCLaunchData)}";

            return deeplink;
        }

        public async Task<string?> QueryServerLocation()
        {
            const string logIdent = "ActivityData::QueryServerLocation";
            string location = null;

            if (!MachineAddressValid)
                throw new InvalidOperationException($"Machine address is invalid ({MachineAddress})");

            await serverQuerySemaphore.WaitAsync();

            try
            {
                if (GlobalCache.ServerLocation.TryGetValue(MachineAddress, out location))
                    return location;

                var ipInfo = await Http.GetJson<IPInfoResponse>($"https://ipinfo.io/{MachineAddress}/json");

                if (string.IsNullOrEmpty(ipInfo?.City))
                    throw new InvalidHTTPResponseException("Reported city was blank");

                location = ipInfo.City == ipInfo.Region
                    ? $"{ipInfo.Region}, {ipInfo.Country}"
                    : $"{ipInfo.City}, {ipInfo.Region}, {ipInfo.Country}";

                GlobalCache.ServerLocation[MachineAddress] = location;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(logIdent, $"Failed to get server location for {MachineAddress}");
                App.Logger.WriteException(logIdent, ex);

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
            var playerPath = new RobloxPlayerData().ExecutablePath;
            Process.Start(playerPath, GetInviteDeeplink(false));
        }
    }
}
