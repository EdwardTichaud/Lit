using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class EnvironmentRuntimeState
{
    private static readonly Dictionary<Type, VolumeComponent> defaultComponents =
        new Dictionary<Type, VolumeComponent>();

    public static VolumeProfile CreateProfile(string profileName)
    {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = profileName;
        return profile;
    }

    public static void CopyProfile(VolumeProfile source, VolumeProfile destination)
    {
        if (destination == null)
        {
            return;
        }

        ResetProfileToNeutral(destination);
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.components.Count; i++)
        {
            VolumeComponent sourceComponent = source.components[i];
            if (sourceComponent == null)
            {
                continue;
            }

            VolumeComponent destinationComponent = EnsureComponent(destination, sourceComponent.GetType());
            CopyComponent(sourceComponent, destinationComponent);
        }
    }

    public static void BuildTargetProfile(
        VolumeProfile baseline,
        VolumeProfile source,
        VolumeProfile destination,
        float sourceWeight)
    {
        if (destination == null)
        {
            return;
        }

        EnsureComponentTypes(destination, baseline);
        EnsureComponentTypes(destination, source);
        ResetProfileToNeutral(destination);
        ApplyBaselineProfile(baseline, destination);

        sourceWeight = Mathf.Clamp01(sourceWeight);
        if (source == null || sourceWeight <= 0f)
        {
            return;
        }

        BlendSourceIntoProfile(destination, source, sourceWeight);
    }

    public static void BlendSourceIntoProfile(
        VolumeProfile destination,
        VolumeProfile source,
        float sourceWeight)
    {
        if (destination == null)
        {
            return;
        }

        sourceWeight = Mathf.Clamp01(sourceWeight);
        if (source == null || sourceWeight <= 0f)
        {
            return;
        }

        EnsureComponentTypes(destination, source);

        for (int i = 0; i < source.components.Count; i++)
        {
            VolumeComponent sourceComponent = source.components[i];
            if (sourceComponent == null || !sourceComponent.active)
            {
                continue;
            }

            VolumeComponent destinationComponent = EnsureComponent(destination, sourceComponent.GetType());
            destinationComponent.active = true;

            // Unity's VolumeComponent.Override blends every overridden parameter using the
            // parameter's own interpolation implementation, so custom HDRP overrides are supported.
            sourceComponent.Override(destinationComponent, sourceWeight);
            destinationComponent.SetAllOverridesTo(true);
        }
    }

    public static void BlendProfileTowards(VolumeProfile current, VolumeProfile target, float blendFactor)
    {
        if (current == null || target == null)
        {
            return;
        }

        blendFactor = Mathf.Clamp01(blendFactor);

        // If the runtime profile has a component that disappeared from the target profile,
        // add a neutral version to the target so the values can fade back smoothly.
        for (int i = 0; i < current.components.Count; i++)
        {
            VolumeComponent currentComponent = current.components[i];
            if (currentComponent == null)
            {
                continue;
            }

            Type componentType = currentComponent.GetType();
            if (!target.Has(componentType))
            {
                VolumeComponent neutralTarget = EnsureComponent(target, componentType);
                ResetComponentToNeutral(neutralTarget);
            }
        }

        for (int i = 0; i < target.components.Count; i++)
        {
            VolumeComponent targetComponent = target.components[i];
            if (targetComponent == null)
            {
                continue;
            }

            VolumeComponent currentComponent = EnsureComponent(current, targetComponent.GetType());
            currentComponent.active = true;
            targetComponent.Override(currentComponent, blendFactor);
            currentComponent.SetAllOverridesTo(true);
        }
    }

    private static void ApplyBaselineProfile(VolumeProfile baseline, VolumeProfile destination)
    {
        if (baseline == null)
        {
            return;
        }

        for (int i = 0; i < baseline.components.Count; i++)
        {
            VolumeComponent baselineComponent = baseline.components[i];
            if (baselineComponent == null)
            {
                continue;
            }

            VolumeComponent destinationComponent = EnsureComponent(destination, baselineComponent.GetType());
            CopyComponent(baselineComponent, destinationComponent);
            destinationComponent.active = true;
            destinationComponent.SetAllOverridesTo(true);
        }
    }

    private static void EnsureComponentTypes(VolumeProfile destination, VolumeProfile source)
    {
        if (destination == null || source == null)
        {
            return;
        }

        for (int i = 0; i < source.components.Count; i++)
        {
            VolumeComponent sourceComponent = source.components[i];
            if (sourceComponent == null)
            {
                continue;
            }

            EnsureComponent(destination, sourceComponent.GetType());
        }
    }

    private static void ResetProfileToNeutral(VolumeProfile profile)
    {
        for (int i = 0; i < profile.components.Count; i++)
        {
            VolumeComponent component = profile.components[i];
            if (component == null)
            {
                continue;
            }

            ResetComponentToNeutral(component);
        }
    }

    private static void ResetComponentToNeutral(VolumeComponent component)
    {
        VolumeComponent defaultComponent = GetDefaultComponent(component.GetType());
        CopyComponent(defaultComponent, component);
        component.active = true;
        component.SetAllOverridesTo(true);
    }

    private static VolumeComponent EnsureComponent(VolumeProfile profile, Type componentType)
    {
        if (!profile.TryGet(componentType, out VolumeComponent component))
        {
            component = profile.Add(componentType);
        }

        return component;
    }

    private static VolumeComponent GetDefaultComponent(Type componentType)
    {
        if (!defaultComponents.TryGetValue(componentType, out VolumeComponent component) || component == null)
        {
            component = ScriptableObject.CreateInstance(componentType) as VolumeComponent;
            defaultComponents[componentType] = component;
        }

        return component;
    }

    private static void CopyComponent(VolumeComponent source, VolumeComponent destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.active = source.active;

        int parameterCount = Mathf.Min(source.parameters.Count, destination.parameters.Count);
        for (int i = 0; i < parameterCount; i++)
        {
            VolumeParameter sourceParameter = source.parameters[i];
            VolumeParameter destinationParameter = destination.parameters[i];
            if (sourceParameter == null || destinationParameter == null)
            {
                continue;
            }

            destinationParameter.SetValue(sourceParameter);
            destinationParameter.overrideState = sourceParameter.overrideState;
        }
    }
}
