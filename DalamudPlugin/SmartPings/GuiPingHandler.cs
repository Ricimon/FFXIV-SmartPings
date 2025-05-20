using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SmartPings.Data;
using SmartPings.Extensions;
using SmartPings.Log;
using SmartPings.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace SmartPings;

public unsafe class GuiPingHandler
{
    private const ushort BLUE = 542;
    private const ushort LIGHT_BLUE = 529;
    private const ushort YELLOW = 25;
    private const ushort GREEN = 43;
    private const ushort RED = 518;

    private readonly IClientState clientState;
    private readonly IDataManager dataManager;
    private readonly IChatGui chatGui;
    private readonly IFramework framework;
    private readonly Chat chat;
    private readonly XivHudNodeMap hudNodeMap;
    private readonly ServerConnection serverConnection;
    private readonly Configuration configuration;
    private readonly ILogger logger;

    private readonly List<Status> statuses = [];

    public GuiPingHandler(
        IClientState clientState,
        IDataManager dataManager,
        IChatGui chatGui,
        IFramework framework,
        Chat chat,
        XivHudNodeMap hudNodeMap,
        ServerConnection serverConnection,
        Configuration configuration,
        ILogger logger)
    {
        this.clientState = clientState;
        this.dataManager = dataManager;
        this.chatGui = chatGui;
        this.framework = framework;
        this.chat = chat;
        this.hudNodeMap = hudNodeMap;
        this.serverConnection = serverConnection;
        this.configuration = configuration;
        this.logger = logger;
    }

    public static string GetLocalPlayerName()
    {
        // Accessing IClientState in a non-framework thread will crash XivAlexander, so this is a
        // different way of getting the local player name
        if (0 < AgentHUD.Instance()->PartyMemberCount)
        {
            return AgentHUD.Instance()->PartyMembers[0].Name.ExtractText();
        }
        return string.Empty;
    }

    public static uint GetLocalPlayerId()
    {
        // Accessing IClientState in a non-framework thread will crash XivAlexander, so this is a
        // different way of getting the local player id
        if (0 < AgentHUD.Instance()->PartyMemberCount)
        {
            return AgentHUD.Instance()->PartyMembers[0].EntityId;
        }
        return default;
    }

