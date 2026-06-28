# TrapLib

[Casualties Unknown](https://store.steampowered.com/app/2586950/Casualties_Unknown/) 的 BepInEx 前置库，为自定义陷阱提供统一的注册、世界生成集成、区域效果和多人联机框架。

## 功能

- **一行注册** — `TrapRegistry.Register<T>(config)` 自动接入世界生成
- **按层级分布** — 陷阱仅在世界纵向带匹配 `MinBiomeDepth`/`MaxBiomeDepth` 的位置生成
- **爆炸陷阱** — 碰撞/自定义触发 → 引信 → 爆炸 → 持续区域 → 粒子
- **接触陷阱** — 肢体触发 + 冷却 + 回调
- **持续区域** — 半径范围效果，支持淡出、进出事件、定时回调
- **多人联机** — 伤害/状态服务端权威，视觉效果双端执行
- **放置工具** — 复用表面射线检测、自身层级忽略、自定义放置辅助
- **贴图加载工具** — 缓存式可选加载，以及用于必需资源的 `Require*` 加载器
- **控制台命令** — `/spawn <id> [cursor|player|random|x,y]` 快速测试
- **中英双语** — 通过 `FullNameCn` / `DescriptionCn` 支持中文名称和描述

## 快速开始

```csharp
// 1. 在插件的 Awake() 中注册
TrapRegistry.Register<MyTrap>(new ExplosiveTrapConfig
{
    Id = "mytrap",
    FullName = "我的陷阱",
    FullNameCn = "我的陷阱",
    Description = "A custom explosive trap.",
    DescriptionCn = "一个自定义爆炸陷阱。",
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

// 2. 定义陷阱类
public class MyTrap : ExplosiveTrapBase { }
```

完整 API 参考见 [DEVELOPER_GUIDE_CN.md](DEVELOPER_GUIDE_CN.md)。

## 依赖

- **硬依赖**：BepInEx + Harmony（BepInEx 自带）
- **软依赖**：RshLib by rushellxyz（自定义物品库，可选）
- **软依赖**：[KrokoshaCasualtiesMP](https://github.com/Krokosha666/cas-unk-krokosha-multiplayer-coop)（多人联机，可选）

编译时零依赖 RshLib/KrokMP——所有集成均通过反射运行时检测。

## 编译

1. 复制 `GamePaths.props.example` 为 `GamePaths.props`，填入你的游戏路径
2. `dotnet build`

输出到 `../build/`。

## 许可证

MIT
