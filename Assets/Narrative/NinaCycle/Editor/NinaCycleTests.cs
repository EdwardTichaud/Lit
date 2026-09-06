#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;

public sealed class NinaCycleTests
{
    [TestCase(0, true, true, false)]
    [TestCase(NinaCycleController.ScientistDefeated, true, true, false)]
    [TestCase(NinaCycleController.CinematicCompleted, false, true, false)]
    [TestCase(NinaCycleController.CinematicCompleted, true, false, false)]
    [TestCase(NinaCycleController.CinematicCompleted, true, true, true)]
    public void NinaRequiresBothRevelationsAndCompletedCinematic(int state, bool dilemma, bool existence, bool expected)
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
}
#endif
