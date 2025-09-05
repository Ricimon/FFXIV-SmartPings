using System;
using System.Reactive.Linq;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Keys;
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

namespace SmartPings.UI.Presenter;

public class MainWindowPresenter(
    MainWindow view,
    DalamudServices dalamud,
    Configuration configuration,
    IAudioDeviceController audioDeviceController,
    ServerConnection serverConnection,
    KeyStateWrapper keyStateWrapper,
    XivHudNodeMap hudNodeMap,
    ILogger logger) : IPluginUIPresenter
{
    public IPluginUIView View => view;

    private bool keyDownListenerSubscribed;

    public void SetupBindings()
    {
        BindVariables();
        BindActions();
    }

    private void BindVariables()
    {
        Bind(view.PublicRoom,
            b => { configuration.PublicRoom = b; configuration.Save(); }, configuration.PublicRoom);
        Bind(view.RoomName,
            s => { configuration.RoomName = s; configuration.Save(); }, configuration.RoomName);
        Bind(view.RoomPassword,
            s => { configuration.RoomPassword = s; configuration.Save(); }, configuration.RoomPassword);
        Bind(view.AutoJoinPrivateRoomOnLogin,
            b => { configuration.AutoJoinPrivateRoomOnLogin = b; configuration.Save(); }, configuration.AutoJoinPrivateRoomOnLogin);

        Bind(view.EnableGroundPings,
            b => { configuration.EnableGroundPings = b; configuration.Save(); }, configuration.EnableGroundPings);
        Bind(view.EnablePingWheel,
            b => { configuration.EnablePingWheel = b; configuration.Save(); }, configuration.EnablePingWheel);
        Bind(view.DefaultGroundPingType,
            b => { configuration.DefaultGroundPingType = b; configuration.Save(); }, configuration.DefaultGroundPingType);

        Bind(view.EnableGuiPings,
            b => { configuration.EnableGuiPings = b; configuration.Save(); }, configuration.EnableGuiPings);
        Bind(view.EnableHpMpPings,
            b => { configuration.EnableHpMpPings = b; configuration.Save(); }, configuration.EnableHpMpPings);
        Bind(view.SendGuiPingsToCustomServer,
            b => { configuration.SendGuiPingsToCustomServer = b; configuration.Save(); }, configuration.SendGuiPingsToCustomServer);
        Bind(view.SendGuiPingsToXivChat,
            b => { configuration.SendGuiPingsToXivChat = b; configuration.Save(); }, configuration.SendGuiPingsToXivChat);
        Bind(view.XivChatSendLocation,
            b => { configuration.XivChatSendLocation = b; configuration.Save(); }, configuration.XivChatSendLocation);

        Bind(view.SelectedAudioOutputDeviceIndex,
            i => audioDeviceController.AudioPlaybackDeviceIndex = i, audioDeviceController.AudioPlaybackDeviceIndex);
        Bind(view.MasterVolume,
            f => { configuration.MasterVolume = f; configuration.Save(); }, configuration.MasterVolume);
        Bind(view.ActiveSoundPack,
            b => { configuration.ActiveSoundPack = b; configuration.Save(); }, configuration.ActiveSoundPack);
        Bind(view.EnableSpatialization,
            b => { configuration.EnableSpatialization = b; configuration.Save(); }, configuration.EnableSpatialization);

        Bind(view.PlayRoomJoinAndLeaveSounds,
            b => { configuration.PlayRoomJoinAndLeaveSounds = b; configuration.Save(); }, configuration.PlayRoomJoinAndLeaveSounds);
        Bind(view.KeybindsRequireGameFocus,
            b => { configuration.KeybindsRequireGameFocus = b; configuration.Save(); }, configuration.KeybindsRequireGameFocus);
        Bind(view.PrintLogsToChat,
            b => { configuration.PrintLogsToChat = b; configuration.Save(); }, configuration.PrintLogsToChat);
        Bind(view.MinimumVisibleLogLevel,
            i => { configuration.MinimumVisibleLogLevel = i; configuration.Save(); }, configuration.MinimumVisibleLogLevel);
    }

    private void BindActions()
    {
        view.JoinRoom.Subscribe(_ =>
        {
            serverConnection.Channel?.ClearLatestDisconnectMessage();
            if (view.PublicRoom.Value)
            {
                serverConnection.JoinPublicRoom();
            }
            else
            {
                if (string.IsNullOrEmpty(view.RoomName.Value))
                {
                    var playerName = dalamud.ClientState.GetLocalPlayerFullName();
                    if (playerName == null)
                    {
                        logger.Error("Player name is null, cannot autofill private room name.");
                        return;
                    }
                    view.RoomName.Value = playerName;
                }
                serverConnection.JoinPrivateRoom(view.RoomName.Value, view.RoomPassword.Value);
            }
        });

        view.LeaveRoom.Subscribe(_ => serverConnection.LeaveRoom(false).SafeFireAndForget(ex => logger.Error(ex.ToString())));

        view.KeybindBeingEdited.Subscribe(k =>
        {
            if (k != Keybind.None && !this.keyDownListenerSubscribed)
            {
                keyStateWrapper.OnKeyDown += OnKeyDown;
                this.keyDownListenerSubscribed = true;
            }
            else if (k == Keybind.None && this.keyDownListenerSubscribed)
            {
                keyStateWrapper.OnKeyDown -= OnKeyDown;
                this.keyDownListenerSubscribed = false;
            }
        });
        view.ClearKeybind.Subscribe(k =>
        {
            switch (k)
            {
                case Keybind.Ping:
                    configuration.PingKeybind = default; break;
                case Keybind.QuickPing:
                    configuration.QuickPingKeybind = default; break;
                default:
                    return;
            }
            configuration.Save();
        });

        view.PrintNodeMap1.Subscribe(_ =>
        {
            foreach (var n in hudNodeMap.CollisionNodeMap)
            {
                logger.Info("Node {0} -> {1}:{2}", n.Key.ToString("X"), n.Value.HudSection, n.Value.Index);
            }
        });
        view.PrintNodeMap2.Subscribe(_ =>
        {
            foreach (var n in hudNodeMap.ElementNodeMap)
            {
                logger.Info("HudSection {0} -> {1}", n.Key.HudSection, n.Value.ToString("X"));
            }
        });
        view.PrintPartyStatuses.Subscribe(_ =>
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
                        var luminaStatuses = dalamud.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>(dalamud.ClientState.ClientLanguage);
                        Status status = new() { Id = characterStatus.StatusId };
                        if (luminaStatuses.TryGetRow(characterStatus.StatusId, out var luminaStatus))
                        {
                            status = new Status(luminaStatus)
                            {
                                RemainingTime = characterStatus.RemainingTime,
                                SourceIsSelf = characterStatus.SourceObject.ObjectId == GuiPingHandler.GetLocalPlayerId(),
                                Stacks = characterStatus.Param,
                            };
                        }
                        logger.Info("Party member {0}, index {1}, has status {2}",
                           partyMember.Object->NameString, partyMember.Index, JsonConvert.SerializeObject(status).ToString());
                    }
                }
            }
        });
        view.PrintTargetStatuses.Subscribe(_ =>
        {
            unsafe
            {
                var targetId = AgentHUD.Instance()->CurrentTargetId;
                var target = CharacterManager.Instance()->LookupBattleCharaByEntityId(targetId);
                if (target != null)
                {
                    logger.Info("Target id {0}, name {1}", targetId, target->NameString);
                    foreach(var targetStatus in target->StatusManager.Status)
                    {
                        if (targetStatus.StatusId == 0) { continue; }
                        var luminaStatuses = dalamud.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>(dalamud.ClientState.ClientLanguage);
                        Status status = new() { Id = targetStatus.StatusId };
                        if (luminaStatuses.TryGetRow(targetStatus.StatusId, out var luminaStatus))
                        {
                            status = new Status(luminaStatus)
                            {
                                RemainingTime = targetStatus.RemainingTime,
                                SourceIsSelf = targetStatus.SourceObject.ObjectId == GuiPingHandler.GetLocalPlayerId(),
                                Stacks = targetStatus.Param,
                            };
                        }
                        logger.Info("Target has status {0}", JsonConvert.SerializeObject(status).ToString());
                    }
                }
                else
                {
                    logger.Info("No target.");
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
        dalamud.Framework.Run(() =>
        {
            var editedKeybind = view.KeybindBeingEdited.Value;
            view.KeybindBeingEdited.Value = Keybind.None;

            switch (editedKeybind)
            {
                case Keybind.Ping:
                    configuration.PingKeybind = key; break;
                case Keybind.QuickPing:
                    configuration.QuickPingKeybind = key; break;
                default:
                    return;
            }
            configuration.Save();
        });
    }

}
