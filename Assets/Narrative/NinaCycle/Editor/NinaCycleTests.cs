#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;

public sealed class NinaCycleTests
{
    private const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    [Test]
    public void ScientistUsesGhostInteractionBeforeCombat()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Characters/9_Ghosts/Luc/Enemy_Model_MadScientist.prefab");
        var ghost = prefab.GetComponent<GhostController>();
        Assert.NotNull(ghost);
        Assert.NotNull(ghost.Data);
        Assert.IsInstanceOf<IGhostInteractionHandler>(prefab.GetComponent<EnemyController>());
        Assert.AreEqual(1, prefab.GetComponents<CharacterInfo>().Length);
        Assert.AreEqual(1, prefab.GetComponents<EnemyController>().Length);
        Assert.IsTrue(prefab.GetComponent<EnemyController>().enabled);
        Assert.IsFalse(prefab.GetComponent<EnemyController>().CombatEnabled);
    }

    [Test]
    public void LeavingGhostModeMakesBodyVisibleAndDisablesGhostInteraction()
    {
        var root = new UnityEngine.GameObject("Ghost to enemy test");
        root.SetActive(false);
        try
        {
            var renderer = root.AddComponent<UnityEngine.MeshRenderer>();
            renderer.enabled = false;
            var ghost = root.AddComponent<GhostController>();
            ghost.SetGhostMode(false);
            Assert.IsFalse(ghost.enabled);
            Assert.IsTrue(renderer.enabled);
            Assert.IsFalse(ghost.InteractWithGhost());
            ghost.SetGhostMode(true);
            Assert.IsTrue(ghost.enabled);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void ScarBodyFallsOntoWorldAndKeepsUpright()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new UnityEngine.GameObject("Scar physics root");
        var floor = new UnityEngine.GameObject("World floor");
        try
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, scene);
            var floorCollider = floor.AddComponent<UnityEngine.BoxCollider>();
            floorCollider.size = new UnityEngine.Vector3(10f, 1f, 10f);
            floor.transform.position = new UnityEngine.Vector3(0f, -.5f, 0f);
            root.transform.position = new UnityEngine.Vector3(0f, 2f, 0f);
            var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Characters/9_Ghosts/Scar/Ghost_Model_Scar.prefab");
            var model = UnityEngine.Object.Instantiate(prefab, root.transform);
            model.transform.localPosition = UnityEngine.Vector3.zero;
            model.transform.localRotation = UnityEngine.Quaternion.identity;
            Assert.IsFalse(model.GetComponent<UnityEngine.Animator>().applyRootMotion);
            var body = root.AddComponent<UnityEngine.Rigidbody>();
            body.mass = 70f;
            body.useGravity = true;
            body.constraints = UnityEngine.RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = UnityEngine.CollisionDetectionMode.Continuous;
            UnityEngine.Collider solid = null;
            foreach (var collider in model.GetComponentsInChildren<UnityEngine.Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Assert.IsNull(solid, "Only the body capsule should support Scar, not bones or accessories.");
                solid = collider;
                Assert.AreEqual(body, collider.attachedRigidbody);
            }
            Assert.IsInstanceOf<UnityEngine.CapsuleCollider>(solid);
            UnityEngine.Physics.SyncTransforms();
            var physics = UnityEngine.PhysicsSceneExtensions.GetPhysicsScene(scene);
            Assert.That(physics, Is.Not.EqualTo(UnityEngine.Physics.defaultPhysicsScene), "Physics test must remain isolated from the open authoring scenes.");
            for (int i = 0; i < 150; i++) physics.Simulate(.02f);
            Assert.That(root.transform.position.y, Is.InRange(-.06f, .06f));
            Assert.That(solid.bounds.min.y, Is.InRange(-.03f, .03f));
            Assert.That(UnityEngine.Vector3.Dot(root.transform.up, UnityEngine.Vector3.up), Is.GreaterThan(.999f));
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(.1f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(floor);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void CicatriceIsScarRewardRatherThanAStartingSkill()
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        var lucian = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Lucian/Lucian.asset");
        Assert.NotNull(definition.FindDialogue("scar").rewardSkill);
        Assert.AreEqual("Cicatrice", definition.FindDialogue("scar").rewardSkill.SkillName);
        Assert.IsFalse(lucian.combatSkills.Contains(definition.FindDialogue("scar").rewardSkill));
    }

    [Test]
    public void ScarRewardSurvivesWorldSnapshotAndIsGrantedOnlyOnce()
    {
        var root = new UnityEngine.GameObject("Scar reward test");
        root.SetActive(false);
        var definition = UnityEngine.ScriptableObject.CreateInstance<CycleDefinition>();
        var skill = UnityEngine.ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            var rules = root.AddComponent<WorldRulesStateManager>();
            var cycle = root.AddComponent<CycleController>();
            definition.cycleId = "test.scar";
            definition.dialogues = new[] { new CycleDialogue { id = "scar", rewardSkill = skill, rewardFlag = 8 } };
            cycle.definition = definition;
            typeof(CycleController).GetField("rules", Private).SetValue(cycle, rules);
            rules.SetInt(definition.StateKey, 16);
            int changes = 0;
            rules.VariablesChanged += () => changes++;
            var grant = typeof(CycleController).GetMethod("ApplyDialogueEffects", Private);
            grant.Invoke(cycle, new object[] { definition.FindDialogue("scar"), false });
            Assert.AreEqual(0, changes, "Opening the reward dialogue must not grant the skill.");
            grant.Invoke(cycle, new object[] { definition.FindDialogue("scar"), true });
            grant.Invoke(cycle, new object[] { definition.FindDialogue("scar"), true });
            Assert.AreEqual(1, changes);
            var snapshot = rules.CaptureVariables();
            rules.ResetRuntimeState();
            rules.ApplyVariables(snapshot);
            Assert.IsTrue(rules.TryGetInt(definition.StateKey, out int state));
            Assert.AreEqual(16 | 8, state);
            changes = 0;
            grant.Invoke(cycle, new object[] { definition.FindDialogue("scar"), true });
            Assert.AreEqual(0, changes, "Loaded rewards must not grant again.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }

    [Test]
    public void ScarRejectsInteractionWithoutEligiblePlayer()
    {
        var root = new UnityEngine.GameObject("Scar interaction test");
        root.SetActive(false);
        var definition = UnityEngine.ScriptableObject.CreateInstance<CycleDefinition>();
        try
        {
            var rules = root.AddComponent<WorldRulesStateManager>();
            var cycle = root.AddComponent<CycleController>();
            cycle.definition = definition;
            typeof(CycleController).GetField("rules", Private).SetValue(cycle, rules);
            typeof(CycleController).GetMethod("BeginDialogue", Private).Invoke(cycle, new object[] { 0UL, "scar", 1, null });
            Assert.IsFalse(rules.TryGetInt(definition.StateKey, out _));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [TestCase("  Une blessure persistante.  ", "COMPÃ‰TENCE APPRISE\n<size=140%>Cicatrice</size>\n\nUne blessure persistante.")]
    [TestCase(" ", "COMPÃ‰TENCE APPRISE\n<size=140%>Cicatrice</size>")]
    public void SkillUnlockUsesKnowledgeStyleTitleAndOptionalDescription(string description, string expected)
    {
        var root = new UnityEngine.GameObject("Skill notification test");
        root.SetActive(false);
        try
        {
            var panel = root.AddComponent<SkillUnlockPanel>();
            var message = typeof(SkillUnlockPanel).GetMethod("FormatMessage", Private).Invoke(panel, new object[] { "Cicatrice", description });
            Assert.AreEqual(expected, message);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void ScientistDeathVoiceUsesForgiveMeAndIsAssignedToPrefab()
    {
        const string voicePath = "Assets/Narrative/NinaCycle/Data/deathVoiceLine.asset";
        var voice = AssetDatabase.LoadAssetAtPath<AudioClipSO>(voicePath);
        Assert.NotNull(voice);
        Assert.NotNull(voice.audioClip);
        Assert.AreEqual("b69f9fddfc2b25740a5bbe6a80383a05",
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(voice.audioClip)));
        Assert.IsFalse(voice.loop);
        Assert.IsFalse(voice.affectedByTimeScale);
        var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
            "Assets/Characters/9_Ghosts/Luc/Enemy_Model_MadScientist.prefab");
        Assert.NotNull(prefab);
        var encounter = prefab.GetComponent<EnemyController>();
        Assert.NotNull(encounter);
        Assert.IsTrue(encounter.StartsAsGhost);
        Assert.IsTrue(encounter.HasDeathPresentation);
        var settings = new SerializedObject(encounter.GetComponent<CharacterInfo>().SourceData);
        Assert.AreEqual(voice, settings.FindProperty("enemyDeathOptions.voiceLine").objectReferenceValue);
        Assert.AreEqual("Qu'est ce que... j'ai fait...", settings.FindProperty("enemyDeathOptions.dialogueLine").stringValue);
    }

    [TestCase(0, false, false, false)]
    [TestCase(0, false, true, false)]
    [TestCase(0, true, false, false)]
    [TestCase(0, true, true, true)]
    [TestCase(1, true, true, true)]
    [TestCase(2, false, true, false)]
    [TestCase(2, true, false, false)]
    [TestCase(2, true, true, true)]
    public void NinaDeadRequiresAllCycleKnowledgeOnly(int state, bool dilemma, bool existence, bool expected)
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        Assert.AreEqual(expected, definition.FindDialogue("nina").condition.Matches(state,
            knowledge => knowledge == definition.knowledgeOnEnemyDefeat[0] ? existence : dilemma));
    }

    [Test]
    public void LetterRevealsDilemmaOnlyWhenRead()
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        var letter = AssetDatabase.LoadAssetAtPath<Item>("Assets/Narrative/NinaCycle/Data/Item_Parchment_Edouard.asset");
        Assert.NotNull(definition);
        Assert.NotNull(letter);
        Assert.AreEqual(Item.ReadableKind.Parchment, letter.readableKind);
        Assert.Contains(definition.FindDialogue("nina").condition.knowledge[1], letter.knowledgeUnlockedOnRead);
        Assert.IsEmpty(letter.knowledgeUnlockedOnPickup);
        Assert.IsFalse(letter.knowledgeUnlockedOnRead.Contains(definition.knowledgeOnEnemyDefeat[0]));
    }

    [TestCase(0, true, false)]
    [TestCase(2, true, false)]
    [TestCase(4, false, false)]
    [TestCase(4, true, true)]
    [TestCase(16, true, true)]
    [TestCase(16, false, false)]
    [TestCase(8, true, false)]
    public void NinaBloodAndScarUnlockWhenDeadDialogueStartsAndSupportExistingSaves(int state, bool allKnowledgeKnown, bool expected)
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        Assert.AreEqual(expected, definition.FindDialogue("scar").condition.Matches(state, _ => allKnowledgeKnown));
    }
}
#endif
