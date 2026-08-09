using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace SimpleAether;

/// <summary>
/// Dessine les pastilles par-dessus l'addon carte, au lieu de passer par les marqueurs
/// temporaires du jeu. Ceux-ci imposaient trois choses dont on ne voulait pas : un
/// recadrage de la vue a chaque redessin, un plafond de 6 cibles, et le mode carte
/// bascule en "recherche de recolte".
///
/// La conversion monde -> ecran est un delta plutot qu'une formule absolue : le pin du
/// joueur donne l'origine ecran, et sa position monde est lue dans le meme struct que
/// l'echelle. Zoom, panoramique et deplacement de la fenetre sont donc absorbes sans
/// que ce fichier ait a les connaitre.
/// </summary>
public sealed class MapOverlay
{
    private const float IconSize = 28f;

    private readonly Config config;

    /// Alimente par le tick ; lu a chaque frame de rendu.
    public IReadOnlyList<AetherTarget> Targets = [];

    public MapOverlay(Config config) => this.config = config;

    public unsafe void Draw()
    {
        if (!config.Enabled || Targets.Count == 0) return;

        var ptr = Plugin.GameGui.GetAddonByName("AreaMap");
        if (ptr.IsNull || !ptr.IsVisible) return;

        var agent = AgentMap.Instance();
        if (agent == null) return;

        // Carte d'une autre zone affichee : le pin du joueur ne s'y trouve pas, donc
        // ni origine ni sens a nos positions.
        if (agent->SelectedMapId != agent->CurrentMapId) return;

        var addon = (AddonAreaMap*)ptr.Address;
        var pin = addon->AreaMap.Atk2DMap.PlayerPin;
        if (pin == null) return;

        var pinNode = &pin->AtkResNode;
        var origin = new Vector2(
            pinNode->ScreenX + pinNode->Width * pinNode->ScaleX / 2f,
            pinNode->ScreenY + pinNode->Height * pinNode->ScaleY / 2f);

        // PlayerMarkerX/Y est en coordonnees MONDE (mesure : identique a la position du
        // joueur au dixieme pres), pas en pixels.
        var playerWorld = new Vector2(addon->AreaMap.PlayerMarkerX, addon->AreaMap.PlayerMarkerY);
        var scale = agent->CurrentMapSizeFactor / 100f * addon->AreaMap.MapScale;
        if (scale <= 0f) return;

        var clip = ClipRect(addon);
        var draw = ImGui.GetBackgroundDrawList();
        draw.PushClipRect(clip.Min, clip.Max, true);

        var mouse = ImGui.GetMousePos();
        string? hovered = null;

        foreach (var t in Targets)
        {
            var pos = origin + (new Vector2(t.WorldPos.X, t.WorldPos.Z) - playerWorld) * scale;
            var half = new Vector2(IconSize / 2f);
            var min = pos - half;
            var max = pos + half;

            var iconId = IconFor(t, config);
            if (iconId == 0)
            {
                DrawDiamond(draw, pos, IconSize / 2f);
            }
            else
            {
                var texture = Plugin.Icon(iconId);
                if (texture != null)
                    draw.AddImage(texture.Handle, min, max);
            }

            if (mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y
                && mouse.X >= clip.Min.X && mouse.X <= clip.Max.X
                && mouse.Y >= clip.Min.Y && mouse.Y <= clip.Max.Y)
            {
                hovered = t.Label;
            }
        }

        draw.PopClipRect();

        if (hovered != null)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(hovered);
            ImGui.EndTooltip();
        }
    }

    /// <summary>
    /// Le losange de l'interface des vents ethers n'est pas une icone indexee mais de
    /// l'art embarque dans une texture d'UI : pas d'id a passer a GetFromGameIcon.
    /// On le trace donc, ce qui donne en prime une forme nette a tout zoom.
    /// </summary>
    private static void DrawDiamond(ImDrawListPtr draw, Vector2 center, float radius)
    {
        var top = new Vector2(center.X, center.Y - radius);
        var right = new Vector2(center.X + radius, center.Y);
        var bottom = new Vector2(center.X, center.Y + radius);
        var left = new Vector2(center.X - radius, center.Y);

        // ImGui empile les couleurs en ABGR, pas en RGBA.
        draw.AddQuadFilled(top, right, bottom, left, 0xFF64D94C);
        draw.AddQuad(top, right, bottom, left, 0xC8102010, 2f);
    }

    /// Icone effective d'une cible, 0 = losange trace a la main.
    public static uint IconFor(AetherTarget t, Config config) =>
        t.Kind == TargetKind.Field ? config.FieldIconId : config.QuestIconId;

    /// Rogne au conteneur de la carte : sans ca les pastilles debordent sur le cadre
    /// de la fenetre et sur le reste de l'ecran des qu'on zoome.
    private static unsafe (Vector2 Min, Vector2 Max) ClipRect(AddonAreaMap* addon)
    {
        var owner = addon->ComponentMap == null ? null : addon->ComponentMap->OwnerNode;
        if (owner == null) return (Vector2.Zero, new Vector2(float.MaxValue));

        var node = &owner->AtkResNode;
        var min = new Vector2(node->ScreenX, node->ScreenY);
        var max = min + new Vector2(node->Width * node->ScaleX, node->Height * node->ScaleY);
        return (min, max);
    }
}
