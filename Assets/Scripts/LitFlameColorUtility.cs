using UnityEngine;

/// <summary>
/// Resolves a flame presentation color without controlling any light or particle state.
/// Kept separate from the removed influence controller because Flame exposes this color
/// to other presentation systems.
/// </summary>
internal static class LitFlameColorUtility
{
    public static Color ResolveFlameColor(Light flameLight, GameObject flameObject, Color fallback)
    {
        if (flameLight != null)
        {
            return flameLight.color;
        }

        if (flameObject == null)
        {
            return fallback;
        }

        ParticleSystem[] particleSystems = flameObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (TryResolveParticleColor(particleSystems[i], out Color color))
            {
                return color;
            }
        }

        return fallback;
    }

    public static bool TryResolveParticleColor(ParticleSystem system, out Color color)
    {
        color = Color.white;
        if (system == null)
        {
            return false;
        }

        ParticleSystem.MinMaxGradient startColor = system.main.startColor;
        switch (startColor.mode)
        {
            case ParticleSystemGradientMode.Color:
                color = startColor.color;
                return true;
            case ParticleSystemGradientMode.TwoColors:
                color = Color.Lerp(startColor.colorMin, startColor.colorMax, 0.5f);
                return true;
            case ParticleSystemGradientMode.Gradient:
                if (startColor.gradient != null)
                {
                    color = startColor.gradient.Evaluate(1f);
                    return true;
                }
                break;
            case ParticleSystemGradientMode.TwoGradients:
                if (startColor.gradientMin != null && startColor.gradientMax != null)
                {
                    color = Color.Lerp(startColor.gradientMin.Evaluate(1f), startColor.gradientMax.Evaluate(1f), 0.5f);
                    return true;
                }
                break;
            case ParticleSystemGradientMode.RandomColor:
                if (startColor.gradient != null)
                {
                    color = startColor.gradient.Evaluate(1f);
                    return true;
                }
                break;
        }

        return false;
    }
}
