using System.Collections.Generic;
using SmartPings.Extensions;

namespace SmartPings.Audio;

public class PingSounds
{
    public enum Pack
    {
        Default = 0,
        LeagueOfLegends = 1,
    }

    private readonly Dictionary<Pack, Dictionary<GroundPing.Type, CachedSound>> sounds;

    public PingSounds(DalamudServices dalamud)
    {
        sounds = new()
        {
            { Pack.Default,
                new()
                {
                    { GroundPing.Type.Basic, new(dalamud.PluginInterface.GetResourcePath("basic_ping.wav")) },
                }
            },
            { Pack.LeagueOfLegends,
                new()
                {
                    { GroundPing.Type.Basic, new(dalamud.PluginInterface.GetResourcePath("lol_generic_ping.wav")) },
                    { GroundPing.Type.Question, new(dalamud.PluginInterface.GetResourcePath("lol_missing_ping.wav")) },
                    { GroundPing.Type.Danger, new(dalamud.PluginInterface.GetResourcePath("lol_retreat_ping.wav")) },
                    { GroundPing.Type.Assist, new(dalamud.PluginInterface.GetResourcePath("lol_assist_ping.wav")) },
                }
            },
        };
    }

    public bool TryGetSound(Pack pack, GroundPing.Type type, out CachedSound sound)
    {
        if (this.sounds.TryGetValue(pack, out var packSounds) &&
            packSounds.TryGetValue(type, out sound!))
        {
            return true;
        }
        sound = null!;
        return false;
    }
}
