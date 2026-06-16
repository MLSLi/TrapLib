using HarmonyLib;
using UnityEngine;

namespace TrapLib.MP;

/// <summary>
/// Intercepts Resources.Load so KrokMP can find custom-trap prefabs by their
/// registered ID (e.g. "pistontrap", "frostmine").  Without this the client
/// logs "Resource does not exist" and the trap never appears.
/// </summary>
[HarmonyPatch(typeof(Resources), "Load", typeof(string), typeof(System.Type))]
internal static class ResourcesLoadPatch
{
    private static bool Prefix(string path, System.Type systemTypeInstance, ref Object __result)
    {
        if (systemTypeInstance != typeof(GameObject)) return true;
        if (!TrapRegistry.Entries.TryGetValue(path, out var entry)) return true;

        var prefab = TrapRegistry.GetOrCreatePrefab(entry.type, entry.config);
        if (prefab != null)
        {
            __result = prefab;
            return false;
        }
        return true;
    }
}
