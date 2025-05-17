﻿using Dalamud.Game;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using Ninject.Activation;
using Ninject.Modules;
using SmartPings.Audio;
using SmartPings.Data;
using SmartPings.Input;
using SmartPings.Log;
using SmartPings.Network;
using SmartPings.UI;
using SmartPings.UI.Presenter;
using SmartPings.UI.View;

namespace SmartPings.Ninject;

public class PluginModule : NinjectModule
{
    public override void Load()
    {
        // Dalamud services
        Bind<IDalamudPluginInterface>().ToConstant(PluginInitializer.PluginInterface).InTransientScope();
        Bind<ICommandManager>().ToConstant(PluginInitializer.CommandManager).InTransientScope();
        Bind<IGameInteropProvider>().ToConstant(PluginInitializer.GameInteropProvider).InTransientScope();
        Bind<IChatGui>().ToConstant(PluginInitializer.ChatGui).InTransientScope();
        Bind<IClientState>().ToConstant(PluginInitializer.ClientState).InTransientScope();
        Bind<ICondition>().ToConstant(PluginInitializer.Condition).InTransientScope();
        Bind<IDutyState>().ToConstant(PluginInitializer.DutyState).InTransientScope();
        Bind<IDataManager>().ToConstant(PluginInitializer.DataManager).InTransientScope();
        Bind<IObjectTable>().ToConstant(PluginInitializer.ObjectTable).InTransientScope();
        Bind<IGameGui>().ToConstant(PluginInitializer.GameGui).InTransientScope();
        Bind<IAddonEventManager>().ToConstant(PluginInitializer.AddonEventManager).InTransientScope();
        Bind<IAddonLifecycle>().ToConstant(PluginInitializer.AddonLifecycle).InTransientScope();
        Bind<IFramework>().ToConstant(PluginInitializer.Framework).InTransientScope();
        Bind<ITextureProvider>().ToConstant(PluginInitializer.TextureProvider).InTransientScope();
        Bind<IKeyState>().ToConstant(PluginInitializer.KeyState).InTransientScope();
        Bind<ISigScanner>().ToConstant(PluginInitializer.SigScanner).InTransientScope();
        Bind<IPluginLog>().ToConstant(PluginInitializer.Log).InTransientScope();

        // External Libraries (and taken code)
        Bind<Chat>().ToSelf().InSingletonScope();

        // Plugin classes
        Bind<Plugin>().ToSelf().InSingletonScope();
        Bind<IDalamudHook>().To<PluginUIContainer>().InSingletonScope();
        Bind<IDalamudHook>().To<CommandDispatcher>().InSingletonScope();
        Bind<InputEventSource>().ToSelf().InSingletonScope();
        Bind<KeyStateWrapper>().ToSelf().InSingletonScope();
        Bind<IAudioDeviceController, AudioDeviceController>().To<AudioDeviceController>().InSingletonScope();
        Bind<ServerConnection>().ToSelf().InSingletonScope();
        Bind<Spatializer>().ToSelf().InSingletonScope();
        Bind<MapManager>().ToSelf().InSingletonScope();
        Bind<GuiPingHandler>().ToSelf().InSingletonScope();
        Bind<XivHudNodeMap>().ToSelf().InSingletonScope();

        // Views and Presenters
        Bind<WindowSystem>().ToMethod(_ => new(PluginInitializer.Name)).InSingletonScope();
        Bind<IPluginUIView, GroundPingView>().To<GroundPingView>().InSingletonScope();
        Bind<IPluginUIPresenter, GroundPingPresenter>().To<GroundPingPresenter>().InSingletonScope();
        Bind<IPluginUIView, MainWindow>().To<MainWindow>().InSingletonScope();
        Bind<IPluginUIPresenter, MainWindowPresenter>().To<MainWindowPresenter>().InSingletonScope();
        Bind<IPluginUIView, ConfigWindow>().To<ConfigWindow>().InSingletonScope();
        Bind<IPluginUIPresenter, ConfigWindowPresenter>().To<ConfigWindowPresenter>().InSingletonScope();

        // Data
        Bind<Configuration>().ToMethod(GetConfiguration).InSingletonScope();

        Bind<ILogger>().To<DalamudLogger>();
        Bind<DalamudLoggerFactory>().ToSelf();
    }

    private Configuration GetConfiguration(IContext context)
    {
        var configuration = 
            PluginInitializer.PluginInterface.GetPluginConfig() as Configuration
            ?? new Configuration();
        configuration.Initialize(PluginInitializer.PluginInterface);
        return configuration;
    }
}