    public bool TryPingUi()
    {
        var collisionNode = AtkStage.Instance()->AtkCollisionManager->IntersectingCollisionNode;
        var addon = AtkStage.Instance()->AtkCollisionManager->IntersectingAddon;

        if (collisionNode == null && addon == null) { return false; }
        // World UI such as Nameplates have this flag
        if (collisionNode != null && collisionNode->NodeFlags.HasFlag(NodeFlags.UseDepthBasedPriority)) { return false; }

        this.logger.Debug("Mouse over collision node {0} and addon {1}",
            ((nint)collisionNode).ToString("X"),
            ((nint)addon).ToString("X"));

        if (!this.configuration.EnableGuiPings) { return true; }

        if (TryGetCollisionNodeElementInfo(collisionNode, out var info))
        {
            var echoMsg = new SeStringBuilder();
            var chatMsg = new StringBuilder();

            // Source name -------------
            var localPlayerName = GetLocalPlayerName();
            echoMsg.AddUiForeground($"({localPlayerName}) ", BLUE);

            // Target name -------------
            if (!info.IsOnSelf)
            {
                echoMsg.AddUiForeground($"{info.OwnerName}: ", info.IsOnHostile ? RED : LIGHT_BLUE);

                chatMsg.Append($"{info.OwnerName}: ");
            }

            if (info.ElementType == HudElementInfo.Type.Status)
            {
                // Status name --------------
                echoMsg.AddStatusLink(info.Status.Id);
                // This is how status links are normally constructed
                echoMsg.AddUiForeground(500);
                echoMsg.AddUiGlow(501);
                echoMsg.Append(SeIconChar.LinkMarker.ToIconString());
                echoMsg.AddUiGlowOff();
                echoMsg.AddUiForegroundOff();
                if (info.Status.IsEnfeeblement)
                {
                    echoMsg.AddUiForeground(518);
                    echoMsg.Append(SeIconChar.Debuff.ToIconString());
                    echoMsg.AddUiForegroundOff();
                }
                else
                {
                    echoMsg.AddUiForeground(517);
                    echoMsg.Append(SeIconChar.Buff.ToIconString());
                    echoMsg.AddUiForegroundOff();
                }
                var beneficial = info.IsOnHostile == info.Status.IsEnfeeblement;
                echoMsg.AddUiForeground($"{info.Status.Name}", beneficial ? YELLOW : RED);
                echoMsg.Append([RawPayload.LinkTerminator]);

                chatMsg.Append("<status>");

                if (info.Status.MaxStacks > 0)
                {
                    echoMsg.AddUiForeground($" x{info.Status.Stacks}", beneficial ? YELLOW : RED);

                    chatMsg.Append($" x{info.Status.Stacks}");
                }

                // Timer ---------------
                if (info.Status.RemainingTime > 0)
                {
                    echoMsg.AddUiForeground(" - ", YELLOW);
                    var remainingTime = info.Status.RemainingTime >= 1 ?
                        MathF.Floor(info.Status.RemainingTime).ToString() :
                        info.Status.RemainingTime.ToString("F1");
                    echoMsg.AddUiForeground($"{remainingTime}s", GREEN);

                    chatMsg.Append($" - {remainingTime}s");
                }
            }
            else if (info.ElementType == HudElementInfo.Type.Hp)
            {
                var hpPercent = (float)info.Hp.Value / info.Hp.MaxValue * 100;
                hpPercent = MathF.Floor(hpPercent * 10) / 10;
                var hpString = hpPercent == 100 ? hpPercent.ToString("F0") : hpPercent.ToString("F1");
                if (hpString == "0.0" && hpPercent > 0) { hpString = "0.1"; }
                echoMsg.AddUiForeground($"HP: {hpString}%", hpPercent < 10 ? RED : YELLOW);
                if (info.IsOnPartyMember || info.IsOnSelf)
                {
                    echoMsg.AddUiForeground($" ({info.Hp.Value:N0}/{info.Hp.MaxValue:N0})", GREEN);
                }

                chatMsg.Append($"HP: {hpString}%");
                if (info.IsOnPartyMember || info.IsOnSelf)
                {
                    chatMsg.Append($" ({info.Hp.Value:N0}/{info.Hp.MaxValue:N0})");
                }

                ImGuiExtensions.CaptureMouseThisFrame();
            }
            else if (info.ElementType == HudElementInfo.Type.Mp)
            {
                var mpPercent = (float)info.Mp.Value / info.Mp.MaxValue * 100;
                mpPercent = MathF.Floor(mpPercent * 10) / 10;
                var mpString = mpPercent == 100 ? mpPercent.ToString("F0") : mpPercent.ToString("F1");
                if (mpString == "0.0" && mpPercent > 0) { mpString = "0.1"; }
                echoMsg.AddUiForeground($"MP: {mpString}%", mpPercent < 10 ? RED : YELLOW);
                if (info.IsOnPartyMember || info.IsOnSelf)
                {
                    echoMsg.AddUiForeground($" ({info.Mp.Value:N0}/{info.Mp.MaxValue:N0})", GREEN);
                }

                chatMsg.Append($"MP: {mpString}%");
                if (info.IsOnPartyMember || info.IsOnSelf)
                {
                    chatMsg.Append($" ({info.Mp.Value:N0}/{info.Mp.MaxValue:N0})");
                }

                ImGuiExtensions.CaptureMouseThisFrame();
            }

            if (this.configuration.SendGuiPingsToXivChat)
            {
                // This method must be called on a framework thread or else XIV will crash.
                this.framework.Run(() =>
                {
                    if (info.ElementType == HudElementInfo.Type.Status)
                    {
                        AgentChatLog.Instance()->ContextStatusId = info.Status.Id;
                    }
                    if (this.configuration.XivChatSendLocation == XivChatSendLocation.Party)
                    {
                        chatMsg.Insert(0, "/party ");
                    }
                    this.chat.SendMessage(chatMsg.ToString());
                });
            }

            if (this.configuration.SendGuiPingsToCustomServer)
            {
                var xivMsg = new XivChatEntry
                {
                    Type = XivChatType.Echo,
                    Message = echoMsg.Build(),
                };
                this.chatGui.Print(xivMsg);

                this.serverConnection.SendChatMessage(xivMsg);
            }
        }

        return true;
    }

