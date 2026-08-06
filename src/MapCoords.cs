using System;
using System.Numerics;

namespace SimpleAether;

public static class MapCoords
{
    public static Vector2 WorldToMap(Vector3 worldPos, ushort sizeFactor, short offsetX, short offsetY)
        => new(Axis(worldPos.X, sizeFactor, offsetX),
               Axis(worldPos.Z, sizeFactor, offsetY));

    private static float Axis(float value, ushort sizeFactor, short offset)
    {
        var scale = sizeFactor / 100f;
        return 41f * ((MathF.Truncate(value) + offset) * scale + 1024f - 1) / 2048f / scale + 1;
    }
}
