using UnityEngine;

namespace TrapLib;

/// <summary>
/// Base configuration shared by all trap types.
/// Developers provide sprites directly — loading method is up to them.
/// </summary>
[System.Serializable]
public class TrapConfig
{
    /// <summary>Unique ID (used as BuildingEntity.id and console spawn target).</summary>
    public string Id;

    /// <summary>Display name shown in the HUD. If null, uses <see cref="Id"/>.</summary>
    public string FullName;

    /// <summary>Chinese display name override. If set and the game is in Chinese, used instead of FullName.</summary>
    public string FullNameCn;

    /// <summary>Description shown when inspecting the building.</summary>
    public string Description;

    /// <summary>Chinese description override. If set and the game is in Chinese, used instead of Description.</summary>
    public string DescriptionCn;

    // ---- Visuals (supplied by caller) ----

    /// <summary>Default / idle sprite.</summary>
    public Sprite Sprite;

    /// <summary>Local scale multiplier applied before placement.</summary>
    public float ObjectScale = 1f;

    // ---- Placement (snap-to-surface) ----

    /// <summary>
    /// Normalised contact point on the sprite. (0.5, 0) = bottom-centre (floor trap),
    /// (0.5, 1) = top-centre (ceiling), (0.5, 0.5) = centre, (0, 0.5) = left-edge (wall).
    /// </summary>
    public Vector2 Pivot = new Vector2(0.5f, 0f);

    /// <summary>Extra world-space offset applied after snapping to the surface.</summary>
    public float SurfaceOffset;

    /// <summary>
    /// Optional delegate for complete placement overrides.
    /// Receives (spawnPos, spriteRenderer, config) and returns the final world position.
    /// If null, the default raycast-snap logic is used.
    /// </summary>
    public System.Func<Vector3, SpriteRenderer, Vector3> CustomPlacement;

    // ---- Building stats ----

    public float Health = 400f;

    /// <summary>Collider size for the physical body (BoxCollider2D).</summary>
    public Vector2 ColliderSize = new Vector2(2f, 1f);

    public bool Metallic = true;

    // ---- Spawning ----

    /// <summary>Minimum biome depth (0-based) for this trap to appear.</summary>
    public int MinBiomeDepth;

    /// <summary>Fraction of totalTrapRarity — lower bound.</summary>
    public float SpawnRateMin;

    /// <summary>Fraction of totalTrapRarity — upper bound.</summary>
    public float SpawnRateMax;

    /// <summary>World-space offset to embed the trap into the surface (units). Passed as spawnYOffset to DistributeEntities.</summary>
    public float InGroundChance;

    // ---- Destruction ----

    /// <summary>(Hit sound ID, Destroy sound ID).</summary>
    public (string hit, string destroy) Sounds = ("scrapmetal", "containerBreak");

    /// <summary>Items dropped via itemsDropOnDestroy.</summary>
    public ItemDrop[] Drops = System.Array.Empty<ItemDrop>();
}

/// <summary>
/// Configuration for traps that explode. Explosion is mandatory; fuse, zone,
/// burst, and the trigger mechanism are all optional (set to null / 0 to disable).
/// </summary>
[System.Serializable]
public class ExplosiveTrapConfig : TrapConfig
{
    // ---- Explosion (mandatory) ----

    /// <summary>Range passed to CreateExplosion.</summary>
    public float ExplosionRange = 25f;

    /// <summary>Explosion parameters (damage, chances, velocity, sound).</summary>
    public ExplosionParams ExplosionParams;

    // ---- Trigger (optional) ----

    /// <summary>
    /// Seconds between trigger and detonation. 0 = instant (no pressed state, no fuse sound).
    /// </summary>
    public float FuseTime = 0.5f;

    /// <summary>
    /// Optional filter for the default collision trigger.
    /// Receives the Collision2D; return true to arm the fuse.
    /// If null, the built-in default is used (mass ≥ 0.5, within 50u, non-kinematic).
    /// </summary>
    public System.Func<Collision2D, bool> TriggerFilter;

