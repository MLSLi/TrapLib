using HarmonyLib;
using TrapLib.MP;
using UnityEngine;

namespace TrapLib.Patches;

/// <summary>
/// Drop handler for BuildingEntity destruction.
/// Uses Utils.Create (RshLib-aware) when available, otherwise Resources.Load.
/// Applied ONLY when RshLib is NOT installed — otherwise RshLib's own patch takes over.
/// </summary>
internal static class BuildingEntityPatch
{
    private static GameObject _cachedDustBig;
    private static GameObject _cachedBuildingBreakParticle;

    private static GameObject DustBigPrefab
    {
        get
        {
            if (_cachedDustBig == null)
                _cachedDustBig = Resources.Load<GameObject>("DustBig");
            return _cachedDustBig;
        }
    }

    private static GameObject BuildingBreakParticlePrefab
    {
        get
        {
            if (_cachedBuildingBreakParticle == null)
                _cachedBuildingBreakParticle = Resources.Load("BuildingBreakParticle") as GameObject;
            return _cachedBuildingBreakParticle;
        }
    }

    /// <summary>Apply the patch. Safe to call regardless of RshLib status.</summary>
    internal static void Apply(Harmony harmony)
    {
        var update = AccessTools.Method(typeof(BuildingEntity), "Update");
        harmony.Patch(update, new HarmonyMethod(typeof(BuildingEntityPatch), nameof(Prefix)));

        // Postfix on Start to restore fullName after Locale.GetBuilding overwrites it.
        // Individual mods can override with their own Postfix (e.g. BuildingEntity_NamePatch).
        var start = AccessTools.Method(typeof(BuildingEntity), "Start");
        harmony.Patch(start, postfix: new HarmonyMethod(typeof(BuildingEntityPatch), nameof(StartPostfix)));
    }

    private static void StartPostfix(BuildingEntity __instance)
    {
        if (!__instance.TryGetComponent<TrapBase>(out var trap) || trap.Config == null) return;
        var cfg = trap.Config;
        var cn = Utilities.LocaleHelper.IsChinese();
        __instance.fullName = (cn && cfg.FullNameCn != null) ? cfg.FullNameCn
            : cfg.FullName ?? cfg.Id;
        __instance.description = (cn && cfg.DescriptionCn != null) ? cfg.DescriptionCn
            : cfg.Description ?? "";
    }

