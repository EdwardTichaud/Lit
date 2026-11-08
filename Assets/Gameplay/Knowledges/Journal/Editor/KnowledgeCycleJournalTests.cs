using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class KnowledgeCycleJournalTests
{
    private readonly List<Object> owned = new List<Object>();
    private T Data<T>() where T : ScriptableObject
    { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
    private KnowledgeSO Knowledge(string title)
    { var value = Data<KnowledgeSO>(); value.title = title; return value; }
    private CycleDefinition Cycle(string id, KnowledgeSO knowledge = null)
    {
        var value = Data<CycleDefinition>(); value.cycleId = id; value.title = id;
        value.steps = knowledge == null ? Array.Empty<CycleStep>() : new[] { new CycleStep { kind = CycleStepKind.Knowledge, knowledge = knowledge } };
        return value;
    }

    [TearDown] public void Cleanup()
    {
        foreach (var value in owned.AsEnumerable().Reverse()) if (value != null) Object.DestroyImmediate(value);
        owned.Clear();
    }

    [UnityTearDown] public IEnumerator LeavePlayMode()
    { if (Application.isPlaying) yield return new ExitPlayMode(); }

    [Test] public void OnlyUnlockedKnowledgeAppearsAndUnknownCyclesStayHidden()
    {
        var known = Knowledge("Known"); var hidden = Knowledge("Hidden");
        var visibleCycle = Cycle("Visible", known); var hiddenCycle = Cycle("Hidden", hidden);
        var groups = KnowledgeCycleGrouping.Build(new[] { known }, new[] { visibleCycle, hiddenCycle });
        Assert.That(groups.Count, Is.EqualTo(1)); Assert.That(groups[0].Cycle, Is.SameAs(visibleCycle));
        Assert.That(groups[0].Entries, Is.EqualTo(new[] { known }));
    }

    [Test] public void OneKnowledgeCanBelongToTwoCyclesWithoutCloningOrMutatingIt()
    {
        var knowledge = Knowledge("Shared"); var first = Cycle("A", knowledge); var second = Cycle("B", knowledge);
        var groups = KnowledgeCycleGrouping.Build(new[] { knowledge, knowledge }, new[] { first, second, first });
        Assert.That(groups.Count, Is.EqualTo(2));
        Assert.That(groups.All(g => g.Entries.Count == 1 && ReferenceEquals(g.Entries[0], knowledge)), Is.True);
        Assert.That(knowledge.cycles, Is.Empty);
    }

    [Test] public void ExplicitAuthorLinksWorkWithoutAProgressionStepOrLoadedController()
    {
        var knowledge = Knowledge("Note"); var cycle = Cycle("Global notes"); knowledge.cycles.Add(cycle);
        var groups = KnowledgeCycleGrouping.Build(new[] { knowledge }, Array.Empty<CycleDefinition>());
        Assert.That(groups.Single().Cycle, Is.SameAs(cycle));
    }

    [Test] public void UnclassifiedEntriesStayAvailableInTheirOwnLastGroup()
    {
        var other = Knowledge("Other"); var related = Knowledge("Related"); var cycle = Cycle("Z", related);
        var groups = KnowledgeCycleGrouping.Build(new[] { other, related, null }, new[] { cycle, null });
        Assert.That(groups.Last().Cycle, Is.Null); Assert.That(groups.Last().Title, Is.EqualTo("Autres connaissances"));
        Assert.That(groups.Last().Entries.Single(), Is.SameAs(other));
    }

    [Test] public void EmptyAndMissingCollectionsDoNotCreateEmptyHeadings()
    {
        Assert.That(KnowledgeCycleGrouping.Build(null, null), Is.Empty);
        var cycle = Cycle("Empty"); cycle.steps = null; cycle.dialogues = null; cycle.prerequisites = null;
        Assert.That(KnowledgeCycleGrouping.Build(Array.Empty<KnowledgeSO>(), new[] { cycle }), Is.Empty);
    }

    [Test] public void KnowledgeRewardsAndLegacyDialogueConditionsAreClassified()
    {
        var reward = Knowledge("Reward"); var dialogue = Knowledge("Dialogue"); var cycle = Cycle("Legacy");
        cycle.steps = new[] { new CycleStep { rewards = new[] { new CycleReward { kind = CycleRewardKind.Knowledge, knowledge = reward } } } };
        cycle.dialogues = new[] { new CycleDialogue { condition = new CycleCondition { knowledge = new[] { dialogue } } } };
        var groups = KnowledgeCycleGrouping.Build(new[] { reward, dialogue }, new[] { cycle });
        Assert.That(groups.Single().Entries, Is.EquivalentTo(new[] { reward, dialogue }));
    }

    [Test] public void RequirementReferencesAreIncludedButUnrelatedRewardFieldsAreNot()
    {
        var required = Knowledge("Required"); var unrelated = Knowledge("Unrelated"); var cycle = Cycle("Requirements");
        cycle.steps = new[] { new CycleStep { prerequisites = new CycleRequirements { conditions = new[] {
            new CycleRequirement { kind = CycleRequirementKind.Knowledge, knowledge = required } } },
            rewards = new[] { new CycleReward { kind = CycleRewardKind.Item, knowledge = unrelated } } } };
        var groups = KnowledgeCycleGrouping.Build(new[] { required, unrelated }, new[] { cycle });
        Assert.That(groups.First().Entries.Single(), Is.SameAs(required)); Assert.That(groups.Last().Cycle, Is.Null);
    }

    [Test] public void HeadingsAndEntriesAreSortedWithoutReorderingTheSourceList()
    {
        var z = Knowledge("Zebra"); var a = Knowledge("Arche"); var source = new[] { z, a };
        var cycle = Cycle("B", z); cycle.steps = cycle.steps.Concat(new[] { new CycleStep { kind = CycleStepKind.Knowledge, knowledge = a } }).ToArray();
        var groups = KnowledgeCycleGrouping.Build(source, new[] { cycle });
        Assert.That(groups.Single().Entries, Is.EqualTo(new[] { a, z })); Assert.That(source, Is.EqualTo(new[] { z, a }));
    }

    [UnityTest] public IEnumerator ActualPanelGroupsRebuildsAndShowsTheOriginalDescriptionInPlay()
    {
        yield return new EnterPlayMode();
        var manager = KnowledgeManager.GetOrCreate();
        var before = manager.UnlockedKnowledge.ToArray();
        var fixture = new GameObject("Owned knowledge journal UI test", typeof(RectTransform), typeof(Canvas));
        owned.Add(fixture); fixture.SetActive(false);
        fixture.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(KnowledgePanelController));
        panelObject.transform.SetParent(fixture.transform, false);
        var panelRect = panelObject.GetComponent<RectTransform>(); panelRect.sizeDelta = new Vector2(700, 500);
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        content.SetParent(panelObject.transform, false); content.sizeDelta = new Vector2(650, 400);
        var template = new GameObject("Slot template", typeof(RectTransform), typeof(KnowledgePanelSlot));
        template.transform.SetParent(fixture.transform, false); template.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 32);
        var title = new GameObject("KnowledgeTitle", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        title.transform.SetParent(template.transform, false); title.fontSize = 20;
        var description = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        description.transform.SetParent(fixture.transform, false);
        var panel = panelObject.GetComponent<KnowledgePanelController>();
        void Set(string field, object value) => typeof(KnowledgePanelController).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, value);
        Set("knowledgeListContent", content); Set("slotPrefab", template.GetComponent<KnowledgePanelSlot>()); Set("descriptionText", description);
        var knowledge = Knowledge("Une note connue"); knowledge.description = "Texte original, sans copie de progression.";
        var cycle = Cycle("Cycle de test uniquement"); knowledge.cycles.Add(cycle);
        try
        {
            manager.RestoreUnlockedKnowledge(new[] { knowledge }); fixture.SetActive(true); yield return null;
            Assert.That(content.GetComponentsInChildren<KnowledgePanelSlot>().Length, Is.EqualTo(1));
            Assert.That(content.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Cycle de test uniquement (1)"), Is.True);
            panel.FocusSlot(content.GetComponentInChildren<KnowledgePanelSlot>());
            Assert.That(description.text, Is.EqualTo(knowledge.description));
            panel.Refresh(); panel.Refresh(); yield return null;
            Assert.That(content.GetComponentsInChildren<KnowledgePanelSlot>().Length, Is.EqualTo(1));
            Assert.That(content.GetComponentsInChildren<TMP_Text>().Count(t => t.gameObject.name == "KnowledgeCycleHeader"), Is.EqualTo(1));
            Assert.That(manager.UnlockedKnowledge, Is.EqualTo(new[] { knowledge }));
            panelObject.SetActive(false); panelObject.SetActive(true); yield return null;
            Assert.That(content.GetComponentsInChildren<KnowledgePanelSlot>().Length, Is.EqualTo(1));
            manager.ClearKnowledge(); yield return null;
            Assert.That(content.GetComponentsInChildren<KnowledgePanelSlot>(), Is.Empty); Assert.That(description.text, Is.Empty);
        }
        finally { manager.RestoreUnlockedKnowledge(before); }
        Cleanup();
        yield return new ExitPlayMode();
    }
}
