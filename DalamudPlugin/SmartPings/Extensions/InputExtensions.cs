using Dalamud.Game.ClientState.Keys;
using WindowsInput.Events;

namespace SmartPings.Extensions;

public static class InputExtensions
{
    public static bool IsMouseButton(this KeyCode keyCode)
    {
        return keyCode switch
        {
            KeyCode.LButton or
            KeyCode.RButton or
            KeyCode.MButton or
            KeyCode.XButton1 or
            KeyCode.XButton2 => true,
            _ => false,
        };
    }

    public static VirtualKey ToVirtualKey(this KeyCode keyCode)
    {
        // This method is here in case any exceptions exist
        return (VirtualKey)keyCode;
    }
}
