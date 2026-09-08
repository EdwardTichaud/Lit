#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;

public sealed class NinaCycleTests
{
    [TestCase(0, false, false, false)]
    [TestCase(0, false, true, false)]
    [TestCase(0, true, false, false)]
    [TestCase(0, true, true, true)]
    [TestCase(NinaCycleController.ScientistDefeated, true, true, true)]
    [TestCase(NinaCycleController.CinematicCompleted, false, true, false)]
    [TestCase(NinaCycleController.CinematicCompleted, true, false, false)]
    [TestCase(NinaCycleController.CinematicCompleted, true, true, true)]
    public void NinaDeadRequiresAllCycleKnowledgeOnly(int state, bool dilemma, bool existence, bool expected)
    {
        Assert.AreEqual(expected, NinaCycleController.CanVisitNina(state, dilemma, existence));
    }

    [Test]
    public void LetterRevealsDilemmaOnlyWhenRead()
    {
        var definition = AssetDatabase.LoadAssetAtPath<NinaCycleDefinition>("Assets/Resources/Narrative/NinaCycle.asset");
        var letter = AssetDatabase.LoadAssetAtPath<Item>("Assets/Narrative/NinaCycle/Data/Item_Edward.asset");
        Assert.NotNull(definition);
        Assert.NotNull(letter);
        Assert.AreEqual(Item.ReadableKind.Parchment, letter.readableKind);
        Assert.Contains(definition.dilemma, letter.knowledgeUnlockedOnRead);
        Assert.IsEmpty(letter.knowledgeUnlockedOnPickup);
        Assert.IsFalse(letter.knowledgeUnlockedOnRead.Contains(definition.existence));
    }

    [TestCase(0, true, false)]
    [TestCase(NinaCycleController.CinematicCompleted, true, false)]
    [TestCase(NinaCycleController.NinaVisited, false, false)]
    [TestCase(NinaCycleController.NinaVisited, true, true)]
    [TestCase(NinaCycleController.NinaDeadSpoken, true, true)]
    [TestCase(NinaCycleController.NinaDeadSpoken, false, false)]
    [TestCase(NinaCycleController.RewardGranted, true, false)]
    public void NinaBloodAndScarUnlockWhenDeadDialogueStartsAndSupportExistingSaves(int state, bool allKnowledgeKnown, bool expected)
    {
        Assert.AreEqual(expected, NinaCycleController.ShouldShowNinaBlood(state, allKnowledgeKnown));
    }
}
#endif
