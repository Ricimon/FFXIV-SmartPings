using Dalamud.Game;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace SmartPings;

public class DalamudServices
{
#pragma warning disable CA1822 // Mark members as static
    public IDalamudPluginInterface PluginInterface => PluginInitializer.PluginInterface;
    public ICommandManager CommandManager => PluginInitializer.CommandManager;
    public IGameInteropProvider GameInteropProvider => PluginInitializer.GameInteropProvider;
    public IClientState ClientState => PluginInitializer.ClientState;
    public IChatGui ChatGui => PluginInitializer.ChatGui;
    public ICondition Condition => PluginInitializer.Condition;
    public IDutyState DutyState => PluginInitializer.DutyState;
    public IDataManager DataManager => PluginInitializer.DataManager;
    public IObjectTable ObjectTable => PluginInitializer.ObjectTable;
    public IGameGui GameGui => PluginInitializer.GameGui;
    public IAddonEventManager AddonEventManager => PluginInitializer.AddonEventManager;
    public IAddonLifecycle AddonLifecycle => PluginInitializer.AddonLifecycle;
    public IFramework Framework => PluginInitializer.Framework;
    public ITextureProvider TextureProvider => PluginInitializer.TextureProvider;
    public IKeyState KeyState => PluginInitializer.KeyState;
    public ISigScanner SigScanner => PluginInitializer.SigScanner;
    public IPluginLog Log => PluginInitializer.Log;
#pragma warning restore CA1822 // Mark members as static
}