    private static bool Prefix(BuildingEntity __instance)
    {
        // Fast-path: avoid expensive TryGetComponent every frame.
        if (!TrapBase.TrapBuildings.Contains(__instance))
            return true;

        // --- rigidbody optimisation (multiplayer-aware) ---
        // Note: Returning false here skips the original Update, which also skips
        // KrokMP's BuildingEntity_Update_MultiplayerPatch transpiler. We replicate
        // its multiplayer-aware chunk visibility logic directly.
        if (__instance.TryGetComponent<Rigidbody2D>(out var rb) && !__instance.ignoreBodyOptimize)
        {
            var world = WorldGeneration.world;
            bool shouldDynamic = false;
            if (world.worldExists)
            {
                var worldPos = (Vector2)__instance.transform.position;
                // In multiplayer, check if ANY player can see this chunk
                if (TrapLibPlugin.KrokMpEnabled)
                {
                    foreach (var body in MPSync.AllPlayerBodies)
                    {
                        if (body == null) continue;
                        float dist = Vector2.Distance(worldPos, body.transform.position);
                        if (dist < 120f) // chunk render distance ~120 units
                        {
                            shouldDynamic = true;
                            break;
                        }
                    }
                }
                else
                {
                    shouldDynamic = world.GetClosestChunkRenderer(
                        world.WorldToBlockPos(__instance.transform.position)).enabled;
                }
            }
            rb.bodyType = shouldDynamic ? RigidbodyType2D.Dynamic : RigidbodyType2D.Static;
        }

        if (!(__instance.health < 0.5f))
            return false;

        // On MP client, destroy locally with visual feedback.
        // Drops are handled by the server; particles are purely cosmetic.
        if (MPSync.IsClient)
        {
            SpawnDestructionParticles(__instance);
            var dustPrefab = DustBigPrefab;
            if (dustPrefab != null)
                Object.Instantiate(dustPrefab, __instance.transform.position, Quaternion.identity);
            if (__instance.TryGetComponent<TrapBase>(out var t))
                t.MarkDestroyed();
            // Deferred destroy: gives KrokMP ~1s to finish any pending sync
            // packets for this object before it's removed from the scene.
            Object.Destroy(__instance.gameObject, 1f);
            return false;
        }

        // --- destruction particles ---
        SpawnDestructionParticles(__instance);

        var dustBig = DustBigPrefab;
        if (dustBig != null)
            Object.Instantiate(dustBig, __instance.transform.position, Quaternion.identity);
        else
            TrapLibPlugin.Log?.LogWarning("[BuildingEntityPatch] Failed to load resource: DustBig");

        if (__instance.animal)
            __instance.gameObject.SendMessage("AnimalDeath");

        Sound.Play("footstep/Rock/11", __instance.transform.position);

        bool freshDrop = PlayerCamera.main?.body != null
            && Vector2.Distance(__instance.transform.position,
                PlayerCamera.main.body.transform.position) < 8f;

        // itemsDropOnDestroy
        foreach (var drop in __instance.itemsDropOnDestroy ?? System.Array.Empty<ItemDrop>())
        {
            if (Random.Range(0f, 1f) >= drop.chance * __instance.dropChanceMultiplier) continue;
            SpawnDropItem(drop.id, __instance.transform.position,
                Random.Range(drop.conditionMin, drop.conditionMax), freshDrop);
        }

        // guaranteedDropAmount
        for (int j = 0; j < __instance.guaranteedDropAmount; j++)
        {
            var categories = __instance.itemCategoriesToAdd;
            if (categories == null || categories.Length == 0) break;
            var pool = ItemLootPool.pool[categories[Random.Range(0, categories.Length)]];
            var pick = pool[Random.Range(0, pool.Count)];
            SpawnDropItem(pick, __instance.transform.position, 1f, freshDrop);
        }

        // alwaysDrop
        foreach (var drop in __instance.alwaysDrop ?? System.Array.Empty<ItemDrop>())
        {
            SpawnDropItem(drop.id, __instance.transform.position,
                Random.Range(drop.conditionMin, drop.conditionMax), freshDrop);
        }

        if (__instance.TryGetComponent<TrapBase>(out var trap))
            trap.MarkDestroyed();
        Object.Destroy(__instance.gameObject);
        return false;
    }

    private static void SpawnDropItem(string id, Vector3 pos, float condition, bool fresh)
    {
        var go = Utils.Create(id, pos, Random.Range(0f, 360f));
        if (go == null) return;
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = new Vector2(Random.Range(-7f, 7f), Random.Range(-7f, 7f));
        var item = go.GetComponent<Item>();
        if (item != null) item.SetCondition(condition);
        if (fresh) go.AddComponent<FreshItemDrop>();
    }

    /// <summary>Spawns sprite-shaped break particle at the building position. Cosmetic only.</summary>
    internal static void SpawnDestructionParticles(BuildingEntity be)
    {
        if (be.TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
        {
            var particlePrefab = BuildingBreakParticlePrefab;
            if (particlePrefab != null)
            {
                var particleObj = Object.Instantiate(particlePrefab, be.transform.position, be.transform.rotation);
                if (particleObj is GameObject go)
                {
                    var shape = go.GetComponent<ParticleSystem>().shape;
                    shape.texture = sr.sprite.texture;
                    shape.sprite = sr.sprite;
                    go.GetComponent<ParticleSystem>().Play();
                }
            }
            else
            {
                TrapLibPlugin.Log?.LogWarning("[BuildingEntityPatch] Failed to load resource: BuildingBreakParticle");
            }
        }
    }
}
