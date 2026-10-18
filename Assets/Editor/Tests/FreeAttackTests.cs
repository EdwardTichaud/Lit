#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class FreeAttackTests
{
    private static Transform fixtureRoot;
    private static ICombatTarget Query(RealTimeCombatManager manager, SkillSO skill, bool contact = false) =>
        (ICombatTarget)typeof(RealTimeCombatManager).GetMethod("FindFreeAttackTarget", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(manager, new object[] { skill, Vector3.forward, contact, null });
    [TestCase(0, true)] [TestCase(24.9f, true)] [TestCase(25.1f, false)]
    [TestCase(30, false)] [TestCase(90, false)] [TestCase(180, false)]
    public void AssistanceNeverOverridesIntentBeyondMaximumCorrection(float angle, bool accepted) =>
        Assert.That(FreeAttackProfile.InCone(Vector3.forward, Quaternion.Euler(0, angle, 0) * Vector3.forward, 60, 25), Is.EqualTo(accepted));

    [TestCase(0, 1)] [TestCase(0, -1)] [TestCase(1, 0)] [TestCase(-1, 0)]
    [TestCase(1, 1)] [TestCase(-1, 1)] [TestCase(1, -1)] [TestCase(-1, -1)]
    public void EightDirectionsFollowCameraNotEnemy(int x, int y)
    {
        var camera = Quaternion.Euler(0, 90, 0);
        var expected = camera * new Vector3(x, 0, y).normalized;
        Assert.That(Vector3.Distance(FreeAttackProfile.ResolveDirection(new Vector2(x, y), camera * Vector3.forward, Vector3.back), expected), Is.LessThan(.0001f));
    }

    [Test]
    public void NoInputUsesFacingAndVerticalCameraRemainsStable()
    {
        Assert.That(FreeAttackProfile.ResolveDirection(Vector2.zero, Vector3.forward, Vector3.left), Is.EqualTo(Vector3.left));
        Assert.That(FreeAttackProfile.ResolveDirection(Vector2.up, Vector3.up, Vector3.left), Is.EqualTo(Vector3.left));
    }

    [Test]
    public void ContextFreezesDirectionAndPositionAndConsumesOnlyItsGeneration()
    {
        var go = new GameObject("Captured enemy");
        try
        {
            go.transform.position = Vector3.forward * 2;
            var target = go.AddComponent<LitBrainsEnemy>();
            var context = new PlayerAttackContext(12, null, Vector3.zero, Vector3.forward, target, false);
            go.transform.position = Vector3.right * 5;
            Assert.That(context.TargetOrigin, Is.EqualTo(Vector3.forward * 2));
            Assert.That(context.Direction, Is.EqualTo(Vector3.forward));
            Assert.That(context.TryConsume(11), Is.False);
            Assert.That(context.TryConsume(12), Is.True);
            Assert.That(context.TryConsume(12), Is.False);
            Object.DestroyImmediate(go);
            Assert.That(context.TargetAlive, Is.False);
            Assert.That(context.TargetRoot, Is.Null);
        }
        finally { if (go != null) Object.DestroyImmediate(go); }
    }

    [Test]
    public void ContractsAreExplicitAndNeverInferredFromMovement()
    {
        var basic = ScriptableObject.CreateInstance<BasicSkillsSO>();
        var dash = ScriptableObject.CreateInstance<SkillSO>();
        var targeted = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            var profile = new FreeAttackProfile();
            dash.targetLunge.enabled = dash.targetLunge.resolveImpactAtContact = true;
            Assert.That(SkillTargetingContract.Validate(basic, out _), Is.False);
            Assert.That(SkillTargetingContract.Validate(dash, out _), Is.False);
            Assert.That(SkillTargetingContract.Validate(targeted, out _), Is.False);
            basic.targetingMode = SkillTargetingMode.FreeAttack;
            dash.targetingMode = SkillTargetingMode.OptionalTarget;
            targeted.targetingMode = SkillTargetingMode.RequiredTarget;
            Assert.That(SkillTargetingContract.Validate(basic, out _), Is.True);
            Assert.That(SkillTargetingContract.Validate(dash, out _), Is.True);
            Assert.That(SkillTargetingContract.Validate(targeted, out _), Is.True);
            Assert.That(profile.freeDashDistance, Is.EqualTo(10));
            Assert.That(typeof(FreeAttackProfile).GetField("skills"), Is.Null);
        }
        finally { Object.DestroyImmediate(basic); Object.DestroyImmediate(dash); Object.DestroyImmediate(targeted); }
    }

    [Test]
    public void AcquisitionUsesAngleThenDistanceAndRejectsWallsAndOutOfRange()
    {
        WithWorld((manager, player, skill) =>
        {
            var centered = Enemy("Centered", new Vector3(0, 0, 2));
            var nearer = Enemy("Near off axis", new Vector3(.35f, 0, 1.4f));
            Physics.SyncTransforms();
            Assert.That(Query(manager, skill)?.Root, Is.EqualTo(centered.transform));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(fixtureRoot);
            wall.transform.position = new Vector3(0, 1, 1);
            wall.transform.localScale = new Vector3(4, 2, .1f);
            Physics.SyncTransforms();
            Assert.That(Query(manager, skill), Is.Null);
            Object.DestroyImmediate(wall);
            centered.transform.position = Vector3.forward * 8;
            nearer.transform.position = Vector3.back;
            Physics.SyncTransforms();
            Assert.That(Query(manager, skill), Is.Null);
        });
    }

    [Test]
    public void LaunchRangeIsDistinctFromContactRangeAndBoundsAreInclusive()
    {
        WithWorld((manager, player, skill) =>
        {
            skill.useLaunchRange = true; skill.minimumLaunchDistance = 2; skill.maximumLaunchDistance = 30;
            var enemy = Enemy("Distant target", Vector3.forward * 30);
            Physics.SyncTransforms();
            Assert.That(Query(manager, skill)?.Root, Is.EqualTo(enemy.transform));
            Assert.That(Query(manager, skill, true), Is.Null);
            enemy.transform.position = Vector3.forward * 30.01f; Physics.SyncTransforms();
            Assert.That(Query(manager, skill), Is.Null);
            enemy.transform.position = Vector3.forward * 2; Physics.SyncTransforms();
            Assert.That(Query(manager, skill)?.Root, Is.EqualTo(enemy.transform));
            enemy.transform.position = Vector3.forward * 1.99f; Physics.SyncTransforms();
            Assert.That(Query(manager, skill), Is.Null);
        });
    }

    [Test]
    public void ContactVolumeDoesNotGuaranteeTheAssistedTargetIsHit()
    {
        WithWorld((manager, player, skill) =>
        {
            var enemy = Enemy("Target", Vector3.forward * 2);
            Physics.SyncTransforms();
            Assert.That(Query(manager, skill, true)?.Root, Is.EqualTo(enemy.transform));
            enemy.transform.position = new Vector3(2, 0, 0); Physics.SyncTransforms();
            Assert.That(Query(manager, skill, true), Is.Null);
        });
    }

    [Test]
    public void SessionConfigurationDoesNotDependOnSceneName()
    {
        WithWorld((manager, player, skill) =>
        {
            manager.ConfigureFreeAttacks(null, null);
            Assert.That(manager.AllowsFreeAttacks, Is.False);
            manager.ConfigureFreeAttacks(new FreeAttackProfile(), null);
            Assert.That(manager.AllowsFreeAttacks, Is.True);
            manager.ConfigureFreeAttacks(new FreeAttackProfile { enabled = false }, null);
            Assert.That(manager.AllowsFreeAttacks, Is.False);
        });
    }

    private static bool Resolve(RealTimeCombatManager manager, SkillSO skill, out PlayerAttackContext context, out string reason)
    {
        object[] args = { skill, 19, null, null };
        bool result = (bool)typeof(RealTimeCombatManager).GetMethod("ResolveSkillTargeting", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, args);
        context = (PlayerAttackContext)args[2]; reason = (string)args[3]; return result;
    }

    [Test]
    public void RequiredEnemyAcquiresWithoutChangingManualLock()
    {
        WithWorld((manager, player, basic) =>
        {
            var skill = ScriptableObject.CreateInstance<SkillSO>();
            try
            {
                skill.targetingMode = SkillTargetingMode.RequiredTarget;
                var enemy = Enemy("Temporary victim", Vector3.forward);
                Physics.SyncTransforms();
                Assert.That(Resolve(manager, skill, out var context, out var reason), Is.True, reason);
                Assert.That(context.TargetRoot, Is.EqualTo(enemy.transform));
                Assert.That(manager.LockedCombatTarget, Is.Null);
                Object.DestroyImmediate(enemy);
                Assert.That(context.TargetAlive, Is.False);
            }
            finally { Object.DestroyImmediate(skill); }
        });
    }

    [Test]
    public void MissingRequiredTargetRejectsButFreeAttackRemainsAvailable()
    {
        WithWorld((manager, player, basic) =>
        {
            Assert.That(Resolve(manager, basic, out var free, out _), Is.True);
            Assert.That(free.Target, Is.Null);
            var skill = ScriptableObject.CreateInstance<SkillSO>();
            try
            {
                skill.targetingMode = SkillTargetingMode.RequiredTarget;
                Assert.That(Resolve(manager, skill, out var rejected, out var reason), Is.False);
                Assert.That(rejected, Is.Null); Assert.That(reason, Is.Not.Empty);
                skill.targetingMode = SkillTargetingMode.OptionalTarget;
                skill.targetLunge.enabled = skill.targetLunge.resolveImpactAtContact = true;
                Assert.That(Resolve(manager, skill, out var dash, out _), Is.True);
                Assert.That(dash.Target, Is.Null);
                skill.targetingMode = SkillTargetingMode.Unconfigured;
                Assert.That(Resolve(manager, skill, out _, out _), Is.False);
            }
            finally { Object.DestroyImmediate(skill); }
        });
    }

    [Test]
    public void RebindingGenerationPreservesCapturedTargetAndZone()
    {
        WithWorld((manager, player, basic) =>
        {
            var enemy = Enemy("Captured", Vector3.forward);
            var first = new PlayerAttackContext(1, basic, Vector3.zero, Vector3.forward,
                enemy.GetComponent<LitBrainsEnemy>(), false, null, Vector3.right * 4);
            enemy.transform.position = Vector3.back * 8;
            var next = first.WithGeneration(2);
            Assert.That(next.TargetOrigin, Is.EqualTo(Vector3.forward));
            Assert.That(next.ZoneOrigin, Is.EqualTo(Vector3.right * 4));
            Assert.That(next.TryConsume(1), Is.False);
            Assert.That(next.TryConsume(2), Is.True);
            Assert.That(next.TryConsume(2), Is.False);
        });
    }

    [Test]
    public void InvalidManualLockNeverSubstitutesAnotherEnemy()
    {
        WithWorld((manager, player, basic) =>
        {
            var skill = ScriptableObject.CreateInstance<SkillSO>();
            try
            {
                skill.targetingMode = SkillTargetingMode.RequiredTarget;
                var selected = Enemy("Selected outside range", Vector3.forward * 20).GetComponent<LitBrainsEnemy>();
                Enemy("Available but not selected", Vector3.forward);
                typeof(RealTimeCombatManager).GetField("externalTarget", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, selected);
                typeof(RealTimeCombatManager).GetField("externalTargetLocked", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, true);
                Physics.SyncTransforms();
                Assert.That(Resolve(manager, skill, out var context, out var reason), Is.False);
                Assert.That(context, Is.Null); Assert.That(reason, Does.Contain("portee"));
            }
            finally { Object.DestroyImmediate(skill); }
        });
    }

    [TestCase(SkillTargetingMode.FreeAttack, true)]
    [TestCase(SkillTargetingMode.RequiredTarget, false)]
    [TestCase(SkillTargetingMode.SelectedAlly, false)]
    public void BasicSkillsCannotDeclareIncompatibleExecutors(SkillTargetingMode mode, bool valid)
    {
        var skill = ScriptableObject.CreateInstance<BasicSkillsSO>();
        try { skill.targetingMode = mode; Assert.That(SkillTargetingContract.Validate(skill, out _), Is.EqualTo(valid)); }
        finally { Object.DestroyImmediate(skill); }
    }

    [TestCase(SkillHealingTarget.Self, SkillTargetingMode.Self)]
    [TestCase(SkillHealingTarget.PlayersInRadius, SkillTargetingMode.AllyArea)]
    [TestCase(SkillHealingTarget.AllyRegeneration, SkillTargetingMode.SelectedAlly)]
    [TestCase(SkillHealingTarget.EnemyLifeSteal, SkillTargetingMode.RequiredTarget)]
    public void HealingContractsMatchOnlyTheirDedicatedBeneficiaries(SkillHealingTarget healing, SkillTargetingMode expected)
    {
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            skill.healingTarget = healing; skill.healingPercent = 20;
            skill.targetingMode = expected;
            Assert.That(SkillTargetingContract.Validate(skill, out _), Is.True);
            skill.targetingMode = SkillTargetingMode.FreeAttack;
            Assert.That(SkillTargetingContract.Validate(skill, out _), Is.False);
        }
        finally { Object.DestroyImmediate(skill); }
    }

    [Test]
    public void SpecializedRangesKeepTheirExistingSourceOfTruth()
    {
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            skill.useLaunchRange = true; skill.minimumLaunchDistance = 2; skill.maximumLaunchDistance = 30;
            SkillTargetingContract.EnemyRange(skill, out var min, out var max);
            Assert.That(min, Is.EqualTo(2)); Assert.That(max, Is.EqualTo(30));
            skill.healingTarget = SkillHealingTarget.EnemyLifeSteal; skill.healingRadius = 10;
            SkillTargetingContract.EnemyRange(skill, out min, out max);
            Assert.That(min, Is.Zero); Assert.That(max, Is.EqualTo(10));
        }
        finally { Object.DestroyImmediate(skill); }
    }

    [TestCase(0, 2, true)] [TestCase(2, 0, false)] [TestCase(0, -2, false)]
    [TestCase(0, 3, false)] [TestCase(.7f, 2, true)]
    public void SpatialSelectionRejectsSidesBackAndOutOfReach(float x, float z, bool accepted)
    {
        WithWorld((manager, player, skill) =>
        {
            Enemy("Spatial candidate", new Vector3(x, 0, z)); Physics.SyncTransforms();
            Assert.That(Query(manager, skill) != null, Is.EqualTo(accepted));
        });
    }

    [Test]
    public void EqualCandidatesHaveStableTieBreakAndCapturedVictimWinsRealContact()
    {
        WithWorld((manager, player, skill) =>
        {
            var a = Enemy("A", new Vector3(-.2f, 0, 1.8f));
            var b = Enemy("B", new Vector3(.2f, 0, 1.8f));
            Physics.SyncTransforms();
            var selected = Query(manager, skill);
            for (int i = 0; i < 12; i++)
            { Physics.SyncTransforms(); Assert.That(Query(manager, skill).Root, Is.EqualTo(selected.Root)); }
            var context = new PlayerAttackContext(3, skill, player.position, Vector3.forward, selected, false);
            var other = selected.Root == a.transform ? b : a;
            other.transform.position = Vector3.forward * 1.2f; Physics.SyncTransforms();
            var contact = (ICombatTarget)typeof(RealTimeCombatManager).GetMethod("FindFreeAttackTarget", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(manager, new object[] { skill, Vector3.forward, true, context.Target });
            Assert.That(contact.Root, Is.EqualTo(selected.Root));
            Assert.That(context.TargetRoot, Is.EqualTo(selected.Root));
            selected.Root.position = Vector3.right * 6; Physics.SyncTransforms();
            Assert.That(context.TargetRoot, Is.EqualTo(selected.Root), "Moving a victim never rebinds the context");
        });
    }

    [TestCase(1.5f, true)] [TestCase(1.81f, false)] [TestCase(-1.81f, false)]
    public void AirborneSelectionUsesLocalHeightTolerance(float height, bool accepted)
    {
        WithWorld((manager, player, skill) =>
        {
            player.position = Vector3.up * 3;
            Enemy("Air target", new Vector3(0, 3 + height, 1)); Physics.SyncTransforms();
            Assert.That(Query(manager, skill) != null, Is.EqualTo(accepted));
        });
    }

    [Test]
    public void DeadCandidateCannotStealAcquisition()
    {
        WithWorld((manager, player, skill) =>
        {
            var dead = Enemy("Dead in front", Vector3.forward);
            var live = Enemy("Living", new Vector3(.2f, 0, 1.8f));
            dead.GetComponent<LitBrainsEnemy>().Health.ForceDefeat();
            Physics.SyncTransforms();
            Assert.That(Query(manager, skill).Root, Is.EqualTo(live.transform));
        });
    }

    [TestCase(false, true)] [TestCase(true, false)]
    public void AbsorbedZeroDamageIsDistinctFromRejectedContact(bool disabled, bool accepted)
    {
        var host = new GameObject("Legacy contact fixture"); host.SetActive(false);
        try
        {
            var enemy = host.AddComponent<EnemyController>();
            host.SetActive(true);
            typeof(EnemyController).GetField("requiresFlameInfluence", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(enemy, false);
            enemy.CombatEnabled = !disabled;
            Assert.That(enemy.ReceivePlayerContactDamage(0, null, out var actual), Is.Zero);
            Assert.That(actual, Is.EqualTo(accepted));
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static GameObject Enemy(string name, Vector3 position)
    {
        var go = new GameObject(name);
        if (fixtureRoot != null) go.transform.SetParent(fixtureRoot);
        go.transform.position = position;
        go.AddComponent<LitBrainsEnemy>().ConfigureCharacterData(null);
        var capsule = go.AddComponent<CapsuleCollider>(); capsule.center = Vector3.up; capsule.height = 2; capsule.radius = .35f;
        return go;
    }

    private static void WithWorld(System.Action<RealTimeCombatManager, Transform, SkillSO> test)
    {
        var world = new GameObject("Free attack fixture");
        fixtureRoot = world.transform;
        var skill = ScriptableObject.CreateInstance<BasicSkillsSO>();
        skill.targetingMode = SkillTargetingMode.FreeAttack;
        try
        {
            var root = new GameObject("Manager fixture"); root.SetActive(false);
            root.transform.SetParent(fixtureRoot);
            var manager = root.AddComponent<RealTimeCombatManager>();
            var player = new GameObject("Player fixture");
            player.transform.SetParent(fixtureRoot);
            typeof(RealTimeCombatManager).GetField("playerRoot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, player.transform);
            typeof(RealTimeCombatManager).GetField("freeAttackProfile", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, new FreeAttackProfile());
            test(manager, player.transform, skill);
        }
        finally
        {
            Object.DestroyImmediate(skill);
            Object.DestroyImmediate(world); fixtureRoot = null;
        }
    }
}
#endif
