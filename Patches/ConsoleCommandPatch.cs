using System;
using HarmonyLib;
using TrapLib.MP;
using UnityEngine;

namespace TrapLib.Patches;

/// <summary>
/// Adds registered trap IDs to the /spawn command autofill list and handles
/// trap instantiation when the command is executed.
/// </summary>
[HarmonyPatch(typeof(ConsoleScript), "RegisterSpawnEntities")]
internal static class ConsoleCommandPatch
{
    private static bool _wrapped;

    private static void Postfix()
    {
        var cmd = ConsoleScript.SearchExact("spawn");
        if (cmd == null) return;

        // Add registered trap IDs to autofill
        if (cmd.argAutofill != null && cmd.argAutofill.TryGetValue(0, out var list))
            foreach (var id in TrapRegistry.Entries.Keys)
                if (!list.Contains(id)) list.Add(id);

        // Wrap the original action once to intercept TrapLib trap IDs
        if (_wrapped) return;
        _wrapped = true;

        var originalAction = cmd.action;
        cmd.action = args =>
        {
            if (args.Length >= 2 && TrapRegistry.Entries.ContainsKey(args[1]))
            {
                if (MPSync.IsClient)
                {
                    TrapLibPlugin.Log?.LogWarning("[ConsoleCommandPatch] /spawn trap blocked on MP client — only server/host can spawn traps");
                    return;
                }
                var pos = GetSpawnPosition(args);
                TrapRegistry.Spawn(args[1], pos);
                return;
            }
            originalAction(args);
        };
    }

    private static Vector3 GetSpawnPosition(string[] args)
    {
        if (args.Length > 2)
        {
            var input = args[2];

            if (input == "cursor")
                return CursorWorldPos();

            if (input == "player" && PlayerCamera.main != null)
                return PlayerCamera.main.body.transform.position;

            if (input == "random" && WorldGeneration.world != null)
                return new Vector2(
                    UnityEngine.Random.Range(-1f, 1f) * WorldGeneration.world.halfWidth,
                    UnityEngine.Random.Range(-1f, 1f) * WorldGeneration.world.halfHeight);

            float x, y;
            if (float.TryParse(input, out x) && args.Length >= 4 && float.TryParse(args[3], out y))
                return new Vector3(x, y, 0f);

            var parts = input.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2
                && float.TryParse(parts[0], out x)
                && float.TryParse(parts[1], out y))
                return new Vector3(x, y, 0f);
        }

        return CursorWorldPos();
    }

    private static Vector3 CursorWorldPos()
    {
        if (Camera.main == null)
        {
            TrapLibPlugin.Log?.LogWarning("[ConsoleCommandPatch] Camera.main is null, cannot get cursor position");
            return Vector3.zero;
        }
        var wp = Camera.main.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
        return new Vector3(wp.x, wp.y, 0f);
    }
}
