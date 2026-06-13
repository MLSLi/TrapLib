# TrapLib — 开发者文档

TrapLib 是一个 BepInEx 前置库，为自定义陷阱提供统一的注册、生成、生命周期、多人同步框架。你只需要准备贴图和写几行行为逻辑，剩下的摆放、掉落、音效、世界生成都由 TrapLib 处理。

## 依赖

- **硬依赖**：BepInEx + Harmony（BepInEx 自带）
- **软依赖**：[RshLib](https://github.com/rushellxyz/rshlib)（自定义物品库，可选）
- **软依赖**：[KrokoshaCasualtiesMP](https://github.com/rushellxyz/KrokoshaCasualtiesMP)（多人联机，可选）

编译时**零依赖** RshLib/KrokMP——所有集成均通过反射运行时检测。

## 架构

```
TrapLib
├─ TrapConfig / ExplosiveTrapConfig / ContactTrapConfig   ← 纯数据，描述陷阱
├─ TrapRegistry.Register<T>(config)                        ← 注册入口
├─ TrapSpawner (自动)                                      ← WorldGeneration hook
│
├─ TrapBase             ← 精灵、BuildingEntity、贴地、破坏音效
│  ├─ ExplosiveTrapBase ← 碰撞/自定义触发 → 引信 → 爆炸 → 区域 → 粒子
│  └─ ContactTrapBase   ← 肢体接触 + 冷却 + 回调
│
└─ TrapZone             ← 持续区域：迷雾贴图、碰撞检测、每秒效果
```

## 快速开始

### 1. 准备贴图

用 `SpriteLoader.FromFile()` 或任意方式加载 `Sprite`：

```csharp
var sprite = SpriteLoader.FromFile("path/to/my_trap.png", ppu: 8f);
```

### 2. 创建陷阱类

```csharp
public class MyTrap : ExplosiveTrapBase
{
    // 不需要写 Awake、Update、OnCollisionEnter2D——
    // 全部由 ExplosiveTrapBase 处理。
    // 如果需要额外初始化，override Awake() 并先调用 base.Awake()。
}
```

### 3. 注册

在你 mod 的 `Awake()` 中：

```csharp
TrapRegistry.Register<MyTrap>(new ExplosiveTrapConfig
{
    Id = "mytrap",
    Sprite = sprite,
    Health = 300f,
    MinBiomeDepth = 1,
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

编译、扔进 BepInEx/plugins，世界生成时自动出现。

也可以手动生成：

```csharp
// 在世界生成完成后手动放置陷阱
var trap = TrapRegistry.Spawn("mytrap", new Vector3(100f, 50f, 0f));
```

也可以用控制台命令：

```
/spawn mytrap              ← 鼠标光标位置（默认）
/spawn mytrap cursor       ← 同上
/spawn mytrap player       ← 玩家位置
/spawn mytrap random       ← 随机位置
/spawn mytrap 150 75       ← 指定坐标 (x, y)
/spawn mytrap 150,75       ← 逗号分隔同上
```

---

## 配置参考

### `TrapConfig`（所有陷阱共用）

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Id` | `string` | 必填 | 唯一 ID。用作 BuildingEntity.id 和 `/spawn` 目标 |
| `FullName` | `string` | `null` | 显示名称。null 时回退到 `Id` |
| `FullNameCn` | `string` | `null` | 中文名称覆盖。游戏语言为中文时优先使用 |
| `Description` | `string` | `null` | 检视时显示的描述文本 |
| `DescriptionCn` | `string` | `null` | 中文描述覆盖。游戏语言为中文时优先使用 |
| `Sprite` | `Sprite` | 必填 | 默认贴图 |
| `ObjectScale` | `float` | `1` | GameObject 的 localScale |
| `Pivot` | `Vector2` | `(0.5, 0)` | 贴图接触点。`(0.5,0)`=底部居中(地面陷阱)，`(0.5,1)`=顶部(天花板)，`(0.5,0.5)`=中心，`(0,0.5)`=左边缘(墙壁) |
| `SurfaceOffset` | `float` | `0` | 贴地后的额外偏移（世界单位） |
| `CustomPlacement` | `Func<Vector3, SpriteRenderer, Vector3>` | `null` | 完全接管摆放逻辑。返回最终世界坐标。不为 null 时跳过默认射线贴地。**注意：使用时需在回调内将 GameObject.layer 设为 Ground，否则鼠标悬停检测失效** |
| `Health` | `float` | `400` | 生命值 |
| `ColliderSize` | `Vector2` | `(2, 1)` | BoxCollider2D 尺寸 |
| `Metallic` | `bool` | `true` | 是否金属（影响脚步声） |
| `MinBiomeDepth` | `int` | `0` | 最低出现层级（0-based。0=第一层） |
| `SpawnRateMin` | `float` | 必填 | 占 `totalTrapRarity` 的下限比例 |
| `SpawnRateMax` | `float` | 必填 | 占 `totalTrapRarity` 的上限比例 |
| `InGroundChance` | `float` | `0` | 半埋入地面的偏移量（世界单位），传给 `DistributeEntities` 的 `spawnYOffset` |
| `Sounds` | `(string hit, string destroy)` | `("scrapmetal", "containerBreak")` | 受击音效 ID / 破坏音效 ID。TrapLib 自动注册，无需手动操作 |
| `Drops` | `ItemDrop[]` | 空数组 | 通过 `itemsDropOnDestroy` 掉落的物品 |

### `ExplosiveTrapConfig` : `TrapConfig`

`ExplosionParams` 必填（无默认值）；其余全部可选（null / 0 = 禁用或使用默认值）。

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `ExplosionRange` | `float` | `25` | 爆炸范围。TrapLib 会自动将其写入 `ExplosionParams.range`——**不要在 ExplosionParams 中手动设置 `range`，会被覆盖** |
| `ExplosionParams` | `ExplosionParams` | 必填 | 爆炸参数。**注意：`range` 和 `position` 由 TrapLib 自动填充（`range` 来自 `ExplosionRange`，`position` 为爆心坐标），手动设置无效。**只需设置伤害、概率、速度、音效 |
| `FuseTime` | `float` | `0.5` | 引信秒数。`0`=瞬爆（无 pressed 状态、无引信音效） |
| `FuseSound` | `string` | `"mine"` | 引信点燃时播放的音效 ID |
| `TriggerFilter` | `Func<Collision2D, bool>` | `null` | 替换默认碰撞检测。null 时使用内置默认（质量≥0.5、50u 内、非 kinematic） |
| `CustomTrigger` | `Action<ExplosiveTrapBase>` | `null` | 完全接管触发逻辑。每帧 Update 调用。调 `trap.Trigger()` 点火。设置后默认 OnCollisionEnter2D 停用 |
| `OnTriggered` | `Action<ExplosiveTrapBase>` | `null` | 引信点燃后立即回调。双端执行。用途：警报、闪烁、召唤 |
| `OnFuseUpdate` | `Action<ExplosiveTrapBase, float, float>` | `null` | 引信期间每帧回调。参数 (trap, elapsed, total)。双端执行。用途：贴图闪烁、倒计时显示 |
| `PressedSprite` | `Sprite` | `null` | 引信期间显示的贴图。null=无变化 |
| `ZoneDuration` | `float` | `30` | 持续区域存在秒数 |
| `FogColor` | `Color` | `white` | 区域迷雾贴图的颜色遮罩 |
| `CreateZone` | `Func<Vector3, ExplosiveTrapConfig, GameObject>` | `null` | 创建持续区域 GameObject。null=无区域。引爆时双端调用；TrapZone 内部自动区分服务端/客户端行为 |
| `BlastRadius` | `float` | `0` | 爆炸瞬间直接命中半径。>0 时自动扫描范围内 Body 并调用 `ApplyBlastDebuff`。0=禁用。服务端执行 |
| `OnBurst` | `Action<Vector3, ExplosiveTrapConfig>` | `null` | 爆炸瞬间额外一次性效果（在 `BlastRadius` 处理之后）。**仅服务端/单机执行** |
| `OnDestroyedWithoutDetonation` | `Action<ExplosiveTrapBase, Vector3>` | `null` | 被打爆但未引爆时回调。参数 (trap, center)。仅服务端/单机执行。用途：核泄漏等 |

### `ContactTrapConfig` : `TrapConfig`

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Cooldown` | `float` | `5` | 冷却秒数 |
| `OnContact` | `Func<Limb, ContactTrapConfig, bool>` | 必填 | 每次有效接触时调用。返回 true 消耗冷却。仅服务端执行 |
| `ContactSound` | `string` | `null` | 接触时播放的音效 ID。null=无声。双端执行 |
| `ContactSprite` | `Sprite` | `null` | 接触时短暂显示的贴图。null=无变化。双端执行 |
| `OnContactTriggered` | `Action<Limb, ContactTrapBase>` | `null` | 接触时立即回调。双端执行。用途：闪烁、粒子等视觉效果 |
| `OnDestroyed` | `Action<Vector3, ContactTrapConfig>` | `null` | 被破坏时调用（仅伤害破坏，非场景卸载）。仅服务端/单机执行 |

---

## 基类核心方法

### `TrapBase`

| 方法 | 说明 |
|------|------|
| `Awake()` | 初始化 SpriteRenderer、BoxCollider2D、Rigidbody2D、BuildingEntity；执行贴地；递增 SpawnCount。**override 时必须先调 base.Awake()** |
| `Update()` | 检测生命值 < 0.5 并播放破坏音效。**override 时必须先调 base.Update()** |
| `Place(int savedLayer)` | virtual——可完全覆盖摆放逻辑。默认走 `CustomPlacement` 或 `Pivot` 射线贴地 |
| `GetOrAdd<T>()` | 获取组件，不存在则添加 |

### `ExplosiveTrapBase : TrapBase`

| 方法 | 说明 |
|------|------|
| `Update()` | 额外：调用 `CustomTrigger`；执行引信倒计时 + `OnFuseUpdate` |
| `OnCollisionEnter2D()` | 默认碰撞触发（`CustomTrigger` 设置后自动跳过）。先走 `TriggerFilter`，fallback 到内置检测 |
| `Trigger()` | **public**——点燃引信。播放 `FuseSound`，切换 `PressedSprite`，调 `OnTriggered`。`FuseTime=0` 时直接引爆 |
| `Detonate()` | virtual——爆炸 → zone → blast → burst → 粒子 → 销毁。可 override |
| `ApplyBlast(Vector3 center)` | virtual——`BlastRadius>0` 时自动扫描半径内 Body，逐体调用 `ApplyBlastDebuff` |
| `ApplyBlastDebuff(Body)` | virtual——override 此方法定义直接命中效果（伤害、减速、疾病等）。服务端执行 |
| `OnDestroy()` | 未引爆即被破坏时调 `OnDestroyedWithoutDetonation`（泄漏等） |

### `ContactTrapBase : TrapBase`

| 方法 | 说明 |
|------|------|
| `Update()` | 额外：冷却递减 |
| `OnCollisionEnter2D()` | 通过 `Body.LimbFromObject` 获取碰撞肢体；播放 `ContactSound`、切换 `ContactSprite`、调用 `OnContactTriggered`（双端），然后调用 `OnContact`（仅服务端）|
| `OnDestroy()` | 破坏时调用 `OnDestroyed`（仅当生命 < 0.5 时）|

### `TrapZone`

| 成员 | 说明 |
|------|------|
| `radius` / `duration` / `fogColor` | 由 `CreateZone` 回调在创建时设置 |
| `fadeTime` | 距过期多少秒时触发 `OnExpiring`（默认 10s） |
| `tickInterval` | `OnTick` 的间隔秒数。0=禁用 |
| `Start()` | 添加 CircleCollider2D + 迷雾精灵 + `Destroy(gameObject, duration)` |
| `FixedUpdate()` | 每秒遍历 Body 调 `ApplyEffect`；检测进入/离开；定期 `OnTick`；过期前 `OnExpiring`。客户端自动跳过伤害 |
| `ApplyEffect(Body, dist)` | **abstract**——每秒每体。服务端执行 |
| `OnBodyEnter(Body)` | virtual——Body 进入区域时调用一次。服务端执行 |
| `OnBodyExit(Body)` | virtual——Body 离开区域时调用一次。服务端执行 |
| `OnExpiring()` | virtual——进入淡出期时调用一次（`_age > duration - fadeTime`）。服务端执行 |
| `OnTick()` | virtual——每 `tickInterval` 秒调用一次。双端执行。用途：粒子、音效 |

---

## 多人同步行为

| 事件 | 服务端 | 客户端 |
|------|--------|--------|
| 世界生成陷阱 | ✓ 生成 | ✓ 生成 |
| 碰撞 / 触发 | ✓ | ✓ |
| 引信 / `OnTriggered` / `OnFuseUpdate` | ✓ | ✓ |
| `Trigger()` 瞬时引爆 | ✓ 爆炸+伤害 | ✓ 仅视觉效果（zone/fog/破坏）|
| `CreateExplosion` | ✓ 真实爆炸 + 发包 | ← 收到包 → 纯视觉爆炸 |
| `CreateZone` / `SpawnFogParticles` | ✓ | ✓ (随本地引爆执行，仅视觉) |
| `TrapZone.ApplyEffect` | ✓ | — |
| `TrapZone.OnBodyEnter/Exit` | ✓ | — |
| `TrapZone.OnExpiring` | ✓ | — |
| `TrapZone.OnTick` | ✓ | ✓ |
| `OnBurst` / `ApplyBlastDebuff` | ✓ | — |
| `OnDestroyedWithoutDetonation` | ✓ | — |
| 掉落物品 | ✓ (BuildingEntity) | — |

要点：
- **伤害/状态修改放 `OnBurst`、`ApplyBlastDebuff` 或 `ApplyEffect`**——这些自动限制服务端
- **视觉效果放 `OnTriggered`、`OnFuseUpdate`、`OnTick`、`OnContactTriggered`**——双端可见
- **`OnExpiring` 仅服务端执行，不要在其中放客户端视觉效果**
- **勿在服务端独占回调中添加 MonoBehaviour 组件**——组件不会自动同步到客户端

---

## 工具类

### `Attenuation` — 距离衰减公式

全部接受 `t = distance / maxRadius` (0=中心, 1=边界)，返回 0~1 倍率：

| 方法 | 公式 | 说明 |
|------|------|------|
| `Linear(t)` | `1 - t` | 线性衰减 |
| `SmoothStep(t)` | `2t³ - 3t² + 1` | 三次 S 曲线，两端平缓 |
| `SmootherStep(t)` | `-6t⁵ + 15t⁴ - 10t³ + 1` | 五次曲线，一二阶导数两端为零 |
| `SquareRoot(t)` | `√(1 - t)` | 前期高，末段骤降 |
| `PowerCurve(t, n)` | `(1 - t)^n` | 可调指数(n>1=中心附近快衰减, n<1=长尾) |
| `Exponential(t, k)` | `e^(-k·t)` | 指数衰减，永不归零 |
| `InverseSquare(t, k)` | `1 / (1 + k·t²)` | 物理平方反比 |
| `RadialThreshold(t, r)` | 内核满→外圈线性 | `t≤r` 返回 1.0，超出线性衰减到 0 |
| `ExponentForEdgeFalloff(m)` | — | 根据期望边缘倍率反算 PowerCurve 指数 |
| `SteepnessForEdgeFalloff(m)` | — | 根据期望边缘倍率反算 Exponential steepness |

### `SpriteLoader` — 贴图加载

```csharp
// 基础：全图加载（pivot 默认中心 0.5,0.5）
var sprite = SpriteLoader.FromFile("完整路径.png", ppu: 8f);

// 自定义 pivot（如底部居中 0.5,0 用于地面陷阱）
var sprite = SpriteLoader.FromFile("path.png", ppu: 8f, pivot: new Vector2(0.5f, 0f));

// 自动裁切透明边框
var sprite = SpriteLoader.FromFileAutoCrop("path.png", ppu: 8f, pivot: new Vector2(0.5f, 0f));
```

所有方法有缓存。失败时返回 null 并 LogWarning。

辅助方法：

| 方法 | 说明 |
|------|------|
| `LoadTexture(path)` | 加载 PNG 为点过滤 Texture2D |
| `GetContentRect(tex, alphaThreshold)` | 查找非透明像素最小包围矩形 |
| `GetWorldSize(sprite, scale)` | 返回 sprite 的世界空间尺寸 |
| `FitColliderToSprite(col, sprite, scale, pivot, wPad, hPad)` | 调整 BoxCollider2D 匹配 sprite 内容 |

### `FogSpriteCache` — 迷雾贴图

128×128 径向渐变纹理，PPU=8，衰减指数 0.7。`TrapZone.Start()` 自动使用，一般不需要手动调用。

### `TrapSounds` — 音效

音效通过 `TrapConfig.Sounds` 设置，`RegisterSounds()` 在 TrapLib 启动时自动注册，无需手动操作。

---

## 完整示例：毒气地雷

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
                // 直接命中：5u 内每肢体 -8 皮肤, -4 肌肉, +15 疾病
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

## 与 RshLib 的关系

- **TrapLib 不依赖 RshLib 编译**——通过 BepInEx 运行时检测
- 如果 RshLib 已安装：`BuildingEntity.Update` 使用 RshLib 的 patch（支持自定义物品掉落）
- 如果 RshLib 未安装：TrapLib 加载自己的 `BuildingEntityPatch`（使用 `Utils.Create`，兼容自定义物品）
- 陷阱掉落物用原版物品 ID（如 `"scrapmetal"`）无需 RshLib；用自定义物品 ID 则需同时安装 RshLib

## 文件结构

```
你的mod/
├── YourMod.csproj     ← 添加 TrapLib 项目引用
├── Plugin.cs           ← Register<T>()
├── YourTrap.cs         ← class YourTrap : ExplosiveTrapBase { }
├── YourZone.cs         ← class YourZone : TrapZone { override ApplyEffect... }
└── res/
    └── your_sprite.png
```
