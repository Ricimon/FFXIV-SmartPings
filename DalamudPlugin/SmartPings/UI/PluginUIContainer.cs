using Dalamud.Interface.Windowing;
using SmartPings.UI.Presenter;

namespace SmartPings.UI;

// It is good to have this be disposable in general, in case you ever need it
// to do any cleanup
public sealed class PluginUIContainer : IDalamudHook
{
    private readonly DalamudServices dalamud;
    private readonly IPluginUIPresenter[] pluginUIPresenters;
    private readonly MainWindowPresenter mainWindowPresenter;
    private readonly WindowSystem windowSystem;

    public PluginUIContainer(
        DalamudServices dalamud,
        IPluginUIPresenter[] pluginUIPresenters,
        MainWindowPresenter mainWindowPresenter,
        WindowSystem windowSystem)
    {
        this.dalamud = dalamud;
        this.pluginUIPresenters = pluginUIPresenters;
        this.mainWindowPresenter = mainWindowPresenter;
        this.windowSystem = windowSystem;

        foreach (var pluginUIPresenter in this.pluginUIPresenters)
        {
            pluginUIPresenter.SetupBindings();
        }
    }

    public void Dispose()
    {
        this.dalamud.PluginInterface.UiBuilder.Draw -= Draw;
        this.dalamud.PluginInterface.UiBuilder.OpenMainUi -= ShowMainWindow;
    }

    public void HookToDalamud()
    {
        this.dalamud.PluginInterface.UiBuilder.Draw += Draw;
        this.dalamud.PluginInterface.UiBuilder.OpenMainUi += ShowMainWindow;
    }

    public void Draw()
    {
        // This is our only draw handler attached to UIBuilder, so it needs to be
        // able to draw any windows we might have open.
        // Each method checks its own visibility/state to ensure it only draws when
        // it actually makes sense.
        // There are other ways to do this, but it is generally best to keep the number of
        // draw delegates as low as possible.

        foreach (var pluginUIPresenter in this.pluginUIPresenters)
        {
            pluginUIPresenter.View.Draw();
        }
    }

    private void ShowMainWindow()
    {
        this.mainWindowPresenter.View.Visible = true;
    }
}
