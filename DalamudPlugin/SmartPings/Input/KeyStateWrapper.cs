using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;

namespace SmartPings.Input;

public sealed class KeyStateWrapper : IKeyState, IDisposable
{
    public event Action<VirtualKey>? OnKeyUp;
    public event Action<VirtualKey>? OnKeyDown;

    private readonly DalamudServices dalamud;

    private readonly Dictionary<VirtualKey, bool> keyStates = [];

    public bool this[VirtualKey vkCode] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public bool this[int vkCode] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public KeyStateWrapper(DalamudServices dalamud)
    {
        this.dalamud = dalamud;

        this.dalamud.Framework.Update += OnFrameworkUpdate;
    }

    public int GetRawValue(int vkCode)
    {
        return this.dalamud.KeyState.GetRawValue(vkCode);
    }

    public int GetRawValue(VirtualKey vkCode)
    {
        return this.dalamud.KeyState.GetRawValue(vkCode);
    }

    public void SetRawValue(int vkCode, int value)
    {
        this.dalamud.KeyState.SetRawValue(vkCode, value);
    }

    public void SetRawValue(VirtualKey vkCode, int value)
    {
        this.dalamud.KeyState.SetRawValue(vkCode, value);
    }

    public bool IsVirtualKeyValid(int vkCode)
    {
        return this.dalamud.KeyState.IsVirtualKeyValid(vkCode);
    }

    public bool IsVirtualKeyValid(VirtualKey vkCode)
    {
        return this.dalamud.KeyState.IsVirtualKeyValid(vkCode);
    }

    public IEnumerable<VirtualKey> GetValidVirtualKeys()
    {
        return this.dalamud.KeyState.GetValidVirtualKeys();
    }

    public void ClearAll()
    {
        this.dalamud.KeyState.ClearAll();
    }

    public void Dispose()
    {
        this.dalamud.Framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        foreach (var key in this.dalamud.KeyState.GetValidVirtualKeys())
        {
            var keyState = this.dalamud.KeyState.GetRawValue(key) != 0;
            if (!this.keyStates.TryGetValue(key, out var oldState))
            {
                this.keyStates.Add(key, keyState);
            }
            else
            {
                if (oldState != keyState)
                {
                    this.keyStates[key] = keyState;
                    if (keyState)
                    {
                        this.OnKeyDown?.Invoke(key);
                    }
                    else
                    {
                        this.OnKeyUp?.Invoke(key);
                    }
                }
            }
        }
    }
}
