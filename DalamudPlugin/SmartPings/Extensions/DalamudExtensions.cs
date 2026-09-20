using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SmartPings.Extensions;

public static class DalamudExtensions
{
    public static string? GetPlayerFullName(this IPlayerCharacter playerCharacter)
    {
        string playerName = playerCharacter.Name.TextValue;
        var homeWorld = playerCharacter.HomeWorld;
        if (homeWorld.IsValid)
        {
            playerName += $"@{homeWorld.Value.Name.ExtractText()}";
        }

        return playerName;
    }

    public static string? GetLocalPlayerFullName(this IPlayerState playerState)
    {
        if (!playerState.IsLoaded) { return null; }

        string playerName = playerState.CharacterName;
        var homeWorld = playerState.HomeWorld;
        if (homeWorld.IsValid)
        {
            playerName += $"@{homeWorld.Value.Name.ExtractText()}";
        }

        return playerName;
    }

    public static unsafe ulong GetPlayerContentId(this IPlayerCharacter playerCharacter)
    {
        BattleChara* bChara = (BattleChara*)playerCharacter.Address;
        if (bChara == null) { return 0; }
        return bChara->ContentId;
    }

    public static IEnumerable<IPlayerCharacter> GetPlayers(this IObjectTable objectTable)
    {
        return objectTable.OfType<IPlayerCharacter>();
    }

    public static string GetResourcePath(this IDalamudPluginInterface pluginInterface, string fileName)
    {
        var resourcesDir = Path.Combine(pluginInterface.AssemblyLocation.Directory?.FullName!, "Resources");
        return Path.Combine(resourcesDir, fileName);
    }

    /// <summary>
    /// A different method to check for hostile status using nameplate color type
    /// </summary>
    public static bool IsHostile(this ref GameObject gameObject)
    {
        var plateType = gameObject.GetNamePlateColorType();

        // 4, 5, 6: Enemy players in PvP
        // 7: yellow, can be attacked, not engaged
        // 8: dead
        // 9: red, engaged with your party
        // 10: purple, engaged with other party
        // 11: orange, aggro'd to your party but not attacked yet
        return plateType >= 4 && plateType <= 11;
    }

    /// <summary>
    /// A different method to check for hostile status using nameplate color type
    /// </summary>
    public static unsafe bool IsHostile(this ref BattleChara battleChara)
    {
        fixed (BattleChara* bc = &battleChara)
        {
            var gameObject = (GameObject*)bc;
            if (gameObject == null) { return false; }
            return (*gameObject).IsHostile();
        }
    }
}
