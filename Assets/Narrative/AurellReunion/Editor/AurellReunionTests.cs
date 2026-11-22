using System;
using System.Linq;
using System.Reflection;
using Lit.Story;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AurellReunionTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject root;
    private WorldRulesStateManager rules;
    private CycleProgressionService service;
    private SoloTeamProgression team;
    private CycleDefinition definition;
    private SquadManager previousSquad;
    private CycleProgressionService previousService;
    private static void Singleton(Type type, object value) => type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);

    [SetUp] public void Setup()
    {
        previousSquad = SquadManager.Instance;
        previousService = CycleProgressionService.Instance;
        root = new GameObject("Aurell cycle scoped fixture");
        root.SetActive(false);
        rules = root.AddComponent<WorldRulesStateManager>();
        service = root.AddComponent<CycleProgressionService>();
        typeof(CycleProgressionService).GetField("rules", Private).SetValue(service, rules);
        Singleton(typeof(CycleProgressionService), service);
        Singleton(typeof(SquadManager), root.AddComponent<SquadManager>());
        team = root.AddComponent<SoloTeamProgression>();
        team.InitializeNewGame();
        definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>(AurellReunionAuthoring.DefinitionPath);
        Assert.That(definition, Is.Not.Null, "Install the authored cycle first.");
        service.RegisterDefinition(definition);
    }
    [TearDown] public void Cleanup()
    {
        Singleton(typeof(SquadManager), previousSquad);
        Singleton(typeof(CycleProgressionService), previousService);
        Object.DestroyImmediate(root);
    }
    private bool Report(CycleStepKind kind, string source) => (bool)typeof(CycleProgressionService)
        .GetMethod("ApplyEvent", Private).Invoke(service, new object[] { definition, kind, source });
    private void Reconcile() => typeof(CycleProgressionService).GetMethod("ReconcileCompanionRewards", Private).Invoke(service, null);
    private void Introduction()
    {
        Report(CycleStepKind.ZoneEntered, "arrival");
        for (int i = 1; i <= 4; i++) Report(CycleStepKind.NamedFact, "absence_" + i);
    }

    [Test] public void ArrivalAndPartialDialogueDoNotRecruitAndRoundTripResumesAtTheMissingLine()
    {
        Assert.That(CompanionReunionState.HasDeparted, Is.False);
        Report(CycleStepKind.ZoneEntered, "arrival");
        Report(CycleStepKind.NamedFact, "absence_1");
        var snapshot = rules.CaptureVariables();
        Assert.That(CompanionReunionState.HasDeparted, Is.True);
        Assert.That(team.IsRecruited(CombatSkillKit.Aurell), Is.False);
        rules.ApplyVariables(Array.Empty<WorldVariableSnapshot>());
        Assert.That(CompanionReunionState.HasDeparted, Is.False);
        rules.ApplyVariables(snapshot);
        Assert.That(service.IsStepCompleted(definition, "absence_1"), Is.True);
        Assert.That(service.IsStepCompleted(definition, "absence_2"), Is.False);
        Assert.That(service.IsCompleted(definition), Is.False);
    }

    [Test] public void SuccessfulReunionGrantsOnlyIntercessionAndDoubleCompletionCannotDuplicateIt()
    {
        Introduction();
        int announcements = 0;
        team.MemberRecruited += _ => announcements++;
        Assert.That(Report(CycleStepKind.SequenceCompleted, "reunion_completed"), Is.True);
        Assert.That(service.IsCompleted(definition), Is.True);
        Assert.That(team.IsUnlocked(CombatSkillKit.Aurell, 0), Is.True);
        for (int i = 1; i < 4; i++) Assert.That(team.IsUnlocked(CombatSkillKit.Aurell, i), Is.False);
        Assert.That(Report(CycleStepKind.SequenceCompleted, "reunion_completed"), Is.False);
        Reconcile(); Reconcile();
        Assert.That(announcements, Is.EqualTo(1));
        Assert.That(CompanionReunionState.ShouldBePresent(CombatSkillKit.Aurell), Is.True);
        Assert.That(CompanionReunionState.ShouldBePresent(CombatSkillKit.Vesta), Is.False);
    }

    [Test] public void CompletedStepRepairsMissingTeamEvenWithARewardReceiptWithoutAnnouncing()
    {
        Introduction(); Report(CycleStepKind.SequenceCompleted, "reunion_completed");
        Assert.That(rules.TryGetInt(definition.RewardKey("reunion_completed", 0), out int receipt) && receipt == 1, Is.True);
        team.InitializeNewGame();
        int announcements = 0; team.MemberRecruited += _ => announcements++;
        Reconcile();
        Assert.That(team.IsRecruited(CombatSkillKit.Aurell), Is.True);
        Assert.That(announcements, Is.Zero);
    }

    [Test] public void ExistingRecruitmentAndAdditionalTechniquesSurviveTheNewCycle()
    {
        team.RecruitMember(CombatSkillKit.Aurell, false);
        team.UnlockSkill(CombatSkillKit.Aurell, 2);
        var saved = team.Capture();
        Introduction(); Report(CycleStepKind.SequenceCompleted, "reunion_completed");
        team.Restore(saved); Reconcile();
        Assert.That(team.IsUnlocked(CombatSkillKit.Aurell, 2), Is.True);
        Assert.That(team.IsUnlocked(CombatSkillKit.Aurell, 1), Is.False);
    }

    [TestCase(true, false, false, true)] [TestCase(true, true, false, false)]
    [TestCase(true, true, true, true)] [TestCase(false, true, false, true)]
    public void HousePresenceFollowsSessionAndRecruitment(bool solo, bool departed, bool recruited, bool expected) =>
        Assert.That(CompanionReunionState.ShouldBePresent(solo, departed, recruited), Is.EqualTo(expected));

    [Test] public void OnlyTheNewCycleIsSoloOnlyAndExistingAutomaticDialoguesRemainModalByDefault()
    {
        Assert.That(definition.IsAvailableInSession(true), Is.True);
        Assert.That(definition.IsAvailableInSession(false), Is.False);
        Assert.That(new CycleAutoDialogueBinding().blocksGameplayInput, Is.True);
        Assert.That((int)CycleRewardKind.Activation, Is.EqualTo(4));
        Assert.That((int)CycleRewardKind.RecruitCompanion, Is.EqualTo(5));
        foreach (var other in CycleCatalog.LoadDefinitions().Where(item => item != definition))
            Assert.That(other.IsAvailableInSession(false), Is.True, other.name);
        CollectionAssert.IsEmpty(definition.ValidateConfiguration());
    }

    [Test] public void AuthoredSceneHasExplicitBindingsAndAnUnconfirmedMovableAnchor()
    {
        var scene = EditorSceneManager.OpenPreviewScene(AurellReunionAuthoring.ScenePath);
        try
        {
            var adapter = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<AurellReunionCycle>(true)).Single();
            Assert.That(adapter.cycle.definition, Is.SameAs(definition));
            Assert.That(adapter.meeting.placementConfirmed, Is.False);
            Assert.That(adapter.meeting.owner, Is.SameAs(adapter));
            Assert.That(adapter.presentation.transform.parent, Is.SameAs(adapter.meeting.transform));
            Assert.That(adapter.reunion.Sequence.TryValidate(out string issue), Is.True, issue);
            Assert.That(adapter.reunion.Sequence.steps.Count, Is.EqualTo(6));
            Assert.That(adapter.reunion.Sequence.playOnce, Is.False);
            Assert.That(adapter.introduction.Length, Is.EqualTo(4));
            Assert.That(adapter.introduction.All(line => !line.blocksGameplayInput && line.speakerCharacterData.characterName == "Lucian"), Is.True);
            Assert.That(adapter.aurell.Animator.runtimeAnimatorController.animationClips.All(clip => clip.isLooping), Is.True);
            Assert.That(adapter.reunion.Sequence.steps.All(step => step.voiceLine.voiceLineAudioClip == null), Is.True);
            Assert.That(adapter.presentation.GetComponentInChildren<SquadCharacterController>(true), Is.Null);
            Assert.That(adapter.presentation.GetComponentInChildren<Unity.Netcode.NetworkObject>(true), Is.Null);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == AurellReunionAuthoring.ScenePath), Is.True);
        Assert.That(AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset").LoadingSceneNames,
            Does.Contain(definition.cycleSceneName));
    }
}
