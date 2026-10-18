using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class EncounterActorTestDouble : MonoBehaviour, IEnemyEncounterActor, ICombatTarget
{
    public bool available = true, flameDormant, dead, suspended;
    public Transform EncounterRoot => transform;
    public ICombatTarget EncounterTarget => this;
    public CharacterInfo Health => GetComponent<CharacterInfo>();
    public bool IsEncounterAvailable => available;
    public bool IsFlameDormant => flameDormant;
    public bool IsSuspended => suspended;
    public CombatTimeDomain TimeDomain => null;
    public IBossEncounterBehaviour BossEncounter => GetComponent<BossEncounterBehaviour>();
    public void SetSuspended(bool value) => suspended = value;
    public bool HasDeathPresentation => false;
    public bool IsDeathPresentationPlaying => false;
    public IEnumerator PlayDeathPresentation() { yield break; }
    public void CancelDeathPresentation() { }
    public Transform Root => transform;
    public Transform LockPoint => transform;
    public Animator Animator => null;
    public bool IsDead => dead;
    public bool IsAttackCommitted => false;
    public int ActionSequenceId => 0;
    public int ReceiveDamage(int damage) => 0;
    public void SetCinematicSuspended(bool value) => SetSuspended(value);
    public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => false;
}

public sealed class GenericEncounterTestBoss : BossEncounterBehaviour
{
    protected override bool SupportsNonLegacyEncounterActor => true;
    public SquadCharacterController observedSource;
    public bool denyDamage;
    public int filterCalls;
    public void Engage() => EngageAuthoritatively();
    public void RemoveSegment() => RemoveSegmentAuthoritatively();
    public void EndPresentation() => EndLocalCombatPresentation();
    public override bool FilterIncomingDamage(int incomingDamage, SquadCharacterController source, out int permittedDamage)
    {
        observedSource = source;
        filterCalls++;
        permittedDamage = denyDamage ? 0 : incomingDamage;
        return !denyDamage;
    }
}

public sealed class EnemyEncounterActorTests
{
    private GameObject root;
    private EncounterActorTestDouble actor;

    [SetUp]
    public void SetUp()
    {
        Assert.IsFalse(Application.isPlaying, "Run these fixtures in Edit Mode.");
        Assert.IsNull(RealTimeCombatManager.Instance, "Do not register test participants into a live combat manager.");
        Assert.IsNull(LocalPlayerContext.LocalCharacterRoot, "Run without a live local encounter participant.");
        root = new GameObject("Encounter actor test");
        actor = root.AddComponent<EncounterActorTestDouble>();
    }

    [TearDown]
    public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

    [Test]
    public void GenericBossBindsWithoutAddingLegacyBrainOrEngaging()
    {
        var boss = root.AddComponent<GenericEncounterTestBoss>();
        Assert.IsTrue(boss.TryBindEncounterActor(actor));
        Assert.AreSame(actor, boss.EncounterActor);
        Assert.AreSame(actor, EnemyEncounterActors.Resolve(boss));
        Assert.IsNull(boss.Enemy);
        Assert.IsNull(root.GetComponent<EnemyController>());
        Assert.IsFalse(boss.IsBossEngaged);
        Assert.IsFalse(actor.IsSuspended, "Binding must not activate or suspend the actor.");
    }

    [Test]
    public void LocalCombatAdapterCannotOpenDormantBoss()
    {
        var boss = root.AddComponent<GenericEncounterTestBoss>();
        var managerObject = new GameObject("Dormant boss manager test");
        managerObject.SetActive(false);
        try
        {
            var manager = managerObject.AddComponent<RealTimeCombatManager>();
            Assert.IsFalse(EnemyEncounterActors.BeginLocalCombat(actor, manager, root.transform));
            Assert.IsFalse(manager.IsCombatActive);
            Assert.IsFalse(boss.IsBossEngaged);
        }
        finally { Object.DestroyImmediate(managerObject); }
    }

