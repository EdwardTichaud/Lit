using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// <summary>A stone angel: sustained flight, aimed bolts and a telegraphed grounded sweep.</summary>
public sealed class LimbesAngelBoss : BossEncounterBehaviour, ILitBrainsEncounterPolicy
{
    protected override bool SupportsNonLegacyEncounterActor => true;
    public IEnemyEncounterActor Actor => EncounterActor;
    private readonly NetworkVariable<bool> replicatedUnlocked = new(false);
    private bool offlineUnlocked;
    public bool unlocked
    {
        get => IsSpawned ? replicatedUnlocked.Value : offlineUnlocked;
        set
        {
            if (!Authority) return;
            if (IsSpawned) replicatedUnlocked.Value = value; else offlineUnlocked = value;
        }
    }
    public bool AllowsDecisions => false;
    public bool AllowsDamage => unlocked && !IsBossResolved;
    bool ILitBrainsEncounterPolicy.IsReturning => false;
    bool ILitBrainsEncounterPolicy.IsFlameDormant => false;
    bool ILitBrainsEncounterPolicy.FilterIncomingDamage(int requested, SquadCharacterController source, out int permitted) { permitted = requested; return unlocked; }
    public Transform arenaCenter;
    public Transform visual;
    public LimbesAngelBolt boltPrefab;
    public float arenaRadius = 11;
    public float flightHeight = 5;
    public float moveSpeed = 5;
    public float meleeRadius = 4;
    public int meleeDamage = 20;
    public int projectileDamage = 12;
    public float projectileSpeed = 11;
    private Rigidbody body;
    private double nextAttack, strikeAt;
    private bool windingUp;
    private Vector3 strikeDirection;
    private Vector3 desiredPosition;
    private bool motorOwned;
    private int previousHp = -1;
    public bool IsFlying { get; private set; }
    public int BoltsFired { get; private set; }
    public int SweepsPerformed { get; private set; }

