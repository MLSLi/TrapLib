using UnityEngine;

namespace TrapLib.Utilities;

/// <summary>
/// Base component that self-destructs after <see cref="duration"/> seconds.
/// Override <see cref="OnUpdate"/> for per-frame work (skipped once expired).
/// Override <see cref="OnExpire"/> for one-shot cleanup.
/// </summary>
public class TimedDestroy : MonoBehaviour
{
    public float duration;
    public float Elapsed { get; private set; }
    public bool Expired => Elapsed >= duration;

    protected virtual void Update()
    {
        Elapsed += Time.deltaTime;
        if (Elapsed >= duration)
        {
            OnExpire();
            Destroy(this);
            return;
        }
        OnUpdate();
    }

    /// <summary>Called every frame while active.</summary>
    protected virtual void OnUpdate() { }

    /// <summary>Called once when the timer expires, before Destroy.</summary>
    protected virtual void OnExpire() { }
}
