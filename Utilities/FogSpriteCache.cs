using UnityEngine;

namespace TrapLib.Utilities;

/// <summary>Cached radial fog sprite — shared by all zone types.</summary>
public static class FogSpriteCache
{
    private static Sprite _cached;

    public static Sprite Get()
    {
        if (_cached != null) return _cached;
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        float center = size * 0.5f;
        float maxDist = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Pow(1f - Mathf.Clamp01(dist / maxDist), 0.7f) * 0.8f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return _cached = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 8f);
    }
}
