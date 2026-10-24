#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;

public sealed class NinaCycleTests
{
    private const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    [Test]
    public void NinaMemoryIsAnEditableBoundTimelineWithoutChangingTheOptionalSaveStep()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(NinaCycleSetup.ScenePath);
        try
        {
            var cycle = System.Linq.Enumerable.Single(System.Linq.Enumerable.SelectMany(scene.GetRootGameObjects(),
                r => r.GetComponentsInChildren<CycleController>(true)));
            var director = cycle.director;
            Assert.NotNull(director);
            Assert.NotNull(director.GetComponent<LitTimelineCinemachineBridge>());
            Assert.NotNull(cycle.bindingProfile);
            Assert.IsTrue(cycle.bindingProfile.Matches(director.playableAsset));
            Assert.IsFalse(director.playOnAwake);
            Assert.AreEqual(UnityEngine.Playables.DirectorWrapMode.None, director.extrapolationMode);
            var timeline = director.playableAsset as UnityEngine.Timeline.TimelineAsset;
            Assert.NotNull(timeline);
            Assert.That(timeline.duration, Is.EqualTo(18).Within(.01));
            var cameraTrack = System.Linq.Enumerable.Single(System.Linq.Enumerable.OfType<Unity.Cinemachine.CinemachineTrack>(timeline.GetOutputTracks()));
            var shots = System.Linq.Enumerable.ToArray(cameraTrack.GetClips());
            Assert.AreEqual(4, shots.Length);
            for (int i = 0; i < shots.Length; i++)
            {
                Assert.That(shots[i].start, Is.EqualTo(i * 4.5).Within(.001));
                Assert.That(shots[i].duration, Is.EqualTo(4.5).Within(.001));
                var camera = ((Unity.Cinemachine.CinemachineShot)shots[i].asset).VirtualCamera.Resolve(director);
                Assert.NotNull(camera);
                Assert.AreEqual(scene, camera.gameObject.scene);
            }
            Assert.That(Lit.Editor.TimelineBindingsValidator.CollectBindingCoverageIssues(director, cycle.bindingProfile), Is.Empty);
            var memory = cycle.definition.FindStep("aftermath_seen");
            Assert.IsFalse(memory.terminal);
            Assert.AreEqual("cinematic", memory.sourceId);
            Assert.IsTrue(cycle.definition.FindStep("scar_reward").terminal);
            Assert.That(director.GetComponentsInChildren<EnemyController>(true), Is.Empty);
            Assert.That(director.GetComponentsInChildren<GhostController>(true), Is.Empty);
            Assert.That(director.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true), Is.Empty);
            foreach (var activation in System.Linq.Enumerable.OfType<UnityEngine.Timeline.ActivationTrack>(timeline.GetOutputTracks()))
            {
                var target = director.GetGenericBinding(activation) as UnityEngine.GameObject;
                Assert.NotNull(target);
                Assert.IsFalse(target.activeSelf, "Presentation must not leak outside Timeline playback.");
                Assert.AreEqual(UnityEngine.Timeline.ActivationTrack.PostPlaybackState.Inactive, activation.postPlaybackState);
            }
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [UnityEngine.TestTools.UnityTest]
    public System.Collections.IEnumerator NinaMemoryNativeGraphShowsOnlyTheCurrentCaptionAndCleansUpOnStop()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(NinaCycleSetup.ScenePath);
        UnityEngine.Playables.PlayableDirector director = null;
        UnityEngine.GameObject cameraObject = null;
        try
        {
            var cycle = System.Linq.Enumerable.Single(System.Linq.Enumerable.SelectMany(scene.GetRootGameObjects(),
                r => r.GetComponentsInChildren<CycleController>(true)));
            director = cycle.director;
            var timeline = (UnityEngine.Timeline.TimelineAsset)director.playableAsset;
            var cameraTrack = System.Linq.Enumerable.Single(System.Linq.Enumerable.OfType<Unity.Cinemachine.CinemachineTrack>(timeline.GetOutputTracks()));
            cameraObject = new UnityEngine.GameObject("Isolated memory preview camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.AddComponent<UnityEngine.Camera>();
            director.SetGenericBinding(cameraTrack, cameraObject.AddComponent<Unity.Cinemachine.CinemachineBrain>());
            var activations = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OfType<UnityEngine.Timeline.ActivationTrack>(timeline.GetOutputTracks()));
            Assert.AreEqual(5, activations.Length);
            var targets = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(activations,
                track => (UnityEngine.GameObject)director.GetGenericBinding(track)));
            director.timeUpdateMode = UnityEngine.Playables.DirectorUpdateMode.Manual;
            director.Play();
            for (int i = 0; i < 4; i++)
            {
                director.time = i * 4.5 + .25;
                director.Evaluate();
                Assert.IsTrue(targets[0].activeSelf, "Nina's visual fragment should be present.");
                for (int caption = 0; caption < 4; caption++)
                    Assert.AreEqual(caption == i, targets[caption + 1].activeSelf, "Only the current quotation should be shown.");
            }
            director.Stop();
            // ActivationMixerPlayable applies postPlaybackState in OnPlayableDestroy.
            // Native graph destruction is deferred until the next editor frame.
            yield return null;
            foreach (var target in targets) Assert.IsFalse(target.activeSelf, "Stopping must remove local presentation: " + target.name);
        }
        finally
        {
            if (director != null) director.Stop();
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void NinaDefinitionLookupPreservesAuthoredAssetIdentity()
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>(NinaCycleSetup.DefinitionPath);
        Assert.NotNull(definition, "Nina editor tools must resolve the existing authored definition.");
        Assert.AreEqual("db3b01997ab4dae40b9843a98bba1c5e", AssetDatabase.AssetPathToGUID(NinaCycleSetup.DefinitionPath));
        Assert.AreEqual("district1.nina", definition.cycleId);
        Assert.AreEqual(System.IO.Path.GetFileNameWithoutExtension(NinaCycleSetup.ScenePath), definition.cycleSceneName,
            "Completion and reload must resolve the manifest's current scene without changing cycle identity.");
        Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(NinaCycleSetup.ScenePath),
            "The creation guard must recognize the existing authored Nina scene.");
    }

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
    public void ScarEffectsUseTheAuthoredBodyRenderer()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Characters/9_Ghosts/Scar/Ghost_Model_Scar.prefab");
        var effects = prefab.GetComponentsInChildren<INab.VFXAssets.UniformMeshSample>(true);
        Assert.That(effects, Is.Not.Empty);
        foreach (var effect in effects)
        {
            Assert.That(effect.meshRenderer, Is.InstanceOf<UnityEngine.SkinnedMeshRenderer>());
            Assert.That(effect.meshRenderer.transform.IsChildOf(prefab.transform), Is.True);
        }
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
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>(NinaCycleSetup.DefinitionPath);
        var lucian = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Lucian/Lucian.asset");
        SkillSO cicatrice = definition.FindStep("scar_reward").rewards[0].skill;
        Assert.NotNull(cicatrice);
        Assert.AreEqual("Cicatrice", cicatrice.SkillName);
        Assert.IsFalse(lucian.combatSkills.Contains(cicatrice));
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

    [TestCase("  Une blessure persistante.  ", "COMP\u00c9TENCE APPRISE\n<size=140%>Cicatrice</size>\n\nUne blessure persistante.")]
    [TestCase(" ", "COMP\u00c9TENCE APPRISE\n<size=140%>Cicatrice</size>")]
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
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>(NinaCycleSetup.DefinitionPath);
        Assert.AreEqual(expected, definition.FindDialogue("nina").condition.Matches(state,
            knowledge => knowledge == definition.FindStep("chimeras_known").knowledge ? existence : dilemma));
    }

    [Test]
    public void LetterRevealsDilemmaOnlyWhenRead()
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>(NinaCycleSetup.DefinitionPath);
        var letter = AssetDatabase.LoadAssetAtPath<Item>("Assets/Narrative/NinaCycle/Data/Item_Parchment_Edouard.asset");
        Assert.NotNull(definition);
        Assert.NotNull(letter);
        Assert.AreEqual(Item.ReadableKind.Parchment, letter.readableKind);
        Assert.Contains(definition.FindDialogue("nina").condition.knowledge[1], letter.knowledgeUnlockedOnRead);
        Assert.IsEmpty(letter.knowledgeUnlockedOnPickup);
        Assert.IsFalse(letter.knowledgeUnlockedOnRead.Contains(definition.FindStep("chimeras_known").knowledge));
    }

    [Test]
    public void NinaBloodAndScarUseTheNamedNinaDialogueStep()
    {
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>(NinaCycleSetup.DefinitionPath);
        var scar = definition.FindDialogue("scar");
        Assert.AreEqual(0, scar.condition.anyFlags);
        Assert.AreEqual(1, scar.condition.requirements.conditions.Length);
        Assert.AreEqual("nina_spoken", scar.condition.requirements.conditions[0].stepId);
    }
}
#endif
