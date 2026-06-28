using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TrapLib.Utilities;

/// <summary>
/// Sprite loading and sizing utilities. Developers can use these or provide sprites
/// through any other means (AssetBundle, Resources, etc.).
/// </summary>
public static class SpriteLoader
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static readonly Queue<string> CacheOrder = new Queue<string>();
    private const int MaxCacheSize = 128;

    // ---- Basic loading ----

    /// <summary>
    /// Load a PNG from disk and create a sprite from the full image (no cropping).
    /// Cached by path + ppu.
    /// </summary>
    public static Sprite FromFile(string path, float ppu = 8f)
    {
        return FromFile(path, ppu, new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Load a PNG from disk and create a sprite from the full image with a custom pivot.
    /// Cached by path + ppu + pivot.
    /// </summary>
    public static Sprite FromFile(string path, float ppu, Vector2 pivot)
    {
        var key = $"{path}@{ppu}@{pivot.x:F3},{pivot.y:F3}";
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var tex = LoadTexture(path);
        if (tex == null) return null;

        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu);

        if (Cache.Count >= MaxCacheSize)
        {
            var oldest = CacheOrder.Dequeue();
            Cache.Remove(oldest);
        }

        Cache[key] = sprite;
        CacheOrder.Enqueue(key);
        return sprite;
    }

    /// <summary>
    /// Load a PNG, auto-crop transparent borders, and create a sprite with a custom pivot.
    /// Cached by path + ppu + pivot.
    /// </summary>
    public static Sprite FromFileAutoCrop(string path, float ppu, Vector2 pivot, byte alphaThreshold = 1)
    {
        var key = $"crop:{path}@{ppu}@{pivot.x:F3},{pivot.y:F3}@a{alphaThreshold}";
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var tex = LoadTexture(path);
        if (tex == null) return null;

        var rect = GetContentRect(tex, alphaThreshold) ?? new Rect(0, 0, tex.width, tex.height);
        var sprite = Sprite.Create(tex, rect, pivot, ppu);

        if (Cache.Count >= MaxCacheSize)
        {
            var oldest = CacheOrder.Dequeue();
            Cache.Remove(oldest);
        }

        Cache[key] = sprite;
        CacheOrder.Enqueue(key);
        return sprite;
    }

    public static Sprite RequireFromFileAutoCrop(string path, float ppu, Vector2 pivot, byte alphaThreshold = 1)
    {
        var sprite = FromFileAutoCrop(path, ppu, pivot, alphaThreshold);
        if (sprite == null)
            throw new FileNotFoundException($"Required trap sprite was not found: {path}", path);
        return sprite;
    }

    // ---- Texture loading ----

    /// <summary>
    /// Load a PNG into a point-filtered, non-mipmapped Texture2D.
    /// Returns null if the file does not exist.
    /// </summary>
    public static Texture2D LoadTexture(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[TrapLib] Texture not found: {path}");
            return null;
        }
        var bytes = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.LoadImage(bytes);
        return tex;
    }

    public static Texture2D RequireTexture(string path)
    {
        var tex = LoadTexture(path);
        if (tex == null)
            throw new FileNotFoundException($"Required trap texture was not found: {path}", path);
        return tex;
    }

    // ---- Content analysis ----

    /// <summary>
    /// Find the smallest rectangle containing all pixels with alpha ≥ threshold.
    /// Coordinates in Unity texture space ((0,0) = bottom-left).
    /// Returns null if the texture is fully transparent.
    /// </summary>
    public static Rect? GetContentRect(Texture2D tex, byte alphaThreshold = 1)
    {
        int w = tex.width;
        int h = tex.height;
        var pixels = tex.GetPixels32();

        int left = w, bottom = h, right = 0, top = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (pixels[y * w + x].a >= alphaThreshold)
                {
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }
        }

        if (left > right)
            return null;

        return new Rect(left, bottom, right - left + 1, top - bottom + 1);
    }

    // ---- Sizing ----

    /// <summary>
    /// World-space size of a sprite, accounting for scale.
    /// </summary>
    public static Vector2 GetWorldSize(Sprite sprite, float scale = 1f)
    {
        if (sprite == null) return Vector2.zero;
        return new Vector2(
            sprite.rect.width / sprite.pixelsPerUnit * scale,
            sprite.rect.height / sprite.pixelsPerUnit * scale);
    }

    /// <summary>
    /// Size a BoxCollider2D to roughly match a sprite's content, accounting for
    /// the sprite's pivot and the GameObject's scale. Padding values shrink the
    /// collider slightly (1.0 = exact fit, 0.9 = 10% smaller).
    /// </summary>
    public static void FitColliderToSprite(BoxCollider2D col, Sprite sprite, float scale,
        Vector2 pivot, float widthPadding = 0.9f, float heightPadding = 0.9f)
    {
        if (col == null || sprite == null) return;

        var worldSize = GetWorldSize(sprite, scale);
        var localW = worldSize.x / scale * widthPadding;
        var localH = worldSize.y / scale * heightPadding;

        col.size = new Vector2(localW, localH);
        // Center the collider on the sprite's visual content, accounting for pivot
        col.offset = new Vector2(
            (0.5f - pivot.x) * worldSize.x / scale,
            (0.5f - pivot.y) * worldSize.y / scale);
    }

    /// <summary>
    /// Clear all cached sprites. Call this when switching worlds or unloading mods
    /// to prevent memory leaks.
    /// </summary>
    public static void ClearCache()
    {
        foreach (var sprite in Cache.Values)
        {
            if (sprite != null)
            {
                var texture = sprite.texture;
                if (texture != null) Object.Destroy(texture);
                Object.Destroy(sprite);
            }
        }
        Cache.Clear();
        CacheOrder.Clear();
    }

    /// <summary>
    /// Get the current number of cached sprites (for debugging).
    /// </summary>
    public static int CacheCount => Cache.Count;
}
