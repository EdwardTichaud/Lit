using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct DissolveRevealSourceInfo
{
    public readonly FlameLightReceiver Source;
    public readonly SquadCharacterController Controller;
    public readonly Vector3 Position;
    public readonly Color Color;
    public readonly bool Active;

    public DissolveRevealSourceInfo(
        FlameLightReceiver source,
        SquadCharacterController controller,
        Vector3 position,
        Color color,
        bool active)
    {
        Source = source;
        Controller = controller;
        Position = position;
        Color = color;
        Active = active;
    }
}

public static class DissolveRevealSystem
{
    private static readonly HashSet<FlameLightReceiver> Sources = new HashSet<FlameLightReceiver>();
    private static readonly List<FlameLightReceiver> StaleSources = new List<FlameLightReceiver>();

    public static event Action SourcesChanged;

    public static void RegisterSource(FlameLightReceiver source)
    {
        if (source == null)
        {
            return;
        }

        if (Sources.Add(source))
        {
            SourcesChanged?.Invoke();
        }
    }

    public static void UnregisterSource(FlameLightReceiver source)
    {
        if (source == null)
        {
            return;
        }

        if (Sources.Remove(source))
        {
            SourcesChanged?.Invoke();
        }
    }

    public static void GetSources(List<DissolveRevealSourceInfo> results, bool requireActiveSource)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        StaleSources.Clear();

        foreach (FlameLightReceiver source in Sources)
        {
            if (source == null)
            {
                StaleSources.Add(source);
                continue;
            }

            if (!source.TryGetRevealSourceInfo(out DissolveRevealSourceInfo info))
            {
                continue;
            }

            if (requireActiveSource && !info.Active)
            {
                continue;
            }

            results.Add(info);
        }

        for (int i = 0; i < StaleSources.Count; i++)
        {
            Sources.Remove(StaleSources[i]);
        }

        StaleSources.Clear();
    }

    public static void ResetRuntimeState()
    {
        Sources.Clear();
        StaleSources.Clear();
        SourcesChanged?.Invoke();
    }
}
