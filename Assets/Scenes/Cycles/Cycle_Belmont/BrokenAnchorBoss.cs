using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>L'Ancre brisée: a static, lockable boss whose only vulnerability is redirected Veillée light.</summary>
[DisallowMultipleComponent]
public sealed class BrokenAnchorBoss : BossEncounterBehaviour
{
    [Header("Puzzle targets")]
    [SerializeField] private Transform emissionPoint;
    [SerializeField] private BrokenAnchorTorch[] torches = Array.Empty<BrokenAnchorTorch>();
    [SerializeField] private BrokenAnchorLightBall orbPrefab;
    [Header("Attack")]
    [SerializeField, Min(0f)] private float telegraphSeconds = .75f;
    [SerializeField, Min(.1f)] private float salvoIntervalSeconds = 3f;
    [SerializeField, Min(.1f)] private float orbSpeed = 12f;
    [SerializeField, Min(.1f)] private float orbLifetimeSeconds = 8f;
    [SerializeField, Min(0)] private int playerDamage = 15;
    [SerializeField, Min(0f), Tooltip("Distance de sortie supplementaire pour que la boule ne naisse jamais dans le collider de l'Ancre.")]
    private float launchClearance = .35f;
    [Header("Release presentation")]
    [SerializeField, Min(.1f)] private float releaseConvergenceSeconds = 3.5f;
    private Coroutine firing;
    private readonly HashSet<BrokenAnchorTorch> acceptedTorches = new();
    private readonly HashSet<BrokenAnchorTorch> observedTorches = new();
    private int lastPresentedSegments = int.MinValue;
    private bool wasFlameDormant;

    public void Configure(Transform source, BrokenAnchorTorch[] targets, BrokenAnchorLightBall projectile, float telegraph, float interval, float speed, float lifetime, int damage, float releaseSeconds)
    {
        emissionPoint = source;
        torches = targets ?? Array.Empty<BrokenAnchorTorch>();
        orbPrefab = projectile;
        telegraphSeconds = Mathf.Max(0f, telegraph);
        salvoIntervalSeconds = Mathf.Max(.1f, interval);
        orbSpeed = Mathf.Max(.1f, speed);
        orbLifetimeSeconds = Mathf.Max(.1f, lifetime);
        playerDamage = Mathf.Max(0, damage);
        releaseConvergenceSeconds = Mathf.Max(.1f, releaseSeconds);
        RebindTorchEvents();
        SynchronizeHealth(CurrentSegments);
    }

    protected override void Awake()
    {
        base.Awake();
        RebindTorchEvents();
        SynchronizeHealth(CurrentSegments);
    }

    private void OnEnable() => RebindTorchEvents();
    protected override void Update()
    {
        base.Update();
        if (Enemy != null && Enemy.IsFlameDormant) { wasFlameDormant = true; return; }
        if (wasFlameDormant)
        {
            wasFlameDormant = false;
            if (Authority && IsBossEngaged) ReconcileLitTorches("flame resumed");
        }
        // Boss segments are replicated independently of CharacterInfo. Mirror them on every
        // peer so the existing combat HUD always displays 3/3, 2/3, 1/3 and 0/3 correctly.
        if (lastPresentedSegments != CurrentSegments)
        {
            lastPresentedSegments = CurrentSegments;
            SynchronizeHealth(CurrentSegments);
        }
        if (Authority && IsBossEngaged && Enemy != null && !Enemy.IsFlameDormant && firing == null) firing = StartCoroutine(FireLoop());
        if ((!IsBossEngaged || IsBossResolved) && firing != null) { StopCoroutine(firing); firing = null; }
    }
    private void OnDisable()
    {
        if (firing != null) StopCoroutine(firing);
        firing = null;
        UnbindTorchEvents();
    }

    private void OnDestroy() => UnbindTorchEvents();

    /// <summary>Authoritative projectile callback; the boss accepts each dedicated torch once.</summary>
    public void NotifyTorchLit(BrokenAnchorTorch torch)
    {
        AcceptLitTorch(torch, "projectile");
    }
    protected override void OnBossEngagedAuthoritatively()
    {
        Trace($"combat engagé | segments={CurrentSegments}/{MaximumSegments}");
        SynchronizeHealth(CurrentSegments);
        ReconcileLitTorches("engagement");
    }
    protected override void OnBossResolvedAuthoritatively()
    {
        if (firing != null) { StopCoroutine(firing); firing = null; }
        SynchronizeHealth(0);
        Trace("résolution : troisième torche validée, tirs arrêtés et séquence Belmont autorisée.");
    }

    protected override void OnSegmentChangedAuthoritatively(int remaining)
    {
        SynchronizeHealth(remaining);
    }

    private void RebindTorchEvents()
    {
        UnbindTorchEvents();
        foreach (BrokenAnchorTorch torch in torches ?? Array.Empty<BrokenAnchorTorch>())
        {
            if (torch == null || !observedTorches.Add(torch)) continue;
            torch.StateChanged += OnTorchStateChanged;
        }
        if (Authority && IsBossEngaged) ReconcileLitTorches("reliure");
    }

