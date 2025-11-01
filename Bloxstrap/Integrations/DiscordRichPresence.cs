using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DiscordRPC;
using Voidstrap.Models.RobloxApi;
using Voidstrap.Models.VoidstrapRPC;

namespace Voidstrap.Integrations
{
    public class DiscordRichPresence : IDisposable
    {
        private readonly DiscordRpcClient _rpcClient = new("1005469189907173486");
        private readonly ActivityWatcher _activityWatcher;
        private readonly ConcurrentQueue<Message> _messageQueue = new();
        private readonly SemaphoreSlim _updateLock = new(1, 1);

        private DiscordRPC.RichPresence? _currentPresence;
        private DiscordRPC.RichPresence? _originalPresence;

        private bool _visible = true;
        private long? _previousPlaceId;

        private DateTime _lastPresenceUpdate = DateTime.MinValue;
        private readonly TimeSpan _updateCooldown = TimeSpan.FromSeconds(5);

        public DiscordRichPresence(ActivityWatcher activityWatcher)
        {
            const string LOG_IDENT = "DiscordRichPresence";
            _activityWatcher = activityWatcher;
            _activityWatcher.OnGameJoin += async (_, _) => await SetCurrentGameAsync();
            _activityWatcher.OnGameLeave += async (_, _) => await SetCurrentGameAsync();
            _activityWatcher.OnRPCMessage += (_, message) => ProcessRPCMessage(message);

            _rpcClient.OnReady += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"Ready: {e.User} ({e.User.ID})");

            _rpcClient.OnPresenceUpdate += (_, _) =>
                App.Logger.WriteLine(LOG_IDENT, "Presence updated");

            _rpcClient.OnError += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"RPC Error: {e.Message}");

            _rpcClient.OnConnectionEstablished += (_, _) =>
                App.Logger.WriteLine(LOG_IDENT, "Connected to Discord RPC");