    [Test]
    public void GlobalBossStateAndManualLockCannotEnrollUnconfirmedLocalPlayer()
    {
        var boss = root.AddComponent<GenericEncounterTestBoss>();
        boss.Engage();
        var managerObject = new GameObject("Unconfirmed boss presentation test");
        var localPlayer = new GameObject("Unrelated local player");
        managerObject.SetActive(false);
        try
        {
            var manager = managerObject.AddComponent<RealTimeCombatManager>();
            var world = managerObject.AddComponent<CombatWorldRuntime>();
            manager.ConfigureFreeAttacks(new FreeAttackProfile(), null);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RealTimeCombatManager).GetField("playerRoot", flags).SetValue(manager, localPlayer.transform);
            typeof(RealTimeCombatManager).GetField("combatActive", flags).SetValue(manager, true);
            typeof(RealTimeCombatManager).GetField("worldLockedTarget", flags).SetValue(manager, actor);
            Assert.IsTrue(boss.IsBossEngaged);
            Assert.IsFalse(EnemyEncounterActors.BeginLocalCombat(actor, manager, localPlayer.transform));
            Assert.IsFalse(EnemyEncounterActors.IsPresenting(actor, manager, localPlayer.transform));
            Assert.Zero(manager.ActiveThreatCount);
            Assert.Zero(world.GetThreats(null).Count);
        }
        finally { Object.DestroyImmediate(managerObject); Object.DestroyImmediate(localPlayer); }
    }

    [Test]
    public void OwnerSnapshotPresentsBossWithoutCallingAuthoritativeWorldRegistration()
    {
        var boss = root.AddComponent<GenericEncounterTestBoss>();
        boss.Engage();
        var managerObject = new GameObject("Confirmed boss presentation test");
        var localPlayer = new GameObject("Confirmed local participant");
        var otherPlayer = new GameObject("Different owner");
        managerObject.SetActive(false);
        try
        {
            var manager = managerObject.AddComponent<RealTimeCombatManager>();
            var world = managerObject.AddComponent<CombatWorldRuntime>();
            manager.ConfigureFreeAttacks(new FreeAttackProfile(), null);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RealTimeCombatManager).GetField("playerRoot", flags).SetValue(manager, localPlayer.transform);
            typeof(RealTimeCombatManager).GetField("combatActive", flags).SetValue(manager, true);
            var threats = (System.Collections.Generic.Dictionary<Transform, ICombatTarget>)typeof(RealTimeCombatManager)
                .GetField("worldThreats", flags).GetValue(manager);
            threats.Add(actor.transform, actor);
            Assert.IsFalse(world.isActiveAndEnabled, "Authoritative registration cannot succeed in this fixture.");
            Assert.IsTrue(EnemyEncounterActors.BeginLocalCombat(actor, manager, localPlayer.transform));
            Assert.IsTrue(EnemyEncounterActors.IsPresenting(actor, manager, localPlayer.transform));
            Assert.IsFalse(EnemyEncounterActors.IsPresenting(actor, manager, otherPlayer.transform));
            boss.enabled = false;
            Assert.IsFalse(EnemyEncounterActors.IsPresenting(actor, manager, localPlayer.transform));
            boss.enabled = true;
            Assert.AreEqual(1, manager.ActiveThreatCount);
            Assert.Zero(world.GetThreats(null).Count);
            threats.Clear();
            Assert.IsFalse(EnemyEncounterActors.BeginLocalCombat(actor, manager, localPlayer.transform));
        }
        finally { Object.DestroyImmediate(managerObject); Object.DestroyImmediate(localPlayer); Object.DestroyImmediate(otherPlayer); }
    }

    [Test]
    public void AnyNearbyGateDoesNotEnrollDistantPlayersButWholePartyGateKeepsItsParty()
    {
        var near = new GameObject("Nearby participant");
        var far = new GameObject("Party member outside trigger radius");
        try
        {
            near.transform.position = Vector3.forward;
            far.transform.position = Vector3.forward * 12;
            var living = new[] { near.transform, far.transform, near.transform, null };
            bool Nearby(Transform player) => player.position.sqrMagnitude <= 64;
            var proximity = BossEncounterParticipants.Collect(living, _ => true, Nearby, false);
            Assert.That(proximity, Is.EqualTo(new[] { near.transform }));
            var party = BossEncounterParticipants.Collect(living, _ => true, Nearby, true);
            Assert.That(party, Is.EqualTo(new[] { near.transform, far.transform }));
            Assert.IsEmpty(BossEncounterParticipants.Collect(living, _ => false, Nearby, true),
                "A failed relay/whole-party arena gate cannot enroll anyone.");
        }
        finally { Object.DestroyImmediate(near); Object.DestroyImmediate(far); }
    }

    [TestCase(typeof(BrokenAnchorBoss))]
    [TestCase(typeof(DeadWeightBoss))]
    [TestCase(typeof(FalseChoirBoss))]
    public void BespokeBossMustExplicitlyOptInBeforeAcceptingNonLegacyBrain(Type bossType)
    {
        root.SetActive(false);
        var boss = (BossEncounterBehaviour)root.AddComponent(bossType);
        Assert.IsFalse(boss.TryBindEncounterActor(actor));
        Assert.IsNull(boss.EncounterActor);
        Assert.IsFalse(boss.IsBossEngaged);
    }

    [Test]
    public void UnavailableAndFlameDormantActorsCannotEngageOrLoseSegments()
    {
        var boss = root.AddComponent<GenericEncounterTestBoss>();
        Assert.IsTrue(boss.TryBindEncounterActor(actor));
        actor.available = false;
        boss.Engage();
        Assert.IsFalse(boss.IsBossEngaged);
        actor.available = true; actor.flameDormant = true;
        boss.Engage();
        Assert.IsFalse(boss.IsBossEngaged);
        actor.flameDormant = false;
        boss.Engage();
        Assert.IsTrue(boss.IsBossEngaged);
        int before = boss.CurrentSegments;
        actor.flameDormant = true;
        boss.RemoveSegment();
        Assert.AreEqual(before, boss.CurrentSegments);
    }

    [Test]
    public void SuspensionDoesNotEraseAuthoredEncounterAvailability()
    {
        actor.SetSuspended(true);
        Assert.IsTrue(EnemyEncounterActors.IsAvailable(actor));
        actor.available = false;
        Assert.IsFalse(EnemyEncounterActors.IsAvailable(actor));
        actor.available = true; actor.dead = true;
        Assert.IsFalse(EnemyEncounterActors.IsAvailable(actor));
    }

    [Test]
    public void EngagedBespokeBossKeepsEngagementWhileOrdinaryBrainIsSuppressed()
    {
        var boss = root.AddComponent<GenericEncounterTestBoss>();
        Assert.IsFalse(EnemyEncounterActors.IsEngagedWith(actor, root.transform));
        boss.Engage();
        actor.SetSuspended(true);
        Assert.IsTrue(boss.SuppressDefaultEnemyBrain);
        Assert.IsTrue(EnemyEncounterActors.IsEngagedWith(actor, root.transform));
        actor.flameDormant = true;
        Assert.IsFalse(EnemyEncounterActors.IsEngagedWith(actor, root.transform));
    }

    [Test]
    public void BossDamageFilterReceivesActualSourceAndCannotBypassClosedGate()
    {
        var boss = root.AddComponent<GenericEncounterTestBoss>();
        boss.Engage();
        var sourceObject = new GameObject("Encounter damage source");
        sourceObject.SetActive(false);
        try
        {
            var source = sourceObject.AddComponent<SquadCharacterController>();
            boss.denyDamage = true;
            Assert.IsFalse(EnemyEncounterActors.TryFilterIncomingDamage(actor, 25, source, out int damage));
            Assert.Zero(damage);
            Assert.AreSame(source, boss.observedSource);
            boss.denyDamage = false;
            Assert.IsTrue(EnemyEncounterActors.TryFilterIncomingDamage(actor, 25, source, out damage));
            Assert.AreEqual(25, damage);
            actor.available = false;
            int calls = boss.filterCalls;
            Assert.IsFalse(EnemyEncounterActors.TryFilterIncomingDamage(actor, 25, source, out damage));
            Assert.Zero(damage);
            Assert.AreEqual(calls, boss.filterCalls);
        }
        finally { Object.DestroyImmediate(sourceObject); }
    }

    [Test]
    public void DormantOrDisabledBossCannotAdmitDamageEvenWithPermissiveFilter()
    {
        var boss = root.AddComponent<GenericEncounterTestBoss>();
        Assert.IsFalse(EnemyEncounterActors.TryFilterIncomingDamage(actor, 25, null, out int damage));
        Assert.Zero(damage);
        Assert.Zero(boss.filterCalls);
        boss.Engage();
        Assert.IsTrue(EnemyEncounterActors.TryFilterIncomingDamage(actor, 25, null, out damage));
        Assert.AreEqual(25, damage);
        int calls = boss.filterCalls;
        boss.enabled = false;
        Assert.IsFalse(EnemyEncounterActors.IsAvailable(actor));
        Assert.IsFalse(EnemyEncounterActors.IsEngagedWith(actor, root.transform));
        Assert.IsFalse(EnemyEncounterActors.TryFilterIncomingDamage(actor, 25, null, out damage));
        Assert.Zero(damage);
        Assert.AreEqual(calls, boss.filterCalls);
    }

    [Test]
    public void BossBoundToAnotherRootCannotAdmitThisActorsDamage()
    {
        var boss = root.AddComponent<GenericEncounterTestBoss>();
        var otherRoot = new GameObject("Mismatched boss binding");
        otherRoot.SetActive(false);
        try
        {
            var other = otherRoot.AddComponent<EnemyController>();
            Assert.IsTrue(boss.TryBindEncounterActor(other));
            Assert.IsFalse(EnemyEncounterActors.IsAvailable(actor));
            Assert.IsFalse(EnemyEncounterActors.TryFilterIncomingDamage(actor, 25, null, out int damage));
            Assert.Zero(damage);
            Assert.Zero(boss.filterCalls);
        }
        finally { Object.DestroyImmediate(otherRoot); }
    }

    [Test]
    public void ActiveEncounterCannotSilentlyChangeActor()
    {
        var boss = root.AddComponent<GenericEncounterTestBoss>();
        Assert.IsTrue(boss.TryBindEncounterActor(actor));
        boss.Engage();
        var other = new GameObject("Other encounter actor");
        try
        {
            var next = other.AddComponent<EncounterActorTestDouble>();
            Assert.IsFalse(boss.TryBindEncounterActor(next));
            Assert.AreSame(actor, boss.EncounterActor);
            Assert.IsTrue(boss.TryBindEncounterActor(actor));
        }
        finally { Object.DestroyImmediate(other); }
    }

    [Test]
    public void AmbiguousTwoActorRootIsRejectedWithoutCreatingAnything()
    {
        root.AddComponent<EncounterActorTestDouble>();
        Assert.IsNull(EnemyEncounterActors.Resolve(actor));
        Assert.AreEqual(2, root.GetComponents<EncounterActorTestDouble>().Length);
        Assert.IsNull(root.GetComponent<EnemyController>());
    }

    [Test]
    public void EndingBossPresentationDoesNotEndOtherWorldThreats()
    {
        var boss = root.AddComponent<GenericEncounterTestBoss>();
        var managerObject = new GameObject("Encounter manager test");
        var playerObject = new GameObject("Encounter player test");
        var otherObject = new GameObject("Other encounter threat test");
        managerObject.SetActive(false);
        try
        {
            var manager = managerObject.AddComponent<RealTimeCombatManager>();
            var other = otherObject.AddComponent<EncounterActorTestDouble>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(RealTimeCombatManager).GetField("combatActive", flags).SetValue(manager, true);
            typeof(RealTimeCombatManager).GetField("playerRoot", flags).SetValue(manager, playerObject.transform);
            typeof(RealTimeCombatManager).GetField("externalTarget", flags).SetValue(manager, actor);
            var threats = (System.Collections.Generic.Dictionary<Transform, ICombatTarget>)typeof(RealTimeCombatManager)
                .GetField("worldThreats", flags).GetValue(manager);
            threats.Add(actor.transform, actor); threats.Add(other.transform, other);
            typeof(BossEncounterBehaviour).GetField("localCombatOpen", flags).SetValue(boss, true);
            typeof(BossEncounterBehaviour).GetField("presentationManager", flags).SetValue(boss, manager);
            typeof(BossEncounterBehaviour).GetField("presentationPlayer", flags).SetValue(boss, playerObject.transform);
            boss.EndPresentation();
            Assert.IsTrue(manager.IsCombatActive);
            Assert.AreEqual(2, manager.ActiveThreatCount);
        }
        finally
        {
            Object.DestroyImmediate(managerObject); Object.DestroyImmediate(playerObject); Object.DestroyImmediate(otherObject);
        }
    }

    [Test]
    public void LegacyScientistGhostAndDeathOptionsRemainBehindExistingGate()
    {
        var legacyObject = new GameObject("Scientist encounter test");
        legacyObject.SetActive(false);
        var data = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            data.enemyEncounterOptions = new EnemyEncounterOptions { startAsGhost = true, introductionSeconds = 2.5f };
            data.enemyDeathOptions = new EnemyDeathOptions { enabled = true, dialogueLine = "Last words", dialogueSeconds = 4f };
            var legacy = legacyObject.AddComponent<EnemyController>();
            legacy.Health.SetCharacterData(data);
            typeof(EnemyController).GetField("requiresFlameInfluence", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(legacy, false);
            IEnemyEncounterActor encounter = legacy;
            Assert.AreSame(legacy, EnemyEncounterActors.Resolve(legacy));
            Assert.IsTrue(legacy.StartsAsGhost);
            Assert.IsFalse(legacy.CombatEnabled);
            Assert.IsFalse(encounter.IsEncounterAvailable);
            var deathPresentation = encounter as IEnemyEncounterDeathPresentation;
            Assert.IsNotNull(deathPresentation);
            Assert.IsTrue(deathPresentation.HasDeathPresentation);
            Assert.AreEqual(legacy.transform, encounter.EncounterTarget.Root);
            Assert.IsFalse(legacy.CombatEnabled, "Resolving the adapter must not open the introduction gate.");
            Assert.IsFalse(deathPresentation.IsDeathPresentationPlaying);
        }
        finally { Object.DestroyImmediate(legacyObject); Object.DestroyImmediate(data); }
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    public void BrainsBridgePreservesIdentityAndRefusesUnportedIntroductionOrLastWords(bool ghost, bool lastWords, bool blocked)
    {
        var brainObject = new GameObject("Brains encounter bridge test");
        brainObject.SetActive(false);
        var data = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            data.hp = 100;
            data.enemyEncounterOptions = new EnemyEncounterOptions { startAsGhost = ghost };
            data.enemyDeathOptions = new EnemyDeathOptions { enabled = lastWords };
            var brain = brainObject.AddComponent<LitBrainsEnemy>();
            brain.ConfigureCharacterData(data);
            IEnemyEncounterActor encounter = brain;
            Assert.AreSame(brain, EnemyEncounterActors.Resolve(brain));
            Assert.AreSame(brain, encounter.EncounterTarget);
            Assert.AreSame(brain.Health, encounter.Health);
            Assert.AreEqual(brain.transform, encounter.EncounterRoot);
            Assert.IsNull(brainObject.GetComponent<EnemyController>());
            Assert.AreEqual(blocked, typeof(LitBrainsEnemy).GetProperty("RequiresLegacyEncounterPresentation",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(brain));
            Assert.IsFalse(encounter.IsEncounterAvailable, "Resolving an inactive actor never activates it.");
        }
        finally { Object.DestroyImmediate(brainObject); Object.DestroyImmediate(data); }
    }

    [Test]
    public void BrainsBridgeRejectsLegacyOnlyBossBeforeTargetingCanUseIt()
    {
        var brainObject = new GameObject("Legacy-only boss bridge test");
        brainObject.SetActive(false);
        try
        {
            var brain = brainObject.AddComponent<LitBrainsEnemy>();
            var boss = brainObject.AddComponent<FalseChoirBoss>();
            Assert.IsFalse(boss.TryBindEncounterActor(brain));
            Assert.IsNull(boss.EncounterActor);
            Assert.IsFalse((bool)typeof(LitBrainsEnemy).GetProperty("HasSupportedEncounterBoss",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(brain));
        }
        finally { Object.DestroyImmediate(brainObject); }
    }
}
