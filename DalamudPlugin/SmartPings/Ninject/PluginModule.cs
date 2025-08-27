using Dalamud.Interface.Windowing;
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
        // External Libraries (and taken code)
        Bind<DalamudServices>().ToSelf();
        Bind<Chat>().ToSelf().InSingletonScope();

        // Plugin classes
        Bind<Plugin>().ToSelf().InSingletonScope();
        Bind<IDalamudHook>().To<PluginUIContainer>().InSingletonScope();
        Bind<IDalamudHook>().To<CommandDispatcher>().InSingletonScope();
        Bind<InputEventSource>().ToSelf().InSingletonScope();
        Bind<KeyStateWrapper>().ToSelf().InSingletonScope();
        Bind<IAudioDeviceController, AudioDeviceController>().To<AudioDeviceController>().InSingletonScope();
        Bind<PingSounds>().ToSelf().InSingletonScope();
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
