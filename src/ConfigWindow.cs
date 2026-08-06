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
            ImGui.TextWrapped(
                "Le jeu limite a 12 marqueurs. Decocher les vents de quete libere des emplacements.");
            ImGui.Separator();

            var fieldIcon = (int)config.FieldIconId;
            if (ImGui.InputInt("Icone vents de terrain", ref fieldIcon))
            {
                config.FieldIconId = (uint)Math.Max(0, fieldIcon);
                changed = true;
            }

            var questIcon = (int)config.QuestIconId;
            if (ImGui.InputInt("Icone vents de quete", ref questIcon))
            {
                config.QuestIconId = (uint)Math.Max(0, questIcon);
                changed = true;
            }

            changed |= ImGui.SliderInt("Rayon du marqueur", ref config.MarkerRadius, 10, 100);

            if (changed) config.Save();
        }
        ImGui.End();
        IsOpen = open;
    }
}
