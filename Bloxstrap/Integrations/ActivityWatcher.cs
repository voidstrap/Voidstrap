﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Voidstrap.Integrations
{
    public class ActivityWatcher : IDisposable
    {
        private const string GameMessageEntry = "[FLog::Output] [VoidstrapRPC]";
        private const string GameJoiningEntry = "[FLog::Output] ! Joining game";
        private const string GameTeleportingEntry = "[FLog::GameJoinUtil] GameJoinUtil::initiateTeleportToPlace";
        private const string GameJoiningPrivateServerEntry = "[FLog::GameJoinUtil] GameJoinUtil::joinGamePostPrivateServer";
        private const string GameJoiningReservedServerEntry = "[FLog::GameJoinUtil] GameJoinUtil::initiateTeleportToReservedServer";
        private const string GameJoiningUniverseEntry = "[FLog::GameJoinLoadTime] Report game_join_loadtime:";
        private const string GameJoiningUDMUXEntry = "[FLog::Network] UDMUX Address = ";
        private const string GameJoinedEntry = "[FLog::Network] serverId:";
        private const string GameDisconnectedEntry = "[FLog::Network] Time to disconnect replication data:";
        private const string GameLeavingEntry = "[FLog::SingleSurfaceApp] leaveUGCGameInternal";
        private const string GamePlayerJoinLeaveEntry = "[ExpChat/mountClientApp (Trace)] - Player ";
        private const string GameMessageLogEntry = "[ExpChat/mountClientApp (Debug)] - Incoming MessageReceived Status: ";

        // Patterns
        private const string GameJoiningEntryPattern = @"! Joining game '([0-9a-f\-]{36})' place ([0-9]+) at ([0-9\.]+)";
        private const string GameJoiningPrivateServerPattern = @"""accessCode"":""([0-9a-f\-]{36})""";
        private const string GameJoiningUniversePattern = @"universeid:([0-9]+).*userid:([0-9]+)";
        private const string GameJoiningUDMUXPattern = @"UDMUX Address = ([0-9\.]+), Port = [0-9]+ \| RCC Server Address = ([0-9\.]+), Port = [0-9]+";
        private const string GameJoinedEntryPattern = @"serverId:\s*([0-9a-f\-]{36})";
        private const string GameMessageEntryPattern = @"\[VoidstrapRPC\] (.*)";
        private const string GamePlayerJoinLeavePattern = @"(added|removed): (.*) (.*[0-9])";
        private const string GameMessageLogPattern = @"Success Text: (.*)";

        private int _logEntriesRead = 0;
        private bool _teleportMarker = false;
        private bool _reservedTeleportMarker = false;

        public event EventHandler<string>? OnLogEntry;
        public event EventHandler? OnGameJoin;
        public event EventHandler? OnGameLeave;
        public event EventHandler? OnLogOpen;
        public event EventHandler? OnAppClose;
        public event EventHandler<ActivityData.UserLog>? OnNewPlayerRequest;
        public event EventHandler<ActivityData.UserMessage>? OnNewMessageRequest;
        public event EventHandler<Message>? OnRPCMessage;

        private DateTime LastRPCRequest;

        public string LogLocation = null!;
        public bool InGame = false;
        public ActivityData Data { get; private set; } = new();
        public List<ActivityData> History = new();
        public Dictionary<int, ActivityData.UserLog> PlayerLogs => Data.PlayerLogs;
        public Dictionary<int, ActivityData.UserMessage> MessageLogs => Data.MessageLogs;
        public bool IsDisposed = false;

        public ActivityWatcher(string? logFile = null)
        {
            if (!string.IsNullOrEmpty(logFile))
                LogLocation = logFile;
        }

        public async void Start()
        {
            const string LOG_IDENT = "ActivityWatcher::Start";

            FileInfo logFileInfo;

            if (string.IsNullOrEmpty(LogLocation))
            {
                string logDirectory = Path.Combine(Paths.LocalAppData, "Roblox\\logs");

                if (!Directory.Exists(logDirectory))
                    return;

                App.Logger.WriteLine(LOG_IDENT, "Opening Roblox log file...");

                while (true)
                {
                    logFileInfo = new DirectoryInfo(logDirectory)
                        .GetFiles()
                        .Where(x => x.Name.Contains("Player", StringComparison.OrdinalIgnoreCase) && x.CreationTime <= DateTime.Now)
                        .OrderByDescending(x => x.CreationTime)
                        .First();

                    if (logFileInfo.CreationTime.AddSeconds(15) > DateTime.Now)
                        break;

                    App.Logger.WriteLine(LOG_IDENT, $"Could not find recent enough log file, waiting... (newest is {logFileInfo.Name})");
                    await Task.Delay(750);
                }

                LogLocation = logFileInfo.FullName;
            }
            else
            {
                logFileInfo = new FileInfo(LogLocation);
            }

            OnLogOpen?.Invoke(this, EventArgs.Empty);

            var logFileStream = logFileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            App.Logger.WriteLine(LOG_IDENT, $"Opened {LogLocation}");

            using var streamReader = new StreamReader(logFileStream);

            while (!IsDisposed)
            {
                string? log = await streamReader.ReadLineAsync();

                if (log is null)
                    await Task.Delay(700);
                else
                    ReadLogEntry(log);
            }
        }

        private void ReadLogEntry(string entry)
        {
            const string LOG_IDENT = "ActivityWatcher::ReadLogEntry";
            OnLogEntry?.Invoke(this, entry);
            _logEntriesRead++;

            if (_logEntriesRead <= 1000 && _logEntriesRead % 50 == 0)
                App.Logger.WriteLine(LOG_IDENT, $"Read {_logEntriesRead} log entries");
            else if (_logEntriesRead % 100 == 0)
                App.Logger.WriteLine(LOG_IDENT, $"Read {_logEntriesRead} log entries");

            if (entry.Contains(GameLeavingEntry))
            {
                App.Logger.WriteLine(LOG_IDENT, "User is back into the desktop app");
                OnAppClose?.Invoke(this, EventArgs.Empty);

                if (Data.PlaceId != 0 && !InGame)
                {
                    App.Logger.WriteLine(LOG_IDENT, "User appears to be leaving from a cancelled/errored join");
                    Data = new();
                }
            }

            // ---- Game joining ----
            if (!InGame && Data.PlaceId == 0)
            {
                if (entry.Contains(GameJoiningPrivateServerEntry))
                {
                    Data.ServerType = ServerType.Private;
                    var match = Regex.Match(entry, GameJoiningPrivateServerPattern);
                    if (match.Groups.Count != 2)
                        return;
                    Data.AccessCode = match.Groups[1].Value;
                }
                else if (entry.Contains(GameJoiningEntry))
                {
                    Match match = Regex.Match(entry, GameJoiningEntryPattern);
                    if (match.Groups.Count != 4)
                        return;

                    InGame = false;
                    Data.PlaceId = long.Parse(match.Groups[2].Value);
                    Data.JobId = match.Groups[1].Value; // ✅ Real Roblox server UUID
                    Data.MachineAddress = match.Groups[3].Value;

                    if (_teleportMarker)
                    {
                        Data.IsTeleport = true;
                        _teleportMarker = false;
                    }

                    if (_reservedTeleportMarker)
                    {
                        Data.ServerType = ServerType.Reserved;
                        _reservedTeleportMarker = false;
                    }

                    App.Logger.WriteLine(LOG_IDENT, $"Joining Game ({Data.JobId})");
                }
            }
            // ---- Game joining details ----
            else if (!InGame && Data.PlaceId != 0)
            {
                if (entry.Contains(GameJoiningUniverseEntry))
                {
                    var match = Regex.Match(entry, GameJoiningUniversePattern);
                    if (match.Groups.Count != 3)
                        return;

                    Data.UniverseId = long.Parse(match.Groups[1].Value);
                    Data.UserId = long.Parse(match.Groups[2].Value);

                    if (History.Any())
                    {
                        var lastActivity = History.First();
                        if (Data.UniverseId == lastActivity.UniverseId && Data.IsTeleport)
                            Data.RootActivity = lastActivity.RootActivity ?? lastActivity;
                    }
                }
                else if (entry.Contains(GameJoiningUDMUXEntry))
                {
                    var match = Regex.Match(entry, GameJoiningUDMUXPattern);
                    if (match.Groups.Count != 3)
                        return;

                    Data.MachineAddress = match.Groups[1].Value;
                    App.Logger.WriteLine(LOG_IDENT, $"Server is UDMUX protected ({Data.MachineAddress})");
                }
                else if (entry.Contains(GameJoinedEntry))
                {
                    // ✅ Updated: we no longer parse the "serverId" log line
                    // We trust the JobId parsed earlier.
                    App.Logger.WriteLine(LOG_IDENT, $"Confirmed game join (JobId = {Data.JobId})");

                    InGame = true;
                    Data.TimeJoined = DateTime.Now;
                    OnGameJoin?.Invoke(this, EventArgs.Empty);
                }
            }
            // ---- In-game ----
            else if (InGame && Data.PlaceId != 0)
            {
                if (entry.Contains(GameDisconnectedEntry))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Disconnected from Game ({Data.JobId})");
                    Data.TimeLeft = DateTime.Now;
                    History.Insert(0, Data);

                    InGame = false;
                    Data = new();

                    OnGameLeave?.Invoke(this, EventArgs.Empty);
                }
                else if (entry.Contains(GameTeleportingEntry))
                {
                    _teleportMarker = true;
                    App.Logger.WriteLine(LOG_IDENT, $"Initiating teleport ({Data.JobId})");
                }
                else if (entry.Contains(GameJoiningReservedServerEntry))
                {
                    _teleportMarker = true;
                    _reservedTeleportMarker = true;
                }
                else if (entry.Contains(GameMessageEntry))
                {
                    var match = Regex.Match(entry, GameMessageEntryPattern);
                    if (match.Groups.Count != 2)
                        return;

                    string messagePlain = match.Groups[1].Value;
                    Message? message;

                    if ((DateTime.Now - LastRPCRequest).TotalSeconds <= 1)
                        return;

                    try
                    {
                        message = JsonSerializer.Deserialize<Message>(messagePlain);
                    }
                    catch
                    {
                        return;
                    }

                    if (message?.Command == "SetLaunchData")
                    {
                        string? data = message.Data.Deserialize<string>();
                        if (data != null && data.Length <= 200)
                            Data.RPCLaunchData = data;
                    }

                    OnRPCMessage?.Invoke(this, message);
                    LastRPCRequest = DateTime.Now;
                }
                else if (entry.Contains(GamePlayerJoinLeaveEntry))
                {
                    var match = Regex.Match(entry, GamePlayerJoinLeavePattern);
                    if (match.Groups.Count != 4)
                        return;

                    var userLog = new ActivityData.UserLog
                    {
                        Type = match.Groups[1].Value,
                        Username = match.Groups[2].Value,
                        UserId = match.Groups[3].Value,
                        Time = DateTime.Now
                    };

                    Data.PlayerLogs[Data.PlayerLogs.Count] = userLog;
                    OnNewPlayerRequest?.Invoke(this, userLog);
                }
                else if (entry.Contains(GameMessageLogEntry))
                {
                    var match = Regex.Match(entry, GameMessageLogPattern);
                    if (match.Groups.Count != 2)
                        return;

                    var messageLog = new ActivityData.UserMessage
                    {
                        Message = match.Groups[1].Value,
                        Time = DateTime.Now
                    };

                    Data.MessageLogs[Data.MessageLogs.Count] = messageLog;
                    OnNewMessageRequest?.Invoke(this, messageLog);
                }
            }
        }

        public void Dispose()
        {
            IsDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
