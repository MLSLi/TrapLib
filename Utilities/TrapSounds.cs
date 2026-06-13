using System.Collections.Generic;
using UnityEngine;

namespace TrapLib.Utilities;

/// <summary>
/// Hit and destroy sound dispatch for BuildingEntity traps.
/// Other mods populate this via <see cref="TrapConfig.Sounds"/> during registration.
/// </summary>
public static class TrapSounds
{
    internal static readonly Dictionary<string, (string hit, string destroy)> Map
        = new Dictionary<string, (string, string)>();

    /// <summary>Called from BuildingEntityPatch when a trap takes melee damage.</summary>
    public static void PlayHit(BuildingEntity be)
    {
        if (be != null && Map.TryGetValue(be.id, out var s))
            Sound.Play(s.hit, be.transform.position);
    }

    /// <summary>Called from TrapBase when health drops below 0.5.</summary>
    public static void PlayDestroy(BuildingEntity be)
    {
        if (be != null && Map.TryGetValue(be.id, out var s))
            Sound.Play(s.destroy, be.transform.position);
    }
}
