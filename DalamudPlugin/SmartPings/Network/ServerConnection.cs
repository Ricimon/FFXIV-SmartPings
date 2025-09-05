using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Dalamud.Game.Text;
using SmartPings.Extensions;
using SmartPings.Log;
using SocketIOClient;

namespace SmartPings.Network;

public sealed class ServerConnection : IDisposable
{
    /// <summary>
    /// When in a public room, this plugin will automatically switch rooms when the player changes maps.
    /// This property indicates if the player should be connected to a public room.
    /// </summary>
    public bool ShouldBeInRoom { get; private set; }
    public bool InRoom { get; private set; }

    public IEnumerable<string> PlayersInRoom
    {
        get
        {
            if (InRoom)
            {
                if (this.playersInRoom == null)
                {
                    return [this.localPlayerFullName ?? "null"];
                }
                else
                {
                    return this.playersInRoom;
                }
            }
            else
            {
                return [];
            }
        }
    }

    public ServerConnectionChannel? Channel { get; private set; }

    private const string PeerType = "player";

    private readonly DalamudServices dalamud;
    private readonly MapManager mapManager;
    private readonly Lazy<GroundPingPresenter> groundPingPresenter;
    private readonly ILogger logger;

    private readonly LoadConfig loadConfig; // server URL and key
    private readonly Configuration configuration; // client plugin settings

    private string? localPlayerFullName;
    private string[]? playersInRoom;

    public ServerConnection(
        DalamudServices dalamud,
        MapManager mapManager,
        Lazy<GroundPingPresenter> groundPingPresenter,
        ILogger logger,
        Configuration configuration)
    {
        this.dalamud = dalamud;
        this.mapManager = mapManager;
        this.groundPingPresenter = groundPingPresenter;
        this.logger = logger;
        this.configuration = configuration;

        var configPath = Path.Combine(this.dalamud.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "config.json");
        this.loadConfig = null!;
        if (File.Exists(configPath))
        {
            var configString = File.ReadAllText(configPath);
            try
            {
                this.loadConfig = JsonSerializer.Deserialize<LoadConfig>(configString)!;
            }
            catch (Exception) { }
        }
        if (this.loadConfig == null)
        {
            logger.Warn("Could not load config file at {0}", configPath);
            this.loadConfig = new();
        }

        this.dalamud.ClientState.Login += OnLogin;
        this.dalamud.ClientState.Logout += OnLogout;
    }

    public void Dispose()
    {
        this.Channel?.Dispose();
        this.dalamud.ClientState.Logout -= OnLogout;
        this.dalamud.ClientState.Login -= OnLogin;
    }

    public void JoinPublicRoom()
    {
        if (this.ShouldBeInRoom)
        {
            this.logger.Error("Already should be in room, ignoring public room join request.");
            return;
        }
        string roomName = this.mapManager.GetCurrentMapPublicRoomName();
        string[]? otherPlayers = this.mapManager.InSharedWorldMap() ? null : GetOtherPlayerNamesInInstance().ToArray();
        JoinRoom(roomName, string.Empty, otherPlayers);
        this.mapManager.OnMapChanged += ReconnectToCurrentMapPublicRoom;
    }

    public void JoinPrivateRoom(string roomName, string roomPassword)
    {
        if (this.ShouldBeInRoom)
        {
            this.logger.Error("Already should be in room, ignoring private room join request.");
            return;
        }
        JoinRoom(roomName, roomPassword, null);
    }

    public Task LeaveRoom(bool autoRejoin)
    {
        if (!autoRejoin)
        {
            this.ShouldBeInRoom = false;
            this.mapManager.OnMapChanged -= ReconnectToCurrentMapPublicRoom;
        }

        if (!this.InRoom)
        {
            return Task.CompletedTask;
        }

        this.logger.Debug("Attempting to leave room.");

        this.InRoom = false;
        this.localPlayerFullName = null;
        this.playersInRoom = null;

        //if (this.configuration.PlayRoomJoinAndLeaveSounds)
        //{
        //    this.audioDeviceController.PlaySfx(this.roomSelfLeaveSound)
        //        .ContinueWith(task => this.audioDeviceController.AudioPlaybackIsRequested = false, TaskContinuationOptions.OnlyOnRanToCompletion)
        //        .SafeFireAndForget(ex =>
        //        {
        //            if (ex is not TaskCanceledException) { this.logger.Error(ex.ToString()); }
        //        });
        //}
        //else
        //{
        //    this.audioDeviceController.AudioPlaybackIsRequested = false;
        //}

        if (this.Channel != null)
        {
            //this.Channel.OnConnected -= OnSignalingServerConnected;
            //this.Channel.OnReady -= OnSignalingServerReady;
            //this.Channel.OnDisconnected -= OnSignalingServerDisconnected;
            //this.Channel.OnErrored -= OnSignalingServerDisconnected;
            this.Channel.OnMessage -= OnMessage;
            return this.Channel.DisconnectAsync();
        }
        else
        {
            return Task.CompletedTask;
        }
    }

    public void SendGroundPing(GroundPing ping)
    {
        if (this.Channel == null || !this.Channel.Connected) { return; }

        this.Channel.SendAsync(new ServerMessage.Payload
        {
            action = ServerMessage.Payload.Action.AddGroundPing,
            groundPingPayload = new ServerMessage.Payload.GroundPingPayload
            {
                pingType = ping.PingType,
                author = ping.Author ?? string.Empty,
                startTimestamp = ping.StartTimestamp,
                mapId = ping.MapId ?? string.Empty,
                worldPositionX = ping.WorldPosition.X,
                worldPositionY = ping.WorldPosition.Y,
                worldPositionZ = ping.WorldPosition.Z,
            }
        }).SafeFireAndForget(ex => this.logger.Error(ex.ToString()));
    }

