using TrapLib.MP;
using TrapLib.Utilities;
using UnityEngine;

namespace TrapLib;

/// <summary>
/// Base for explosive traps. Explosion is mandatory; trigger, fuse, zone and
/// burst are all optional — disable by leaving the corresponding config fields null / 0.
/// </summary>
public abstract class ExplosiveTrapBase : TrapBase
{
    private const float ArmedHealthSyncDelta = 0.123f;

    public float timeSincePressed;
    protected bool _pressed, _detonated;

    private static GameObject _cachedDustBig;
    private static GameObject _cachedExplosionParticle;
    private static GameObject _cachedBlastmark;
    private Collider2D[] _cachedColliders;

    private static GameObject DustBigPrefab
    {
        get
        {
            if (_cachedDustBig == null)
                _cachedDustBig = Resources.Load<GameObject>("DustBig");
            return _cachedDustBig;
        }
    }

    private static GameObject ExplosionParticlePrefab
    {
        get
        {
            if (_cachedExplosionParticle == null)
                _cachedExplosionParticle = Resources.Load("Special/ExplosionParticle") as GameObject;
            return _cachedExplosionParticle;
        }
    }

    private static GameObject BlastmarkPrefab
    {
        get
        {
            if (_cachedBlastmark == null)
                _cachedBlastmark = Resources.Load<GameObject>("Special/blastmark");
            return _cachedBlastmark;
        }
    }

    protected ExplosiveTrapConfig ExpConfig => (ExplosiveTrapConfig)Config;

    protected override void Update()
    {
        base.Update();
        if (Config == null) return; // prefab template

        // On pure clients: if the server has detonated (health → 0), run our
        // own local Detonate for visual feedback. This catches cases where the
        // client's CustomTrigger logic couldn't independently detect the trigger
        // (e.g. NuclearBomb triggered by a remote player the client can't see).
        if (MPSync.IsClient && !_detonated && _build != null && _build.health < 0.5f)
        {
            Detonate();
            return;
        }

        if (ShouldApplyRemoteArmedState())
            Trigger();

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
            // Standard filter: requires a dynamic rigidbody with sufficient mass nearby.
            if (!collision.collider.attachedRigidbody || collision.collider.attachedRigidbody.isKinematic)
            {
                // Fallback: ragdolled players have kinematic/joint-driven limbs that fail
                // the standard check. Detect player limbs directly so traps still trigger.
                if (!IsPlayerLimbCollision(collision))
                    return;
            }
            else if (collision.collider.attachedRigidbody.mass < 0.5f)
            {
                if (!IsPlayerLimbCollision(collision))
                    return;
            }
            if (PlayerCamera.main?.body == null) return;
            if (Vector2.Distance(transform.position, PlayerCamera.main.body.transform.position) > 50f) return;
        }