    // To determine what status was clicked on, we need to go from AtkImageNode (inherits AtkCollisionNode) to Status information.
    // An AtkImageNode of a status only holds the image used for the status.
    // One tested method to retrieve status from AtkImageNode is to find the status by image name.
    // However, this does not work for all statuses, as stackable statuses use a different image per stack count,
    // and a search using a stack-image fails to find the original status.
    // So, the method employed here is to use the address of the clicked AtkImageNode.
    // The game instantiates every UI slot that a status can go in, and then sets the visibility and texture of
    // each specific slot that a status should go in when statuses update.
    // The game also holds the statuses each character has in arrays, but not necessarily in the order that they are displayed in the UI.
    // We can, however, reconstruct the order they're expected to go in the UI,
    // as it's been found through testing that statuses are displayed first in PartyListPriority, and then in array order.
    // The final solution then, is to create a map of all UI nodes that are expected to hold some status to
    // exactly the status that should be in that UI node.
    // Upon clicking one of these UI nodes, we can determine what status should belong in there given the
    // existing statuses and their predicted display order, and pull the status information from the character status arrays.

    private bool TryGetCollisionNodeElementInfo(AtkCollisionNode* collisionNode,
        out HudElementInfo info)
    {
        info = default;

        // Search for the node in our node map to determine if it's a relevant HUD element
        if (!this.hudNodeMap.TryGetAsHudElement((nint)collisionNode, out var hudElement)) { return false; }

        switch (hudElement.HudSection)
        {
            case XivHudNodeMap.HudSection.StatusEnhancements:
                if (0 < AgentHUD.Instance()->PartyMemberCount)
                {
                    var character = AgentHUD.Instance()->PartyMembers[0];
                    var statuses = character.Object->StatusManager.Status;
                    // Find the corresponding status given our predicted UI display order
                    if (TryGetStatus(statuses, StatusType.SelfEnhancement, hudElement.Index, out info.Status))
                    {
                        info.ElementType = HudElementInfo.Type.Status;
                        info.OwnerName = character.Name.ExtractText();
                        info.IsOnSelf = true;
                        info.IsOnPartyMember = true;
                        return true;
                    }
                }
                break;

            case XivHudNodeMap.HudSection.StatusEnfeeblements:
                if (0 < AgentHUD.Instance()->PartyMemberCount)
                {
                    var character = AgentHUD.Instance()->PartyMembers[0];
                    var statuses = character.Object->StatusManager.Status;
                    if (TryGetStatus(statuses, StatusType.SelfEnfeeblement, hudElement.Index, out info.Status))
                    {
                        info.ElementType = HudElementInfo.Type.Status;
                        info.OwnerName = character.Name.ExtractText();
                        info.IsOnSelf = true;
                        info.IsOnPartyMember = true;
                        return true;
                    }
                }
                break;

            case XivHudNodeMap.HudSection.StatusOther:
                if (0 < AgentHUD.Instance()->PartyMemberCount)
                {
                    var character = AgentHUD.Instance()->PartyMembers[0];
                    var statuses = character.Object->StatusManager.Status;
                    if (TryGetStatus(statuses, StatusType.SelfOther, hudElement.Index, out info.Status))
                    {
                        info.ElementType = HudElementInfo.Type.Status;
                        info.OwnerName = character.Name.ExtractText();
                        info.IsOnSelf = true;
                        info.IsOnPartyMember = true;
                        return true;
                    }
                }
                break;

            case XivHudNodeMap.HudSection.StatusConditionalEnhancements:
                if (0 < AgentHUD.Instance()->PartyMemberCount)
                {
                    var character = AgentHUD.Instance()->PartyMembers[0];
                    var statuses = character.Object->StatusManager.Status;
                    if (TryGetStatus(statuses, StatusType.SelfConditionalEnhancement, hudElement.Index, out info.Status))
                    {
                        info.ElementType = HudElementInfo.Type.Status;
                        info.OwnerName = character.Name.ExtractText();
                        info.IsOnSelf = true;
                        info.IsOnPartyMember = true;
                        return true;
                    }
                }
                break;

            case XivHudNodeMap.HudSection.PartyList1Status:
            case XivHudNodeMap.HudSection.PartyList2Status:
            case XivHudNodeMap.HudSection.PartyList3Status:
            case XivHudNodeMap.HudSection.PartyList4Status:
            case XivHudNodeMap.HudSection.PartyList5Status:
            case XivHudNodeMap.HudSection.PartyList6Status:
            case XivHudNodeMap.HudSection.PartyList7Status:
            case XivHudNodeMap.HudSection.PartyList8Status:
            case XivHudNodeMap.HudSection.PartyList9Status:
                {
                    var partyMemberIndex = hudElement.HudSection - XivHudNodeMap.HudSection.PartyList1Status;
                    if (partyMemberIndex < AgentHUD.Instance()->PartyMemberCount)
                    {
                        var partyMember = AgentHUD.Instance()->PartyMembers[partyMemberIndex];
                        if (partyMember.Object == null) { break; }
                        var statuses = partyMember.Object->StatusManager.Status;
                        if (TryGetStatus(statuses, StatusType.PartyListStatus, hudElement.Index, out info.Status))
                        {
                            info.ElementType = HudElementInfo.Type.Status;
                            info.OwnerName = partyMember.Name.ExtractText();
                            info.IsOnSelf = partyMemberIndex == 0;
                            info.IsOnPartyMember = true;
                            return true;
                        }
                    }
                }
                break;

            case XivHudNodeMap.HudSection.PartyList1CollisionNode:
            case XivHudNodeMap.HudSection.PartyList2CollisionNode:
            case XivHudNodeMap.HudSection.PartyList3CollisionNode:
            case XivHudNodeMap.HudSection.PartyList4CollisionNode:
            case XivHudNodeMap.HudSection.PartyList5CollisionNode:
            case XivHudNodeMap.HudSection.PartyList6CollisionNode:
            case XivHudNodeMap.HudSection.PartyList7CollisionNode:
            case XivHudNodeMap.HudSection.PartyList8CollisionNode:
            case XivHudNodeMap.HudSection.PartyList9CollisionNode:
                {
                    if (!this.configuration.EnableHpMpPings) { break; }

                    var partyMemberIndex = hudElement.HudSection - XivHudNodeMap.HudSection.PartyList1CollisionNode;
                    if (partyMemberIndex < AgentHUD.Instance()->PartyMemberCount)
                    {
                        var partyMember = AgentHUD.Instance()->PartyMembers[partyMemberIndex];
                        if (partyMember.Object == null) { break; }
                        var mousePosition = new Vector2(UIInputData.Instance()->CursorInputs.PositionX, UIInputData.Instance()->CursorInputs.PositionY);
                        // Check for HP node
                        var element = new XivHudNodeMap.HudElement(XivHudNodeMap.HudSection.PartyList1Hp + partyMemberIndex);
                        if (this.hudNodeMap.TryGetHudElementNode(element, out var hpNode) &&
                            IsPositionInNode(mousePosition, (AtkResNode*)hpNode))
                        {
                            info.ElementType = HudElementInfo.Type.Hp;
                            info.OwnerName = partyMember.Name.ExtractText();
                            info.IsOnSelf = partyMemberIndex == 0;
                            info.IsOnPartyMember = true;
                            info.Hp.Value = partyMember.Object->Health;
                            info.Hp.MaxValue = partyMember.Object->MaxHealth;
                            return true;
                        }
                        // Check for MP node
                        element = new XivHudNodeMap.HudElement(XivHudNodeMap.HudSection.PartyList1Mp + partyMemberIndex);
                        if (this.hudNodeMap.TryGetHudElementNode(element, out var mpNode) &&
                            IsPositionInNode(mousePosition, (AtkResNode*)mpNode))
                        {
                            info.ElementType = HudElementInfo.Type.Mp;
                            info.OwnerName = partyMember.Name.ExtractText();
                            info.IsOnSelf = partyMemberIndex == 0;
                            info.IsOnPartyMember = true;
                            info.Mp.Value = partyMember.Object->Mana;
                            info.Mp.MaxValue = partyMember.Object->MaxMana;
                            return true;
                        }
                    }
                }
                break;

            case XivHudNodeMap.HudSection.TargetHp:
                {
                    var targetId = AgentHUD.Instance()->CurrentTargetId;
                    var target = CharacterManager.Instance()->LookupBattleCharaByEntityId(targetId);
                    if (target != null)
                    {
                        info.ElementType = HudElementInfo.Type.Hp;
                        info.OwnerName = target->NameString;
                        info.IsOnSelf = targetId == GetLocalPlayerId();
                        info.IsOnPartyMember = target->IsPartyMember;
                        info.IsOnHostile = target->IsHostile;
                        info.Hp.Value = target->Health;
                        info.Hp.MaxValue = target->MaxHealth;
                        return true;
                    }
                }
                break;

            case XivHudNodeMap.HudSection.TargetStatus:
                {
                    var targetId = AgentHUD.Instance()->CurrentTargetId;
                    var target = CharacterManager.Instance()->LookupBattleCharaByEntityId(targetId);
                    if (target != null)
                    {
                        var statuses = target->StatusManager.Status;
                        if (TryGetStatus(statuses, StatusType.TargetStatus, hudElement.Index, out info.Status))
                        {
                            info.ElementType = HudElementInfo.Type.Status;
                            info.OwnerName = target->NameString;
                            info.IsOnSelf = targetId == GetLocalPlayerId();
                            info.IsOnPartyMember = target->IsPartyMember;
                            info.IsOnHostile = target->IsHostile;
                            return true;
                        }
                    }
                }
                break;
        }

        return false;
    }

