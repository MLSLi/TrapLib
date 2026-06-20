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

    /// <summary>Caches which BuildingEntity instances belong to traps — avoids
    /// expensive TryGetComponent in BuildingEntityPatch.Prefix every frame.</summary>
    internal static readonly System.Collections.Generic.HashSet<BuildingEntity> TrapBuildings = new System.Collections.Generic.HashSet<BuildingEntity>();

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
        TrapBuildings.Add(_build);
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

        // Ground traps should be destroyed when the block beneath is broken.
        if (Config?.DoNotBreakOnGroundDestroyed != true && WorldGeneration.world?.worldExists == true)
        {
            _build.requireGround = true;

            // Temporarily change layer so the raycast doesn't hit our own collider.
            int originalLayer = gameObject.layer;
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            var groundMask = LayerMask.GetMask("Ground");
            var hit = Physics2D.Raycast(transform.position + Vector3.up * 2f, Vector2.down, 4f, groundMask);
            if (hit)
            {
                // Use a point slightly *inside* the ground block so WorldToBlockPos
                // reliably resolves to the solid block, not the air above it.
                _build.blockPlacedOn = WorldGeneration.world.WorldToBlockPos(hit.point - hit.normal * 0.1f);
            }
            else
            {
                _build.blockPlacedOn = WorldGeneration.world.WorldToBlockPos(transform.position);
            }

            gameObject.layer = originalLayer;
        }

        // Don't call CheckSeating here — the per-frame check in Update is enough.
        // Calling it immediately after spawn can destroy traps before the world
        // has finished setting up its block colliders (especially thin platforms).
        SpawnCount++;

        if (pos != Vector3.zero)
            TrapLibPlugin.Log?.LogInfo($"{GetType().Name} ({pos.x:F1},{pos.y:F1}) id={Config?.Id}");

        // Delayed name/description setup — ensures BuildingEntity.Start has run first.
        if (Config != null)
            Invoke(nameof(SetTrapNameAndDescription), 0.05f);
    }

    private float _seatingCheckTimer;
    private float _healthCheckTimer;

    protected virtual void Update()
    {
        if (Config == null) return; // prefab template — Awake skipped setup

        // Ground traps: check if supporting block is still present.
        // Throttled to once every 0.5s — traps are static, frequent checks are wasteful.
        if (!_destroyed && _build != null && _build.requireGround && Config?.DoNotBreakOnGroundDestroyed != true)
        {
            _seatingCheckTimer -= Time.deltaTime;
            if (_seatingCheckTimer <= 0f)
            {
                _seatingCheckTimer = 0.5f;
                _build.CheckSeating();
            }
        }

        // Health check — throttled to ~1 Hz to reduce per-frame overhead.
        if (!_destroyed && _build != null)
        {
            _healthCheckTimer -= Time.deltaTime;
            if (_healthCheckTimer <= 0f)
            {
                _healthCheckTimer = 1f;
                if (_build.health < 0.5f)
                {
                    _destroyed = true;
                    TrapSounds.PlayDestroy(_build);
                }
            }
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
        var origin = pos + Vector3.up * 1f;
        for (int safety = 0; safety < 20 && Physics2D.OverlapPoint(origin, mask); safety++)
            origin.y += 0.5f;
        var hit = Physics2D.Raycast(origin, Vector2.down, 12f, mask);
        if (!hit)
        {
            // Fallback 1: pick the closest surface in four directions
            var hits = new RaycastHit2D[4];
            hits[0] = Physics2D.Raycast(pos, Vector2.down,  8f, mask);
            hits[1] = Physics2D.Raycast(pos, Vector2.left,  8f, mask);
            hits[2] = Physics2D.Raycast(pos, Vector2.right, 8f, mask);
            hits[3] = Physics2D.Raycast(pos, Vector2.up,    8f, mask);
            float bestDist = float.MaxValue;
            foreach (var h in hits)
                if (h && h.distance < bestDist) { bestDist = h.distance; hit = h; }
        }

        if (!hit)
        {
            // Fallback 2: long vertical cast straight down — catches cases where
            // DistributeEntities spawned us far above ground (cliffs, large caverns).
            var longHit = Physics2D.Raycast(pos + Vector3.up * 30f, Vector2.down, 60f, mask);
            if (longHit) hit = longHit;
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
        // Ground on our right  → move left  so our left   edge touches it
        // Ground on our left   → move right so our right  edge touches it
        // Ground below us      → move down  so our bottom edge touches it
        // Ground above us      → move up    so our top    edge touches it
        if (dotRight > 0.7f)       offset = toLeft;
        else if (dotRight < -0.7f) offset = toRight;
        else if (dotUp > 0.7f)     offset = toBottom;
        else if (dotUp < -0.7f)    offset = toTop;
        // For angled surfaces, fall back to the largest projection
        else
            offset = Mathf.Max(
                Mathf.Abs(dotRight) * (dotRight > 0 ? toLeft  : toRight),
                Mathf.Abs(dotUp)    * (dotUp    > 0 ? toBottom : toTop));

        return offset;
    }

    // ---- Helpers ----

    protected T GetOrAdd<T>() where T : Component
    {
        var c = GetComponent<T>();
        return c != null ? c : gameObject.AddComponent<T>();
    }

    protected virtual void OnDestroy()
    {
        if (_build != null)
            TrapBuildings.Remove(_build);
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

    /// <summary>
    /// Sets the BuildingEntity display name and description after Start has run,
    /// ensuring any locale overrides or other mods have finished first.
    /// </summary>
    private void SetTrapNameAndDescription()
    {
        if (Config == null || _build == null) return;
        var cn = Utilities.LocaleHelper.IsChinese();
        _build.fullName = (cn && Config.FullNameCn != null) ? Config.FullNameCn
            : Config.FullName ?? Config.Id;
        _build.description = (cn && Config.DescriptionCn != null) ? Config.DescriptionCn
            : Config.Description ?? "";
    }
}
