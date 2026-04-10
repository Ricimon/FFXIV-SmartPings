using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using SmartPings.UI.Presenter;

namespace SmartPings.UI.View;

public class AdvancedConfigWindow(Configuration configuration) : IPluginUIPresenter, IPluginUIView
{
    public IPluginUIView View => this;

    // this extra bool exists for ImGui, since you can't ref a property
    private bool visible = false;
    public bool Visible
    {
        get => this.visible;
        set => this.visible = value;
    }

    private readonly string windowName = $"{PluginInitializer.Name} Advanced Config##AdvancedConfig";

    public void SetupBindings()
    {
    }

    public void Draw()
    {
        if (!Visible) { return; }

        ImGui.SetNextWindowSize(Vector2.Zero, ImGuiCond.Always);
        var width = Math.Min(ImGui.GetMainViewport().Size.X / ImGuiHelpers.GlobalScale / 2, 250);
        ImGui.SetNextWindowSizeConstraints(
            new(width, 150),
            new(2 * width, 300));
        if (ImGui.Begin(this.windowName, ref this.visible, ImGuiWindowFlags.NoCollapse))
        {
            DrawContents();
        }
        ImGui.End();
    }

    private void DrawContents()
    {
        var enableOnlyInCombat = configuration.OnlyEnableInCombat;
        if (ImGui.Checkbox("Enable only in combat", ref enableOnlyInCombat))
        {
            configuration.OnlyEnableInCombat = enableOnlyInCombat;
            configuration.Save();
        }

        var pingKeybindBlocksGameInput = configuration.PingKeybindBlocksGameInput;
        if (ImGui.Checkbox("Ping keybind blocks game input", ref pingKeybindBlocksGameInput))
        {
            configuration.PingKeybindBlocksGameInput = pingKeybindBlocksGameInput;
            configuration.Save();
        }

        var y = ImGui.GetWindowHeight() - 1.5f * ImGui.GetTextLineHeightWithSpacing() - ImGui.GetStyle().WindowPadding.Y;
        ImGui.SetCursorPos(new(ImGui.GetCursorPosX(), y));
        if (ImGui.Button("Close",
            new Vector2(ImGui.GetWindowWidth() - 2.5f * ImGui.GetStyle().WindowPadding.X, 0)))
        {
            this.visible = false;
        }
    }
}
