using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;

namespace SimpleAether;

public sealed class ConfigWindow
{
    private const int BrowsePerRow = 10;
    private const int BrowseCount = 100;

    private readonly Config config;

    /// Debut de la planche d'icones ; 60550 tombe sur les marqueurs de carte.
    private int browseStart = 60550;

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

            var fieldIcon = (int)config.FieldIconId;
            if (ImGui.InputInt("Icone vents de terrain (0 = losange)", ref fieldIcon))
            {
                config.FieldIconId = (uint)Math.Max(0, fieldIcon);
                changed = true;
            }
            Preview(config.FieldIconId);

            var questIcon = (int)config.QuestIconId;
            if (ImGui.InputInt("Icone vents de quete", ref questIcon))
            {
                config.QuestIconId = (uint)Math.Max(0, questIcon);
                changed = true;
            }
            Preview(config.QuestIconId);

            ImGui.Separator();
            changed |= DrawBrowser();

            if (changed) config.Save();
        }
        ImGui.End();
        IsOpen = open;
    }

    /// <summary>
    /// Planche d'icones du jeu : les ids ne sont documentes nulle part, la seule facon
    /// fiable de trouver le bon marqueur est de le voir. Clic gauche = vents de terrain,
    /// clic droit = vents de quete.
    /// </summary>
    private bool DrawBrowser()
    {
        if (!ImGui.CollapsingHeader("Chercher une icone")) return false;

        ImGui.InputInt("Premier id", ref browseStart);
        browseStart = Math.Max(0, browseStart);
        ImGui.TextDisabled("Clic = terrain, clic droit = quete. Marqueurs carte vers 60000-60600.");

        var changed = false;
        for (var i = 0; i < BrowseCount; i++)
        {
            var id = (uint)(browseStart + i);
            var texture = Plugin.Icon(id);

            if (i % BrowsePerRow != 0) ImGui.SameLine();

            if (texture == null) { ImGui.Dummy(new Vector2(40f)); continue; }

            ImGui.Image(texture.Handle, new Vector2(40f));
            if (!ImGui.IsItemHovered()) continue;

            ImGui.SetTooltip(id.ToString());
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) { config.FieldIconId = id; changed = true; }
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) { config.QuestIconId = id; changed = true; }
        }
        return changed;
    }

    /// Apercu de l'icone saisie : sans ca, trouver le bon id revient a relancer le jeu
    /// une fois par essai.
    private static void Preview(uint iconId)
    {
        if (iconId == 0) { ImGui.TextDisabled("losange vert trace"); return; }

        var texture = Plugin.Icon(iconId);
        if (texture == null) { ImGui.TextDisabled($"icone {iconId} introuvable"); return; }

        ImGui.Image(texture.Handle, new Vector2(32f));
    }
}
