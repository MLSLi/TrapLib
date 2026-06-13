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
        // Only override destruction for TrapLib traps — let everything else run original Update
        if (!__instance.TryGetComponent<TrapBase>(out _))
            return true;

        // --- rigidbody optimisation ---
        if (__instance.TryGetComponent<Rigidbody2D>(out var rb) && !__instance.ignoreBodyOptimize)
        {
            var world = WorldGeneration.world;
            rb.bodyType = (!world.worldExists
                || !world.GetClosestChunkRenderer(world.WorldToBlockPos(__instance.transform.position)).enabled
                || !(Time.timeScale <= 5f))
                ? RigidbodyType2D.Static
                : RigidbodyType2D.Dynamic;
        }

        if (!(__instance.health < 0.5f))
            return false;

        // On MP client, destroy locally — server handles drops and particles.
        // TrapLib traps are spawned during worldgen on both sides; client destruction
        // is safe because KrokMP's tracker component handles NetObjectRegistry cleanup.
        if (MPSync.IsClient)
        {
            if (__instance.TryGetComponent<TrapBase>(out var t))
                t.MarkDestroyed();
            Object.Destroy(__instance.gameObject);
            return false;
        }

        // --- destruction particles ---
        if (__instance.TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
        {
            var particleObj = Object.Instantiate(Resources.Load("BuildingBreakParticle"),
                __instance.transform.position, __instance.transform.rotation);
            if (particleObj is GameObject go)
            {
                var shape = go.GetComponent<ParticleSystem>().shape;
                shape.texture = sr.sprite.texture;
                shape.sprite = sr.sprite;
                go.GetComponent<ParticleSystem>().Play();
            }
        }

        Object.Instantiate(Resources.Load<GameObject>("DustBig"),
            __instance.transform.position, Quaternion.identity);

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
        go.GetComponent<Rigidbody2D>().velocity = new Vector2(Random.Range(-7f, 7f), Random.Range(-7f, 7f));
        go.GetComponent<Item>().SetCondition(condition);
        if (fresh) go.AddComponent<FreshItemDrop>();
    }
}
