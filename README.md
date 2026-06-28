# TrapLib

A BepInEx library for [Casualties Unknown](https://store.steampowered.com/app/4576490/_/), providing a unified framework for creating custom traps with world-gen integration, zone effects, and multiplayer support.

## Features

- **One-line registration** — `TrapRegistry.Register<T>(config)` hooks into world generation automatically
- **Biome-aware distribution** — traps are placed only in world bands matching `MinBiomeDepth`/`MaxBiomeDepth`
- **Explosive traps** — collision/custom triggers → fuse → explosion → persistent zone → particles
- **Contact traps** — limb-based contact with cooldowns and callbacks
- **Persistent zones** — radius-based area effects with fade-out, body enter/exit events, and tick timers
- **Multiplayer-aware** — server-authoritative damage/state; visual effects run on both sides
- **Placement utilities** — reusable surface raycasts, self-layer masking, and custom placement helpers
- **Sprite loading helpers** — cached optional loaders plus `Require*` loaders for fail-fast resources
- **Console spawn command** — `/spawn <id> [cursor|player|random|x,y]` for testing
- **Locale support** — English and Chinese display names/descriptions via `FullNameCn` / `DescriptionCn`

## Quick Start

```csharp
// 1. Register in your plugin's Awake()
TrapRegistry.Register<MyTrap>(new ExplosiveTrapConfig
{
    Id = "mytrap",
    FullName = "My Trap",
    Description = "A custom explosive trap.",
    Sprite = SpriteLoader.RequireFromFileAutoCrop("path/to/sprite.png", ppu: 8f, pivot: new Vector2(0.5f, 0f)),
    Health = 300f,
    MinBiomeDepth = 1,
    SpawnRateMin = 0.15f,
    SpawnRateMax = 0.20f,
    SpawnYOffset = 0.6f,
    ExplosionRange = 25f,
    ExplosionParams = new ExplosionParams
    {
        muscleDamage = new RangeF(3f, 10f),
        skinDamage   = new RangeF(5f, 20f),
        sound        = "mine",
    },
    Drops = new[] { new ItemDrop { id = "scrapmetal", chance = 1f } },
});

// 2. Define your trap class
public class MyTrap : ExplosiveTrapBase { }
```

See [DEVELOPER_GUIDE_EN.md](DEVELOPER_GUIDE_EN.md) for the full API reference.

中文文档见 [README_CN.md](README_CN.md) 和 [DEVELOPER_GUIDE_CN.md](DEVELOPER_GUIDE_CN.md)。

## Dependencies

- **Hard**: BepInEx + Harmony (bundled with BepInEx)
- **Soft**: RshLib by rushellxyz (custom item drops, optional)
- **Soft**: [KrokoshaCasualtiesMP](https://github.com/Krokosha666/cas-unk-krokosha-multiplayer-coop) (multiplayer, optional)

Zero compile-time dependency on RshLib or KrokMP — all integration is runtime via reflection.

## Building

1. Copy `GamePaths.props.example` to `GamePaths.props` and set your game paths.
2. `dotnet build`

Output goes to `../build/` relative to the project.

## License

MIT
