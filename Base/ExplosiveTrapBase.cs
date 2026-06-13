using TrapLib.MP;
using UnityEngine;

namespace TrapLib;

/// <summary>
/// Base for explosive traps. Explosion is mandatory; trigger, fuse, zone and
/// burst are all optional — disable by leaving the corresponding config fields null / 0.
/// </summary>
public abstract class ExplosiveTrapBase : TrapBase
{
    public float timeSincePressed;
    protected bool _pressed, _detonated;

    protected ExplosiveTrapConfig ExpConfig => (ExplosiveTrapConfig)Config;

    protected override void Update()
    {
        base.Update();
        if (Config == null) return; // prefab template

        if (!_detonated)
            ExpConfig.CustomTrigger?.Invoke(this);

        if (_pressed && !_detonated)
        {
            timeSincePressed += Time.deltaTime;
            ExpConfig.OnFuseUpdate?.Invoke(this, timeSincePressed, ExpConfig.FuseTime);
            if (timeSincePressed > ExpConfig.FuseTime)
                Detonate();
        }
    }

    // ---- Default collision trigger (skipped when CustomTrigger is set) ----

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (Config == null) return;
        if (ExpConfig.CustomTrigger != null) return;
        if (_pressed || _detonated) return;

        if (ExpConfig.TriggerFilter != null)
        {
            if (!ExpConfig.TriggerFilter(collision)) return;
        }
        else
        {
            if (!collision.collider.attachedRigidbody || collision.collider.attachedRigidbody.isKinematic) return;
            if (collision.collider.attachedRigidbody.mass < 0.5f) return;
            if (PlayerCamera.main?.body == null) return;
            if (Vector2.Distance(transform.position, PlayerCamera.main.body.transform.position) > 50f) return;
        }

        Trigger();
    }

    /// <summary>
    /// Arm the fuse. Safe to call from custom triggers.
    /// Runs on both server and client for visual feedback (sprite, sound).
    /// If <see cref="ExplosiveTrapConfig.FuseTime"/> is 0, detonates immediately
    /// (visuals on both ends; damage server-only).
    /// </summary>
    public void Trigger()
    {
        if (_pressed || _detonated) return;
        _pressed = true;

        if (ExpConfig.FuseTime > 0f)
            Sound.Play(ExpConfig.FuseSound, transform.position);

        if (_sr != null && ExpConfig.PressedSprite != null)
            _sr.sprite = ExpConfig.PressedSprite;

        ExpConfig.OnTriggered?.Invoke(this);

        if (ExpConfig.FuseTime <= 0f)
            Detonate();
    }

    // ---- Detonation ----

    /// <summary>
    /// Execute detonation. Zone, fog particles, and destruction run on all instances.
    /// <see cref="WorldGeneration.CreateExplosion"/>, blast, and burst run on server only.
    /// </summary>
    protected virtual void Detonate()
    {
        if (_detonated) return;
        _detonated = true;

        Vector3 center = transform.position;

        // Disable our colliders so CreateExplosion's OverlapCircleAll doesn't find
        // our Static Rigidbody2D and try to set velocity on it.
        foreach (var c in GetComponentsInChildren<Collider2D>())
            c.enabled = false;

        // Visuals — all instances
        ExpConfig.CreateZone?.Invoke(center, ExpConfig);
        SpawnFogParticles(center, ExpConfig.ExplosionRange, ExpConfig.FogColor);

        if (MPSync.IsServerOrSP)
        {
            if (ExpConfig.ExplosionParams != null)
            {
                var src = ExpConfig.ExplosionParams;
                var ep = new ExplosionParams
                {
                    position = center + Vector3.up,
                    range = ExpConfig.ExplosionRange,
                    structuralDamage = src.structuralDamage,
                    muscleDamage = src.muscleDamage,
                    skinDamage = src.skinDamage,
                    skinDamageChance = src.skinDamageChance,
                    boneBreakChance = src.boneBreakChance,
                    dislocationChance = src.dislocationChance,
                    disfigureChance = src.disfigureChance,
                    bleedChance = src.bleedChance,
                    bleedAmount = src.bleedAmount,
                    shrapnelChance = src.shrapnelChance,
                    velocity = src.velocity,
                    sound = src.sound,
                };
                WorldGeneration.CreateExplosion(ep);
            }

            ApplyBlast(center);
            ExpConfig.OnBurst?.Invoke(center, ExpConfig);
        }

        _build.health = 0f;
        Object.Destroy(gameObject);
    }

    // ---- Blast (direct-hit burst zone) ----

    /// <summary>
    /// If <see cref="ExplosiveTrapConfig.BlastRadius"/> &gt; 0, iterates all bodies
    /// within that radius and calls <see cref="ApplyBlastDebuff"/> on each.
    /// Override ApplyBlastDebuff to define the effect.
    /// </summary>
    protected virtual void ApplyBlast(Vector3 center)
    {
        float r = ExpConfig.BlastRadius;
        if (r <= 0f) return;

        float rSqr = r * r;
        foreach (var body in Object.FindObjectsOfType<Body>())
        {
            if (body == null) continue;
            if (((Vector2)(body.transform.position - center)).sqrMagnitude < rSqr)
                ApplyBlastDebuff(body);
        }
    }

    /// <summary>
    /// Called per body within <see cref="ExplosiveTrapConfig.BlastRadius"/> at detonation.
    /// Override to apply burst debuffs (damage, slow, sickness, etc.). Server only.
    /// </summary>
    protected virtual void ApplyBlastDebuff(Body body) { }

    // ---- Non-detonated destruction ----

    protected virtual void OnDestroy()
    {
        if (Config == null) return;
        if (!_destroyed || _detonated) return;
        if (!MPSync.IsServerOrSP) return;
        ExpConfig.OnDestroyedWithoutDetonation?.Invoke(this, transform.position);
    }

    // ---- Shared visual helpers ----

    protected static void SpawnFogParticles(Vector3 center, float radius, Color tint)
    {
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            var go = Object.Instantiate(Resources.Load<GameObject>("DustBig"), center + offset, Quaternion.identity);
            if (go != null)
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = tint;
                Object.Destroy(go, 5f);
            }
        }
        var burst = Object.Instantiate(Resources.Load<GameObject>("DustBig"), center, Quaternion.identity);
        if (burst != null)
        {
            var sr = burst.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = tint;
            Object.Destroy(burst, 5f);
        }
    }
}