    protected override void Awake() { base.Awake(); body = GetComponent<Rigidbody>(); }
    protected override void Update()
    {
        base.Update();
        if (!Authority || Actor == null || Actor.Health == null) return;
        if (previousHp != Actor.Health.CurrentHp)
        {
            previousHp = Actor.Health.CurrentHp;
            SetSegments(previousHp);
            if (Actor.Health.IsDead) { ResolveAuthoritatively(); return; }
        }
        if (!IsBossEngaged || IsBossResolved || !Actor.IsEncounterAvailable || Actor.IsFlameDormant) return;
        if (!motorOwned) { Actor.SetSuspended(true); motorOwned = true; }
        var players = CycleGroupSequenceGate.LivingPlayers().Select(p => p.player)
            .Where(p => arenaCenter != null && (p.position - arenaCenter.position).sqrMagnitude < 900)
            .OrderBy(p => (p.position - transform.position).sqrMagnitude).ToArray();
        if (players.Length == 0) { windingUp = false; return; }
        Transform target = players[0];
        float phase = (float)(EncounterTime % 18);
        IsFlying = phase < 9;
        Vector3 center = arenaCenter != null ? arenaCenter.position : transform.position;
        if (IsFlying)
        {
            if (windingUp) Telegraph(false);
            float angle = (float)EncounterTime * .4f;
            desiredPosition = center + new Vector3(Mathf.Cos(angle) * 6, flightHeight, Mathf.Sin(angle) * 6);
            windingUp = false;
            if (EncounterTime >= nextAttack)
            {
                Fire(target.position + Vector3.up);
                nextAttack = EncounterTime + 2.4;
            }
        }
        else
        {
            Vector3 point = target.position - center;
            point.y = 0;
            point = Vector3.ClampMagnitude(point, Mathf.Max(1, arenaRadius - 2));
            desiredPosition = center + point;
            if (windingUp)
            {
                desiredPosition = transform.position;
                desiredPosition.y = center.y;
                if (EncounterTime >= strikeAt)
                {
                    foreach (Transform player in players)
                    {
                        Vector3 delta = player.position - transform.position;
                        if (delta.magnitude <= meleeRadius && Vector3.Angle(strikeDirection, Vector3.ProjectOnPlane(delta, Vector3.up)) <= 75)
                            player.GetComponentInChildren<SquadCharacterController>()?.ApplyDamage(meleeDamage, "Gargouille des Limbes — balayage");
                    }
                    SweepsPerformed++;
                    windingUp = false;
                    nextAttack = EncounterTime + 2.5;
                    Telegraph(false);
                }
            }
            else if (transform.position.y < center.y + 1 && Vector3.Distance(transform.position, target.position) < meleeRadius && EncounterTime >= nextAttack)
            {
                windingUp = true;
                strikeDirection = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up).normalized;
                strikeAt = EncounterTime + 1.1;
                Telegraph(true);
            }
        }
        Vector3 facing = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
        if (!windingUp && facing.sqrMagnitude > .01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), Time.deltaTime * 100);
    }

    private void FixedUpdate()
    {
        if (!Authority || !motorOwned || !IsBossEngaged || IsBossResolved || Actor == null || !Actor.IsEncounterAvailable || Actor.IsFlameDormant) return;
        float delta = Actor.TimeDomain != null ? Actor.TimeDomain.FixedDeltaTime : Time.fixedDeltaTime;
        Vector3 requested = Vector3.MoveTowards(body.position, desiredPosition, moveSpeed * delta);
        Vector3 movement = requested - body.position;
        if (movement.sqrMagnitude > .00001f && Physics.SphereCast(body.position + Vector3.up, .6f, movement.normalized,
            out var hit, movement.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            !hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<SquadCharacterController>() == null)
            return;
        body.MovePosition(requested);
    }
    private void Fire(Vector3 target)
    {
        BoltsFired++;
        Vector3 source = transform.position + Vector3.up * 2 + transform.forward;
        if (IsSpawned) FireRpc(source, target); else SpawnBolt(source, target);
    }
    [Rpc(SendTo.ClientsAndHost)] private void FireRpc(Vector3 source, Vector3 target) => SpawnBolt(source, target);
    private void SpawnBolt(Vector3 source, Vector3 target)
    {
        if (boltPrefab == null) return;
        var bolt = Instantiate(boltPrefab, source, Quaternion.identity);
        bolt.Launch(this, target, projectileSpeed, projectileDamage, Authority);
    }
    private void Telegraph(bool active)
    {
        if (IsSpawned) TelegraphRpc(active); else ApplyTelegraph(active);
    }
    [Rpc(SendTo.ClientsAndHost)] private void TelegraphRpc(bool active) => ApplyTelegraph(active);
    private void ApplyTelegraph(bool active)
    {
        // Fixed stone body leans before the sweep; no animation event grants damage.
        if (visual != null) visual.localRotation = active ? Quaternion.Euler(-18, 0, 0) : Quaternion.identity;
    }
    public override bool FilterIncomingDamage(int incomingDamage, SquadCharacterController source, out int permittedDamage)
    {
        if (!IsBossEngaged) { permittedDamage = 0; return false; }
        return base.FilterIncomingDamage(incomingDamage, source, out permittedDamage);
    }
    protected override void OnBossResolvedAuthoritatively()
    {
        windingUp = false;
        if (motorOwned && Actor != null) Actor.SetSuspended(false);
        motorOwned = false;
        foreach (var bolt in FindObjectsByType<LimbesAngelBolt>()) if (bolt.Owner == this) Destroy(bolt.gameObject);
        Actor?.Health?.ForceDefeat();
    }
    protected override void OnDisable()
    {
        windingUp = false;
        if (motorOwned && Actor != null) Actor.SetSuspended(false);
        motorOwned = false;
        base.OnDisable();
    }
}
