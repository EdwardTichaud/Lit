#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class CombatWorldMigrationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> owned = new List<Object>();
    private RealTimeCombatManager manager;

    [SetUp]
    public void SetUp()
    {
        Assert.That(Application.isPlaying, Is.False, "These fixtures are Edit Mode only.");
        Assert.That(RealTimeCombatManager.Instance, Is.Null, "Do not modify a live combat manager.");
        Assert.That(AudioManager.Instance, Is.Null, "Do not modify a live audio service.");
        Assert.That(Object.FindAnyObjectByType<RealTimeCombatInput>(), Is.Null,
            "Run without an active gameplay input host.");
        Assert.That(CombatProtectionDamage.HasAuthority, Is.True, "Run outside a remote client session.");
        manager = Inactive("Combat world manager fixture").AddComponent<RealTimeCombatManager>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    [TestCase("CombatWorldMigration_Overworld")]
    [TestCase("CombatWorldMigration_Standalone")]
    public void ConfigurationNeedsNoAuthoredSceneOrLaboratoryName(string sceneName)
    {
        var scene = SceneManager.GetActiveScene();
        string previousName = scene.name;
        scene.name = sceneName + "_" + Guid.NewGuid().ToString("N");
        try
        {
            Assert.That(manager.AllowsFreeAttacks, Is.False);
            var profile = new FreeAttackProfile { freeDashDistance = 7f, turnDegreesPerSecond = 720f };
            manager.ConfigureFreeAttacks(profile, null);
            Assert.That(manager.AllowsFreeAttacks, Is.True);
            Assert.That(manager.FreeDashDistance, Is.EqualTo(7f));
            Assert.That(manager.FreeAttackTurnRate, Is.EqualTo(720f));
            Assert.That(manager.PlayerRoot, Is.Null);
            Assert.That(manager.IsCombatActive, Is.False);
            Assert.That(manager.ActiveThreatCount, Is.Zero);
            Assert.That(Read<Camera>(manager, "freeAttackCamera"), Is.Null);

            manager.ConfigureFreeAttacks(new FreeAttackProfile { enabled = false }, null);
            Assert.That(manager.AllowsFreeAttacks, Is.False);
            manager.ConfigureFreeAttacks(null, null);
            Assert.That(manager.AllowsFreeAttacks, Is.False);
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded) scene.name = previousName;
        }
    }

    [Test]
    public void SoloSnapshotFilterNeverRemovesOrdinaryNpcsOrLucian()
    {
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        Assert.That(catalog, Is.Not.Null);
        var predicate = typeof(SpawnManager).GetMethod("IsSoloSupportCharacter", BindingFlags.Static | BindingFlags.NonPublic);
        bool IsSupport(CharacterData data) => (bool)predicate.Invoke(null, new object[] { data });
        Assert.That(IsSupport(catalog.GetCharacter(CombatSkillKit.Lucian)), Is.False);
        foreach (var kit in new[] { CombatSkillKit.Vesta, CombatSkillKit.Soren, CombatSkillKit.Aurell })
            Assert.That(IsSupport(catalog.GetCharacter(kit)), Is.True, kit.ToString());
        var npc = Own(ScriptableObject.CreateInstance<CharacterData>());
        npc.characterName = "World NPC";
        Assert.That(IsSupport(npc), Is.False);
        npc.isEnemy = true;
        Assert.That(IsSupport(npc), Is.False);
    }

    [Test]
    public void ActorSwapReplacesPopulatedServicesAndClearsMissingReferences()
    {
        var first = Actor("First actor");
        var health = first.AddComponent<CharacterInfo>();
        health.SetHealth(10, 10);
        var loadout = first.AddComponent<RealTimeCombatLoadout>();
        var controller = first.AddComponent<SquadCharacterController>();
        var animator = first.GetComponent<Animator>();
        var animation = first.AddComponent<PlayerAnimationController>();
        first.AddComponent<PlayerRootMotionRelay>();
        var animatorController = Own(new UnityEditor.Animations.AnimatorController());
        animator.runtimeAnimatorController = animatorController;
        animation.Configure(first.transform, animator, first.transform);
        Assert.That(animation.ValidateContract(out string error), Is.True, error);

        manager.RegisterControlledActor(first.transform);
        Assert.That(manager.PlayerLoadout, Is.SameAs(loadout));
        Assert.That(manager.PlayerAnimator, Is.SameAs(animator));
        Assert.That(Read<CharacterInfo>(manager, "playerHealth"), Is.SameAs(health));
        Assert.That(Read<SquadCharacterController>(manager, "playerController"), Is.SameAs(controller));
        Assert.That(Read<LitOpsiveLocomotionBridge>(manager, "playerLocomotionBridge"),
            Is.SameAs(first.GetComponent<LitOpsiveLocomotionBridge>()));

        var second = Actor("Actor without optional services");
        manager.RegisterControlledActor(second.transform);
        Assert.That(manager.PlayerRoot, Is.SameAs(second.transform));
        Assert.That(manager.PlayerLoadout, Is.Null);
        Assert.That(manager.PlayerAnimator, Is.Null);
        Assert.That(Read<CharacterInfo>(manager, "playerHealth"), Is.Null);
        Assert.That(Read<SquadCharacterController>(manager, "playerController"), Is.Null);
        Assert.That(Read<LitOpsiveLocomotionBridge>(manager, "playerLocomotionBridge"), Is.Null);
        Assert.That(Read<PlayerActionPresentationController>(manager, "playerActionPresentation"),
            Is.SameAs(second.GetComponent<PlayerActionPresentationController>()));
    }

    [Test]
    public void RebindingSameActorPreservesThreatsAndCurrentPresentation()
    {
        var actor = BindActor();
        var target = Target("Existing threat");
        Assert.That(Engage(actor, target), Is.True);
        var presentation = actor.GetComponent<PlayerActionPresentationController>();
        int generation = presentation.BeginExternalPresentation(manager);
        int ended = 0;
        Assert.That(presentation.RegisterActionCleanup(generation, () => ended++), Is.True);

        manager.RegisterControlledActor(actor.transform);

        Assert.That(manager.IsCombatActive, Is.True);
        Assert.That(manager.ActiveThreatCount, Is.EqualTo(1));
        Assert.That(presentation.IsCurrentSession(generation), Is.True);
        Assert.That(ended, Is.Zero);
    }

    [Test]
    public void ActorSwapEndsOldEngagementWithoutCarryingThreatsToNewActor()
    {
        var first = BindActor();
        Assert.That(Engage(first, Target("Old actor threat")), Is.True);
        var second = Actor("Replacement actor");

        manager.RegisterControlledActor(second.transform);

        Assert.That(manager.PlayerRoot, Is.SameAs(second.transform));
        Assert.That(manager.IsCombatActive, Is.False);
        Assert.That(manager.ActiveThreatCount, Is.Zero);
        Assert.That(manager.CombatTarget, Is.Null);
        AssertNoLock();
    }

    [Test]
    public void MultipleThreatsRemainOneContinuousEncounterWithoutImplicitLock()
    {
        var actor = BindActor();
        var first = Target("First threat");
        var second = Target("Second threat");
        var transitions = new List<bool>();
        int aggroEntries = 0;
        manager.CombatStateChanged += transitions.Add;
        manager.EncounterAggroStarted += _ => aggroEntries++;

        Assert.That(Engage(actor, first), Is.True);
        Assert.That(Engage(actor, second), Is.True);
        Assert.That(Engage(actor, first), Is.True, "Duplicate perception must be idempotent by target root.");
        Assert.That(manager.ActiveThreatCount, Is.EqualTo(2));
        Assert.That(transitions, Is.EqualTo(new[] { true }));
        Assert.That(aggroEntries, Is.EqualTo(1));
        AssertNoLock();

        first.IsDead = true;
        manager.RefreshWorldEngagements();
        Assert.That(manager.ActiveThreatCount, Is.EqualTo(1));
        Assert.That(manager.IsCombatActive, Is.True);
        Assert.That(manager.CombatTarget, Is.SameAs(second));
        Assert.That(transitions, Is.EqualTo(new[] { true }));
        AssertNoLock();

        manager.RefreshWorldEngagements();
        Assert.That(manager.ActiveThreatCount, Is.EqualTo(1));
        second.Root.gameObject.SetActive(false);
        manager.RefreshWorldEngagements();
        Assert.That(manager.ActiveThreatCount, Is.Zero);
        Assert.That(manager.IsCombatActive, Is.False);
        Assert.That(transitions, Is.EqualTo(new[] { true, false }));
        AssertNoLock();
    }

    [Test]
    public void PlayerContactAddsAnotherThreatWithoutReplacingItsCurrentAction()
    {
        var actor = BindActor();
        Assert.That(Engage(actor, Target("Perception threat")), Is.True);
        var presentation = actor.GetComponent<PlayerActionPresentationController>();
        int generation = presentation.BeginExternalPresentation(manager);
        var skill = Own(ScriptableObject.CreateInstance<SkillSO>());
        skill.targetingMode = SkillTargetingMode.FreeAttack;
        var context = new PlayerAttackContext(generation, skill, Vector3.zero, Vector3.forward, null, false);
        Set(presentation, "attackContext", context);
        var victim = Target("Contact threat");
        Assert.That(manager.TryEngageFromPlayerContact(actor.transform, victim, context), Is.False,
            "An unconsumed contact must not register a threat.");
        Assert.That(context.TryConsume(generation), Is.True);

        Assert.That(manager.TryEngageFromPlayerContact(actor.transform, victim, context), Is.True);
        Assert.That(manager.ActiveThreatCount, Is.EqualTo(2));
        Assert.That(presentation.IsCurrentSession(generation), Is.True);
        Assert.That(manager.CurrentAttackContext, Is.SameAs(context));
        AssertNoLock();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void LastThreatLossDoesNotEndAnActiveExternalPresentation(bool targetDied)
    {
        var actor = BindActor();
        var target = Target("Last threat");
        Assert.That(Engage(actor, target), Is.True);
        var presentation = actor.GetComponent<PlayerActionPresentationController>();
        int generation = presentation.BeginExternalPresentation(manager);
        int ended = 0;
        Assert.That(presentation.RegisterActionCleanup(generation, () => ended++), Is.True);
        if (targetDied) target.IsDead = true;
        else target.Root.gameObject.SetActive(false);

        manager.RefreshWorldEngagements();
        manager.RefreshWorldEngagements();

        Assert.That(manager.ActiveThreatCount, Is.Zero);
        Assert.That(manager.IsCombatActive, Is.False);
        Assert.That(manager.CombatTarget, Is.Null);
        Assert.That(presentation.IsCurrentSession(generation), Is.True,
            "Passive last-threat loss must not use the unconditional EndCombat teardown.");
        Assert.That(ended, Is.Zero);
        AssertNoLock();
        presentation.EndExternalPresentation(generation);
        Assert.That(presentation.IsCurrentSession(generation), Is.False);
        Assert.That(ended, Is.EqualTo(1), "The original owner must still be able to finish its action once.");
    }

    [Test]
    public void ExplicitEndCombatStillCancelsExternalPresentation()
    {
        var actor = BindActor();
        Assert.That(Engage(actor, Target("Explicit teardown threat")), Is.True);
        var presentation = actor.GetComponent<PlayerActionPresentationController>();
        int generation = presentation.BeginExternalPresentation(manager);
        int ended = 0;
        Assert.That(presentation.RegisterActionCleanup(generation, () => ended++), Is.True);

        manager.EndCombat();

        Assert.That(manager.IsCombatActive, Is.False);
        Assert.That(manager.ActiveThreatCount, Is.Zero);
        Assert.That(presentation.IsCurrentSession(generation), Is.False);
        Assert.That(ended, Is.EqualTo(1));
    }

    [Test]
    public void EmptyConfirmedSnapshotRetriesExitAfterCinematicWithoutCancellingTheAction()
    {
        var actor = BindActor();
        Assert.That(Engage(actor, Target("Confirmed threat")), Is.True);
        var presentation = actor.GetComponent<PlayerActionPresentationController>();
        int generation = presentation.BeginExternalPresentation(manager);
        var transitions = new List<bool>();
        manager.CombatStateChanged += transitions.Add;
        manager.SetCinematicSequenceActive(true);

        manager.ApplyConfirmedWorldThreats(Array.Empty<ICombatTarget>());
        Assert.That(manager.ActiveThreatCount, Is.Zero);
        Assert.That(manager.IsCombatActive, Is.True);
        Assert.That(Read<bool>(manager, "confirmedWorldExitPending"), Is.True);

        manager.SetCinematicSequenceActive(false);
        // Exercise the exact pre-authority cleanup used by remote clients without
        // pretending this Edit Mode fixture is a live transport session.
        typeof(RealTimeCombatManager).GetMethod("TryFinishConfirmedWorldExit", Private).Invoke(manager, null);
        manager.RefreshWorldEngagements();

        Assert.That(manager.IsCombatActive, Is.False);
        Assert.That(Read<bool>(manager, "confirmedWorldExitPending"), Is.False);
        Assert.That(transitions, Is.EqualTo(new[] { false }));
        Assert.That(presentation.IsCurrentSession(generation), Is.True);
        AssertNoLock();
    }

    [Test]
    public void NewConfirmedThreatCancelsTheDeferredExit()
    {
        var actor = BindActor();
        Assert.That(Engage(actor, Target("Old threat")), Is.True);
        manager.SetCinematicSequenceActive(true);
        manager.ApplyConfirmedWorldThreats(Array.Empty<ICombatTarget>());
        var next = Target("New threat");

        manager.ApplyConfirmedWorldThreats(new[] { next });
        manager.SetCinematicSequenceActive(false);
        manager.RefreshWorldEngagements();

        Assert.That(manager.IsCombatActive, Is.True);
        Assert.That(manager.ActiveThreatCount, Is.EqualTo(1));
        Assert.That(manager.CombatTarget, Is.SameAs(next));
        Assert.That(Read<bool>(manager, "confirmedWorldExitPending"), Is.False);
    }

    [Test]
    public void DeadManualLockClearsWithoutDroppingAnotherConfirmedThreat()
    {
        var actor = BindActor();
        var locked = Target("Dead lock");
        var other = Target("Remaining confirmed threat");
        Assert.That(Engage(actor, locked), Is.True);
        manager.ApplyConfirmedWorldThreats(new[] { other });
        Set(manager, "worldLockedTarget", locked);
        locked.IsDead = true;

        typeof(RealTimeCombatManager).GetMethod("RefreshWorldLockLiveness", Private).Invoke(manager, null);

        AssertNoLock();
        Assert.That(manager.IsCombatActive, Is.True);
        Assert.That(manager.ActiveThreatCount, Is.EqualTo(1));
        Assert.That(manager.CombatTarget, Is.SameAs(other));
    }

    [Test]
    public void ActorSwapDoesNotCarryADeferredConfirmedExit()
    {
        var actor = BindActor();
        Assert.That(Engage(actor, Target("Previous actor threat")), Is.True);
        manager.SetCinematicSequenceActive(true);
        manager.ApplyConfirmedWorldThreats(Array.Empty<ICombatTarget>());

        manager.RegisterControlledActor(Actor("Replacement actor").transform);

        Assert.That(manager.IsCombatActive, Is.False);
        Assert.That(Read<bool>(manager, "confirmedWorldExitPending"), Is.False);
    }

    private GameObject BindActor()
    {
        manager.ConfigureFreeAttacks(new FreeAttackProfile(), null);
        var actor = Actor("Controlled actor");
        manager.RegisterControlledActor(actor.transform);
        return actor;
    }

    private bool Engage(GameObject actor, ICombatTarget target)
    {
        // Skip unrelated AudioManager bootstrapping; no real audio token is owned in this empty fixture.
        Set(manager, "combatMusicOverrideToken", int.MaxValue);
        return manager.TryEngageFromEnemyPerception(actor.transform, target);
    }

    private void AssertNoLock()
    {
        Assert.That(manager.HasLockedCombatTarget, Is.False);
        Assert.That(manager.LockedCombatTarget, Is.Null);
        Assert.That(manager.LockedTargetPoint, Is.Null);
        Assert.That(manager.LockedEnemy, Is.Null);
    }

    private GameObject Actor(string name)
    {
        var actor = Inactive(name);
        actor.AddComponent<PlayerActionPresentationController>();
        return actor;
    }

    private GameObject Inactive(string name)
    {
        var root = Own(new GameObject(name));
        root.SetActive(false);
        return root;
    }

    private FakeTarget Target(string name) => new FakeTarget(Own(new GameObject(name)).transform);
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static T Read<T>(object owner, string field) => (T)typeof(RealTimeCombatManager).GetField(field, Private).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);

    private sealed class FakeTarget : ICombatTarget
    {
        public Transform Root { get; }
        public Transform LockPoint => Root;
        public Animator Animator => null;
        public bool IsDead { get; set; }
        public bool IsAttackCommitted => false;
        public int ActionSequenceId => 1;
        public FakeTarget(Transform root) { Root = root; }
        public int ReceiveDamage(int damage) => damage;
        public void SetCinematicSuspended(bool suspended) { }
        public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => false;
    }
}
#endif
