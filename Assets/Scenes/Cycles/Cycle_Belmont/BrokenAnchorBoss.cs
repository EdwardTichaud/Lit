using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Puzzle boss for the Belmont conduits. The Anchor does not enter combat: it
/// fires at Lucian's captured position, letting a last-moment dodge send light
/// into one of the three Veille torches.
/// Attach this to the BelmontCycle NetworkObject, not to the visual itself.
/// </summary>
public sealed class BrokenAnchorBoss : NetworkBehaviour
{
    [Header("Scene references")]
    [SerializeField] private GameObject anchorVisual;
    [SerializeField] private Transform emissionPoint;
    [SerializeField] private Flame[] torches = Array.Empty<Flame>();
    [SerializeField] private BrokenAnchorLightOrb orbPrefab;

    [Header("Attack")]
    [SerializeField, Min(0f)] private float telegraphSeconds = 0.75f;
    [SerializeField, Min(0.1f)] private float salvoIntervalSeconds = 3f;
    [SerializeField, Min(0.1f)] private float orbSpeed = 12f;
    [SerializeField, Min(0.1f)] private float orbLifetimeSeconds = 8f;
    [SerializeField, Min(0)] private int playerDamage = 15;

    [Header("Timeline presentation")]
    [SerializeField, Min(0.1f)] private float releaseConvergenceSeconds = 3.5f;

    private Coroutine firing;
    private Coroutine releasePresentation;

    private bool Authority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer;
    private bool IsPuzzleRunning => anchorVisual != null && anchorVisual.activeInHierarchy && !AllTorchesLit();

    private void Update()
    {
        if (Authority && IsPuzzleRunning && firing == null)
        {
            firing = StartCoroutine(FireLoop());
        }
        else if ((!IsPuzzleRunning || AllTorchesLit()) && firing != null)
        {
            StopCoroutine(firing);
            firing = null;
        }

        // The CycleController starts the AnchorRelease Timeline as soon as the
        // third persistent Flame reports its step. This local presentation is
        // deliberately driven by that same replicated Flame state: every peer
        // sees the three lights converge while the Timeline is playing.
        if (AllTorchesLit() && anchorVisual != null && anchorVisual.activeInHierarchy && releasePresentation == null)
        {
            releasePresentation = StartCoroutine(PlayReleasePresentation());
        }
    }

    private void OnDisable()
    {
        if (firing != null) StopCoroutine(firing);
        if (releasePresentation != null) StopCoroutine(releasePresentation);
        firing = null;
        releasePresentation = null;
    }

    private IEnumerator FireLoop()
    {
        while (IsPuzzleRunning)
        {
            Transform player = ResolveTarget();
            if (player != null)
            {
                Vector3 origin = emissionPoint != null ? emissionPoint.position : anchorVisual.transform.position + Vector3.up * 1.5f;
                Vector3 destination = player.position + Vector3.up;
                yield return new WaitForSecondsRealtime(telegraphSeconds);
                if (!IsPuzzleRunning) break;
                Fire(origin, destination);
            }
            yield return new WaitForSecondsRealtime(salvoIntervalSeconds);
        }
        firing = null;
    }

