using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;

namespace SimpleAether;

public sealed class ConfigWindow : Window
{
    private const int BrowsePerRow = 10;
    private const int BrowseCount = 100;

    private readonly Config config;

    /// Debut de la planche d'icones ; 60550 tombe sur les marqueurs de carte.
    private int browseStart = 60550;

    public ConfigWindow(Config config) : base("SimpleAether###SimpleAetherConfig")
    {
        this.config = config;
    }

    public override void Draw()
    {
        var changed = false;

        changed |= ImGui.Checkbox("Enable plugin", ref config.Enabled);
        changed |= ImGui.Checkbox("Mark field aether currents", ref config.ShowFieldCurrents);
        changed |= ImGui.Checkbox("Mark quest aether currents", ref config.ShowQuestCurrents);

        ImGui.Separator();

        var fieldIcon = (int)config.FieldIconId;
        if (ImGui.InputInt("Field current icon (0 = diamond)", ref fieldIcon))
        {
            config.FieldIconId = (uint)Math.Max(0, fieldIcon);
            changed = true;
        }
        Preview(config.FieldIconId);

        var questIcon = (int)config.QuestIconId;
        if (ImGui.InputInt("Quest current icon", ref questIcon))
        {
            config.QuestIconId = (uint)Math.Max(0, questIcon);
            changed = true;
        }
        Preview(config.QuestIconId);

        changed |= ImGui.SliderFloat(
            "Quest marker vertical offset", ref config.QuestOffsetY, -64f, 64f, "%.0f px");

        ImGui.Separator();
        changed |= DrawBrowser();

        if (changed) config.Save();
    }

    /// <summary>
    /// Planche d'icones du jeu : les ids ne sont documentes nulle part, la seule facon
    /// fiable de trouver le bon marqueur est de le voir. Clic gauche = vents de terrain,
    /// clic droit = vents de quete.
    /// </summary>
    private bool DrawBrowser()
    {
        if (!ImGui.CollapsingHeader("Browse icons")) return false;

        ImGui.InputInt("First id", ref browseStart);
        browseStart = Math.Max(0, browseStart);
        ImGui.TextDisabled("Left click = field, right click = quest. Map markers are around 60000-60600.");

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
        if (iconId == 0) { ImGui.TextDisabled("drawn green diamond"); return; }

        var texture = Plugin.Icon(iconId);
        if (texture == null) { ImGui.TextDisabled($"icon {iconId} not found"); return; }

        ImGui.Image(texture.Handle, new Vector2(32f));
    }
}
