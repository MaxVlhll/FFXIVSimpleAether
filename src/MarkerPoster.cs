using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace SimpleAether;

/// <summary>
/// Seul endroit qui touche AgentMap. D'autres fichiers dependent aussi de
/// structures internes du jeu (PlayerState/QuestManager dans CurrentResolver,
/// GetAddonByName/AtkUnitBasePtr dans Plugin) et peuvent tout autant casser a
/// un patch ; seul l'acces a AgentMap est isole ici.
/// </summary>
public sealed class MarkerPoster
{
    /// Le jeu ne garde que 12 marqueurs temporaires (FixedSizeArray12<TempMapMarker>).
    private const int MaxMarkers = 12;

    private bool hasPosted;

    /// True si le dernier appel a Post/PostCalibrationMarker a effectivement pose un marqueur.
    public bool HasPosted => hasPosted;

    public unsafe void Clear()
    {
        if (!hasPosted) return;
        var agent = AgentMap.Instance();
        if (agent == null) return;

        // Le jeu n'expose pas de retrait unitaire : on purge tout puis on repose.
        agent->ResetMapMarkers();
        agent->ResetMiniMapMarkers();
        hasPosted = false;
    }

    /// <returns>true si au moins un marqueur a ete pose.</returns>
    public unsafe bool Post(IReadOnlyList<AetherTarget> targets,
        ushort sizeFactor, short offsetX, short offsetY,
        uint fieldIconId, uint questIconId, int radius)
    {
        Clear();

        var agent = AgentMap.Instance();
        if (agent == null || targets.Count == 0) return false;

        // Les vents de terrain passent avant les vents de quete quand il faut trancher.
        var ordered = targets.OrderBy(t => t.Kind == TargetKind.Field ? 0 : 1).ToList();
        if (ordered.Count > MaxMarkers)
        {
            Plugin.Log.Information(
                $"{ordered.Count} cibles pour {MaxMarkers} emplacements : " +
                $"{ordered.Count - MaxMarkers} marqueur(s) non pose(s).");
        }

        var count = Math.Min(ordered.Count, MaxMarkers);
        for (var i = 0; i < count; i++)
        {
            var t = ordered[i];
            var iconId = t.Kind == TargetKind.Field ? fieldIconId : questIconId;
            Add(agent, t.WorldPos, sizeFactor, offsetX, offsetY, iconId, radius, t.Label);
        }
        hasPosted = true;
        return true;
    }

    /// <summary>
    /// Pose un marqueur unique a la position du joueur. Sert a calibrer l'unite
    /// attendue par AddGatheringTempMarker, que la signature ne precise pas.
    /// Prend l'icone et le rayon en parametres pour passer exactement par la meme
    /// conversion que Post, via le meme helper Add.
    /// </summary>
    public unsafe void PostCalibrationMarker(Vector3 playerWorldPos,
        ushort sizeFactor, short offsetX, short offsetY, uint iconId, int radius)
    {
        Clear();
        var agent = AgentMap.Instance();
        if (agent == null) return;

        var coord = MapCoords.WorldToMap(playerWorldPos, sizeFactor, offsetX, offsetY);
        Plugin.Log.Information(
            $"calibration : monde=({playerWorldPos.X:0.0}, {playerWorldPos.Z:0.0}) " +
            $"carte=({coord.X:0.0}, {coord.Y:0.0}) envoye=({(int)MathF.Round(coord.X)}, {(int)MathF.Round(coord.Y)})");
        Add(agent, playerWorldPos, sizeFactor, offsetX, offsetY, iconId, radius, "calibration");
        hasPosted = true;
    }

    /// Seul point d'appel a AddGatheringTempMarker : Post et PostCalibrationMarker
    /// passent tous les deux par ici, donc utilisent toujours la meme conversion.
    private static unsafe void Add(AgentMap* agent, Vector3 worldPos,
        ushort sizeFactor, short offsetX, short offsetY,
        uint iconId, int radius, string tooltip)
    {
        var coord = MapCoords.WorldToMap(worldPos, sizeFactor, offsetX, offsetY);
        agent->AddGatheringTempMarker(
            (int)MathF.Round(coord.X), (int)MathF.Round(coord.Y),
            radius, iconId, 4, tooltip);
    }
}
