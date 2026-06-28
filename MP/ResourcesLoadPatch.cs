using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TrapLib.MP;

/// <summary>
/// Handles KrokMP object sync for TrapLib prefabs without globally patching Resources.Load.
/// </summary>
[HarmonyPatch]
internal static class ResourcesLoadPatch
{
    private static MethodBase TargetMethod()
    {
        var type = System.Type.GetType("KrokoshaCasualtiesMP.NewCoolerObjectPacketWriteReadSystem, KrokoshaCasualtiesMP");
        if (type == null) return null;

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (method.Name != "LoadObjectResource") continue;
            var parameters = method.GetParameters();
            if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string)) return method;
        }
        return null;
    }

    private static bool Prefix(object[] __args, ref GameObject __result)
    {
        if (__args == null || __args.Length < 2) return true;
        if (__args[0] is not string resourceId) return true;
        if (!TrapRegistry.Entries.TryGetValue(resourceId, out var entry)) return true;

        var prefab = TrapRegistry.GetOrCreatePrefab(entry.type, entry.config);
        if (prefab == null) return true;

        var position = __args[1] is Vector2 vector ? vector : Vector2.zero;
        __result = Object.Instantiate(prefab, position, Quaternion.identity);
        return false;
    }
}
