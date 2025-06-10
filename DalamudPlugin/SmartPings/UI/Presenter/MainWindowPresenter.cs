using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Newtonsoft.Json;
using Reactive.Bindings;
using SmartPings.Audio;
using SmartPings.Data;
using SmartPings.Extensions;
using SmartPings.Input;
using SmartPings.Log;
using SmartPings.Network;
using SmartPings.UI.View;
using System;
using System.Reactive.Linq;

namespace SmartPings.UI.Presenter;

public class MainWindowPresenter(
    MainWindow view,
    Configuration configuration,
    IClientState clientState,
    IFramework framework,
    IDataManager dataManager,
    IAudioDeviceController audioDeviceController,
    ServerConnection serverConnection,
    KeyStateWrapper keyStateWrapper,
    XivHudNodeMap hudNodeMap,
    ILogger logger) : IPluginUIPresenter
{
    public IPluginUIView View => this.view;

    private readonly MainWindow view = view;
    private readonly Configuration configuration = configuration;
    private readonly IClientState clientState = clientState;
    private readonly IFramework framework = framework;
    private readonly IDataManager dataManager = dataManager;
    private readonly IAudioDeviceController audioDeviceController = audioDeviceController;
    private readonly ServerConnection serverConection = serverConnection;
    private readonly KeyStateWrapper keyStateWrapper = keyStateWrapper;
    private readonly XivHudNodeMap hudNodeMap = hudNodeMap;
    private readonly ILogger logger = logger;

    private bool keyDownListenerSubscribed;

    public void SetupBindings()
    {
        BindVariables();
        BindActions();
    }

    private void BindVariables()
    {
        Bind(this.view.PublicRoom,
            b =>
            {
                this.configuration.PublicRoom = b; this.configuration.Save();
            },
            this.configuration.PublicRoom);
        Bind(this.view.RoomName,
            s => { this.configuration.RoomName = s; this.configuration.Save(); }, this.configuration.RoomName);
        Bind(this.view.RoomPassword,
            s => { this.configuration.RoomPassword = s; this.configuration.Save(); }, this.configuration.RoomPassword);

        Bind(this.view.EnableGroundPings,
            b => { this.configuration.EnableGroundPings = b; this.configuration.Save(); }, this.configuration.EnableGroundPings);
        Bind(this.view.EnablePingWheel,
            b => { this.configuration.EnablePingWheel = b; this.configuration.Save(); }, this.configuration.EnablePingWheel);
        Bind(this.view.DefaultGroundPingType,
            b => { this.configuration.DefaultGroundPingType = b; this.configuration.Save(); }, this.configuration.DefaultGroundPingType);

        Bind(this.view.EnableGuiPings,
            b => { this.configuration.EnableGuiPings = b; this.configuration.Save(); }, this.configuration.EnableGuiPings);
        Bind(this.view.EnableHpMpPings,
            b => { this.configuration.EnableHpMpPings = b; this.configuration.Save(); }, this.configuration.EnableHpMpPings);
        Bind(this.view.SendGuiPingsToCustomServer,
            b => { this.configuration.SendGuiPingsToCustomServer = b; this.configuration.Save(); }, this.configuration.SendGuiPingsToCustomServer);
        Bind(this.view.SendGuiPingsToXivChat,
            b => { this.configuration.SendGuiPingsToXivChat = b; this.configuration.Save(); }, this.configuration.SendGuiPingsToXivChat);
        Bind(this.view.XivChatSendLocation,
            b => { this.configuration.XivChatSendLocation = b; this.configuration.Save(); }, this.configuration.XivChatSendLocation);

        Bind(this.view.MasterVolume,
            f => { this.configuration.MasterVolume = f; this.configuration.Save(); }, this.configuration.MasterVolume);

        Bind(this.view.PlayRoomJoinAndLeaveSounds,
            b => { this.configuration.PlayRoomJoinAndLeaveSounds = b; this.configuration.Save(); }, this.configuration.PlayRoomJoinAndLeaveSounds);
        Bind(this.view.KeybindsRequireGameFocus,
            b => { this.configuration.KeybindsRequireGameFocus = b; this.configuration.Save(); }, this.configuration.KeybindsRequireGameFocus);
        Bind(this.view.PrintLogsToChat,
            b => { this.configuration.PrintLogsToChat = b; this.configuration.Save(); }, this.configuration.PrintLogsToChat);
        Bind(this.view.MinimumVisibleLogLevel,
            i => { this.configuration.MinimumVisibleLogLevel = i; this.configuration.Save(); }, this.configuration.MinimumVisibleLogLevel);
    }

    private void BindActions()
    {
        this.view.JoinRoom.Subscribe(_ =>
        {
            this.serverConection.Channel?.ClearLatestDisconnectMessage();
            if (this.view.PublicRoom.Value)
            {
                this.serverConection.JoinPublicRoom();
            }
            else
            {
                if (string.IsNullOrEmpty(this.view.RoomName.Value))
                {
                    var playerName = this.clientState.GetLocalPlayerFullName();
                    if (playerName == null)
                    {
                        this.logger.Error("Player name is null, cannot autofill private room name.");
                        return;
                    }
                    this.view.RoomName.Value = playerName;
                }
                this.serverConection.JoinPrivateRoom(this.view.RoomName.Value, this.view.RoomPassword.Value);
            }
        });

        this.view.LeaveRoom.Subscribe(_ => this.serverConection.LeaveRoom(false).SafeFireAndForget(ex => this.logger.Error(ex.ToString())));

        this.view.KeybindBeingEdited.Subscribe(k =>
        {
            if (k != Keybind.None && !this.keyDownListenerSubscribed)
            {
                this.keyStateWrapper.OnKeyDown += OnKeyDown;
                this.keyDownListenerSubscribed = true;
            }
            else if (k == Keybind.None && this.keyDownListenerSubscribed)
            {
                this.keyStateWrapper.OnKeyDown -= OnKeyDown;
                this.keyDownListenerSubscribed = false;
            }
        });
        this.view.ClearKeybind.Subscribe(k =>
        {
            switch (k)
            {
                case Keybind.Ping:
                    this.configuration.PingKeybind = default; break;
                case Keybind.QuickPing:
                    this.configuration.QuickPingKeybind = default; break;
                default:
                    return;
            }
            this.configuration.Save();
        });

        this.view.PrintNodeMap1.Subscribe(_ =>
        {
            foreach (var n in this.hudNodeMap.CollisionNodeMap)
            {
                this.logger.Info("Node {0} -> {1}:{2}", n.Key.ToString("X"), n.Value.HudSection, n.Value.Index);
            }
        });
        this.view.PrintNodeMap2.Subscribe(_ =>
        {
            foreach (var n in this.hudNodeMap.ElementNodeMap)
            {
                this.logger.Info("HudSection {0} -> {1}", n.Key.HudSection, n.Value.ToString("X"));
            }
        });
        this.view.PrintPartyStatuses.Subscribe(_ =>
        {
            unsafe
            {
                // The PartyMembers array always has 10 slots, but accessing an index at or above PartyMemberCount
                // will crash XivAlexander
                for (var i = 0; i < AgentHUD.Instance()->PartyMemberCount; i++)
                {
                    var partyMember = AgentHUD.Instance()->PartyMembers[i];
                    if (partyMember.Object == null) { continue; }
                    // These include Other statuses
                    // These seem randomly sorted, but statuses with the same PartyListPriority are
                    // sorted relative to each other
                    foreach (var characterStatus in partyMember.Object->StatusManager.Status)
                    {
                        if (characterStatus.StatusId == 0) { continue; }
                        var luminaStatuses = this.dataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>(this.clientState.ClientLanguage);
                        Status status = new() { Id = characterStatus.StatusId };
                        if (luminaStatuses.TryGetRow(characterStatus.StatusId, out var luminaStatus))
                        {
                            status = new Status(luminaStatus)
                            {
                                SourceIsSelf = characterStatus.SourceObject.ObjectId == GuiPingHandler.GetLocalPlayerId(),
                                Stacks = characterStatus.Param,
                            };
                        }
                        this.logger.Info("Party member {0}, index {1}, has status {2}",
                           partyMember.Object->NameString, partyMember.Index, JsonConvert.SerializeObject(status).ToString());
                    }
                }
            }
        });
        this.view.PrintTargetStatuses.Subscribe(_ =>
        {
            unsafe
            {
                var targetId = AgentHUD.Instance()->CurrentTargetId;
                var target = CharacterManager.Instance()->LookupBattleCharaByEntityId(targetId);
                if (target != null)
                {
                    this.logger.Info("Target id {0}, name {1}", targetId, target->NameString);
                    foreach(var targetStatus in target->StatusManager.Status)
                    {
                        if (targetStatus.StatusId == 0) { continue; }
                        var luminaStatuses = this.dataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>(this.clientState.ClientLanguage);
                        Status status = new() { Id = targetStatus.StatusId };
                        if (luminaStatuses.TryGetRow(targetStatus.StatusId, out var luminaStatus))
                        {
                            status = new Status(luminaStatus)
                            {
                                SourceIsSelf = targetStatus.SourceObject.ObjectId == GuiPingHandler.GetLocalPlayerId(),
                                Stacks = targetStatus.Param,
                            };
                        }
                        this.logger.Info("Target has status {0}", JsonConvert.SerializeObject(status).ToString());
                    }
                }
                else
                {
                    this.logger.Info("No target.");
                }
            }
        });
    }

    private void Bind<T>(
        IReactiveProperty<T> reactiveProperty,
        Action<T> dataUpdateAction,
        T initialValue)
    {
        if (initialValue != null)
        {
            reactiveProperty.Value = initialValue;
        }
        reactiveProperty.Subscribe(dataUpdateAction);
    }

    private void OnKeyDown(VirtualKey key)
    {
        // Disallow any keybinds to left mouse
        if (key == VirtualKey.LBUTTON) { return; }

        // This callback can be called from a non-framework thread, and UI values should only be modified
        // on the framework thread (or else the game can crash)
        this.framework.Run(() =>
        {
            var editedKeybind = this.view.KeybindBeingEdited.Value;
            this.view.KeybindBeingEdited.Value = Keybind.None;

            switch (editedKeybind)
            {
                case Keybind.Ping:
                    this.configuration.PingKeybind = key; break;
                case Keybind.QuickPing:
                    this.configuration.QuickPingKeybind = key; break;
                default:
                    return;
            }
            this.configuration.Save();
        });
    }

}
