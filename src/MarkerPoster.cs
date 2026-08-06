using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace SimpleAether;

/// <summary>
/// Seul endroit qui touche AgentMap. Si l'API bouge a un patch, c'est ici et
/// nulle part ailleurs.
/// </summary>
public sealed class MarkerPoster
{
    /// Le jeu ne garde que 12 marqueurs temporaires (FixedSizeArray12<TempMapMarker>).
    private const int MaxMarkers = 12;

    private bool hasPosted;

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

    public unsafe void Post(IReadOnlyList<AetherTarget> targets,
        ushort sizeFactor, short offsetX, short offsetY,
        uint fieldIconId, uint questIconId, int radius)
    {
        Clear();

        var agent = AgentMap.Instance();
        if (agent == null || targets.Count == 0) return;

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
            var coord = MapCoords.WorldToMap(t.WorldPos, sizeFactor, offsetX, offsetY);
            var iconId = t.Kind == TargetKind.Field ? fieldIconId : questIconId;
            agent->AddGatheringTempMarker(
                (int)MathF.Round(coord.X), (int)MathF.Round(coord.Y),
                radius, iconId, 4, t.Label);
        }
        hasPosted = true;
    }

    /// <summary>
    /// Pose un marqueur unique a la position du joueur. Sert a calibrer l'unite
    /// attendue par AddGatheringTempMarker, que la signature ne precise pas.
    /// </summary>
    public unsafe void PostCalibrationMarker(Vector3 playerWorldPos,
        ushort sizeFactor, short offsetX, short offsetY)
    {
        Clear();
        var agent = AgentMap.Instance();
        if (agent == null) return;

        var coord = MapCoords.WorldToMap(playerWorldPos, sizeFactor, offsetX, offsetY);
        Plugin.Log.Information(
            $"calibration : monde=({playerWorldPos.X:0.0}, {playerWorldPos.Z:0.0}) " +
            $"carte=({coord.X:0.0}, {coord.Y:0.0}) envoye=({(int)MathF.Round(coord.X)}, {(int)MathF.Round(coord.Y)})");
        agent->AddGatheringTempMarker(
            (int)MathF.Round(coord.X), (int)MathF.Round(coord.Y), 30, 60561, 4, "calibration");
        hasPosted = true;
    }
}