            _rpcClient.OnClose += (_, e) =>
            {
                App.Logger.WriteLine(LOG_IDENT, $"Connection closed: {e.Reason} ({e.Code})");
                Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    try
                    {
                        _rpcClient.Initialize();
                        App.Logger.WriteLine(LOG_IDENT, "Reinitialized Discord RPC after closure.");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to reinitialize RPC: {ex.Message}");
                    }
                });
            };

            _rpcClient.Initialize();
        }

        public void ProcessRPCMessage(Message message, bool implicitUpdate = true)
        {
            if (message.Command != "SetRichPresence" && message.Command != "SetLaunchData") return;

            if (_currentPresence is null || _originalPresence is null)
            {
                _messageQueue.Enqueue(message);
                return;
            }

            if (message.Command == "SetLaunchData")
            {
                _currentPresence.Buttons = GetButtons();
            }
            else if (message.Command == "SetRichPresence")
            {
                if (!TryDeserializePresence(message.Data, out Voidstrap.Models.VoidstrapRPC.RichPresence? presenceData))
                    return;

                _currentPresence.Details = UpdateField(_currentPresence.Details, presenceData.Details, _originalPresence.Details, 128);
                _currentPresence.State = UpdateField(_currentPresence.State, presenceData.State, _originalPresence.State, 128);

                presenceData.TimestampStart = _currentPresence.Timestamps.StartUnixMilliseconds.HasValue
                    ? (ulong?)_currentPresence.Timestamps.StartUnixMilliseconds.Value
                    : null;

                presenceData.TimestampEnd = _currentPresence.Timestamps.EndUnixMilliseconds.HasValue
                    ? (ulong?)_currentPresence.Timestamps.EndUnixMilliseconds.Value
                    : null;

                UpdateAssets(_currentPresence.Assets, _originalPresence.Assets, presenceData.SmallImage, true);
                UpdateAssets(_currentPresence.Assets, _originalPresence.Assets, presenceData.LargeImage, false);
            }

            if (implicitUpdate)
                UpdatePresence();
        }

        private static bool TryDeserializePresence(JsonElement data, out Voidstrap.Models.VoidstrapRPC.RichPresence? presence)
        {
            try
            {
                presence = data.Deserialize<Voidstrap.Models.VoidstrapRPC.RichPresence>();
                return presence != null;
            }
            catch
            {
                presence = null;
                return false;
            }
        }

        private static string? UpdateField(string? current, string? newValue, string? original, int maxLength)
        {
            if (string.IsNullOrEmpty(newValue)) return current;
            if (newValue == "<reset>") return original;
            if (newValue.Length > maxLength) return current;
            return newValue;
        }

        private void UpdateAssets(Assets current, Assets original, RichPresenceImage? data, bool small)
        {
            if (data == null) return;

            if (data.Clear)
            {
                if (small)
                    current.SmallImageKey = "";
                else
                    current.LargeImageKey = "";
                return;
            }

            if (data.Reset)
            {
                if (small)
                {
                    current.SmallImageKey = original.SmallImageKey;
                    current.SmallImageText = original.SmallImageText;
                }
                else
                {
                    current.LargeImageKey = original.LargeImageKey;
                    current.LargeImageText = original.LargeImageText;
                }
                return;
            }

            if (!string.IsNullOrEmpty(data.CustomKey))
            {
                if (small)
                    current.SmallImageKey = data.CustomKey;
                else
                    current.LargeImageKey = data.CustomKey;
                return;
            }

            if (data.AssetId.HasValue)
            {
                var url = $"https://assetdelivery.roblox.com/v1/asset/?id={data.AssetId.Value}";
                if (small)
                    current.SmallImageKey = url;
                else
                    current.LargeImageKey = url;
            }

            if (!string.IsNullOrEmpty(data.HoverText))
            {
                if (small)
                    current.SmallImageText = data.HoverText;
                else
                    current.LargeImageText = data.HoverText;
            }
        }

        public void SetVisibility(bool visible)
        {
            _visible = visible;
            if (_visible)
                UpdatePresence();
            else
                _rpcClient.ClearPresence();
        }

        private async Task SetCurrentGameAsync()
        {
            if (!await _updateLock.WaitAsync(0)) return;
            try
            {
                await SetCurrentGame();
            }
            finally
            {
                _updateLock.Release();
            }
        }

        public async Task<bool> SetCurrentGame()
        {
            const string LOG_IDENT = "DiscordRichPresence";

            if (!_activityWatcher.InGame)
            {
                _currentPresence = _originalPresence = null;
                _messageQueue.Clear();
                _previousPlaceId = null;
                UpdatePresence();
                App.Logger.WriteLine(LOG_IDENT, "Not in game, cleared presence.");
                return true;
            }

            var activity = _activityWatcher.Data;
            var timeStarted = activity.RootActivity?.TimeJoined ?? activity.TimeJoined;
            var placeId = activity.PlaceId;

            bool teleported = _previousPlaceId.HasValue && _previousPlaceId.Value != placeId;
            _previousPlaceId = placeId;

            if (activity.UniverseDetails is null)
            {
                try
                {
                    await UniverseDetails.FetchSingle(activity.UniverseId);
                    activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
                    App.Logger.WriteLine(LOG_IDENT, $"Fetched universe details for {activity.UniverseId}");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to fetch universe details: {ex.Message}");
                    return false;
                }
            }

            var universe = activity.UniverseDetails!;
            var (smallImage, smallText) = await GetSmallImageAsync(activity);

            if (!_activityWatcher.InGame || placeId != _activityWatcher.Data.PlaceId)
            {
                App.Logger.WriteLine(LOG_IDENT, "Player left the game during setup.");
                return false;
            }

            string serverPrivacy = activity.ServerType switch
            {
                ServerType.Private => "Private Server",
                ServerType.Reserved => "Reserved Server",
                _ => "Public Server"
            };

            App.Logger.WriteLine(LOG_IDENT, $"Server type detected: {serverPrivacy}");

            string universeName;
            if (!string.IsNullOrWhiteSpace(App.Settings.Prop.CustomGameName))
            {
                universeName = App.Settings.Prop.CustomGameName!;
                App.Logger.WriteLine(LOG_IDENT, $"Using custom game name: {universeName}");
            }
            else
            {
                universeName = universe.Data.Name.Length < 2
                    ? universe.Data.Name + "\x2800\x2800\x2800"
                    : universe.Data.Name;
            }

            if (teleported)
            {
                universeName = $"Teleported to {universeName}";
                App.Logger.WriteLine(LOG_IDENT, "Player teleported to a new place.");
            }

            string serverLocation = string.Empty;
            if (App.Settings.Prop.ServerLocationGame)
            {
                try
                {
                    serverLocation = await activity.QueryServerLocation();
                }
                catch
                {
                    serverLocation = "Unknown Location";
                }
            }

            var detailsParts = new List<string>();
            if (App.Settings.Prop.GameNameChecked)
                detailsParts.Add(universeName);
            if (App.Settings.Prop.GameStatusChecked)
                detailsParts.Add(serverPrivacy);
            if (App.Settings.Prop.ServerLocationGame)
                detailsParts.Add(serverLocation);

            string details = string.Join(" • ", detailsParts);

            string state = App.Settings.Prop.GameCreatorChecked
                ? $"by {universe.Data.Creator.Name}{(universe.Data.Creator.HasVerifiedBadge ? " ☑️" : "")}"
                : "";
            string largeImageKey = !string.IsNullOrWhiteSpace(App.Settings.Prop.UseCustomIcon)
                ? App.Settings.Prop.UseCustomIcon
                : (App.Settings.Prop.GameIconChecked ? universe.Thumbnail.ImageUrl : "");

            string largeImageText = !string.IsNullOrWhiteSpace(App.Settings.Prop.UseCustomIcon)
                ? ""
                : (App.Settings.Prop.GameIconChecked && App.Settings.Prop.GameNameChecked ? universe.Data.Name : "");

            _currentPresence = new DiscordRPC.RichPresence
            {
                Details = details,
                State = state,
                Timestamps = new Timestamps { Start = timeStarted.ToUniversalTime() },
                Buttons = GetButtons(),
                Assets = new Assets
                {
                    LargeImageKey = largeImageKey,
                    LargeImageText = largeImageText,
                    SmallImageKey = smallImage,
                    SmallImageText = smallText
                }
            };

            _originalPresence = _currentPresence;

            while (_messageQueue.TryDequeue(out var msg))
                ProcessRPCMessage(msg, false);

            UpdatePresence();
            App.Logger.WriteLine(LOG_IDENT, $"Updated presence for {details}");
            return true;
        }

        private async Task<(string key, string text)> GetSmallImageAsync(ActivityData activity)
        {
            if (!App.Settings.Prop.ShowAccountOnRichPresence)
                return ("roblox", "Roblox");

            try
            {
                var user = await UserDetails.Fetch(activity.UserId);
                return (user.Thumbnail.ImageUrl, $"{user.Data.DisplayName} (@{user.Data.Name})");
            }
            catch
            {
                return ("roblox", "Roblox");
            }
        }

        public Button[] GetButtons()
        {
            var data = _activityWatcher.Data;
            var buttons = new List<Button>();

            if (!App.Settings.Prop.HideRPCButtons)
            {
                string? inviteUrl = null;

                if (data.ServerType == ServerType.Public ||
                    (data.ServerType == ServerType.Reserved && !string.IsNullOrEmpty(data.RPCLaunchData)))
                {
                    inviteUrl = data.GetInviteDeeplink();
                }

                if (!string.IsNullOrEmpty(inviteUrl))
                    buttons.Add(new Button { Label = "Join server", Url = inviteUrl });
            }

            buttons.Add(new Button
            {
                Label = "Game Page",
                Url = $"https://www.roblox.com/games/{data.PlaceId}"
            });

            return buttons.ToArray();
        }

        public void UpdatePresence()
        {
            if (_currentPresence is null)
            {
                _rpcClient.ClearPresence();
                return;
            }

            if (!_visible) return;
            if ((DateTime.UtcNow - _lastPresenceUpdate) < _updateCooldown) return;

            _lastPresenceUpdate = DateTime.UtcNow;
            _rpcClient.SetPresence(_currentPresence);
        }

        public void Dispose()
        {
            _rpcClient.ClearPresence();
            _rpcClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