    private void UnbindTorchEvents()
    {
        foreach (BrokenAnchorTorch torch in observedTorches)
            if (torch != null) torch.StateChanged -= OnTorchStateChanged;
        observedTorches.Clear();
    }

    private void OnTorchStateChanged(BrokenAnchorTorch torch, bool lit)
    {
        if (!lit) return;
        AcceptLitTorch(torch, "événement de torche");
    }

    private void ReconcileLitTorches(string source)
    {
        foreach (BrokenAnchorTorch torch in torches ?? Array.Empty<BrokenAnchorTorch>())
            if (torch != null && torch.IsLit) AcceptLitTorch(torch, source);
    }

    private void AcceptLitTorch(BrokenAnchorTorch torch, string source)
    {
        if (Enemy == null || Enemy.IsFlameDormant) return;
        if (!Authority || torch == null || Array.IndexOf(torches, torch) < 0 || !torch.IsLit)
        {
            Trace($"torche refusée ({source}) : état ou autorité invalide.");
            return;
        }
        if (!IsBossEngaged || IsBossResolved)
        {
            Trace($"torche '{torch.name}' détectée ({source}) mais combat non actif.");
            return;
        }
        if (!acceptedTorches.Add(torch))
        {
            Trace($"torche '{torch.name}' ignorée ({source}) : déjà comptabilisée.");
            return;
        }

        int before = CurrentSegments;
        RemoveSegmentAuthoritatively();
        Trace($"torche '{torch.name}' acceptée ({source}) : segments {before}/{MaximumSegments} -> {CurrentSegments}/{MaximumSegments}.");
    }

    private void SynchronizeHealth(int current)
    {
        if (Enemy != null && Enemy.Health != null)
            Enemy.Health.SetHealth(Mathf.Clamp(current, 0, MaximumSegments), MaximumSegments);
    }

    private void Trace(string message) => Debug.Log("[BrokenAnchorBoss] " + message, this);

    private IEnumerator FireLoop()
    {
        while (IsBossEngaged && !IsBossResolved)
        {
            while (Enemy != null && Enemy.IsFlameDormant && !IsBossResolved) yield return null;
            if (IsBossResolved) break;
            Transform target = ResolveTarget();
            if (target != null)
            {
                Vector3 origin = emissionPoint != null ? emissionPoint.position : Enemy.transform.position + Vector3.up * 1.5f;
                Vector3 destination = target.position + Vector3.up;
                yield return WaitForEncounterSeconds(telegraphSeconds);
                if (!IsBossEngaged || IsBossResolved) break;
                Fire(origin, destination);
            }
            yield return WaitForEncounterSeconds(salvoIntervalSeconds);
        }
        firing = null;
    }
    private Transform ResolveTarget()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && manager.IsServer)
        {
            Transform nearest = null; float nearestDistance = float.PositiveInfinity;
            foreach (ulong id in manager.ConnectedClientsIds)
            {
                Transform candidate = NetcodePlayerUtils.GetPlayerTransform(id);
                if (candidate == null) continue;
                float distance = (candidate.position - Enemy.transform.position).sqrMagnitude;
                if (distance < nearestDistance) { nearest = candidate; nearestDistance = distance; }
            }
            if (nearest != null) return nearest;
        }
        return LocalPlayerContext.LocalCharacterRoot;
    }
    private void Fire(Vector3 origin, Vector3 destination)
    {
        Vector3 launchOrigin = ResolveLaunchOrigin(origin, destination);
        SpawnOrb(launchOrigin, destination, true);
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned) FireVisualClientRpc(origin, destination);
    }
    [ClientRpc] private void FireVisualClientRpc(Vector3 origin, Vector3 destination)
    {
        if (!IsServer) SpawnOrb(ResolveLaunchOrigin(origin, destination), destination, false);
    }

    private Vector3 ResolveLaunchOrigin(Vector3 emissionOrigin, Vector3 destination)
    {
        Vector3 direction = Vector3.ProjectOnPlane(destination - emissionOrigin, Vector3.up);
        if (direction.sqrMagnitude < .0001f) direction = Enemy != null ? Enemy.transform.forward : Vector3.forward;
        direction.Normalize();
        float radius = 0f;
        if (Enemy != null)
        {
            foreach (Collider collider in Enemy.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || collider.isTrigger) continue;
                radius = Mathf.Max(radius, Vector3.ProjectOnPlane(collider.bounds.extents, Vector3.up).magnitude);
            }
        }
        return emissionOrigin + direction * (radius + launchClearance);
    }
    private void SpawnOrb(Vector3 origin, Vector3 destination, bool authoritative)
    {
        if (orbPrefab == null) { Debug.LogError("[Belmont] Prefab BrokenAnchorLightBall manquant.", this); return; }
        BrokenAnchorLightBall orb = Instantiate(orbPrefab, origin, Quaternion.identity);
        orb.Launch(destination, orbSpeed, orbLifetimeSeconds, playerDamage, authoritative, this, Enemy != null ? Enemy.transform : null);
    }
}
