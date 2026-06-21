# TrapLib — Developer Guide

TrapLib is a BepInEx library that provides a unified framework for custom traps: registration, world-gen distribution, lifecycle, and multiplayer sync. You supply sprites and a few lines of behaviour logic — TrapLib handles placement, drops, audio, and world integration.

## Dependencies

- **Hard**: BepInEx + Harmony (bundled with BepInEx)
- **Soft**: RshLib by rushellxyz (custom item library, optional)
- **Soft**: [KrokoshaCasualtiesMP](https://github.com/Krokosha666/cas-unk-krokosha-multiplayer-coop) (multiplayer, optional)

Zero compile-time dependency on RshLib/KrokMP — all integration is runtime via reflection.

## Architecture

```
TrapLib
├─ TrapConfig / ExplosiveTrapConfig / ContactTrapConfig   ← data-only descriptors
├─ TrapRegistry.Register<T>(config)                        ← entry point
├─ TrapSpawner (automatic)                                 ← WorldGeneration hook
│
├─ TrapBase             ← sprite, BuildingEntity, snap-to-surface, destroy SFX
│  ├─ ExplosiveTrapBase ← collision/custom trigger → fuse → explosion → zone → particles
│  └─ ContactTrapBase   ← limb contact + cooldown + callbacks
│
└─ TrapZone             ← persistent zone: fog sprite, collision, per-second effects
```

## Quick Start

### 1. Prepare a sprite

```csharp
var sprite = SpriteLoader.FromFile("path/to/my_trap.png", ppu: 8f);
```

### 2. Create a trap class

```csharp
public class MyTrap : ExplosiveTrapBase
{
    // No need to write Awake, Update, or OnCollisionEnter2D —
    // ExplosiveTrapBase handles everything.
    // Override Awake() and call base.Awake() first if extra init is needed.
}
```

### 3. Register

In your mod's `Awake()`:

```csharp
TrapRegistry.Register<MyTrap>(new ExplosiveTrapConfig
{
    Id = "mytrap",
    Sprite = sprite,
    Health = 300f,
    MinBiomeDepth = 1,
    MaxBiomeDepth = 0, // 0 = no upper limit
    SpawnRateMin = 0.15f,
    SpawnRateMax = 0.20f,
    ExplosionRange = 25f,
    ExplosionParams = new ExplosionParams
    {
        muscleDamage = new RangeF(3f, 10f),
        skinDamage   = new RangeF(5f, 20f),
        sound        = "mine",
    },
    Drops = new[] { new ItemDrop { id = "scrapmetal", chance = 1f } },
    Sounds = ("jumppad", "containerBreak"),
});
```

Build and drop into BepInEx/plugins — traps appear automatically during world generation.

Manual spawn:

```csharp
var trap = TrapRegistry.Spawn("mytrap", new Vector3(100f, 50f, 0f));
```

Or via console:

```
/spawn mytrap              ← mouse cursor (default)
/spawn mytrap cursor       ← same
/spawn mytrap player       ← player position
/spawn mytrap random       ← random position
/spawn mytrap 150 75       ← coordinates (x, y)
/spawn mytrap 150,75       ← comma-separated
```

---

## Configuration Reference

### `TrapConfig` (shared by all trap types)

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Id` | `string` | required | Unique ID. Used as BuildingEntity.id and `/spawn` target |
| `FullName` | `string` | `null` | Display name shown in HUD. Falls back to `Id` |
| `FullNameCn` | `string` | `null` | Chinese name override. Used when game language is Chinese |
| `Description` | `string` | `null` | Description shown when inspecting the building |
| `DescriptionCn` | `string` | `null` | Chinese description override |
| `Sprite` | `Sprite` | required | Default / idle sprite |
| `ObjectScale` | `float` | `1` | GameObject localScale |
| `Pivot` | `Vector2` | `(0.5, 0)` | Normalised sprite contact point. `(0.5,0)`=bottom-centre (floor), `(0.5,1)`=top (ceiling), `(0.5,0.5)`=centre, `(0,0.5)`=left edge (wall) |
| `SurfaceOffset` | `float` | `0` | Extra world-space offset after surface snap |
| `CustomPlacement` | `Func<Vector3, SpriteRenderer, Vector3>` | `null` | Full placement override. Returns final world position. **Note: when set, you must set `GameObject.layer` to Ground inside the callback, or hover detection breaks** |
| `Health` | `float` | `400` | Hit points |
| `ColliderSize` | `Vector2` | `(2, 1)` | BoxCollider2D size |
| `Metallic` | `bool` | `true` | Metallic surface (affects footstep sounds) |
| `MinBiomeDepth` | `int` | `0` | Minimum biome depth for spawning (0-based) |
| `SpawnRateMin` | `float` | required | Lower bound fraction of `totalTrapRarity` |
| `SpawnRateMax` | `float` | required | Upper bound fraction of `totalTrapRarity` |
| `InGroundChance` | `float` | `0` | Embed offset (world units), passed as `spawnYOffset` to `DistributeEntities` |
| `Sounds` | `(string hit, string destroy)` | `("scrapmetal", "containerBreak")` | Hit / destroy sound IDs. Auto-registered by TrapLib |
| `Drops` | `ItemDrop[]` | empty | Items dropped via `itemsDropOnDestroy` |
| `DoNotBreakOnGroundDestroyed` | `bool` | `false` | When true, the trap is NOT destroyed when the block beneath it is broken. Use for floating/hovering traps |

### `ExplosiveTrapConfig : TrapConfig`

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `ExplosionRange` | `float` | `25` | Explosion radius. TrapLib writes this into `ExplosionParams.range` — **do not set `range` manually** |
| `ZoneRadius` | `float` | `0` | Persistent zone radius. ≤0 falls back to `ExplosionRange`. Decoupled from explosion radius — large fog zone + small explosion damage |
| `ExplosionParams` | `ExplosionParams` | required | Explosion parameters. **Note: `range` and `position` are auto-filled by TrapLib.** Only set damage, chances, velocity, and sound |
| `FuseTime` | `float` | `0.5` | Fuse seconds. `0` = instant (no pressed state, no fuse sound) |
| `FuseSound` | `string` | `"mine"` | Sound played when fuse is armed |
| `TriggerFilter` | `Func<Collision2D, bool>` | `null` | Custom collision filter. null uses built-in default (mass ≥ 0.5, within 50u, non-kinematic) |
| `CustomTrigger` | `Action<ExplosiveTrapBase>` | `null` | Full trigger override. Called every frame in Update. Call `trap.Trigger()` to arm. Disables default OnCollisionEnter2D |
| `OnTriggered` | `Action<ExplosiveTrapBase>` | `null` | Called immediately after fuse is armed. Runs on both sides. Use for alarms, flashing, spawns |
| `OnFuseUpdate` | `Action<ExplosiveTrapBase, float, float>` | `null` | Called every frame during fuse. (trap, elapsed, total). Both sides. Use for sprite flashing, countdown |
| `PressedSprite` | `Sprite` | `null` | Sprite shown during fuse. null = no visual change |
| `ZoneDuration` | `float` | `30` | Persistent zone lifetime in seconds |
| `FogColor` | `Color` | `white` | Zone fog sprite colour tint |
| `CreateZone` | `Func<Vector3, ExplosiveTrapConfig, GameObject>` | `null` | Create the persistent zone GameObject. null = no zone. Both sides |
| `BlastRadius` | `float` | `0` | Direct-hit radius at detonation. > 0 auto-scans for Body. Server-only |
| `OnBurst` | `Action<Vector3, ExplosiveTrapConfig>` | `null` | One-shot effect at detonation (after BlastRadius). **Server-only** |
| `OnDestroyedWithoutDetonation` | `Action<ExplosiveTrapBase, Vector3>` | `null` | Called when destroyed by damage without triggering. Server-only |
| `NoClientFallback` | `bool` | `false` | When true, skips the client-side explosion fallback (particles, sound, blastmark) even with KrokMP. Use when KrokMP reliably syncs `CreateExplosion` for this trap type, to avoid double effects |

### `ContactTrapConfig : TrapConfig`

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Cooldown` | `float` | `5` | Cooldown in seconds |
| `OnContact` | `Func<Limb, ContactTrapConfig, bool>` | required | Called on each valid contact. Return true to consume cooldown. Server-only |
| `ContactSound` | `string` | `null` | Sound ID on contact. null = silent. Both sides |
| `ContactSprite` | `Sprite` | `null` | Sprite shown briefly on contact. null = no change. Both sides |
| `OnContactTriggered` | `Action<Limb, ContactTrapBase>` | `null` | Called on contact (before OnContact). Both sides. Use for flash, particles |
| `OnDestroyed` | `Action<Vector3, ContactTrapConfig>` | `null` | Called when destroyed by damage. Server-only |

---

## Base Class Methods

### `TrapBase`

| Method | Description |
|--------|-------------|
| `Awake()` | Initialises SpriteRenderer, BoxCollider2D, Rigidbody2D, BuildingEntity; snaps to surface; increments SpawnCount. **Must call base.Awake() first when overriding** |
| `Update()` | Checks health < 0.5 and plays destroy sound. **Must call base.Update() first when overriding** |
| `Place(int savedLayer)` | virtual — override for full placement control. Default uses `CustomPlacement` or `Pivot` raycast snap |
| `GetOrAdd<T>()` | Gets component, adds if missing |

### `ExplosiveTrapBase : TrapBase`

| Method | Description |
|--------|-------------|
| `Update()` | Additionally: calls `CustomTrigger`; runs fuse countdown + `OnFuseUpdate` |
| `OnCollisionEnter2D()` | Default collision trigger (skipped when `CustomTrigger` is set). Uses `TriggerFilter` first, fallback to built-in |
| `Trigger()` | **public** — arms the fuse. Plays `FuseSound`, switches to `PressedSprite`, calls `OnTriggered`. Detonates immediately if `FuseTime=0` |
| `Detonate()` | virtual — explosion → zone → blast → burst → particles → destroy |
| `ApplyBlast(Vector3)` | virtual — scans radius for Body when `BlastRadius>0`, calls `ApplyBlastDebuff` per body |
| `ApplyBlastDebuff(Body)` | virtual — override to define direct-hit effects (damage, slow, sickness). Server-only |
| `OnDestroy()` | Calls `OnDestroyedWithoutDetonation` if destroyed without triggering |

### `ContactTrapBase : TrapBase`

| Method | Description |
|--------|-------------|
| `Update()` | Additionally: counts down cooldown |
| `OnCollisionEnter2D()` | Gets limb via `Body.LimbFromObject`; plays `ContactSound`, switches `ContactSprite`, calls `OnContactTriggered` (both sides), then `OnContact` (server) |
| `OnDestroy()` | Calls `OnDestroyed` when health < 0.5 |

### `TrapZone`

| Member | Description |
|--------|-------------|
| `radius` / `duration` / `fogColor` | Set by `CreateZone` callback on creation |
| `fadeTime` | Seconds before expiry to trigger `OnExpiring` (default 10) |
| `tickInterval` | Seconds between `OnTick` calls. 0 = disabled |
| `Start()` | Adds CircleCollider2D + fog sprite + `Destroy(gameObject, duration)` |
| `FixedUpdate()` | Per-second Body scan; enter/exit detection; periodic `OnTick`; `OnExpiring` near expiry. Client-side skips damage automatically |
| `ApplyEffect(Body, dist)` | **abstract** — per body per second. Server-only |
| `OnBodyEnter(Body)` | virtual — called once when Body enters. Server-only |
| `OnBodyExit(Body)` | virtual — called once when Body exits. Server-only |
| `OnExpiring()` | virtual — called every frame during the fade-out period. Everyone |
| `OnTick()` | virtual — called every `tickInterval` seconds. Both sides. Use for particles, SFX |

---

## Multiplayer Sync

| Event | Server | Client |
|-------|--------|--------|
| World-gen spawn | ✓ | ✓ |
| Collision / trigger | ✓ | ✓ |
| Fuse / `OnTriggered` / `OnFuseUpdate` | ✓ | ✓ |
| `Trigger()` instant | ✓ explosion + damage | ✓ visual only (zone/fog/destroy) |
| `CreateExplosion` | ✓ real + packet | ← receive → visual only |
| `CreateZone` / `SpawnFogParticles` | ✓ | ✓ (visual only) |
| `TrapZone.ApplyEffect` | ✓ | — |
| `TrapZone.OnBodyEnter/Exit` | ✓ | — |
| `TrapZone.OnExpiring` | ✓ | ✓ |
| `TrapZone.OnTick` | ✓ | ✓ |
| `OnBurst` / `ApplyBlastDebuff` | ✓ | — |
| `OnDestroyedWithoutDetonation` | ✓ | — |
| Item drops | ✓ (BuildingEntity) | — |

Key rules:

- **Damage / state changes go in `OnBurst`, `ApplyBlastDebuff`, or `ApplyEffect`** — auto-limited to server
- **Visual effects go in `OnTriggered`, `OnFuseUpdate`, `OnTick`, `OnContactTriggered`** — visible on both sides
- **`OnExpiring` runs on both sides — use for client visual fade effects**
- **Do not add MonoBehaviour components in server-only callbacks** — components do not replicate

---

## Utilities

### `Attenuation` — distance falloff

All methods accept `t = distance / maxRadius` (0=centre, 1=boundary), return 0–1 multiplier:

| Method | Formula | Description |
|--------|---------|-------------|
| `Linear(t)` | `1 - t` | Linear falloff |
| `SmoothStep(t)` | `2t³ - 3t² + 1` | Cubic S-curve, flat at both ends |
| `SmootherStep(t)` | `-6t⁵ + 15t⁴ - 10t³ + 1` | Quintic, first & second derivatives zero at ends |
| `SquareRoot(t)` | `√(1 - t)` | High early, steep drop near edge |
| `PowerCurve(t, n)` | `(1 - t)^n` | Adjustable (n>1=fast centre falloff, n<1=long tail) |
| `Exponential(t, k)` | `e^(-k·t)` | Exponential, never reaches zero |
| `InverseSquare(t, k)` | `1 / (1 + k·t²)` | Inverse-square law |
| `RadialThreshold(t, r)` | core full → linear outer | `t≤r` returns 1.0, linear to 0 beyond |
| `ExponentForEdgeFalloff(m)` | — | Derives PowerCurve exponent from target edge multiplier |
| `SteepnessForEdgeFalloff(m)` | — | Derives Exponential steepness from target edge multiplier |

### `SpriteLoader`

```csharp
// Full image (default pivot centre 0.5,0.5)
var sprite = SpriteLoader.FromFile("path.png", ppu: 8f);

// Custom pivot (e.g. bottom-centre for floor traps)
var sprite = SpriteLoader.FromFile("path.png", ppu: 8f, pivot: new Vector2(0.5f, 0f));

// Auto-crop transparent borders
var sprite = SpriteLoader.FromFileAutoCrop("path.png", ppu: 8f, pivot: new Vector2(0.5f, 0f));
```

All methods are cached. Returns null on failure, logs a warning.

Helper methods:

| Method | Description |
|--------|-------------|
| `LoadTexture(path)` | Load PNG as point-filtered Texture2D |
| `GetContentRect(tex, alphaThreshold)` | Find minimal bounding rect of non-transparent pixels |
| `GetWorldSize(sprite, scale)` | Return world-space size of a sprite |
| `FitColliderToSprite(col, sprite, scale, pivot, wPad, hPad)` | Fit a BoxCollider2D to sprite content |

### `FogSpriteCache`

128×128 radial gradient texture, PPU=8, exponent 0.7. Used automatically by `TrapZone.Start()`.

### `TrapSounds`

Sounds are configured via `TrapConfig.Sounds`. The sound map is lazily populated from `TrapRegistry` on first access — no manual setup needed. Call `TrapSounds.Refresh()` to force re-population after late registrations.

---

## Full Example: Poison Gas Mine

```csharp
// MyMod/Plugin.cs
[BepInPlugin("com.example.mymod", "MyMod", "1.0.0")]
[BepInDependency("com.vertigo.traplib")]
public class MyModPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        var defaultSprite = SpriteLoader.FromFile(
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "..", "res", "gas_mine.png"), 8f);
        var pressedSprite = SpriteLoader.FromFile(/* ... */);

        TrapRegistry.Register<GasMine>(new ExplosiveTrapConfig
        {
            Id = "gasmine",
            Sprite = defaultSprite,
            PressedSprite = pressedSprite,
            Health = 400f,
            ColliderSize = new Vector2(2f, 1f),
            MinBiomeDepth = 1,
            SpawnRateMin = 0.2f,
            SpawnRateMax = 0.25f,
            ExplosionRange = 20f,
            FuseTime = 0.5f,
            FogColor = new Color(0.7f, 1f, 0.2f, 0.5f),
            ZoneDuration = 30f,
            ExplosionParams = new ExplosionParams
            {
                muscleDamage = new RangeF(3f, 6f),
                skinDamage   = new RangeF(10f, 25f),
                skinDamageChance = 0.05f,
                velocity = 2.5f,
                sound = "mine",
            },
            CreateZone = (center, cfg) =>
            {
                var go = new GameObject("GasZone");
                go.transform.position = center;
                var zone = go.AddComponent<GasZone>();
                zone.radius = cfg.ExplosionRange;
                zone.duration = cfg.ZoneDuration;
                zone.fogColor = cfg.FogColor;
                return go;
            },
            OnBurst = (center, cfg) =>
            {
                float blastSqr = 5f * 5f;
                foreach (var body in Object.FindObjectsOfType<Body>())
                {
                    if (body == null) continue;
                    if (((Vector2)(body.transform.position - center)).sqrMagnitude >= blastSqr) continue;
                    foreach (var limb in body.limbs)
                    {
                        if (limb == null) continue;
                        limb.skinHealth -= 8f;
                        limb.muscleHealth -= 4f;
                    }
                    body.sicknessAmount += 15f;
                }
            },
            Drops = new[] { new ItemDrop { id = "scrapmetal", chance = 1f, conditionMin = 0f, conditionMax = 0.2f } },
            Sounds = ("jumppad", "containerBreak"),
        });
    }
}

// MyMod/GasZone.cs
public class GasZone : TrapZone
{
    protected override void ApplyEffect(Body body, float dist)
    {
        float mult = Attenuation.Linear(dist / radius);
        foreach (var limb in body.limbs)
        {
            if (limb == null) continue;
            limb.skinHealth   -= 2f * mult;
            limb.muscleHealth -= 1f * mult;
        }
        body.sicknessAmount += 2f * mult;
    }
}
```

---

## RshLib Integration

- **TrapLib does not depend on RshLib at compile time** — runtime detection via BepInEx
- If RshLib is installed: `BuildingEntity.Update` uses RshLib's patch (custom item drops)
- If RshLib is not installed: TrapLib loads its own `BuildingEntityPatch` (uses `Utils.Create`, compatible with custom items)
- Vanilla item IDs (e.g. `"scrapmetal"`) work without RshLib; custom item IDs require RshLib

## File Structure

```
YourMod/
├── YourMod.csproj     ← add TrapLib project reference
├── Plugin.cs           ← Register<T>()
├── YourTrap.cs         ← class YourTrap : ExplosiveTrapBase { }
├── YourZone.cs         ← class YourZone : TrapZone { override ApplyEffect... }
└── res/
    └── your_sprite.png
```