    private bool TryGetStatus(Span<FFXIVClientStructs.FFXIV.Client.Game.Status> allStatuses, StatusType type, uint index,
        out Status status)
    {
        status = default;

        // Early out cases
        // Cannot search for a conditional enhancement if conditional enhancements are not enabled
        if (type == StatusType.SelfConditionalEnhancement && !this.hudNodeMap.IsConditionalEnhancementsEnabled()) { return false; }

        this.statuses.Clear();

        var isConditionalEnhancementsEnabled = this.hudNodeMap.IsConditionalEnhancementsEnabled();
        var isOwnEnhancementsPrioritized = this.hudNodeMap.IsOwnEnhancementsPrioritized();
        var isOthersEnhancementsDisplayedInOthers = this.hudNodeMap.IsOthersEnhancementsDisplayedInOthers();
        var localPlayerId = GetLocalPlayerId();

        // Fill status list with relevant statuses to sort
        foreach (var s in allStatuses)
        {
            if (s.StatusId == 0) { continue; }
            var luminaStatuses = this.dataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>(this.clientState.ClientLanguage);
            if (!luminaStatuses.TryGetRow(s.StatusId, out var luminaStatus)) { continue; }
            var statusInfo = new Status(luminaStatus)
            {
                SourceIsSelf = s.SourceId == localPlayerId,
                Stacks = s.Param,
            };

            if (!statusInfo.IsVisible) { continue; }

            // Intentionally putting switch inside foreach instead of outside for code clarity
            switch (type)
            {
                case StatusType.SelfEnhancement:
                    // Conditional Enhancements are treated as Enhancements if their Addon is disabled
                    if (!statusInfo.IsEnhancement &&
                        (!statusInfo.IsConditionalEnhancement || isConditionalEnhancementsEnabled))
                    {
                        continue;
                    }
                    // Enhancements applied by others are treated as Other if the HUD config option is set
                    if (isOthersEnhancementsDisplayedInOthers && !statusInfo.SourceIsSelf) { continue; }
                    break;

                case StatusType.SelfEnfeeblement:
                    if (!statusInfo.IsEnfeeblement) { continue; }
                    break;

                case StatusType.SelfOther:
                    // Enhancements applied by others are treated as Other if the HUD config option is set
                    if (!statusInfo.IsOther &&
                        (!isOthersEnhancementsDisplayedInOthers || statusInfo.SourceIsSelf))
                    {
                        continue;
                    }
                    break;

                case StatusType.SelfConditionalEnhancement:
                    if (!statusInfo.IsConditionalEnhancement) { continue; }
                    break;

                case StatusType.PartyListStatus:
                    // Other statuses are not displayed in the party list
                    if (statusInfo.IsOther) { continue; }
                    break;

                case StatusType.TargetStatus:
                    break;
            }

            statusInfo.RemainingTime = s.RemainingTime;
            this.statuses.Add(statusInfo);
        }

        IOrderedEnumerable<Status> sortedStatuses;
        if (type == StatusType.TargetStatus)
        {
            sortedStatuses = this.statuses.OrderByDescending(s => s.SourceIsSelf)
                .ThenByDescending(s => s.PartyListPriority);
        }
        else
        {
            sortedStatuses = this.statuses.OrderByDescending(s => s.PartyListPriority);
            if (isOwnEnhancementsPrioritized && type == StatusType.SelfEnhancement)
            {
                sortedStatuses = sortedStatuses.ThenByDescending(s => s.SourceIsSelf);
            }
            if (isOthersEnhancementsDisplayedInOthers && type == StatusType.SelfOther)
            {
                sortedStatuses = sortedStatuses.ThenBy(s => s.IsOther);
            }
        }

        var i = 0;
        foreach (var s in sortedStatuses)
        {
            if (index == i)
            {
                status = s;
                return true;
            }
            else
            {
                i++;
            }
        }

        return false;
    }

    private bool IsPositionInNode(Vector2 position, AtkResNode* node)
    {
        var xMin = node->ScreenX;
        var yMin = node->ScreenY;
        var xMax = xMin + node->Width;
        var yMax = yMin + node->Height;

        return position.X > xMin && position.X < xMax && position.Y > yMin && position.Y < yMax;
    }
}
