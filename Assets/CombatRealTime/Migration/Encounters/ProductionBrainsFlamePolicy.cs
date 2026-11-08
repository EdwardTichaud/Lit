using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Production fixed-flame gate; the server samples the same contacts as the legacy actor.</summary>
[DefaultExecutionOrder(-75)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CombatTimeDomain))]
public sealed class ProductionBrainsFlamePolicy : NetworkBehaviour, ILitBrainsEncounterPolicy, ILitBrainsMovementPolicy
{
    public bool requiresFlameInfluence = true;
    private readonly NetworkVariable<bool> activeInfluence = new NetworkVariable<bool>(false);
    private bool offlineInfluence, agentCaptured, savedStopped;
    private float nextSample;
    private Collider[] contacts;
    private CharacterInfo health;
    private CombatTimeDomain clock;
    private NavMeshAgent agent;
    private readonly System.Collections.Generic.List<Vector2> movementIntervals = new System.Collections.Generic.List<Vector2>();
    public bool AllowsDecisions => isActiveAndEnabled && !IsFlameDormant;
    public bool AllowsDamage => isActiveAndEnabled && !IsFlameDormant;
    public bool IsReturning => false;
    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    public bool IsFlameDormant => requiresFlameInfluence && !(Online ? IsSpawned && activeInfluence.Value : offlineInfluence) &&
        !(health != null && health.IsDead);

    private void Awake() => Resolve();
    private void Resolve()
    {
        health = GetComponent<CharacterInfo>();
        clock = GetComponent<CombatTimeDomain>();
        agent = GetComponent<NavMeshAgent>();
        // Detection/weapon triggers describe reach, not the illuminated body.
        // A large perception sphere must not wake an enemy outside the light.
        contacts = System.Array.FindAll(GetComponentsInChildren<Collider>(true), collider => !collider.isTrigger);
    }
    private void OnEnable()
    {
        Resolve();
        if (health != null) health.HealthChanged += OnHealthChanged;
        Refresh();
    }
    private void OnHealthChanged(CharacterInfo changed) => ApplyPause();
    public override void OnNetworkSpawn()
    {
        activeInfluence.OnValueChanged += OnInfluenceChanged;
        Refresh();
    }
    public override void OnNetworkDespawn()
    {
        activeInfluence.OnValueChanged -= OnInfluenceChanged;
        ReleasePause();
        offlineInfluence = false;
    }
    private void OnInfluenceChanged(bool previous, bool current) => ApplyPause();
    private void Update()
    {
        if (Time.unscaledTime >= nextSample) Refresh();
        ApplyPause();
    }
    public void Refresh()
    {
        if (clock == null) Resolve();
        nextSample = Time.unscaledTime + .25f;
        if (!Online || IsSpawned && IsServer)
        {
            bool sampled = !requiresFlameInfluence || SampleContacts();
            if (Online) activeInfluence.Value = sampled; else offlineInfluence = sampled;
        }
        ApplyPause();
    }
    private bool SampleContacts()
    {
        foreach (var flame in Flame.ActiveInfluenceFlames)
            if (flame != null)
                foreach (var contact in contacts)
                    if (contact != null && flame.ProvidesEnemyActivationTo(contact)) return true;
        return false;
    }

    // Clip the whole segment to the connected union of lit fixed Flame spheres.
    // Checking only its endpoint would permit a dash across a dark gap.
    public Vector3 ConstrainStep(Vector3 position, Vector3 step)
    {
        if (!requiresFlameInfluence || !isActiveAndEnabled || step.sqrMagnitude < .0000001f) return step;
        if (contacts == null) Resolve();
        movementIntervals.Clear();
        foreach (var flame in Flame.ActiveInfluenceFlames)
        {
            if (flame == null) continue;
            foreach (var contact in contacts)
            {
                if (contact == null || !flame.TryGetEnemyMovementSphere(contact, out var center, out float radius)) continue;
                // Keep the root safely inside; perception triggers never expand this territory.
                radius = Mathf.Max(0, radius - .08f);
                Vector3 offset = position - center;
                float a = step.sqrMagnitude, b = Vector3.Dot(offset, step);
                float discriminant = b * b - a * (offset.sqrMagnitude - radius * radius);
                if (discriminant >= 0)
                {
                    float root = Mathf.Sqrt(discriminant);
                    movementIntervals.Add(new Vector2((-b - root) / a, (-b + root) / a));
                }
                break;
            }
        }
        float covered = 0;
        for (int pass = 0; pass < movementIntervals.Count; pass++)
        {
            float before = covered;
            foreach (var interval in movementIntervals)
                if (interval.x <= covered + .00001f && interval.y > covered) covered = interval.y;
            if (covered >= 1) return step;
            if (covered == before) break;
        }
        return step * Mathf.Clamp01(covered);
    }

    private void LateUpdate()
    {
        if (!requiresFlameInfluence || Online && (!IsSpawned || !IsServer) || agent == null ||
            !agent.isActiveAndEnabled || !agent.isOnNavMesh || agent.isStopped) return;
        // Local avoidance may deviate from the validated path. Bound its next
        // velocity too, without changing the Transform or teleporting the agent.
        float dt = Mathf.Max(Time.deltaTime, .001f);
        Vector3 step = agent.desiredVelocity * dt;
        Vector3 allowed = ConstrainStep(transform.position, step);
        if ((allowed - step).sqrMagnitude > .0000001f) agent.velocity = allowed / dt;
    }
    private void ApplyPause()
    {
        bool paused = IsFlameDormant;
        clock?.SetIntrinsicPause(this, paused);
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        if (paused)
        {
            if (!agentCaptured) { savedStopped = agent.isStopped; agentCaptured = true; }
            agent.isStopped = true;
        }
        else if (agentCaptured)
        {
            if (GetComponent<LitBrainsEnemy>()?.IsCinematicSuspended != true) agent.isStopped = savedStopped;
            agentCaptured = false;
        }
    }
    private void ReleasePause()
    {
        clock?.SetIntrinsicPause(this, false);
        if (agentCaptured && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh &&
            GetComponent<LitBrainsEnemy>()?.IsCinematicSuspended != true) agent.isStopped = savedStopped;
        agentCaptured = false;
    }
    private void OnDisable()
    {
        if (health != null) health.HealthChanged -= OnHealthChanged;
        ReleasePause();
        offlineInfluence = false;
    }
    public bool FilterIncomingDamage(int requested, SquadCharacterController source, out int permitted)
    {
        permitted = AllowsDamage ? Mathf.Max(0, requested) : 0;
        return AllowsDamage;
    }
}
