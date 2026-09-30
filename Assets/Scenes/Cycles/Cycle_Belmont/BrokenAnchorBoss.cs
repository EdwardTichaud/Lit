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
    [SerializeField] private Flame[] torches = Array.Empty<Flame>();
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
    private Coroutine releasePresentation;
    private readonly HashSet<Flame> acceptedTorches = new();

    public void Configure(Transform source, Flame[] targets, BrokenAnchorLightBall projectile, float telegraph, float interval, float speed, float lifetime, int damage, float releaseSeconds)
    {
        emissionPoint = source;
        torches = targets ?? Array.Empty<Flame>();
        orbPrefab = projectile;
        telegraphSeconds = Mathf.Max(0f, telegraph);
        salvoIntervalSeconds = Mathf.Max(.1f, interval);
        orbSpeed = Mathf.Max(.1f, speed);
        orbLifetimeSeconds = Mathf.Max(.1f, lifetime);
        playerDamage = Mathf.Max(0, damage);
        releaseConvergenceSeconds = Mathf.Max(.1f, releaseSeconds);
    }

    protected override void Awake()
    {
        base.Awake();
        foreach (Flame torch in torches) if (torch != null && torch.IsEffectivelyLit) acceptedTorches.Add(torch);
    }
    protected override void Update()
    {
        base.Update();
        if (Authority && IsBossEngaged && firing == null) firing = StartCoroutine(FireLoop());
        if ((!IsBossEngaged || IsBossResolved) && firing != null) { StopCoroutine(firing); firing = null; }
        if (IsBossResolved && releasePresentation == null) releasePresentation = StartCoroutine(PlayReleasePresentation());
    }
    private void OnDisable()
    {
        if (firing != null) StopCoroutine(firing);
        if (releasePresentation != null) StopCoroutine(releasePresentation);
        firing = null; releasePresentation = null;
    }

    /// <summary>Authoritative projectile callback; torch state itself remains owned by Flame/progression.</summary>
    public void NotifyTorchLit(Flame torch)
    {
        if (!Authority || !IsBossEngaged || torch == null || Array.IndexOf(torches, torch) < 0 || !torch.IsEffectivelyLit) return;
        if (!acceptedTorches.Add(torch)) return;
        RemoveSegmentAuthoritatively();
    }
    protected override void OnBossEngagedAuthoritatively()
    {
        foreach (Flame torch in torches) if (torch != null && torch.IsEffectivelyLit && acceptedTorches.Add(torch)) RemoveSegmentAuthoritatively();
    }
    protected override void OnBossResolvedAuthoritatively()
    {
        if (firing != null) { StopCoroutine(firing); firing = null; }
        if (Enemy != null && Enemy.Health != null) Enemy.Health.SetHealth(0, MaximumSegments);
    }

    protected override void OnSegmentChangedAuthoritatively(int remaining)
    {
        if (Enemy != null && Enemy.Health != null) Enemy.Health.SetHealth(remaining, MaximumSegments);
    }

    private IEnumerator FireLoop()
    {
        while (IsBossEngaged && !IsBossResolved)
        {
            Transform target = ResolveTarget();
            if (target != null)
            {
                Vector3 origin = emissionPoint != null ? emissionPoint.position : Enemy.transform.position + Vector3.up * 1.5f;
                Vector3 destination = target.position + Vector3.up;
                yield return new WaitForSecondsRealtime(telegraphSeconds);
                if (!IsBossEngaged || IsBossResolved) break;
                Fire(origin, destination);
            }
            yield return new WaitForSecondsRealtime(salvoIntervalSeconds);
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
    private IEnumerator PlayReleasePresentation()
    {
        if (Enemy == null) yield break;
        Vector3 destination = Enemy.transform.position + Vector3.up * 1.25f;
        List<Transform> motes = new();
        foreach (Flame torch in torches)
        {
            if (torch == null) continue;
            GameObject mote = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mote.name = "AnchorRelease_Light";
            mote.transform.position = torch.transform.position + Vector3.up * .85f;
            mote.transform.localScale = Vector3.one * .16f;
            Destroy(mote.GetComponent<Collider>());
            Light light = mote.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(.55f, .85f, 1f); light.intensity = 4f; light.range = 3f;
            motes.Add(mote.transform);
        }
        Vector3[] origins = motes.ConvertAll(mote => mote.position).ToArray();
        for (float elapsed = 0f; elapsed < releaseConvergenceSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / releaseConvergenceSeconds);
            for (int i = 0; i < motes.Count; i++) if (motes[i] != null) motes[i].position = Vector3.Lerp(origins[i], destination, progress);
            yield return null;
        }
        foreach (Transform mote in motes) if (mote != null) Destroy(mote.gameObject);
        releasePresentation = null;
    }
}
