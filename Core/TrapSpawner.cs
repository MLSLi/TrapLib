using HarmonyLib;
using TrapLib.MP;
using UnityEngine;

namespace TrapLib;

/// <summary>
/// Hooks WorldGeneration.Update to distribute registered traps.
/// Prefab templates are created on first use via TrapRegistry.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "Update")]
internal static class TrapSpawner
{
    internal static bool DidSpawn;
    internal static bool WasGenerating;

    private static void Postfix(WorldGeneration __instance)
    {
        if (__instance.generatingWorld) { WasGenerating = true; return; }
        if (WasGenerating) { DidSpawn = false; WasGenerating = false; }
        if (DidSpawn) return;

        // In multiplayer, clients receive world objects from the server. Running
        // local random distribution creates duplicate traps and heavy physics load.
        if (MPSync.IsClient) { DidSpawn = true; return; }

        var instantiating = Traverse.Create(__instance).Field("instantiatingWorld").GetValue<bool>();
        if (instantiating) return;

        if (!__instance.worldExists) return;

        DidSpawn = true;

        TrapLibPlugin.Log?.LogInfo($"Generation done: biomeDepth={__instance.biomeDepth}, trapRarity={__instance.totalTrapRarity}");

        var world = __instance;
        var bandHeight = (int)(world.height / world.amountOfLayers);

        foreach (var kv in TrapRegistry.Entries)
        {
            var config = kv.Value.config;
            var type = kv.Value.type;
            if (world.biomeDepth < config.MinBiomeDepth) continue;
            if (config.MaxBiomeDepth > 0 && world.biomeDepth > config.MaxBiomeDepth) continue;

            float min = config.SpawnRateMin * world.totalTrapRarity;
            float max = config.SpawnRateMax * world.totalTrapRarity;

            var prefab = TrapRegistry.GetOrCreatePrefab(type, config);
            if (prefab == null) continue;

            TrapBase.ResetSpawnCount();

            var minBiome = config.MinBiomeDepth;
            var maxBiome = config.MaxBiomeDepth;

            world.DistributeEntities(prefab, min, max, config.SpawnYOffset, 0f,
                config.SpawnYOffsetDeviation, config.SpawnInGround, false,
                (Vector2Int blockPos) =>
                {
                    // biome 0 = top band (high blockY), biome N = bottom band (low blockY)
                    int biome = ((int)world.height - blockPos.y - 1) / bandHeight;
                    if (biome < 0) biome = 0;
                    if (biome >= world.amountOfLayers) biome = world.amountOfLayers - 1;
                    if (biome < minBiome) return false;
                    if (maxBiome > 0 && biome > maxBiome) return false;
                    return true;
                },
                true);

            TrapLibPlugin.Log?.LogInfo($"{config.Id}: spawned={TrapBase.SpawnCount}");
        }
    }
}

[HarmonyPatch(typeof(WorldGeneration), "Start")]
internal static class TrapSpawner_Reset
{
    private static void Postfix()
    {
        TrapSpawner.DidSpawn = false;
        TrapSpawner.WasGenerating = false;
        TrapBase.TrapBuildings.Clear();
    }
}