    /// <summary>
    /// Completely custom trigger logic. Called every frame in Update.
    /// Call <c>trap.Trigger()</c> to arm the fuse.
    /// When set, the default OnCollisionEnter2D handler is skipped.
    /// </summary>
    public System.Action<ExplosiveTrapBase> CustomTrigger;

    // ---- Fuse / triggered behaviour (optional) ----

    /// <summary>Sound ID played when the fuse is armed. Default = "mine".</summary>
    public string FuseSound = "mine";

    /// <summary>
    /// Called when the fuse is armed (immediately after Trigger()).
    /// Runs on both server and clients in multiplayer.
    /// Receives the trap MonoBehaviour — use for alarms, flashing, spawns, etc.
    /// </summary>
    public System.Action<ExplosiveTrapBase> OnTriggered;

    /// <summary>
    /// Called every frame during the fuse countdown.
    /// Receives (trap, elapsed seconds, total fuse seconds).
    /// Runs on both server and clients. Use for flashing sprites, beeping, etc.
    /// </summary>
    public System.Action<ExplosiveTrapBase, float, float> OnFuseUpdate;

    /// <summary>Sprite shown between trigger and detonation. null = no visual change.</summary>
    public Sprite PressedSprite;

    // ---- Zone (optional) ----

    /// <summary>Lifetime of the persistent zone in seconds.</summary>
    public float ZoneDuration = 30f;

    /// <summary>Color tint of the zone fog sprite.</summary>
    public Color FogColor = Color.white;

    /// <summary>
    /// Create the persistent zone GameObject. Receives (center, config).
    /// null = no zone is created. Called on all instances during detonation;
    /// TrapZone handles the server/client split internally.
    /// </summary>
    public System.Func<Vector3, ExplosiveTrapConfig, GameObject> CreateZone;

    // ---- Burst (optional) ----

    /// <summary>
    /// Radius within which an extra burst effect is applied at detonation.
    /// When > 0, <see cref="ExplosiveTrapBase"/> automatically calls
    /// <see cref="ApplyBlastDebuff"/> for each body within this radius.
    /// Set to 0 to disable. Runs server-only in multiplayer.
    /// </summary>
    public float BlastRadius;

    /// <summary>
    /// One-shot effect applied at the moment of detonation. Receives (center, config).
    /// Use for additional logic beyond the built-in <see cref="BlastRadius"/> handling.
    /// Runs on server only in multiplayer.
    /// </summary>
    public System.Action<Vector3, ExplosiveTrapConfig> OnBurst;

    // ---- Non-detonated destruction (optional) ----

    /// <summary>
    /// Called when the trap is destroyed by damage WITHOUT detonating
    /// (e.g. health dropped below 0.5 before the fuse was armed).
    /// Receives (trap, center position). Server / single-player only.
    /// </summary>
    public System.Action<ExplosiveTrapBase, Vector3> OnDestroyedWithoutDetonation;
}

/// <summary>
/// Configuration for traps that trigger on contact with a cooldown.
/// </summary>
[System.Serializable]
public class ContactTrapConfig : TrapConfig
{
    public float Cooldown = 5f;

    /// <summary>Called on each valid contact. Receives (limb, config). Return true to consume the cooldown.</summary>
    public System.Func<Limb, ContactTrapConfig, bool> OnContact;

    /// <summary>Sound ID played on contact. null = silent.</summary>
    public string ContactSound;

    /// <summary>Sprite shown briefly on contact. null = no visual change.</summary>
    public Sprite ContactSprite;

    /// <summary>Called when contact is triggered (before OnContact). Runs on both server and client. Use for visual effects (flash, particles).</summary>
    public System.Action<Limb, ContactTrapBase> OnContactTriggered;

    /// <summary>Optional: called when the trap is destroyed by damage. Receives (position, config). Server / single-player only.</summary>
    public System.Action<Vector3, ContactTrapConfig> OnDestroyed;
}
