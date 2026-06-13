using System;
using TrapLib.Utilities;
using UnityEngine;

namespace TrapLib;

/// <summary>
/// Abstract base for all traps. Handles sprite, BuildingEntity, placement,
/// destruction tracking, and hit sounds.
///
/// Override Awake/Update to add custom behaviour; call the base method first.
/// Override <see cref="Place"/> to replace the snap-to-surface logic entirely.
/// </summary>
public abstract class TrapBase : MonoBehaviour
{
    public TrapConfig Config;

    protected BuildingEntity _build;
    protected SpriteRenderer _sr;
    protected bool _destroyed;

    internal static int SpawnCount;
    internal static void ResetSpawnCount() => SpawnCount = 0;

    // ---- Unity lifecycle ----

    protected virtual void Awake()
    {
        if (transform.position == Vector3.zero) return; // prefab template

        // Unity Instantiate may drop plain-C# references — recover from registry
        if (Config == null)
            Config = TrapRegistry.GetConfig(GetType());

        Vector3 pos = transform.position;
        int savedLayer = gameObject.layer;

        _sr = GetOrAdd<SpriteRenderer>();
        if (Config?.Sprite != null)
            _sr.sprite = Config.Sprite;

        var col = GetOrAdd<BoxCollider2D>();
        col.isTrigger = false;
        col.size = Config?.ColliderSize ?? new Vector2(2f, 1f);

        var rb = GetOrAdd<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;

        _build = GetOrAdd<BuildingEntity>();
        _build.health = Config?.Health ?? 400f;
        _build.requireGround = false;
        _build.ignoreBodyOptimize = true;
        _build.id = Config?.Id ?? gameObject.name;
        _build.metallic = Config?.Metallic ?? true;
        _build.skipDescriptionSet = true;
        _build.fullName = Config?.Id ?? gameObject.name;
        // Load hit sound AudioClip — Body.Attack calls Sound.Play(hitSound, ...) directly
        var hitId = Config?.Sounds.hit ?? "scrapmetal";
        _build.hitSound = Resources.Load<AudioClip>("Sounds/" + hitId);
        _build.alwaysDrop = Array.Empty<ItemDrop>();
        _build.itemCategoriesToAdd = Array.Empty<string>();
        if ((_build.itemsDropOnDestroy == null || _build.itemsDropOnDestroy.Length == 0)
            && Config?.Drops != null)
        {
            _build.itemsDropOnDestroy = Config.Drops;
        }

        if (Config != null && Config.ObjectScale != 1f)
            transform.localScale = Vector3.one * Config.ObjectScale;

        Place(savedLayer);

        _build.CheckSeating();
        SpawnCount++;

        if (pos != Vector3.zero)
            TrapLibPlugin.Log?.LogInfo($"{GetType().Name} ({pos.x:F1},{pos.y:F1}) id={Config?.Id}");
    }

    protected virtual void Update()
    {
        if (Config == null) return; // prefab template — Awake skipped setup
        if (!_destroyed && _build != null && _build.health < 0.5f)
        {
            _destroyed = true;
            TrapSounds.PlayDestroy(_build);
        }
    }

    // ---- Placement ----

    /// <summary>
    /// Determine the final world position. Override for complete control;
    /// the default implementation uses <see cref="TrapConfig.CustomPlacement"/>
    /// if set, otherwise raycasts to the nearest Ground surface and
    /// aligns using <see cref="TrapConfig.Pivot"/>.
    /// </summary>
    protected virtual Vector3 Place(int savedLayer)
    {
        Vector3 pos = transform.position;

        if (Config?.CustomPlacement != null)
        {
            pos = Config.CustomPlacement(pos, _sr);
        }
        else
        {
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            var mask = LayerMask.GetMask("Ground");

            pos = SnapToSurface(pos, mask);
            gameObject.layer = savedLayer == 0 ? LayerMask.NameToLayer("Ground") : savedLayer;
        }

        transform.position = pos;
        return pos;
    }

    /// <summary>
    /// Default raycast snap using <see cref="TrapConfig.Pivot"/> to compute the offset.
    /// Casts downward first; falls back to the nearest of four directions.
    /// </summary>
    private Vector3 SnapToSurface(Vector3 pos, int mask)
    {
        var origin = pos + Vector3.up * 4f;
        var hit = Physics2D.Raycast(origin, Vector2.down, 12f, mask);
        if (!hit)
        {
            // Fallback: pick the closest surface in four directions
            var hits = new RaycastHit2D[4];
            hits[0] = Physics2D.Raycast(pos, Vector2.down,  8f, mask);
            hits[1] = Physics2D.Raycast(pos, Vector2.left,  8f, mask);
            hits[2] = Physics2D.Raycast(pos, Vector2.right, 8f, mask);
            hits[3] = Physics2D.Raycast(pos, Vector2.up,    8f, mask);
            float bestDist = float.MaxValue;
            foreach (var h in hits)
                if (h && h.distance < bestDist) { bestDist = h.distance; hit = h; }
        }

        if (hit)
        {
            float offset = ComputePivotOffset(hit.normal);
            pos = (Vector3)(hit.point + hit.normal * (offset + (Config?.SurfaceOffset ?? 0f)))
                + Vector3.forward * pos.z;
        }
        return pos;
    }

    /// <summary>
    /// Given a surface normal, compute the world-space distance from the sprite's
    /// <see cref="TrapConfig.Pivot"/> to the contact edge in that normal's direction.
    /// </summary>
    private float ComputePivotOffset(Vector2 normal)
    {
        if (_sr?.sprite == null) return 0f;

        Vector2 pivot = Config?.Pivot ?? new Vector2(0.5f, 0f);
        float worldW = _sr.sprite.rect.width  / _sr.sprite.pixelsPerUnit * transform.localScale.x;
        float worldH = _sr.sprite.rect.height / _sr.sprite.pixelsPerUnit * transform.localScale.y;

        // Signed distance from pivot to each edge (positive = in the normal direction)
        float toRight  = (1f - pivot.x) * worldW;
        float toLeft   = pivot.x * worldW;
        float toTop    = (1f - pivot.y) * worldH;
        float toBottom = pivot.y * worldH;

        // Choose the edge that faces the normal
        float dotRight = Vector2.Dot(normal, Vector2.right);
        float dotUp    = Vector2.Dot(normal, Vector2.up);

        float offset = 0f;
        if (dotRight > 0.7f)       offset = toRight;
        else if (dotRight < -0.7f) offset = toLeft;
        else if (dotUp > 0.7f)     offset = toTop;
        else if (dotUp < -0.7f)    offset = toBottom;
        // For angled surfaces, fall back to the largest projection
        else
            offset = Mathf.Max(
                Mathf.Abs(dotRight) * (dotRight > 0 ? toRight : toLeft),
                Mathf.Abs(dotUp)    * (dotUp    > 0 ? toTop   : toBottom));

        return offset;
    }

    // ---- Helpers ----

    protected T GetOrAdd<T>() where T : Component
    {
        var c = GetComponent<T>();
        return c != null ? c : gameObject.AddComponent<T>();
    }

    /// <summary>Call before Object.Destroy to ensure OnDestroy sees the destroyed flag
    /// regardless of MonoBehaviour Update execution order.</summary>
    internal void MarkDestroyed()
    {
        if (_destroyed) return;
        _destroyed = true;
        TrapSounds.PlayDestroy(_build);
    }

    // Unity SendMessage target — must be public
    public void BuildingHit(AttackInfo atk)
    {
        if (_build != null) TrapSounds.PlayHit(_build);
    }
}
