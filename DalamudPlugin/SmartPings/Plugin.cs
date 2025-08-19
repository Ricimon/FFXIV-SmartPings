using System.Collections.Generic;
using Dalamud.Plugin;
using SmartPings.Audio;
using SmartPings.Log;

namespace SmartPings;

public class Plugin(
    IDalamudPluginInterface pluginInterface,
    IEnumerable<IDalamudHook> dalamudHooks,
    IAudioDeviceController audioDeviceController,
    Spatializer spatializer,
    ILogger logger)
{
    private IDalamudPluginInterface PluginInterface { get; init; } = pluginInterface;
    private IEnumerable<IDalamudHook> DalamudHooks { get; init; } = dalamudHooks;
    private IAudioDeviceController AudioDeviceController { get; init; } = audioDeviceController;
    private Spatializer Spatializer { get; init; } = spatializer;
    private ILogger Logger { get; init; } = logger;

    public void Initialize()
    {
        foreach (var dalamudHook in this.DalamudHooks)
        {
            dalamudHook.HookToDalamud();
        }

        this.AudioDeviceController.AudioPlaybackIsRequested = true;
        this.Spatializer.StartUpdateLoop();

        Logger.Info("SmartPings initialized");
    }
}