    public void SendChatMessage(XivChatEntry chatEntry)
    {
        if (this.Channel == null || !this.Channel.Connected) { return; }

        this.Channel.SendAsync(new ServerMessage.Payload
        {
            action = ServerMessage.Payload.Action.SendChatMessage,
            chatMessagePayload = new ServerMessage.Payload.ChatMessagePayload
            {
                chatType = chatEntry.Type ?? XivChatType.None,
                message = chatEntry.MessageBytes,
            }
        });
    }

    private void OnLogout(int type, int code)
    {
        LeaveRoom(false);
    }

    private void OnLogin()
    {
        if (configuration.RoomName == null || configuration.RoomPassword == null || configuration.RoomPassword.Length < 4)
        {
            dalamud.Log.Error("Failed to automatically join private room! Room data invalid.");
            return;
        }

        if (configuration.RoomLoginOnLaunch)
        {
            JoinPrivateRoom(configuration.RoomName, configuration.RoomPassword);
        }
    }

    private IEnumerable<string> GetOtherPlayerNamesInInstance()
    {
        return this.dalamud.ObjectTable.GetPlayers()
            .Select(p => p.GetPlayerFullName())
            .Where(s => s != null)
            .Where(s => s != this.dalamud.ClientState.GetLocalPlayerFullName())
            .Cast<string>();
    }

    private void JoinRoom(string roomName, string roomPassword, string[]? playersInInstance)
    {
        if (this.InRoom)
        {
            this.logger.Error("Already in room, ignoring join request.");
            return;
        }

        this.logger.Debug("Attemping to join room.");

        var playerName = this.dalamud.ClientState.GetLocalPlayerFullName();
        if (playerName == null)
        {
#if DEBUG
            playerName = "testPeer14";
            this.logger.Warn("Player name is null. Setting it to {0} for debugging.", playerName);
#else
            this.logger.Error("Player name is null, cannot join voice room.");
            return;
#endif
        }

        this.InRoom = true;
        this.ShouldBeInRoom = true;
        this.localPlayerFullName = playerName;

        this.logger.Trace("Creating ServerConnectionChannel class with peerId {0}", playerName);
        if (this.Channel == null)
        {
            this.Channel = new ServerConnectionChannel(playerName,
                PeerType,
                this.loadConfig.serverUrl,
                this.loadConfig.serverToken,
                this.logger,
                true);
        }
        else
        {
            this.Channel.PeerId = playerName;
        }

        this.Channel.OnMessage += OnMessage;

        this.logger.Debug("Attempting to connect to server.");
        this.Channel.ConnectAsync(roomName, roomPassword, playersInInstance).SafeFireAndForget(ex =>
        {
            if (ex is not OperationCanceledException)
            {
                this.logger.Error(ex.ToString());
            }
        });
    }

    private void ReconnectToCurrentMapPublicRoom()
    {
        if (this.ShouldBeInRoom &&
            (!this.InRoom || this.Channel?.RoomName != this.mapManager.GetCurrentMapPublicRoomName()))
        {
            Task.Run(async () =>
            {
                await this.LeaveRoom(true);
                // Add an arbitrary delay here as loading a new map can result in a null local player name during load.
                // This delay hopefully allows the game to populate that field before a reconnection attempt happens.
                // Also in some housing districts, the mapId is different after the OnTerritoryChanged event
                await Task.Delay(1000);
                // Accessing the object table must happen on the main thread
                this.dalamud.Framework.Run(() =>
                {
                    var roomName = this.mapManager.GetCurrentMapPublicRoomName();
                    string[]? otherPlayers = this.mapManager.InSharedWorldMap() ? null : GetOtherPlayerNamesInInstance().ToArray();
                    this.JoinRoom(roomName, string.Empty, otherPlayers);
                }).SafeFireAndForget(ex => this.logger.Error(ex.ToString()));
            });
        }
    }

    private void OnMessage(SocketIOResponse response)
    {
        ServerMessage message;
        ServerMessage.Payload payload;
        try
        {
            message = response.GetValue<ServerMessage>();
            payload = message.payload;
        }
        catch (Exception e)
        {
            this.logger.Error(e.ToString());
            return;
        }

        switch (payload.action)
        {
            case ServerMessage.Payload.Action.UpdatePlayersInRoom:
                this.playersInRoom = payload.players;
                break;
            case ServerMessage.Payload.Action.AddGroundPing:
                AddGroundPing(payload.groundPingPayload);
                break;
            case ServerMessage.Payload.Action.SendChatMessage:
                PrintChatMessage(payload.chatMessagePayload);
                break;
        }
    }

    private void AddGroundPing(ServerMessage.Payload.GroundPingPayload payload)
    {
        var ping = new GroundPing
        {
            PingType = payload.pingType,
            Author = payload.author,
            StartTimestamp = payload.startTimestamp,
            MapId = payload.mapId,
            WorldPosition = new Vector3
            {
                X = payload.worldPositionX,
                Y = payload.worldPositionY,
                Z = payload.worldPositionZ,
            },
        };
        this.groundPingPresenter.Value.GroundPings.AddLast(ping);
    }

    private void PrintChatMessage(ServerMessage.Payload.ChatMessagePayload payload)
    {
        var xivMsg = new XivChatEntry
        {
            Type = payload.chatType,
            MessageBytes = payload.message
        };
        this.dalamud.ChatGui.Print(xivMsg);
    }
}
