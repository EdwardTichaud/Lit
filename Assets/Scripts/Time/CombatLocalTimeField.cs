using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Local time bubble used by a local combat presentation. It never touches
/// Unity's global clock, so a QTE on one client cannot slow other players.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatLocalTimeField : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float radius = 10f;
    [SerializeField, Range(0.01f, 1f)] private float defaultScale = 0.4f;
    [SerializeField, Min(0.01f)] private float refreshIntervalRealtime = 0.05f;
    [SerializeField] private bool logDiagnostics;

    private readonly Dictionary<CombatTimeDomain, TimeManager.TimeRequestHandle> handles =
        new Dictionary<CombatTimeDomain, TimeManager.TimeRequestHandle>();

    private Transform center;
    private CombatCinematicRig cinematicRig;
    private float activeScale = 1f;
    private float nextRefreshAtRealtime;
    private bool active;

    public bool IsActive => active;
    public float Radius => radius;
    public int AffectedActorCount => handles.Count;

    public void Begin(Transform fieldCenter, CombatCinematicRig rig = null, float scale = -1f)
    {
        if (fieldCenter == null)
        {
            Debug.LogWarning("[CombatLocalTimeField] Champ ignore : centre absent.", this);
            return;
        }

        center = fieldCenter;
        cinematicRig = rig;
        activeScale = Mathf.Clamp01(scale > 0f ? scale : defaultScale);
        active = true;
        nextRefreshAtRealtime = 0f;
        cinematicRig?.SetLocalPlaybackScale(activeScale);
        RefreshDomains();
    }

    public void End()
    {
        if (!active && handles.Count == 0) return;

        foreach (TimeManager.TimeRequestHandle handle in handles.Values)
        {
            TimeManager.Instance?.Release(handle);
        }

        handles.Clear();
        cinematicRig?.SetLocalPlaybackScale(1f);
        cinematicRig = null;
        center = null;
        active = false;
    }

    private void Update()
    {
        if (!active) return;

        if (center == null)
        {
            End();
            return;
        }

        if (Time.unscaledTime < nextRefreshAtRealtime) return;
        RefreshDomains();
    }

    private void OnDisable()
    {
        End();
    }

    private void OnDestroy()
    {
        End();
    }

    private void RefreshDomains()
    {
        TimeManager manager = TimeManager.EnsureInstance();
        if (manager == null)
        {
            Debug.LogWarning("[CombatLocalTimeField] TimeManager absent : aucun ralentissement local applique.", this);
            End();
            return;
        }

        nextRefreshAtRealtime = Time.unscaledTime + refreshIntervalRealtime;
        CombatTimeDomain[] domains = FindObjectsByType<CombatTimeDomain>(FindObjectsInactive.Exclude);
        float radiusSquared = radius * radius;
        var inside = new HashSet<CombatTimeDomain>();
        for (int i = 0; i < domains.Length; i++)
        {
            CombatTimeDomain domain = domains[i];
            if (domain == null || !domain.isActiveAndEnabled) continue;
            if ((domain.transform.position - center.position).sqrMagnitude > radiusSquared) continue;

            inside.Add(domain);
            if (!handles.ContainsKey(domain))
            {
                handles.Add(domain, manager.AcquireLocal(domain, activeScale, this));
            }
        }

        var outside = new List<CombatTimeDomain>();
        foreach (KeyValuePair<CombatTimeDomain, TimeManager.TimeRequestHandle> pair in handles)
        {
            if (pair.Key == null || !inside.Contains(pair.Key))
            {
                manager.Release(pair.Value);
                outside.Add(pair.Key);
            }
        }

        for (int i = 0; i < outside.Count; i++) handles.Remove(outside[i]);

        if (logDiagnostics)
        {
            Debug.Log("[CombatLocalTimeField] centre='" + center.name + "' | radius=" + radius.ToString("F1") +
                      " | scale=" + activeScale.ToString("F2") + " | actors=" + handles.Count + ".", this);
        }
    }
}
