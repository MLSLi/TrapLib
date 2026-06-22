using System;
using System.Reflection;
using HarmonyLib;
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

        var instantiating = Traverse.Create(__instance).Field("instantiatingWorld").GetValue<bool>();
        if (instantiating) return;

        if (!__instance.worldExists) return;

        DidSpawn = true;

        TrapLibPlugin.Log?.LogInfo($"Generation done: biomeDepth={__instance.biomeDepth}, trapRarity={__instance.totalTrapRarity}");

        foreach (var kv in TrapRegistry.Entries)
        {
            var config = kv.Value.config;
            var type = kv.Value.type;
            if (__instance.biomeDepth < config.MinBiomeDepth) continue;
            if (config.MaxBiomeDepth > 0 && __instance.biomeDepth > config.MaxBiomeDepth) continue;

            float min = config.SpawnRateMin * __instance.totalTrapRarity;
            float max = config.SpawnRateMax * __instance.totalTrapRarity;

            var prefab = TrapRegistry.GetOrCreatePrefab(type, config);
            if (prefab == null) continue;

            var methodFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
            var resetMethod = type.GetMethod("ResetSpawnCount", methodFlags);
            resetMethod?.Invoke(null, null);

            __instance.DistributeEntities(prefab, min, max, config.SpawnYOffset, 0f,
                config.SpawnYOffsetDeviation, config.SpawnInGround, false, null, true);

            var fieldFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
            var countField = type.GetField("SpawnCount", fieldFlags);
            int count = countField != null ? (int)countField.GetValue(null) : -1;
            TrapLibPlugin.Log?.LogInfo($"{config.Id}: spawned={count}");
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
