using System;
using Dalamud.Bindings.ImGui;

namespace SimpleAether;

public sealed class ConfigWindow
{
    private readonly Config config;

    public bool IsOpen;

    public ConfigWindow(Config config) => this.config = config;

    public void Draw()
    {
        if (!IsOpen) return;

        var open = IsOpen;
        if (ImGui.Begin("SimpleAether", ref open))
        {
            var changed = false;

            changed |= ImGui.Checkbox("Activer le plugin", ref config.Enabled);
            changed |= ImGui.Checkbox("Marquer les vents de terrain", ref config.ShowFieldCurrents);
            changed |= ImGui.Checkbox("Marquer les vents de quete", ref config.ShowQuestCurrents);

            ImGui.Separator();

            var questIcon = (int)config.QuestIconId;
            if (ImGui.InputInt("Icone vents de quete", ref questIcon))
            {
                config.QuestIconId = (uint)Math.Max(0, questIcon);
                changed = true;
            }

            if (changed) config.Save();
        }
        ImGui.End();
        IsOpen = open;
    }
}
