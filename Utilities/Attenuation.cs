using UnityEngine;

namespace TrapLib.Utilities;

/// <summary>
/// Distance-based attenuation formulas for zone effects.
/// All methods take <c>t = distance / maxRadius</c> (0 = centre, 1 = edge)
/// and return a multiplier in [0, 1] (1 = full effect, 0 = none).
/// </summary>
public static class Attenuation
{
    /// <summary>Linear falloff: 1 at centre, 0 at edge.</summary>
    public static float Linear(float t) => 1f - Mathf.Clamp01(t);

    /// <summary>
    /// Smooth cubic S-curve: flat at centre, steep in middle, flat at edge.
    /// f(t) = 2t³ - 3t² + 1
    /// </summary>
    public static float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return 2f * t * t * t - 3f * t * t + 1f;
    }

    /// <summary>
    /// Smoother 5th-order step: zero 1st and 2nd derivatives at both ends.
    /// f(t) = -6t⁵ + 15t⁴ - 10t³ + 1
    /// </summary>
    public static float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return -6f * t * t * t * t * t + 15f * t * t * t * t - 10f * t * t * t + 1f;
    }

    /// <summary>
    /// Square-root curve: stays higher for longer, drops fast near the edge.
    /// </summary>
    public static float SquareRoot(float t) => Mathf.Sqrt(1f - Mathf.Clamp01(t));

    /// <summary>
    /// Configurable power curve: <c>(1 - t)^n</c>.
    /// n > 1 = sharper drop-off near centre, n &lt; 1 = longer tail.
    /// </summary>
    public static float PowerCurve(float t, float exponent = 2f)
        => Mathf.Pow(1f - Mathf.Clamp01(t), exponent);

    /// <summary>
    /// Exponential decay: <c>exp(-steepness * t)</c>.
    /// Reaches ~5% at t=1 when steepness=3, ~1.8% when steepness=4.
    /// </summary>
    public static float Exponential(float t, float steepness = 3f)
        => Mathf.Exp(-steepness * Mathf.Clamp01(t));

    /// <summary>
    /// Physically-inspired inverse-square: <c>1 / (1 + falloff * t²)</c>.
    /// Long tail — at t=1, multiplier = 1/(1+falloff).
    /// </summary>
    public static float InverseSquare(float t, float falloff = 4f)
        => 1f / (1f + falloff * Mathf.Clamp01(t) * Mathf.Clamp01(t));

    /// <summary>
    /// Radial threshold: full effect within an inner safe radius, then linear to 0.
    /// When <c>t ≤ innerRatio</c>, returns 1.0; beyond that, returns linear(remapped t).
    /// </summary>
    public static float RadialThreshold(float t, float innerRatio = 0.3f)
    {
        t = Mathf.Clamp01(t);
        if (t <= innerRatio) return 1f;
        return 1f - (t - innerRatio) / (1f - innerRatio);
    }

    /// <summary>
    /// Configure a rate from a desired multiplier-at-edge.
    /// E.g. <c>FromEdgeFalloff(0.1f)</c> returns the exponent where PowerCurve(t=1) ≈ 0.1.
    /// Useful with <see cref="PowerCurve"/>.
    /// </summary>
    public static float ExponentForEdgeFalloff(float edgeMultiplier)
        => Mathf.Log(edgeMultiplier) / Mathf.Log(0.01f); // normalised so (1 - 0.99)^n ≈ edge

    /// <summary>
    /// Steepness value for <see cref="Exponential"/> so that multiplier ≈ <c>edgeMultiplier</c> at t=1.
    /// </summary>
    public static float SteepnessForEdgeFalloff(float edgeMultiplier)
        => -Mathf.Log(Mathf.Max(edgeMultiplier, 0.001f));
}
