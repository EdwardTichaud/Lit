using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyAggroRegressionTests
{
    [Test]
    public void EnemyImpactUsesSingleSkillEventReceiver()
    {
        var receiver = typeof(RealTimeCombatAnimationEvents);
        Assert.That(receiver.GetMethod("EnemyAttack", new[] { typeof(SkillSO) }), Is.Not.Null);
        foreach (string obsolete in new[] { "OpenEnemyAttackHitbox", "CloseEnemyAttackHitbox",
                     "HitPlayer", "HitPlayerIf", "ResolveThresholdFailureImpact",
                     "InstantiateEnemySkillVFX", "InstantiateEnemySkillVFXAtIndex" })
            Assert.That(receiver.GetMethod(obsolete), Is.Null, obsolete);
    }

    [Test]
    public void AssomoirHasOneBoundInstantImpactAtAuthorRequestedTime()
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillSO>(AssetDatabase.GUIDToAssetPath(
            "a182c503e933adb42ad6608ded0f8702"));
        Assert.That(skill, Is.Not.Null);
        int impacts = 0;
        foreach (var evt in AnimationUtility.GetAnimationEvents(skill.AnimationClip))
        {
            if (evt.functionName != "EnemyAttack") continue;
            impacts++;
            Assert.That(evt.time, Is.EqualTo(1.7f).Within(.0001f));
            Assert.That(evt.objectReferenceParameter, Is.EqualTo(skill));
        }
        Assert.That(impacts, Is.EqualTo(1));
    }

    [TestCase(3, 10, 3)]
    [TestCase(10, 4, 4)]
    [TestCase(10, 0, 0)]
    [TestCase(0, 4, 0)]
    public void PlayerDamagePopupShowsOnlyActualHealthLoss(int health, int damage, int expected)
    {
        var root = new GameObject("Damage feedback fixture");
        root.SetActive(false);
        try
        {
            var manager = root.AddComponent<RealTimeCombatManager>();
            var hp = root.AddComponent<CombatHealth>();
            hp.SetHealth(health, 10);
            WriteReaction(manager, "playerRoot", root.transform);
            WriteReaction(manager, "playerHealth", hp);
            var apply = typeof(RealTimeCombatManager).GetMethod("ApplyPlayerDamage", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply.Invoke(manager, new object[] { damage }), Is.EqualTo(expected));
            var popups = root.GetComponentsInChildren<CombatDamageWorldFeedback>(true);
            Assert.That(popups.Length, Is.EqualTo(expected > 0 ? 1 : 0));
            if (expected > 0)
                Assert.That(popups[0].GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text, Is.EqualTo("-" + expected));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void MissingPlayerHealthCannotProduceFictitiousDamageOrPopup()
    {
        var root = new GameObject("Missing health fixture");
        root.SetActive(false);
        try
        {
            var manager = root.AddComponent<RealTimeCombatManager>();
            WriteReaction(manager, "playerRoot", root.transform);
            var apply = typeof(RealTimeCombatManager).GetMethod("ApplyPlayerDamage", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply.Invoke(manager, new object[] { 10 }), Is.EqualTo(0));
            Assert.That(root.GetComponentsInChildren<CombatDamageWorldFeedback>(true), Is.Empty);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(EnemyAttackReaction.Dodge, .45, true)]
    [TestCase(EnemyAttackReaction.Dodge, .5, false)]
    [TestCase(EnemyAttackReaction.Counter, .15, true)]
    [TestCase(EnemyAttackReaction.Counter, .2, false)]
    [TestCase(EnemyAttackReaction.Counter, .45, false)]
    public void InvisibleReactionUsesIndependentRealtimeWindows(EnemyAttackReaction reaction, double elapsed, bool expected)
    {
        WithReactionFixture((controller, enemy, player) =>
        {
            double opened = ReadReaction<double>(controller, "attackReactionOpenedAt");
            var eligible = typeof(CombatHealthThresholdController).GetMethod("IsReactionPressEligible",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(eligible.Invoke(controller, new object[] { reaction, opened + elapsed }), Is.EqualTo(expected));
            Assert.That(ReadReaction<bool>(controller, "qteOpen"), Is.False);
            Assert.That(ReadReaction<QTEPanelController>(controller, "qtePanel"), Is.Null);
        });
    }

    [Test]
    public void InvisibleReactionIgnoresDuplicatesAndRequiresRelease()
    {
        WithReactionFixture((controller, enemy, player) =>
        {
            double deadline = ReadReaction<double>(controller, "dodgeDeadline");
            controller.OpenEnemyReactionOpportunity(enemy);
            Assert.That(ReadReaction<double>(controller, "dodgeDeadline"), Is.EqualTo(deadline));
            WriteReaction(controller, "dodgeAwaitingRelease", true);
            Assert.That(controller.TryHandleEnemyReaction(EnemyAttackReaction.Dodge), Is.False);
            controller.ReleaseEnemyReactionButton(EnemyAttackReaction.Dodge);
            Assert.That(controller.TryHandleEnemyReaction(EnemyAttackReaction.Dodge), Is.True);
            Assert.That(controller.IsAttackDodged(enemy, player, enemy.ActiveSkill), Is.False,
                "Une roulade sans controleur de mobilite ne doit jamais donner de protection.");
            controller.CancelAttackQte(enemy);
            controller.OpenEnemyReactionOpportunity(enemy);
            Assert.That(ReadReaction<bool>(controller, "attackQteActive"), Is.False, "Ne pas reouvrir le meme coup apres expiration/impact.");
        });
    }

    [Test]
    public void TimedDodgeProtectionSurvivesImpactButNotNextActionOrVictim()
    {
        WithReactionFixture((controller, enemy, player) =>
        {
            WriteReaction(controller, "attackDodgeProtected", true);
            controller.CancelAttackQte(enemy);
            Assert.That(controller.IsAttackDodged(enemy, player, enemy.ActiveSkill), Is.True);
            Assert.That(controller.IsAttackDodged(enemy, actor.transform, enemy.ActiveSkill), Is.False);
            Assert.That(controller.IsAttackDodged(actor.GetComponent<RealTimeCombatEnemy>(), player, enemy.ActiveSkill), Is.False);
            WriteReaction(enemy, "<ActionSequenceId>k__BackingField", enemy.ActionSequenceId + 1);
            Assert.That(controller.IsAttackDodged(enemy, player, enemy.ActiveSkill), Is.False);
            controller.EndEnemyReactionAction(enemy);
            Assert.That(ReadReaction<bool>(controller, "attackDodgeProtected"), Is.False);
        });
    }

    private static void WithReactionFixture(System.Action<CombatHealthThresholdController, RealTimeCombatEnemy, Transform> test)
    {
        var root = new GameObject("Invisible reaction fixture");
        var enemyObject = new GameObject("Reaction enemy");
        var player = new GameObject("Reaction player");
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            var manager = root.AddComponent<RealTimeCombatManager>();
            var controller = root.AddComponent<CombatHealthThresholdController>();
            var enemy = enemyObject.AddComponent<RealTimeCombatEnemy>();
            WriteReaction(manager, "combatActive", true);
            WriteReaction(manager, "engagedEnemy", enemy);
            WriteReaction(manager, "playerRoot", player.transform);
            WriteReaction(enemy, "activeSkill", skill);
            WriteReaction(controller, "combatManager", manager);
            WriteReaction(controller, "reactionTimeScale", 1f);
            controller.OpenEnemyReactionOpportunity(enemy);
            test(controller, enemy, player.transform);
            WriteReaction(manager, "combatActive", false);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(skill);
        }
    }

    private static void WriteReaction(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static T ReadReaction<T>(object target, string field) => (T)target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    [Test]
    public void CounterClipCleanupPreservesAuthoredImpactAndRemovesMissingReceivers()
    {
        var clip = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Characters/1_Squad/Lucian/Animation/Counter_Sword.anim"));
        try
        {
            var events = new System.Collections.Generic.List<AnimationEvent> {
                new AnimationEvent { functionName = "ResolveCounterSkillImpact", time = .7f },
                new AnimationEvent { functionName = "ResolveCounterSkillImpact", time = .9f },
                new AnimationEvent { functionName = "CustomFeedback", time = .8f }
            };
            foreach (string name in new[] { "Take", "Release", "SlowCombatTimeTo", "RestoreCombatTime", "CounterHit" })
                events.Add(new AnimationEvent { functionName = name, time = .1f });
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            var configure = typeof(CounterSkillPrototypeBuilder).GetMethod("ConfigureCounterAnimationEvent",
                BindingFlags.Static | BindingFlags.NonPublic);
            for (int i = 0; i < 2; i++)
            {
                configure.Invoke(null, new object[] { clip });
                var cleaned = AnimationUtility.GetAnimationEvents(clip);
                Assert.That(cleaned.Length, Is.EqualTo(2));
                Assert.That(cleaned[0].functionName, Is.EqualTo("ResolveCounterSkillImpact"));
                Assert.That(cleaned[0].time, Is.EqualTo(.7f).Within(.001f));
                Assert.That(cleaned[1].functionName, Is.EqualTo("CustomFeedback"));
            }
        }
        finally { Object.DestroyImmediate(clip); }
    }

    [TestCase(0f, 5f, 0f)]
    [TestCase(0f, -5f, 0f)]
    [TestCase(3f, -4f, 5f)]
    public void RushUsesAllThreeAxesAndHonorsStopDistance(float x, float y, float z)
    {
        var motor = actor.GetComponent<CombatEnemyPhysicsMotor>();
        var target = new GameObject("Rush target");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(CombatEnemyPhysicsMotor);
        var profile = new EnemyActionMotionProfile { enableHomingRush = true,
            rushMaximumSpeed = 15f, rushImpulseDuration = .1f, rushStoppingDistance = 1f };
        try
        {
            type.GetField("activeMotionProfile", flags).SetValue(motor, profile);
            type.GetField("bodyCollider", flags).SetValue(motor, null);
            type.GetField("body", flags).SetValue(motor, null);
            type.GetField("<State>k__BackingField", flags).SetValue(motor, CombatEnemyPhysicsState.AirborneAction);
            actor.transform.position = Vector3.zero;
            var direction = new Vector3(x, y, z);
            target.transform.position = direction;
            motor.BeginEnemyRush(target.transform);
            var resolve = type.GetMethod("ResolveRushDelta", flags);
            var delta = (Vector3)resolve.Invoke(motor, new object[] { Vector3.zero });
            Assert.That(delta.magnitude, Is.GreaterThan(0f));
            Assert.That(Vector3.Angle(delta, direction), Is.LessThan(.01f));
            Assert.That(delta.magnitude, Is.LessThanOrEqualTo(direction.magnitude - 1f + .0001f));
            target.transform.position = -direction;
            delta = (Vector3)resolve.Invoke(motor, new object[] { Vector3.zero });
            Assert.That(Vector3.Angle(delta, direction), Is.LessThan(.01f));
            for (int i = 0; i < 100; i++) resolve.Invoke(motor, new object[] { Vector3.zero });
            Assert.That(type.GetField("rushActive", flags).GetValue(motor), Is.False,
                "L'impulsion doit se terminer sans EndEnemyRush dans le clip.");
            Assert.That((Vector3)resolve.Invoke(motor, new object[] { Vector3.zero }), Is.EqualTo(Vector3.zero));
        }
        finally { Object.DestroyImmediate(target); }
    }

    [Test]
    public void AirborneOnlyLoadoutPursuesAndRepeatsWithoutIgnoringCooldown()
    {
        var info = actor.GetComponent<CharacterInfo>();
        var data = Object.Instantiate(info.CharacterData);
        var profile = Object.Instantiate(data.enemyCombatProfile);
        var jump = AssetDatabase.LoadAssetAtPath<SkillSO>("Assets/Characters/3_Enemy/Juggernaut/Skill_Juggernaut_Assomoir.asset");
        var strike = AssetDatabase.LoadAssetAtPath<SkillSO>("Assets/Characters/3_Enemy/Juggernaut/Skill_Juggernaut_Strike.asset");
        try
        {
            data.combatSkills = new System.Collections.Generic.List<SkillSO> { jump };
            info.SetCharacterData(data);
            profile.preferMeleeApproach = true;
            profile.airborneAlternativeChance = 0f;
            Set("profile", profile);
            Set("skills", actor.GetComponent<EnemySkills>());
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var approach = typeof(EnemyCombatBrain).GetMethod("TryResolveApproach", flags);
            var prefer = typeof(EnemyCombatBrain).GetMethod("ShouldPreferMelee", flags);
            var available = typeof(EnemyCombatBrain).GetMethod("IsPatternAvailable", flags);
            Assert.That(prefer.Invoke(brain, null), Is.False);
            object[] args = { 15f, false, 0f };
            Assert.That(approach.Invoke(brain, args), Is.True);
            Assert.That(args[1], Is.False);
            Assert.That((float)args[2], Is.EqualTo(5.8f).Within(.001f));
            args[0] = 4f;
            Assert.That(approach.Invoke(brain, args), Is.True);
            Assert.That(args[1], Is.True);
            args[0] = 1f;
            approach.Invoke(brain, args);
            Assert.That(args[1], Is.False, "Respecter aussi la portee minimale du pattern.");
            var pattern = profile.patterns.Find(p => p.skills[0] == jump);
            Set("previousPattern", pattern);
            Set("consecutiveUses", 20);
            Assert.That(available.Invoke(brain, new object[] { pattern }), Is.True);
            var cooldowns = Get<System.Collections.Generic.Dictionary<EnemyCombatPattern, float>>("cooldowns");
            cooldowns[pattern] = float.MaxValue;
            Assert.That(available.Invoke(brain, new object[] { pattern }), Is.False);
            cooldowns.Clear();
            data.combatSkills.Add(strike);
            Assert.That(prefer.Invoke(brain, null), Is.True);
            Assert.That(available.Invoke(brain, new object[] { pattern }), Is.False);
            data.combatSkills.Clear();
            Assert.That(prefer.Invoke(brain, null), Is.False);
            Assert.That(approach.Invoke(brain, args), Is.False);
        }
        finally { Object.DestroyImmediate(data); Object.DestroyImmediate(profile); }
    }

    [TestCase("Strike", .55f)]
    [TestCase("Followup", .55f)]
    [TestCase("Sweep", .625f)]
    [TestCase("Assomoir", .837f)]
    public void EssentialEventsAreConfiguredAndReinstallationIsStable(string name, float qteTime)
    {
        var source = AssetDatabase.LoadAssetAtPath<SkillSO>("Assets/Characters/3_Enemy/Juggernaut/Skill_Juggernaut_" + name + ".asset");
        JuggernautEssentialEvents.Validate(source);
        var sourceEvents = AnimationUtility.GetAnimationEvents(source.AnimationClip);
        var qte = System.Array.Find(sourceEvents, e => e.functionName == "OpenEnemyReactionOpportunity");
        Assert.That(qte, Is.Not.Null);
        Assert.That(source.CombatWarning.enabled, Is.False);
        Assert.That(source.ReactionTelegraph.enabled, Is.False);
        Assert.That(source.AcceptedEnemyReactions.Count, Is.Zero);
        var copy = Object.Instantiate(source);
        var clip = Object.Instantiate(source.AnimationClip);
        try
        {
            var serialized = new SerializedObject(copy);
            serialized.FindProperty("animationClip").objectReferenceValue = clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            JuggernautEssentialEvents.ConfigureSkill(copy, qteTime);
            Assert.That(copy.VfxCues.Count, Is.EqualTo(source.VfxCues.Count));
            var after = AnimationUtility.GetAnimationEvents(clip);
            Assert.That(after.Length, Is.EqualTo(sourceEvents.Length));
            for (int i = 0; i < after.Length; i++)
            {
                Assert.That(after[i].functionName, Is.EqualTo(sourceEvents[i].functionName));
                Assert.That(after[i].time, Is.EqualTo(sourceEvents[i].time));
                Assert.That(after[i].stringParameter, Is.EqualTo(sourceEvents[i].stringParameter));
                if (after[i].functionName == "EnemyAttack") Assert.That(after[i].objectReferenceParameter, Is.SameAs(copy));
            }
        }
        finally { Object.DestroyImmediate(copy); Object.DestroyImmediate(clip); }
    }

    [Test]
    public void PendingThresholdDoesNotSuspendCommittedAttack()
    {
        var thresholds = container.AddComponent<CombatHealthThresholdController>();
        var enemy = actor.GetComponent<RealTimeCombatEnemy>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(CombatHealthThresholdController).GetField("activeEnemy", flags).SetValue(thresholds, enemy);
        var state = typeof(CombatHealthThresholdController).GetField("state", flags);
        state.SetValue(thresholds, System.Enum.Parse(state.FieldType, "Pending"));
        var skillField = typeof(RealTimeCombatEnemy).GetField("activeSkill", flags);
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            skillField.SetValue(enemy, skill);
            Assert.That(enemy.IsAttackCommitted, Is.True);
            Assert.That(thresholds.HasPendingStage(enemy), Is.True);
            Assert.That(thresholds.BlocksEnemyActions(enemy), Is.True);
            Assert.That(thresholds.ShouldSuspendEnemy(enemy), Is.False);
            skillField.SetValue(enemy, null);
            Assert.That(thresholds.ShouldSuspendEnemy(enemy), Is.True);
        }
        finally { Object.DestroyImmediate(skill); }
    }

    [Test]
    public void ReturnFacingIsClearedOnStopAndCombatRetarget()
    {
        var locomotion = actor.GetComponent<CombatEnemyLocomotionController>();
        var flag = typeof(CombatEnemyLocomotionController).GetField("returnFacingActive", BindingFlags.Instance | BindingFlags.NonPublic);
        locomotion.SetReturnFacing(Vector3.forward * 10f);
        Assert.That((bool)flag.GetValue(locomotion), Is.True);
        Assert.That(actor.GetComponent<UnityEngine.AI.NavMeshAgent>().updateRotation, Is.False);
        locomotion.StopNavigation();
        Assert.That((bool)flag.GetValue(locomotion), Is.False);
        locomotion.SetReturnFacing(Vector3.forward * 10f);
        locomotion.SetCombatTarget(container.transform);
        Assert.That((bool)flag.GetValue(locomotion), Is.False);
    }
    [Test]
    public void ReturnProximityDetectsBehindWithoutChangingNormalVision()
    {
        var observer = new GameObject("Proximity observer");
        var player = new GameObject("Proximity target");
        try
        {
            observer.transform.position = new Vector3(10000f, 10000f, 10000f);
            player.transform.position = observer.transform.position - Vector3.forward * 3f;
            var vision = observer.AddComponent<VisionField>();
            Assert.That(vision.CanSee(player.transform), Is.False);
            Assert.That(vision.CanSenseNearby(player.transform, 6f), Is.True);
            Assert.That(vision.CanSenseNearby(player.transform, 2f), Is.False);
            Assert.That(vision.CanSenseNearby(player.transform, 0f), Is.False);
        }
        finally { Object.DestroyImmediate(player); Object.DestroyImmediate(observer); }
    }
    [TestCase(1.8f, 1f, 1.8f, 1f)]
    [TestCase(.9f, .5f, 1.8f, 1f)]
    [TestCase(.9f, 1f, 1.8f, .5f)]
    [TestCase(10f, 1f, 1.8f, 1.35f)]
    [TestCase(1f, 0f, 1.8f, 0f)]
    public void CadenceCompensatesLocalTimeOnlyOnce(float speed, float scale, float reference, float expected)
    {
        Assert.That(CombatEnemyLocomotionController.ResolvePlaybackRate(speed, scale, reference), Is.EqualTo(expected).Within(.0001f));
    }

    [Test]
    public void LocomotionActivityHasHysteresis()
    {
        Assert.That(CombatEnemyLocomotionController.ShouldPresentLocomotion(.05f, false), Is.False);
        Assert.That(CombatEnemyLocomotionController.ShouldPresentLocomotion(.05f, true), Is.True);
        Assert.That(CombatEnemyLocomotionController.ShouldPresentLocomotion(.02f, true), Is.False);
    }
    private GameObject container;
    private GameObject actor;
    private EnemyCombatBrain brain;

    [SetUp]
    public void SetUp()
    {
        container = new GameObject("Aggro regression fixture");
        container.SetActive(false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab");
        actor = Object.Instantiate(prefab, container.transform);
        brain = actor.GetComponent<EnemyCombatBrain>();
        Set("home", actor.transform.position);
        Set("homeRotation", actor.transform.rotation);
        Set("locomotion", actor.GetComponent<CombatEnemyLocomotionController>());
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(container);

    [Test]
    public void IdleAtSpawnDoesNotPermanentlyDisableVision()
    {
        TickReturn();
        TickReturn();
        Assert.That(Get<bool>("returning"), Is.False);
        Assert.That(brain.Phase, Is.EqualTo(EnemyCombatBrain.CombatPhase.Idle));
    }

    [Test]
    public void CompletedReturnRearmsVisionAndClearsObservation()
    {
        Set("returning", true);
        Set("observeUntil", 10f);
        TickReturn();
        Assert.That(Get<bool>("returning"), Is.False);
        Assert.That(Get<float>("observeUntil"), Is.Zero);
    }

    [Test]
    public void DisengagementDoesNotRequireNavigationOrEnemyDeath()
    {
        var enemy = actor.GetComponent<RealTimeCombatEnemy>();
        Set("enemy", enemy);
        Set("profile", actor.GetComponent<CharacterInfo>().CharacterData.enemyCombatProfile);
        Set("navigation", null);
        Set("home", actor.transform.position + Vector3.forward * 10f);
        TickReturn();
        Assert.That(Get<bool>("returning"), Is.True);
        Assert.That(brain.Target, Is.Null);
        Assert.That(enemy.Health == null || !enemy.Health.IsDead, Is.True);
    }

    [Test]
    public void SkillsResolveFromCharacterDataBeforeFirstReservation()
    {
        var skills = actor.GetComponent<EnemySkills>();
        var data = actor.GetComponent<CharacterInfo>().CharacterData;
        Assert.That(skills.Skills.Count, Is.EqualTo(data.combatSkills.Count));
        Assert.That(skills.SetActiveSkill(data.combatSkills[0]), Is.True);
    }

    private void TickReturn() => typeof(EnemyCombatBrain)
        .GetMethod("TickReturn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(brain, null);

    [TestCase(0f, 3f, 2.8f)]
    [TestCase(2.5f, 6f, 5.8f)]
    [TestCase(2f, 2.1f, 2.05f)]
    [TestCase(2f, 2f, 2f)]
    public void ApproachStaysInsideAuthoredRange(float minimum, float maximum, float expected)
    {
        Assert.That(EnemyCombatBrain.ResolveApproachDistance(minimum, maximum),
            Is.EqualTo(expected).Within(.0001f));
    }

    [Test]
    public void RunningUsesHysteresisBetweenSixAndEightMeters()
    {
        var locomotion = actor.GetComponent<CombatEnemyLocomotionController>();
        var type = typeof(CombatEnemyLocomotionController);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetField("navigationAgent", flags).SetValue(locomotion, actor.GetComponent<UnityEngine.AI.NavMeshAgent>());
        var pace = type.GetMethod("SetMovementPace", flags);
        var running = type.GetField("runPhase", flags);
        pace.Invoke(locomotion, new object[] { 15f });
        Assert.That(running.GetValue(locomotion), Is.True);
        pace.Invoke(locomotion, new object[] { 7f });
        Assert.That(running.GetValue(locomotion), Is.True);
        pace.Invoke(locomotion, new object[] { 5f });
        Assert.That(running.GetValue(locomotion), Is.False);
        pace.Invoke(locomotion, new object[] { 7f });
        Assert.That(running.GetValue(locomotion), Is.False);
    }

    private void Set(string name, object value) => typeof(EnemyCombatBrain)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(brain, value);

    private T Get<T>(string name) => (T)typeof(EnemyCombatBrain)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(brain);
}
