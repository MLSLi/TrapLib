using System.Collections.Generic;
using TrapLib.MP;
using TrapLib.Utilities;
using UnityEngine;

namespace TrapLib;

/// <summary>
/// Persistent zone effect with a radial fog sprite and CircleCollider2D.
/// Fog visuals appear on all clients; <see cref="ApplyEffect"/> only runs
/// on the server / single-player.
/// </summary>
public abstract class TrapZone : MonoBehaviour
{
    public float radius;
    public float duration;
    public Color fogColor = Color.white;

    /// <summary>Seconds before expiry when <see cref="OnExpiring"/> is first called.</summary>
    public float fadeTime = 10f;

    /// <summary>Interval in seconds for <see cref="OnTick"/>. 0 = disabled.</summary>
    public float tickInterval;

    private HashSet<Body> _current = new HashSet<Body>();
    private HashSet<Body> _previous = new HashSet<Body>();
    internal HashSet<Body> Current => _current;
    private float _accum;
    private float _tickAccum;
    private float _age;
    private bool _expiring;

    protected virtual void Start()
    {
        var col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radius;
        Destroy(gameObject, duration);

        var fogGo = new GameObject("Fog");
        fogGo.transform.SetParent(transform, false);
        var sr = fogGo.AddComponent<SpriteRenderer>();
        sr.sprite = FogSpriteCache.Get();
        sr.color = fogColor;
        sr.sortingOrder = 5000;
        fogGo.transform.localScale = Vector3.one * (radius / 8f);
    }

    // ---- Body tracking ----

    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        if (MPSync.IsClient) return; // client only needs fog visual — skip body tracking
        TrackBody(other);
    }

    protected void TrackBody(Collider2D other)
    {
        var body = other.GetComponentInParent<Body>();
        if (body != null) _current.Add(body);
    }

    protected virtual void FixedUpdate()
    {
        _age += Time.fixedDeltaTime;

        // Expiration fade-out (server / SP only)
        if (!MPSync.IsClient && !_expiring && _age > duration - fadeTime)
        {
            _expiring = true;
            OnExpiring();
        }

        // Entry / exit detection (server / SP only — client sees visuals via fog)
        if (!MPSync.IsClient)
        {
            foreach (var body in _current)
            {
                if (!_previous.Contains(body))
                    OnBodyEnter(body);
            }
            foreach (var body in _previous)
            {
                if (!_current.Contains(body))
                    OnBodyExit(body);
            }
        }

        // Per-second effect — server only
        _accum += Time.fixedDeltaTime;
        while (_accum >= 1f)
        {
            _accum -= 1f;

            if (!MPSync.IsClient)
            {
                foreach (var body in _current)
                {
                    if (body != null)
                        ApplyEffect(body, Vector2.Distance(transform.position, body.transform.position));
                }
            }
        }

        // Periodic tick for particles etc. — everyone
        if (tickInterval > 0f)
        {
            _tickAccum += Time.fixedDeltaTime;
            while (_tickAccum >= tickInterval)
            {
                _tickAccum -= tickInterval;
                OnTick();
            }
        }

        (_previous, _current) = (_current, _previous);
        _current.Clear();
    }

    // ---- Overridable hooks ----

    /// <summary>Called once per second for each body inside the zone. Server / SP only.</summary>
    protected abstract void ApplyEffect(Body body, float distanceFromCenter);

    /// <summary>Called when a body enters the zone for the first time. Server / SP only.</summary>
    protected virtual void OnBodyEnter(Body body) { }

    /// <summary>Called when a body leaves the zone. Server / SP only.</summary>
    protected virtual void OnBodyExit(Body body) { }

    /// <summary>
    /// Called once when the zone enters its fade-out period
    /// (<see cref="fadeTime"/> seconds before expiry). Everyone.
    /// </summary>
    protected virtual void OnExpiring() { }

    /// <summary>
    /// Called every <see cref="tickInterval"/> seconds while the zone is active.
    /// Use for periodic particles or audio. Everyone.
    /// </summary>
    protected virtual void OnTick() { }

    // ---- Factory ----

    /// <summary>
    /// Create a zone GameObject with common setup (Ground layer, trigger collider,
    /// fog sprite, radius/duration/fogColor from config). Returns the zone component
    /// so the caller can set extra parameters.
    /// </summary>
    public static T Create<T>(string name, Vector3 center, ExplosiveTrapConfig cfg) where T : TrapZone
    {
        var go = new GameObject(name);
        go.transform.position = center;
        go.layer = LayerMask.NameToLayer("Ground");
        var zone = go.AddComponent<T>();
        zone.radius = cfg.ZoneRadius > 0f ? cfg.ZoneRadius : cfg.ExplosionRange;
        zone.duration = cfg.ZoneDuration;
        zone.fogColor = cfg.FogColor;
        return zone;
    }
}
