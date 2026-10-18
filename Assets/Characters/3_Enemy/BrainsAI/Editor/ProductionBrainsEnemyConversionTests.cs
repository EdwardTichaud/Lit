#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ProductionBrainsEnemyConversionTests
{
    private const string CharacterPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset";

    [Test]
    public void ProductionJuggernautAuditProvesAllOwnAuthoredAttacksWithoutFlatteningPatterns()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterPath);
        Assert.IsNotNull(data);
        string before = EditorJsonUtility.ToJson(data);
        string prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data.ResolveWorldPrefab()));
        var report = ProductionBrainsEnemyConversion.Analyze(data);
        Assert.AreSame(data.enemyCombatProfile, report.sourceProfile);
        Assert.AreEqual(AssetDatabase.AssetPathToGUID(CharacterPath), report.characterGuid);
        Assert.AreEqual(prefabGuid, report.prefabGuid);
        Assert.IsTrue(report.CanGenerate, string.Join("\n", report.blockers));
        Assert.IsTrue(report.CanApply, string.Join("\n", report.conversionBlockers));
        Assert.IsTrue(report.requiresFlameInfluence);
        Assert.AreEqual(4, report.attacks.Count);
        CollectionAssert.AreEquivalent(data.enemyCombatProfile.patterns.SelectMany(pattern => pattern.skills).Distinct().ToArray(), report.attacks.Select(mapping => mapping.skill).ToArray());
        Assert.IsNotEmpty(report.runtimeRequirements);
        Assert.AreEqual(before, EditorJsonUtility.ToJson(data));
        foreach (var attack in report.attacks)
        {
            Assert.IsTrue(data.enemyCombatProfile.patterns.Any(pattern => pattern.skills.Contains(attack.skill)));
            Assert.AreSame(attack.skill.AnimationClip, attack.clip);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(attack.skill)), attack.skillGuid);
        }
    }

    [Test]
    public void InspectionCandidateContainsOnlyExactSourceSkillsAndDoesNotReplaceCharacterData()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterPath);
        var report = ProductionBrainsEnemyConversion.Analyze(data);
        var candidate = ProductionBrainsEnemyConversion.CreateInspectionCandidate(report);
        try
        {
            Assert.AreSame(data.enemyCombatProfile, candidate.productionSourceProfile);
            CollectionAssert.AreEqual(report.attacks.Select(attack => attack.skill).ToArray(), candidate.attacks.Select(attack => attack.skill).ToArray());
            CollectionAssert.AreEqual(report.attacks.Select(attack => attack.clip).ToArray(), candidate.attacks.Select(attack => attack.clip).ToArray());
            Assert.AreEqual(data.enemySettings.ActorHitAnimatorState, candidate.hurtState);
            Assert.IsFalse(AssetDatabase.Contains(candidate));
            Assert.AreSame(data, AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterPath));
        }
        finally { Object.DestroyImmediate(candidate); }
    }

    [Test]
    public void BlockedConversionCannotOverwriteProductionAssetsOrBorrowPrototypeAttacks()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterPath);
        string dataBefore = EditorJsonUtility.ToJson(data);
        var prefab = data.ResolveWorldPrefab();
        string prefabBefore = EditorJsonUtility.ToJson(prefab);
        var request = ScriptableObject.CreateInstance<ProductionBrainsEnemyConversionRequest>();
        try
        {
            request.character = data;
            Assert.Throws<System.InvalidOperationException>(() => ProductionBrainsEnemyConversion.Apply(request));
            Assert.AreEqual(dataBefore, EditorJsonUtility.ToJson(data));
            Assert.AreEqual(prefabBefore, EditorJsonUtility.ToJson(prefab));
        }
        finally { Object.DestroyImmediate(request); }
    }

    [Test]
    public void NullOrNonEnemyCharacterNeverReceivesInferredAttacks()
    {
        var report = ProductionBrainsEnemyConversion.Analyze(null);
        Assert.IsFalse(report.CanApply);
        Assert.IsEmpty(report.attacks);
        var data = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            data.isEnemy = false;
            report = ProductionBrainsEnemyConversion.Analyze(data);
            Assert.IsFalse(report.CanApply);
            Assert.IsEmpty(report.attacks);
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void GeneratorProducesExecutableSourceMappedAssetsAndSecondPassPreservesTheirGuids()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterPath);
        string dataBefore = EditorJsonUtility.ToJson(data);
        string sourceBefore = EditorJsonUtility.ToJson(data.enemyCombatProfile);
        string root = "Assets/__BrainsProductionTest_" + System.Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", root.Substring("Assets/".Length));
        try
        {
            var request = ProductionBrainsEnemyConversion.GenerateReviewedAssets(data, root + "/BrainsProduction");
            Assert.IsTrue(request.reviewedProfile.executeProductionPatterns);
            Assert.AreSame(data.enemyCombatProfile, request.reviewedProfile.productionSourceProfile);
            Assert.AreEqual(4, request.reviewedProfile.attacks.Length);
            using var clashSource = new CombatClashAuthoring.ConfigurationScope();
            foreach (var attack in request.reviewedProfile.attacks)
            {
                Assert.AreNotSame(attack.skill.AnimationClip, attack.clip);
                var original = AnimationUtility.GetAnimationEvents(attack.skill.AnimationClip)
                    .Where(entry => entry.functionName != "OpenAttackClashWindow" && entry.functionName != "CloseAttackClashWindow").ToArray();
                var mapped = AnimationUtility.GetAnimationEvents(attack.clip);
                var unchanged = mapped.Where(entry => entry.functionName != "OpenAttackClashWindow" && entry.functionName != "CloseAttackClashWindow").ToArray();
                CollectionAssert.AreEqual(original.Select(entry => entry.time).ToArray(), unchanged.Select(entry => entry.time).ToArray());
                CollectionAssert.AreEqual(original.Select(entry => ProductionBrainsEnemyConversion.MapEvent(entry.functionName)).ToArray(), unchanged.Select(entry => entry.functionName).ToArray());
                bool compatible = CombatClashAuthoring.IsCompatibleEnemySkill(clashSource.Config, attack.skill);
                var opens = mapped.Where(entry => entry.functionName == "OpenAttackClashWindow").ToArray();
                var closes = mapped.Where(entry => entry.functionName == "CloseAttackClashWindow").ToArray();
                Assert.AreEqual(compatible ? 1 : 0, opens.Length);
                Assert.AreEqual(opens.Length, closes.Length);
                if (compatible)
                {
                    Assert.AreEqual(Mathf.Max(0, attack.impactSeconds - .08f), opens[0].time, .00001f);
                    Assert.AreEqual(Mathf.Min(attack.clip.length, attack.impactSeconds + .06f), closes[0].time, .00001f);
                }
            }
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(request.reviewedProfile));
            string controllerPath = AssetDatabase.GetAssetPath(request.reviewedController);
            string controllerGuid = AssetDatabase.AssetPathToGUID(controllerPath);
            AssetDatabase.ImportAsset(controllerPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var reloaded = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
            ProductionBrainsAnimatorGraph.Validate(reloaded);
            Assert.IsNotNull(reloaded.layers[0].stateMachine.defaultState);
            Assert.AreEqual("CombatIdle", reloaded.layers[0].stateMachine.defaultState.name);
            Assert.IsEmpty(reloaded.layers[0].stateMachine.anyStateTransitions);
            foreach (var state in reloaded.layers[0].stateMachine.states)
                Assert.IsEmpty(state.state.transitions, "The production executor directly owns state changes; legacy transitions must not leak into the new graph.");
            var generatedBytes = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith(root + "/BrainsProduction/", System.StringComparison.Ordinal))
                .ToDictionary(path => path, System.IO.File.ReadAllBytes);
            var second = ProductionBrainsEnemyConversion.GenerateReviewedAssets(data, root + "/BrainsProduction");
            Assert.AreSame(request, second);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(second.reviewedProfile)));
            Assert.AreEqual(controllerGuid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(second.reviewedController)));
            foreach (var entry in generatedBytes)
                CollectionAssert.AreEqual(entry.Value, System.IO.File.ReadAllBytes(entry.Key), "Second generation rewrote " + entry.Key);
            Assert.AreEqual(dataBefore, EditorJsonUtility.ToJson(data));
            Assert.AreEqual(sourceBefore, EditorJsonUtility.ToJson(data.enemyCombatProfile));
            Assert.IsTrue(request.requiresFlameInfluence);

            var report = ProductionBrainsEnemyConversion.Analyze(data, request.sourceController);
            float reach = request.reviewedProfile.attacks[0].reach;
            try
            {
                request.reviewedProfile.attacks[0].reach += 1;
                Assert.Throws<System.InvalidOperationException>(() => ProductionBrainsEnemyConversion.ValidateDestination(
                    report, request.reviewedProfile, request.reviewedController));
                Assert.Throws<System.InvalidOperationException>(() => ProductionBrainsEnemyConversion.GenerateReviewedAssets(data, root + "/BrainsProduction"));
            }
            finally { request.reviewedProfile.attacks[0].reach = reach; }
        }
        finally { AssetDatabase.DeleteAsset(root); }
    }

    [TestCase("Assets/Narrative/NinaCycle/Data/Enemy_ScientifiqueFou.asset", "last-words")]
    [TestCase("Assets/Scenes/Cycles/Cycle_Etienne/Data/CharacterData_DeadWeight.asset", "Boss encounter")]
    [TestCase("Assets/Scenes/Cycles/Cycle_Belmont/Data/CharacterData_BrokenAnchor.asset", "Boss encounter")]
    [TestCase("Assets/Scenes/Cycles/Cycle_Ardent/Data/CharacterData_FalseChoir.asset", "Boss encounter")]
    public void AuthoredSpecialEnemiesKeepTheirOwnIdentityAndRejectGenericConversion(string path, string expectedBlocker)
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        Assert.IsNotNull(data, "The actual production identity must resolve; do not replace it with a fixture.");
        byte[] before = System.IO.File.ReadAllBytes(path);
        var report = ProductionBrainsEnemyConversion.Analyze(data);
        Assert.AreSame(data, report.character);
        Assert.AreSame(data.enemyCombatProfile, report.sourceProfile);
        Assert.IsFalse(report.CanApply);
        Assert.That(report.blockers, Has.Some.Contains(expectedBlocker));
        CollectionAssert.AreEqual(before, System.IO.File.ReadAllBytes(path));
    }

    [Test]
    public void ShadowGuardianVfxBinderResolvesToItsExactInstalledPackageScript()
    {
        const string guid = "cdafc37f32b176349b1684c4455b98e9";
        string path = AssetDatabase.GUIDToAssetPath(guid);
        Assert.IsNotEmpty(path, "Sync/import the Visual Effect Graph dependency into the isolated project, not a replacement script.");
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        Assert.IsNotNull(script);
        Assert.AreEqual("UnityEngine.VFX.Utility.VFXPropertyBinder", script.GetClass()?.FullName);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/Cycles/Cycle_Belmont/Prefabs/ShadowGuardian.prefab");
        Assert.IsNotNull(prefab);
        Assert.IsFalse(prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component == null));
    }

    [Test]
    public void ScriptSlotDiagnosticReadsTheExactComponentReferenceInsteadOfGuessingFromAnotherGuid()
    {
        const string text = "--- !u!1 &10\nGameObject:\n  m_Component:\n  - component: {fileID: 20}\n  - component: {fileID: 30}\n" +
            "--- !u!4 &20\nTransform:\n  m_GameObject: {fileID: 10}\n" +
            "--- !u!114 &30\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: cdafc37f32b176349b1684c4455b98e9, type: 3}\n";
        Assert.IsTrue(ProductionBrainsScriptDiagnostics.TryReadSerializedReference(text, 10, 1, out long component, out string guid));
        Assert.AreEqual(30, component);
        Assert.AreEqual("cdafc37f32b176349b1684c4455b98e9", guid);
        Assert.IsFalse(ProductionBrainsScriptDiagnostics.TryReadSerializedReference(text, 10, 0, out _, out _));
        Assert.IsFalse(ProductionBrainsScriptDiagnostics.TryReadSerializedReference(text, 10, 2, out _, out _));
    }

    [Test]
    public void UnavailableSerializedScriptEvidenceDoesNotClaimAnAbsentAuthorAsset()
    {
        var slot = new ProductionBrainsScriptDiagnostics.Slot();
        ProductionBrainsScriptDiagnostics.DescribeScript(slot);
        StringAssert.Contains("no source-absence conclusion", slot.status);
    }

    [Test]
    public void GiantAliasesAreAuditedIndependentlyWithoutIdentityRepair()
    {
        var first = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut_CharacterData.asset");
        var second = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/GiantJuggernaut.asset");
        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreNotEqual(first.UniqueId, second.UniqueId);
        Assert.AreSame(first.ResolveWorldPrefab(), second.ResolveWorldPrefab());
        var reports = new[] { ProductionBrainsEnemyConversion.Analyze(first), ProductionBrainsEnemyConversion.Analyze(second) };
        Assert.AreNotEqual(reports[0].characterGuid, reports[1].characterGuid);
        Assert.IsFalse(reports[0].CanApply);
        Assert.IsFalse(reports[1].CanApply);
        Assert.That(reports[1].blockers, Has.Some.Contains("Root CharacterInfo"));
        Assert.That(reports[0].blockers, Has.Some.Contains("nested Animator/event-receiver adapter"));
        Assert.That(reports[1].blockers, Has.Some.Contains("nested Animator/event-receiver adapter"));
    }
}
#endif