        Trigger();
    }

    private static bool IsPlayerLimbCollision(Collision2D collision)
    {
        var limb = Body.LimbFromObject(collision.collider.gameObject, collision.GetContact(0).point);
        return limb != null && limb.body != null;
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

        MarkArmedForClients();

        if (ExpConfig.FuseTime > 0f && !string.IsNullOrEmpty(ExpConfig.FuseSound))
            Sound.Play(ExpConfig.FuseSound, transform.position);

        if (_sr != null && ExpConfig.PressedSprite != null)
            _sr.sprite = ExpConfig.PressedSprite;

        ExpConfig.OnTriggered?.Invoke(this);

        if (ExpConfig.FuseTime <= 0f)
            Detonate();
    }

    private bool ShouldApplyRemoteArmedState()
    {
        // KrokMP syncs BuildingEntity.health for custom objects. The server nudges
        // health slightly when arming the fuse so remote clients can start visuals.
        return MPSync.IsClient && !_pressed && !_detonated && _build != null && Config != null
            && _build.health >= 0.5f && _build.health < Config.Health - ArmedHealthSyncDelta * 0.5f;
    }

    private void MarkArmedForClients()
    {
        if (!MPSync.IsServerOrSP || _build == null || _build.health <= 1f) return;
        _build.health = Mathf.Max(1f, _build.health - ArmedHealthSyncDelta);
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
        if (_cachedColliders == null)
            _cachedColliders = GetComponentsInChildren<Collider2D>();
        foreach (var c in _cachedColliders)
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

            _build.health = 0f;
            Object.Destroy(gameObject);
        }
        else if (!ExpConfig.NoClientFallback)
        {
            // Client: local visual explosion + particles.
            // KrokMP may not reliably sync CreateExplosion for custom traps, so we
            // spawn the explosion effect directly. Damage is handled by the server.
            // Skip if NoClientFallback is set (KrokMP handles sync for this trap).
            if (ExpConfig.ExplosionParams != null)
            {
                var e = ExpConfig.ExplosionParams;
                Sound.Play(e.sound, center, twoDimensional: false, pitchShift: false);

                var explosionParticle = ExplosionParticlePrefab;
                if (explosionParticle != null)
                    Object.Instantiate(explosionParticle, center, Quaternion.identity);
                else
                    TrapLibPlugin.Log?.LogWarning($"[ExplosiveTrapBase] Failed to load resource: Special/ExplosionParticle");

                var blastmark = BlastmarkPrefab;
                if (blastmark != null)
                {
                    var blast = Object.Instantiate(blastmark, center, Quaternion.identity);
                    blast.transform.eulerAngles = new Vector3(0, 0, Random.value * 360f);
                }
                else
                    TrapLibPlugin.Log?.LogWarning($"[ExplosiveTrapBase] Failed to load resource: Special/blastmark");
                
                SpawnFogParticles(center, ExpConfig.ExplosionRange, ExpConfig.FogColor);
            }
            // Note: we deliberately do NOT apply ApplyBlastDebuff on the client.
            // The server runs ApplyBlast authoritatively; applying it on the
            // client as well would create a ~0.9s state divergence window where
            // the server's sync packet overwrites the client's local values,
            // causing visible health/temperature flickering if any other system
            // modifies those body fields during that window.
            Patches.BuildingEntityPatch.SpawnDestructionParticles(_build);
            var dustPrefab = DustBigPrefab;
            if (dustPrefab != null)
                Object.Instantiate(dustPrefab, center, Quaternion.identity);
            TrapSounds.PlayDestroy(_build);
            if (_sr != null) _sr.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider2D>())
                c.enabled = false;
            // Set health to zero so TrapBase.Update and BuildingEntityPatch.Prefix
            // can properly clean up the object, avoiding desync issues with KrokMP.
            if (_build != null) _build.health = 0f;
        }
        else
        {
            // NoClientFallback: KrokMP handles explosion sync.
            // Still need to clean up locally — hide sprite, play destroy sound, destroy object.
            if (_sr != null) _sr.enabled = false;
            Patches.BuildingEntityPatch.SpawnDestructionParticles(_build);
            TrapSounds.PlayDestroy(_build);
            // Set health to zero so TrapBase.Update and BuildingEntityPatch.Prefix
            // can properly clean up the object, avoiding desync issues with KrokMP.
            if (_build != null) _build.health = 0f;
        }
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
        foreach (var body in MPSync.AllPlayerBodies)
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

    protected override void OnDestroy()
    {
        if (Config == null) { base.OnDestroy(); return; }
        if (!_destroyed || _detonated) { base.OnDestroy(); return; }
        // OnDestroyedWithoutDetonation runs on all instances so clients see the
        // visual effect (zone fog). Zone damage (ApplyEffect) is server-only.
        ExpConfig.OnDestroyedWithoutDetonation?.Invoke(this, transform.position);
        base.OnDestroy();
    }

    // ---- Shared visual helpers ----

    protected static void SpawnFogParticles(Vector3 center, float radius, Color tint)
    {
        var dustBig = DustBigPrefab;
        if (dustBig == null)
        {
            TrapLibPlugin.Log?.LogWarning("[ExplosiveTrapBase] Failed to load resource: DustBig");
            return;
        }

        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            var go = Object.Instantiate(dustBig, center + offset, Quaternion.identity);
            if (go != null)
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = tint;
                Object.Destroy(go, 5f);
            }
        }

        var burst = Object.Instantiate(dustBig, center, Quaternion.identity);
        if (burst != null)
        {
            var sr = burst.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = tint;
            Object.Destroy(burst, 5f);
        }
    }
}
