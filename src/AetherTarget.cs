using System.Numerics;

namespace SimpleAether;

public enum TargetKind
{
    /// Vent pose dans le monde, a aller toucher.
    Field,

    /// Vent donne en recompense de quete : la cible est le PNJ donneur.
    Quest,
}

public readonly record struct AetherTarget(
    Vector3 WorldPos,
    TargetKind Kind,
    uint AetherCurrentId,
    string Label);
