using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Dalamud.Game.Text;
using SmartPings.Data;
using SmartPings.Extensions;
using SmartPings.Log;
using SocketIOClient;
using ZLinq;

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
    private readonly Lazy<GuiPingHandler> guiPingHandler;
    private readonly Configuration configuration; // client plugin settings
    private readonly ILogger logger;

    private readonly LoadConfig loadConfig; // server URL and key

    private string? localPlayerFullName;
    private string[]? playersInRoom;
    private bool isAutoJoin;

    public ServerConnection(
        DalamudServices dalamud,
        MapManager mapManager,
        Lazy<GroundPingPresenter> groundPingPresenter,
        Lazy<GuiPingHandler> guiPingHandler,
        Configuration configuration,
        ILogger logger)
    {
        this.dalamud = dalamud;
        this.mapManager = mapManager;
        this.groundPingPresenter = groundPingPresenter;
        this.guiPingHandler = guiPingHandler;
        this.configuration = configuration;
        this.logger = logger;

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
        this.dalamud.ClientState.Login -= OnLogin;
        this.dalamud.ClientState.Logout -= OnLogout;
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
        this.isAutoJoin = false;

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
            this.Channel.OnMessage -= OnMessage;
            this.Channel.OnDisconnected -= OnDisconnect;
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
                authorId = (long)ping.AuthorId,
                startTimestamp = ping.StartTimestamp,
                mapId = ping.MapId ?? string.Empty,
                worldPositionX = ping.WorldPosition.X,
                worldPositionY = ping.WorldPosition.Y,
                worldPositionZ = ping.WorldPosition.Z,
            }
        }).SafeFireAndForget(ex => this.logger.Error(ex.ToString()));
    }

    public void SendUiPing(string sourceName, HudElementInfo hudElementInfo)
    {
        if (this.Channel == null || !this.Channel.Connected) { return; }

        this.Channel.SendAsync(new ServerMessage.Payload
        {
            action = ServerMessage.Payload.Action.SendUiPing,
            uiPingPayload = new ServerMessage.Payload.UiPingPayload
            {
                sourceName = sourceName,
                hudElementInfo = hudElementInfo,
            }
        });
    }

    private void OnLogin()
    {
        if (this.configuration.AutoJoinPrivateRoomOnLogin)
        {
            if (string.IsNullOrEmpty(this.configuration.RoomName) ||
                string.IsNullOrEmpty(this.configuration.RoomPassword))
            {
                this.logger.Warn("No private room credentials found to auto-join.");
                return;
            }
            this.isAutoJoin = true;
            this.dalamud.ChatGui.Print($"Auto-joining {this.configuration.RoomName}'s room.", PluginInitializer.Name);
            JoinPrivateRoom(this.configuration.RoomName, this.configuration.RoomPassword);
        }
    }

    private void OnLogout(int type, int code)
    {
        LeaveRoom(false);
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
        this.Channel.OnDisconnected += OnDisconnect;

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
        catch (JsonException jsonEx)
        {
            this.logger.Error("Failed to read server JSON data. You may need to update the plugin. Error:\n{0}", jsonEx.ToString());
            return;
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
                if (payload.groundPingPayload.HasValue)
                {
                    AddGroundPing(payload.groundPingPayload.Value);
                }
                break;
            case ServerMessage.Payload.Action.SendUiPing:
                if (payload.uiPingPayload.HasValue)
                {
                    EchoUiPing(payload.uiPingPayload.Value);
                }
                break;
            case ServerMessage.Payload.Action.Close:
                // Temp logic to update the player list before the server gets fixed to send a player list update
                this.playersInRoom = this.playersInRoom?.AsValueEnumerable().Where(p => p != message.from).ToArray();
                break;
        }
    }

    private void OnDisconnect()
    {
        if (this.isAutoJoin)
        {
            this.dalamud.ChatGui.PrintError($"Failed to auto join {this.configuration.RoomName}'s room.", PluginInitializer.Name);
            this.configuration.AutoJoinPrivateRoomOnLogin = false;
            this.configuration.Save();
        }
        this.isAutoJoin = false;
    }

    private void AddGroundPing(ServerMessage.Payload.GroundPingPayload payload)
    {
        var ping = new GroundPing
        {
            PingType = payload.pingType,
            Author = payload.author,
            AuthorId = (ulong)payload.authorId,
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

    private void EchoUiPing(ServerMessage.Payload.UiPingPayload payload)
    {
        var echoMsg = GuiPingHandler.CreateUiPingString(GuiPingHandler.UiPingType.Echo, payload.sourceName, payload.hudElementInfo).Item1;
        if (echoMsg != null)
        {
            this.guiPingHandler.Value.EchoUiPing(echoMsg);
        }
    }
}
