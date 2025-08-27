using Dalamud.Game.Command;
using SmartPings.UI.Presenter;

namespace SmartPings;

public sealed class CommandDispatcher(
    DalamudServices dalamud,
    MainWindowPresenter mainWindowPresenter) : IDalamudHook
{
    private const string commandName = "/smartpings";
    private const string commandNameAlt = "/sp";

    public void HookToDalamud()
    {
        dalamud.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the SmartPings window"
        });
        dalamud.CommandManager.AddHandler(commandNameAlt, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the SmartPings window"
        });
    }

    public void Dispose()
    {
        dalamud.CommandManager.RemoveHandler(commandName);
        dalamud.CommandManager.RemoveHandler(commandNameAlt);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just display our main ui
        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        mainWindowPresenter.View.Visible = true;
    }
}
