using TrapLib.MP;
using UnityEngine;

namespace TrapLib;

/// <summary>
/// Base for traps that trigger on limb contact with a cooldown.
/// No explosion, no zone — delegates behaviour to <see cref="ContactTrapConfig"/> callbacks.
/// Visual feedback (sound, sprite, callback) runs on all instances;
/// <see cref="ContactTrapConfig.OnContact"/> runs only on server / single-player.
/// </summary>
public abstract class ContactTrapBase : TrapBase
{
    protected float _cooldown;
    private Sprite _originalSprite;
    private bool _spriteSwapped;

    protected ContactTrapConfig ContactConfig => (ContactTrapConfig)Config;

    protected override void Update()
    {
        base.Update();
        if (Config == null) return;

        _cooldown -= Time.deltaTime;

        if (_spriteSwapped && _cooldown <= 0f && _sr != null && _originalSprite != null)
        {
            _sr.sprite = _originalSprite;
            _spriteSwapped = false;
        }

        if (MPSync.IsServerOrSP && _cooldown <= 0f && TryFindOverlappingLimb(0.1f, out var limb))
            TriggerContact(limb, visual: true);
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (Config == null) return;
        if (_cooldown > 0f) return;

        var limb = Body.LimbFromObject(collision.collider.gameObject, collision.GetContact(0).point);
        if (limb == null || limb.body == null) return;

        TriggerContact(limb, visual: true);
    }

    private void TriggerContact(Limb limb, bool visual)
    {
        // Visual feedback — all instances
        if (visual && !string.IsNullOrEmpty(ContactConfig.ContactSound))
            Sound.Play(ContactConfig.ContactSound, transform.position);

        if (visual && ContactConfig.ContactSprite != null && _sr != null)
        {
            if (!_spriteSwapped) _originalSprite = _sr.sprite;
            _sr.sprite = ContactConfig.ContactSprite;
            _spriteSwapped = true;
        }

        if (visual)
            ContactConfig.OnContactTriggered?.Invoke(limb, this);

        // Server-authoritative effect
        if (MPSync.IsServerOrSP)
        {
            ContactConfig.OnContact?.Invoke(limb, ContactConfig);
            _cooldown = ContactConfig.Cooldown;
        }
        else
        {
            _cooldown = ContactConfig.Cooldown;
        }
    }

    protected override void OnDestroy()
    {
        if (!_destroyed) { base.OnDestroy(); return; }
        if (!MPSync.IsServerOrSP) { base.OnDestroy(); return; }
        ContactConfig.OnDestroyed?.Invoke(transform.position, ContactConfig);
        base.OnDestroy();
    }
}
