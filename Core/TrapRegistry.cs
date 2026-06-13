using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrapLib;

public static class TrapRegistry
{
    /// <summary>All registered traps, keyed by their TrapConfig.Id.</summary>
    internal static readonly Dictionary<string, (TrapConfig config, Type type)> Entries
        = new Dictionary<string, (TrapConfig, Type)>();

    /// <summary>Prefab templates (one per registered type), keyed by type name.</summary>
    internal static readonly Dictionary<string, GameObject> Prefabs
        = new Dictionary<string, GameObject>();

    /// <summary>
    /// Register a trap so it is automatically distributed during world generation.
    /// The type <typeparamref name="T"/> must derive from <see cref="TrapBase"/>.
    /// </summary>
    public static void Register<T>(TrapConfig config) where T : TrapBase
    {
        if (string.IsNullOrEmpty(config.Id))
            throw new ArgumentException("TrapConfig.Id must not be null or empty.");

        if (Entries.ContainsKey(config.Id))
            throw new InvalidOperationException($"Trap '{config.Id}' is already registered.");

        Entries[config.Id] = (config, typeof(T));
        TrapLibPlugin.Log?.LogInfo($"Registered trap: {config.Id} ({typeof(T).Name})");
    }

    /// <summary>
    /// Look up the <see cref="TrapConfig"/> for a given trap type. Returns null if not registered.
    /// Used by traps to recover Config after Unity Instantiate drops plain-C# references.
    /// </summary>
    public static TrapConfig GetConfig(Type type)
    {
        foreach (var entry in Entries.Values)
            if (entry.type == type) return entry.config;
        return null;
    }

    /// <summary>
    /// Spawn a registered trap at a given world position. Returns the <see cref="TrapBase"/>
    /// component, or null if the id is not registered or instantiation fails.
    /// World generation must be complete for proper placement (Place() needs the world).
    /// </summary>
    public static TrapBase Spawn(string id, Vector3 position)
    {
        if (!Entries.TryGetValue(id, out var entry))
        {
            TrapLibPlugin.Log?.LogWarning($"TrapRegistry.Spawn: '{id}' is not registered.");
            return null;
        }

        var prefab = GetOrCreatePrefab(entry.type, entry.config);
        if (prefab == null) return null;

        var go = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        go.transform.position = position; // re-assert after Awake/Place snap-to-surface
        return go.GetComponent<TrapBase>();
    }

    internal static GameObject GetOrCreatePrefab(Type type, TrapConfig config)
    {
        var key = type.Name;
        if (Prefabs.TryGetValue(key, out var existing))
            return existing;

        var go = new GameObject(key);
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent(type);

        // Strip render/collision/physics from the template
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>()) UnityEngine.Object.DestroyImmediate(sr);
        foreach (var c  in go.GetComponentsInChildren<Collider2D>())     UnityEngine.Object.DestroyImmediate(c);
        var rb  = go.GetComponent<Rigidbody2D>();    if (rb)  UnityEngine.Object.DestroyImmediate(rb);
        var be  = go.GetComponent<BuildingEntity>(); if (be)  UnityEngine.Object.DestroyImmediate(be);

        var trap = go.GetComponent<TrapBase>();
        if (trap != null) trap.Config = config;

        Prefabs[key] = go;
        return go;
    }
}
