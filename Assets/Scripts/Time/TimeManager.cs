using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent authority for global and combat-actor local time. No gameplay
/// system other than this component may write Unity's global time settings.
/// </summary>
[DefaultExecutionOrder(-9500)]
[DisallowMultipleComponent]
public sealed class TimeManager : MonoBehaviour
{
    public readonly struct TimeRequestHandle
    {
        internal readonly int Id;
        internal TimeRequestHandle(int id) => Id = id;
        public bool IsValid => Id != 0;
    }

    private sealed class Request
    {
        public int id;
        public object owner;
        public CombatTimeDomain domain;
        public float scale;
        public bool pause;
        public float expiresAtRealtime;
    }

    public static TimeManager Instance { get; private set; }

    [SerializeField, Range(0.01f, 2f)] private float baseGlobalScale = 1f;
    [SerializeField] private bool logDiagnostics;

    private readonly Dictionary<int, Request> requests = new Dictionary<int, Request>();
    private readonly HashSet<CombatTimeDomain> registeredDomains = new HashSet<CombatTimeDomain>();
    private int nextRequestId = 1;
    private float initialFixedDeltaTime;
    private float effectiveGlobalScale = 1f;

    public float GlobalScale => effectiveGlobalScale;
    public float BaseGlobalScale => baseGlobalScale;

    public static TimeManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        TimeManager existing = FindAnyObjectByType<TimeManager>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        ApplicationRoot root = FindAnyObjectByType<ApplicationRoot>(FindObjectsInactive.Include);
        if (root == null) return null;
        return root.gameObject.AddComponent<TimeManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        initialFixedDeltaTime = Time.fixedDeltaTime;
        CombatTimeDomain[] existingDomains = FindObjectsByType<CombatTimeDomain>(FindObjectsInactive.Include);
        for (int i = 0; i < existingDomains.Length; i++)
        {
            registeredDomains.Add(existingDomains[i]);
        }
        Recompute();
    }

    private void Update()
    {
        bool changed = false;
        List<int> expired = null;
        foreach (KeyValuePair<int, Request> pair in requests)
        {
            Request request = pair.Value;
            bool ownerDestroyed = request.owner is UnityEngine.Object unityOwner && unityOwner == null;
            if (ownerDestroyed || (request.expiresAtRealtime > 0f && Time.unscaledTime >= request.expiresAtRealtime))
            {
                expired ??= new List<int>();
                expired.Add(pair.Key);
            }
        }

        if (expired == null) return;
        for (int i = 0; i < expired.Count; i++)
        {
            requests.Remove(expired[i]);
            changed = true;
        }
        if (changed) Recompute();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        Time.timeScale = 1f;
        if (initialFixedDeltaTime > 0f) Time.fixedDeltaTime = initialFixedDeltaTime;
        Instance = null;
    }

    public TimeRequestHandle AcquireGlobal(float scale, object owner, float durationRealtime = 0f)
    {
        return Acquire(scale, owner, null, false, durationRealtime);
    }

    public TimeRequestHandle AcquireGlobalPause(object owner, float durationRealtime = 0f)
    {
        return Acquire(0f, owner, null, true, durationRealtime);
    }

    public TimeRequestHandle AcquireLocal(CombatTimeDomain domain, float scale, object owner, float durationRealtime = 0f)
    {
        if (domain == null)
        {
            Debug.LogWarning("[TimeManager] Requete locale ignoree : CombatTimeDomain absent.", this);
            return default;
        }

        RegisterDomain(domain);
        return Acquire(scale, owner, domain, false, durationRealtime);
    }

    public TimeRequestHandle AcquireLocalPause(CombatTimeDomain domain, object owner, float durationRealtime = 0f)
    {
        if (domain == null) return default;
        RegisterDomain(domain);
        return Acquire(0f, owner, domain, true, durationRealtime);
    }

    public void Release(TimeRequestHandle handle)
    {
        if (!handle.IsValid || !requests.Remove(handle.Id)) return;
        Recompute();
    }

    public void ReleaseOwner(object owner)
    {
        if (owner == null) return;
        List<int> owned = null;
        foreach (KeyValuePair<int, Request> pair in requests)
        {
            if (!ReferenceEquals(pair.Value.owner, owner)) continue;
            owned ??= new List<int>();
            owned.Add(pair.Key);
        }

        if (owned == null) return;
        for (int i = 0; i < owned.Count; i++) requests.Remove(owned[i]);
        Recompute();
    }

    public void RegisterDomain(CombatTimeDomain domain)
    {
        if (domain == null) return;
        registeredDomains.Add(domain);
        RecomputeDomains();
    }

    public void UnregisterDomain(CombatTimeDomain domain)
    {
        if (domain == null || !registeredDomains.Remove(domain)) return;
        RecomputeDomains();
    }

    private TimeRequestHandle Acquire(float scale, object owner, CombatTimeDomain domain, bool pause, float durationRealtime)
    {
        if (owner == null)
        {
            Debug.LogWarning("[TimeManager] Requete ignoree : owner obligatoire pour garantir la restitution.", this);
            return default;
        }

        int id = nextRequestId++;
        if (nextRequestId == int.MaxValue) nextRequestId = 1;
        requests[id] = new Request
        {
            id = id,
            owner = owner,
            domain = domain,
            scale = Mathf.Clamp01(scale),
            pause = pause,
            expiresAtRealtime = durationRealtime > 0f ? Time.unscaledTime + durationRealtime : 0f
        };
        Recompute();
        return new TimeRequestHandle(id);
    }

    private void Recompute()
    {
        bool globalPause = false;
        float globalScale = baseGlobalScale;
        foreach (Request request in requests.Values)
        {
            if (request.domain != null) continue;
            globalPause |= request.pause;
            globalScale = Mathf.Min(globalScale, request.scale);
        }

        effectiveGlobalScale = globalPause ? 0f : Mathf.Clamp(globalScale, 0f, 2f);
        Time.timeScale = effectiveGlobalScale;
        Time.fixedDeltaTime = initialFixedDeltaTime * effectiveGlobalScale;
        RecomputeDomains();

        if (logDiagnostics)
        {
            Debug.Log("[TimeManager] Global=" + effectiveGlobalScale.ToString("F3") + " | requests=" + requests.Count + " | domains=" + registeredDomains.Count + ".", this);
        }
    }

    private void RecomputeDomains()
    {
        registeredDomains.RemoveWhere(domain => domain == null);
        foreach (CombatTimeDomain domain in registeredDomains)
        {
            bool pause = false;
            float scale = 1f;
            foreach (Request request in requests.Values)
            {
                if (request.domain != domain) continue;
                pause |= request.pause;
                scale = Mathf.Min(scale, request.scale);
            }
            domain.ApplyManagerScale(pause ? 0f : scale);
        }
    }

    [ContextMenu("Log Time Diagnostics")]
    private void LogTimeDiagnostics()
    {
        var report = new System.Text.StringBuilder();
        report.Append("[TimeManager] global=").Append(effectiveGlobalScale.ToString("F3"));
        report.Append(" | requests=").Append(requests.Count).Append(" | domains=").Append(registeredDomains.Count);

        foreach (Request request in requests.Values)
        {
            report.Append("\n - ").Append(request.domain == null ? "global" : request.domain.name);
            report.Append(" scale=").Append(request.pause ? "pause" : request.scale.ToString("F3"));
            report.Append(" owner=").Append(request.owner is UnityEngine.Object unityOwner && unityOwner != null ? unityOwner.name : request.owner.GetType().Name);
        }

        Debug.Log(report.ToString(), this);
    }
}