    private Transform ResolveTarget()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && manager.IsServer && manager.ConnectedClientsIds != null)
        {
            Transform nearest = null;
            float nearestDistance = float.PositiveInfinity;
            Vector3 origin = anchorVisual != null ? anchorVisual.transform.position : transform.position;
            for (int index = 0; index < manager.ConnectedClientsIds.Count; index++)
            {
                Transform candidate = NetcodePlayerUtils.GetPlayerTransform(manager.ConnectedClientsIds[index]);
                if (candidate == null) continue;
                float distance = (candidate.position - origin).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = candidate;
                nearestDistance = distance;
            }
            if (nearest != null) return nearest;
        }

        return LocalPlayerUtils.GetControlledCharacter()?.transform;
    }

    private IEnumerator PlayReleasePresentation()
    {
        Vector3 destination = anchorVisual.transform.position + Vector3.up * 1.25f;
        List<Transform> motes = new List<Transform>();
        for (int index = 0; index < torches.Length; index++)
        {
            Flame torch = torches[index];
            if (torch == null) continue;
            GameObject mote = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mote.name = "AnchorRelease_Light";
            mote.transform.position = torch.transform.position + Vector3.up * .85f;
            mote.transform.localScale = Vector3.one * .16f;
            Collider collider = mote.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Light light = mote.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(.55f, .85f, 1f);
            light.intensity = 4f;
            light.range = 3f;
            motes.Add(mote.transform);
        }

        float elapsed = 0f;
        Vector3[] origins = new Vector3[motes.Count];
        for (int index = 0; index < motes.Count; index++) origins[index] = motes[index].position;
        while (elapsed < releaseConvergenceSeconds && anchorVisual != null && anchorVisual.activeInHierarchy)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / releaseConvergenceSeconds);
            for (int index = 0; index < motes.Count; index++)
                if (motes[index] != null) motes[index].position = Vector3.Lerp(origins[index], destination, progress);
            yield return null;
        }

        for (int index = 0; index < motes.Count; index++)
            if (motes[index] != null) Destroy(motes[index].gameObject);
        releasePresentation = null;
    }

    private void Fire(Vector3 origin, Vector3 destination)
    {
        SpawnOrb(origin, destination, true);
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned)
        {
            FireVisualClientRpc(origin, destination);
        }
    }

    [ClientRpc]
    private void FireVisualClientRpc(Vector3 origin, Vector3 destination)
    {
        if (IsServer) return;
        SpawnOrb(origin, destination, false);
    }

    private void SpawnOrb(Vector3 origin, Vector3 destination, bool authoritative)
    {
        BrokenAnchorLightOrb orb = orbPrefab != null
            ? Instantiate(orbPrefab, origin, Quaternion.identity)
            : BrokenAnchorLightOrb.CreateRuntimeOrb(origin);
        orb.Launch(destination, torches, orbSpeed, orbLifetimeSeconds, playerDamage, authoritative);
    }

    private bool AllTorchesLit()
    {
        if (torches == null || torches.Length == 0) return false;
        for (int index = 0; index < torches.Length; index++)
            if (torches[index] == null || !torches[index].IsEffectivelyLit) return false;
        return true;
    }
}

[DisallowMultipleComponent]
public sealed class BrokenAnchorTorchReceiver : MonoBehaviour
{
    [SerializeField] private Flame flame;
    public Flame Flame => flame != null ? flame : GetComponentInParent<Flame>();

    public bool TryLight()
    {
        Flame target = Flame;
        if (target == null || target.IsEffectivelyLit) return false;
        target.SetLit(true);
        return true;
    }
}

[DisallowMultipleComponent]
public sealed class BrokenAnchorLightOrb : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float expiresAt;
    private int damage;
    private bool authoritative;
    private bool resolved;

    public static BrokenAnchorLightOrb CreateRuntimeOrb(Vector3 position)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = "BrokenAnchor_LightOrb";
        root.transform.position = position;
        root.transform.localScale = Vector3.one * .38f;
        Collider collider = root.GetComponent<Collider>();
        collider.isTrigger = true;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(.55f, .82f, 1f);
        light.intensity = 5f;
        light.range = 4f;
        Renderer renderer = root.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (renderer != null && shader != null)
        {
            Material material = new Material(shader);
            material.color = new Color(.35f, .72f, 1f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(.5f, .85f, 1f) * 4f);
            renderer.material = material;
        }
        return root.AddComponent<BrokenAnchorLightOrb>();
    }

    public void Launch(Vector3 destination, Flame[] _, float travelSpeed, float lifetime, int playerDamage, bool isAuthoritative)
    {
        Vector3 delta = destination - transform.position;
        direction = delta.sqrMagnitude > .0001f ? delta.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        speed = Mathf.Max(.1f, travelSpeed);
        expiresAt = Time.unscaledTime + Mathf.Max(.1f, lifetime);
        damage = Mathf.Max(0, playerDamage);
        authoritative = isAuthoritative;
        foreach (Collider collider in GetComponents<Collider>()) collider.enabled = authoritative;
    }

    private void Update()
    {
        transform.position += direction * (speed * Time.unscaledDeltaTime);
        if (Time.unscaledTime >= expiresAt) Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!authoritative || resolved || other == null) return;
        BrokenAnchorTorchReceiver torch = other.GetComponentInParent<BrokenAnchorTorchReceiver>();
        if (torch != null)
        {
            resolved = true;
            torch.TryLight();
            Destroy(gameObject);
            return;
        }

        SquadCharacterController player = other.GetComponentInParent<SquadCharacterController>();
        if (player != null)
        {
            resolved = true;
            player.ApplyDamage(damage, "BrokenAnchorLightOrb");
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
        {
            resolved = true;
            Destroy(gameObject);
        }
    }
}
