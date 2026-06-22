using System;
using UnityEngine;

namespace TrapLib.Utilities;

/// <summary>Shared raycast helpers for placing traps against world surfaces.</summary>
public static class TrapPlacement
{
    public static T WithIgnoredSelfLayer<T>(GameObject go, Func<T> action)
    {
        if (go == null) return action();

        int previousLayer = go.layer;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        try { return action(); }
        finally { go.layer = previousLayer; }
    }

    public static bool TryFindNearestSurface(Vector3 pos, int mask, out RaycastHit2D best,
        float horizontalDistance = 10f, float upDistance = 10f, float downDistance = 12f,
        float fallbackHeight = 30f, float fallbackDistance = 60f)
    {
        var origin = pos + Vector3.up;
        for (int safety = 0; safety < 20 && Physics2D.OverlapPoint(origin, mask); safety++)
            origin.y += 0.5f;

        var hits = new[]
        {
            Physics2D.Raycast(origin, Vector2.left, horizontalDistance, mask),
            Physics2D.Raycast(origin, Vector2.right, horizontalDistance, mask),
            Physics2D.Raycast(origin, Vector2.up, upDistance, mask),
            Physics2D.Raycast(origin, Vector2.down, downDistance, mask),
        };

        best = default;
        float bestDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit && hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                best = hit;
            }
        }

        if (!best)
            best = Physics2D.Raycast(pos + Vector3.up * fallbackHeight, Vector2.down, fallbackDistance, mask);

        return best;
    }

    public static bool TryFindFloorThenNearestSurface(Vector3 pos, int mask, out RaycastHit2D best,
        float floorDistance = 12f, float nearestDistance = 8f)
    {
        var origin = pos + Vector3.up;
        for (int safety = 0; safety < 20 && Physics2D.OverlapPoint(origin, mask); safety++)
            origin.y += 0.5f;

        best = Physics2D.Raycast(origin, Vector2.down, floorDistance, mask);
        if (best) return true;

        var hits = new[]
        {
            Physics2D.Raycast(pos, Vector2.down, nearestDistance, mask),
            Physics2D.Raycast(pos, Vector2.left, nearestDistance, mask),
            Physics2D.Raycast(pos, Vector2.right, nearestDistance, mask),
            Physics2D.Raycast(pos, Vector2.up, nearestDistance, mask),
        };

        float bestDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit && hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                best = hit;
            }
        }
        return best;
    }

    public static Vector3 OffsetFromSurface(RaycastHit2D hit, float offset, float z)
    {
        var surfacePos = hit.point + hit.normal * offset;
        return new Vector3(surfacePos.x, surfacePos.y, z);
    }

    public static float VerticalSpriteHalfHeight(SpriteRenderer sr, float scale)
    {
        if (sr?.sprite == null) return 0f;
        return sr.sprite.rect.height / sr.sprite.pixelsPerUnit * scale * 0.5f;
    }
}
