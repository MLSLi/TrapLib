# TrapLib — 开发者文档

TrapLib 是一个 BepInEx 前置库，为自定义陷阱提供统一的注册、生成、生命周期、多人同步框架。你只需要准备贴图和写几行行为逻辑，剩下的摆放、掉落、音效、世界生成都由 TrapLib 处理。

## 依赖

- **硬依赖**：BepInEx + Harmony（BepInEx 自带）
- **软依赖**：RshLib by rushellxyz（自定义物品库，可选）
- **软依赖**：[KrokoshaCasualtiesMP](https://github.com/Krokosha666/cas-unk-krokosha-multiplayer-coop)（多人联机，可选）

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
├─ TrapZone             ← 持续区域：迷雾贴图、碰撞检测、每秒效果
└─ Utilities            ← SpriteLoader、TrapPlacement、Attenuation、TrapSounds
```

## 快速开始

### 1. 准备贴图

用 `SpriteLoader.FromFile()` 或任意方式加载 `Sprite`：

```csharp
var sprite = SpriteLoader.RequireFromFileAutoCrop("path/to/my_trap.png", ppu: 8f,
    pivot: new Vector2(0.5f, 0f));
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
    MaxBiomeDepth = 0, // 0 = 无上限
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
| `CustomPlacement` | `Func<Vector3, SpriteRenderer, Vector3>` | `null` | 完全接管摆放逻辑。返回最终世界坐标。不为 null 时跳过默认射线贴地。TrapLib 会在回调后恢复 GameObject layer，鼠标悬停检测仍可正常工作 |
| `Health` | `float` | `400` | 生命值 |
| `ColliderSize` | `Vector2` | `(2, 1)` | BoxCollider2D 尺寸 |
| `Metallic` | `bool` | `true` | 是否金属（影响脚步声） |
| `MinBiomeDepth` | `int` | `0` | 最低出现层级（0-based。0=第一层）。陷阱只会在世界对应纵向带内分布 |
| `MaxBiomeDepth` | `int` | `0` | 最高出现层级（0-based，含）。`0` = 无上限 |
| `SpawnRateMin` | `float` | 必填 | 占 `totalTrapRarity` 的下限比例 |
| `SpawnRateMax` | `float` | 必填 | 占 `totalTrapRarity` 的上限比例 |
| `SpawnYOffset` | `float` | `0` | 传给 `DistributeEntities` 的 `spawnYOffset`，表示沿表面法线的世界空间偏移 |
| `SpawnYOffsetDeviation` | `float` | `0` | 围绕 `SpawnYOffset` 的随机偏移范围 |
| `SpawnInGround` | `bool` | `false` | 是否允许 `DistributeEntities` 从地块内部开始寻找放置点 |
| `Sounds` | `(string hit, string destroy)` | `("scrapmetal", "containerBreak")` | 受击音效 ID / 破坏音效 ID。TrapLib 自动注册，无需手动操作 |
| `Drops` | `ItemDrop[]` | 空数组 | 通过 `itemsDropOnDestroy` 掉落的物品 |
| `DoNotBreakOnGroundDestroyed` | `bool` | `false` | 为 true 时，下方方块被破坏不会连带摧毁陷阱。适用于悬浮/吸附类陷阱 |

### `ExplosiveTrapConfig` : `TrapConfig`

`ExplosionParams` 必填（无默认值）；其余全部可选（null / 0 = 禁用或使用默认值）。

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `ExplosionRange` | `float` | `25` | 爆炸伤害半径 (`CreateExplosion.range`)。独立于区域半径 |
| `ZoneRadius` | `float` | `0` | 持续区域半径。≤0 时回退到 `ExplosionRange`。与爆炸半径解耦——大范围迷雾 + 小范围爆炸伤害 |
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
| `BlastRadius` | `float` | `0` | 爆炸瞬间冲击波半径。>0 时对范围内 Body 调用 `ApplyBlastDebuff`（服务端对所有玩家，客户端对本地玩家提供即时反馈）。0=禁用 |
| `OnBurst` | `Action<Vector3, ExplosiveTrapConfig>` | `null` | 爆炸瞬间额外一次性效果（在 `BlastRadius` 处理之后）。**仅服务端/单机执行** |
| `OnDestroyedWithoutDetonation` | `Action<ExplosiveTrapBase, Vector3>` | `null` | 被打爆但未引爆时回调。参数 (trap, center)。仅服务端/单机执行。用途：核泄漏等 |
| `NoClientFallback` | `bool` | `false` | 为 true 时，即使安装 KrokMP 也跳过客户端爆炸回退效果（粒子、音效、冲击痕迹）。当 KrokMP 可靠同步该陷阱的 `CreateExplosion` 时使用，避免双份效果 |

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
| `OnExpiring()` | virtual——进入淡出期时每帧调用。双端执行 |
| `OnTick()` | virtual——每 `tickInterval` 秒调用一次。双端执行。用途：粒子、音效 |
| `Create<T>(name, center, cfg)` | **static**——创建 zone GameObject（设 Ground 层、触发器、迷雾精灵、radius/duration/fogColor）。返回 `T` 以便设置额外参数 |
| `Create<T>(name, center, cfg, configure)` | **static**——同上，然后执行 `configure(zone)` 并返回 GameObject |

### `TimedDestroy` (`TrapLib.Utilities`)

可复用基类——`duration` 秒后自毁。子类覆盖 `OnUpdate()`（到期前每帧）和 `OnExpire()`（一次性清理）。

```csharp
class MyDebuff : TimedDestroy
{
    protected override void OnUpdate() { /* 每帧效果 */ }
    protected override void OnExpire() { /* 到期清理 */ }
}
```

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
| `TrapZone.OnExpiring` | ✓ | ✓ |
| `TrapZone.OnTick` | ✓ | ✓ |
| `OnBurst` | ✓ | — |
| `ApplyBlastDebuff` | ✓ 全部玩家 | ✓ 仅本地玩家 (即时反馈) |
| `Trigger` 默认碰撞 (布娃娃) | ✓ | ✓ (含肢体回退检测) |
| `OnDestroyedWithoutDetonation` | ✓ | — |
| 掉落物品 | ✓ (BuildingEntity) | — |

要点：

- **伤害/状态修改放 `OnBurst`、`ApplyBlastDebuff` 或 `ApplyEffect`**——这些自动限制服务端
- **视觉效果放 `OnTriggered`、`OnFuseUpdate`、`OnTick`、`OnContactTriggered`**——双端可见
- **`OnExpiring` 双端执行——可用于客户端视觉淡出效果**
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

// 必需资源：找不到时抛 FileNotFoundException，而不是返回 null
var sprite = SpriteLoader.RequireFromFileAutoCrop("path.png", ppu: 8f, pivot: new Vector2(0.5f, 0f));
var tex = SpriteLoader.RequireTexture("path.png");
```

所有贴图加载方法均有缓存。`From*` 方法失败时返回 null 并 LogWarning；`Require*` 方法用于必需资源，失败时抛 `FileNotFoundException`。

辅助方法：

| 方法 | 说明 |
|------|------|
| `LoadTexture(path)` | 加载 PNG 为点过滤 Texture2D |
| `RequireTexture(path)` | 加载 PNG，失败时抛 `FileNotFoundException` |
| `GetContentRect(tex, alphaThreshold)` | 查找非透明像素最小包围矩形 |
| `GetWorldSize(sprite, scale)` | 返回 sprite 的世界空间尺寸 |
| `FitColliderToSprite(col, sprite, scale, pivot, wPad, hPad)` | 调整 BoxCollider2D 匹配 sprite 内容 |

### `TrapPlacement` — 自定义放置辅助

用于 `CustomPlacement` 或重写 `Place()` 时复用射线检测和 layer 处理：

| 方法 | 说明 |
|------|------|
| `WithIgnoredSelfLayer(go, action)` | 临时把 `go` 放到 `Ignore Raycast`，执行 `action` 后恢复原 layer |
| `TryFindNearestSurface(pos, mask, out hit, ...)` | 向左/右/上/下射线寻找最近表面，失败后长距离向下兜底 |
| `TryFindFloorThenNearestSurface(pos, mask, out hit, ...)` | 优先找地面，失败后找最近表面 |
| `OffsetFromSurface(hit, offset, z)` | 根据命中点、法线偏移和 z 值计算最终世界坐标 |
| `VerticalSpriteHalfHeight(sr, scale)` | 计算缩放后 sprite 的半高，用于直立物体贴地 |

### `FogSpriteCache` — 迷雾贴图

128×128 径向渐变纹理，PPU=8，衰减指数 0.7。`TrapZone.Start()` 自动使用，一般不需要手动调用。

### `TrapSounds` — 音效

音效通过 `TrapConfig.Sounds` 设置。音效地图在首次访问时从 `TrapRegistry` 延迟加载——无需手动操作。调用 `TrapSounds.Refresh()` 可在延迟注册后强制重新加载。

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
            CreateZone = (center, cfg) => TrapZone.Create<GasZone>("GasZone", center, cfg),
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

---

## 更新日志

### v1.1.3

- **KrokMP v4.0.1 支持**：`ResourcesLoadPatch` 改为 hook KrokMP 内部的 `LoadObjectResource`，不再全局拦截 `Resources.Load`。新增 `MPSync.QueueObjectSync` 用于服务端权威状态同步。服务端 `Object.Destroy` 改为延迟 0.1s，确保 health=0 同步先于 GO 销毁到达客户端。`BuildingEntityPatch` 区块可见性使用 KrokMP 的 `CheckIfChunkOnThisPositionIsVisibleByAnyPlayer`。
- **按层级分布**：`DistributeEntities` 现在通过方块 Y 坐标反算所属 biome，按 `[MinBiomeDepth, MaxBiomeDepth]` 过滤候选位置，陷阱不再出现在错误层级。
- **重复破坏守卫**：`BuildingEntityPatch.Prefix` 在 `TrapBase.IsDestroyed` 已为 true 时跳过破坏逻辑，防止 0.1s 销毁延迟窗口内重复掉落物品和粒子。
- **客户端受击追踪**：`BuildingHit` 记录 `_lastClientHitTime`；`ExplosiveTrapBase` 据此判断 `WasRecentlyHitOnClient` 来决定客户端预测引爆行为。
- **ContactTrapBase** 服务端通过 `TryFindOverlappingLimb` 检测重叠肢体。
- **SpriteLoader.ClearCache** 现在正确销毁缓存的纹理和精灵。

### v1.1.2

- 新增 `MaxBiomeDepth` 字段支持 `[MinBiomeDepth, MaxBiomeDepth]` 范围约束
- `TrapPlacement` 辅助工具：忽略自身层级、贴地、法线偏移
- `SpriteLoader.RequireTexture` / `RequireFromFileAutoCrop` 快速失败式贴图加载
- `ExplosiveTrapBase` 通过 health marker 实现 KrokMP 客户端引信状态同步
- `TrapZone.Create` 添加初始化委托重载
- `CustomPlacement` 后恢复 GameObject layer，修复核弹等自定义放置陷阱的悬停 tooltip
- 重命名生成偏移字段为 `SpawnYOffset` / `SpawnYOffsetDeviation` / `SpawnInGround`

### v1.1.1

- `TrapConfig` 新增 `MaxBiomeDepth`，约束陷阱生成的上层级边界

### v1.1.0

- 修复 `SnapToSurface` 起点过高：从 4f 改为 1f，防止窄洞穴内陷阱贴天花板而非地面
- 修复 `SpawnCount` 反射缺少 `FlattenHierarchy` 导致派生陷阱类日志计数累积
- 缓存资源预制体（`DustBig`、`ExplosionParticle`、`Blastmark`、`BuildingBreakParticle`），避免重复 `Resources.Load`
- 引入 `TrapBuildings` `HashSet<BuildingEntity>` 实现 O(1) 查找，替代每帧 `TryGetComponent`
- 多人物理体优化使用 `MPSync.AllPlayerBodies` 检测玩家距离
- 客户端 health→0 时触发 `Detonate`，解决 `CustomTrigger` 无法感知远程触发导致的视觉不一致
- 客户端延迟销毁（1s），给 KrokMP 同步数据包留出时间
- 新增 `NoClientFallback` 配置，当 KrokMP 处理 `CreateExplosion` 同步时跳过重复爆炸效果
- `TrapSounds.Map` 首次访问时从 `TrapRegistry` 惰性填充
- `SpriteLoader`：FIFO 缓存驱逐（最大 128）、`ClearCache()`、`CacheCount`
- `MPSync`：缓存 `PropertyInfo`、`AllPlayerBodies` 枚举器、`RunAfterDelay` 协程、改进错误日志
- 修复 `ContactTrapBase`/`ExplosiveTrapBase` `OnDestroy` 正确调用基类
- `Utils.Create`、`Camera.main`、资源加载添加空安全检查
- `TrapZone.OnExpiring` 每帧在所有实例上运行以实现视觉淡出
- 阻止 MP 客户端使用 `/spawn`（仅服务端/主机可用）

### v1.0.2

- 修复 `MPSync.IsClient` 过期缓存——每次调用重新评估 `Net.running`/`Net.is_server`，修复纯客户端"Client can't do that"崩溃
- `MPSync.QueueHealthSync` 在纯客户端静默 no-op，invoke 层添加 try-catch 防御
- `ExplosiveTrapConfig` 新增 `ZoneRadius`，将区域半径与 `ExplosionRange` 解耦
- `TrapZone.Create<T>(name, center, cfg)` 静态工厂消除区域 GameObject 初始化重复代码
- `TimedDestroy` 可复用基类，替换重复的 `_elapsed += dt`/destroy 模式
- `ExplosiveTrapBase.OnCollisionEnter2D` 添加 `Body.LimbFromObject` 回退以支持布娃娃玩家触发地雷
- `ExplosiveTrapBase.Detonate` 客户端路径：本地应用冲击 debuff 提供即时反馈

### v1.0.1

- `TrapSpawner` 添加 `worldExists` 守卫，防止世界重生成（`Clear`→`InstantiateWorld` 间隙）导致的陷阱重复
- 修复 `SnapToSurface`：射线起点前添加 `OverlapPoint` 安全循环，防止陷阱在墙壁/悬崖附近悬空或错位

### v1.0.0

- 初始发布：陷阱注册、世界生成集成、爆炸/接触陷阱基类、持续区域、多人同步、控制台 `/spawn` 命令、中英双语支持
