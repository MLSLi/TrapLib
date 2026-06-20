using System.Collections.Generic;
using UnityEngine;

namespace TrapLib.Utilities;

/// <summary>
/// Hit and destroy sound dispatch for BuildingEntity traps.
/// Other mods populate this via <see cref="TrapConfig.Sounds"/> during registration.
/// The map is lazily populated from TrapRegistry on first access.
/// </summary>
public static class TrapSounds
{
    internal static readonly Dictionary<string, (string hit, string destroy)> Map
        = new Dictionary<string, (string, string)>();

    private static bool _populated;

    private static void EnsurePopulated()
    {
        if (_populated) return;
        _populated = true;
        foreach (var entry in TrapRegistry.Entries.Values)
            Map[entry.config.Id] = (entry.config.Sounds.hit, entry.config.Sounds.destroy);
    }

    /// <summary>Called from BuildingEntityPatch when a trap takes melee damage.</summary>
    public static void PlayHit(BuildingEntity be)
    {
        if (be == null) return;
        EnsurePopulated();
        if (Map.TryGetValue(be.id, out var s))
            Sound.Play(s.hit, be.transform.position);
    }

    /// <summary>Called from TrapBase when health drops below 0.5.</summary>
    public static void PlayDestroy(BuildingEntity be)
    {
        if (be == null) return;
        EnsurePopulated();
        if (Map.TryGetValue(be.id, out var s))
            Sound.Play(s.destroy, be.transform.position);
    }

    /// <summary>Force re-population from TrapRegistry (e.g. after late registration).</summary>
    public static void Refresh()
    {
        _populated = false;
        Map.Clear();
    }
}
