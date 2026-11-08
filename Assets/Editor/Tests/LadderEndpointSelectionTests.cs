using NUnit.Framework;
using UnityEngine;

public sealed class LadderEndpointSelectionTests
{
    private GameObject ladderRoot, player;
    private LadderController ladder;
    private Transform previousRoot;
    private string previousSource;
    private LocalPlayerContext.Authority previousAuthority;

    [SetUp]
    public void SetUp()
    {
        previousRoot = LocalPlayerContext.LocalCharacterRoot;
        previousSource = LocalPlayerContext.LocalCharacterSource;
        previousAuthority = LocalPlayerContext.LocalCharacterAuthority;
        ladderRoot = new GameObject("Two-level ladder selection regression");
        ladderRoot.SetActive(false);
        ladder = ladderRoot.AddComponent<LadderController>();
        ladder.leafs1 = new GameObject("Lower endpoint").transform;
        ladder.leafs2 = new GameObject("Upper endpoint").transform;
        ladder.leafs1.SetParent(ladderRoot.transform);
        ladder.leafs2.SetParent(ladderRoot.transform);
        ladder.leafs2.position = Vector3.up * 4;
        var trigger = ladderRoot.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = Vector3.up * 2;
        trigger.size = new Vector3(2, 6, 2);
        ladder.interactionCollider = trigger;
        player = new GameObject("Local selection origin");
        LocalPlayerContext.SetLocalCharacter(player.transform, "Ladder endpoint test");
    }

    [TestCase(0, false)]
    [TestCase(4, true)]
    public void UnselectedLadderUsesNearbyEndpointForTriggerRange(float height, bool upper)
    {
        player.transform.position = new Vector3(.25f, height, 0);
        Transform anchor = ladder.GetInteractionAnchor();
        Assert.That(anchor, Is.SameAs(upper ? ladder.leafs2 : ladder.leafs1));
        Vector3 rangePoint = CharacterInteractionDetection.GetInteractionRangePoint(ladder.interactionCollider, anchor, player.transform.position);
        Assert.That(Vector3.Distance(rangePoint, player.transform.position), Is.LessThan(.5f),
            "Trigger reach must resolve to the usable nearby endpoint even before detection assigns the character.");
    }

    [Test]
    public void ChangingControlledPositionReevaluatesEndpointWithoutSelectionState()
    {
        player.transform.position = ladder.leafs2.position;
        Assert.That(ladder.GetInteractionAnchor(), Is.SameAs(ladder.leafs2));
        player.transform.position = ladder.leafs1.position;
        Assert.That(ladder.GetInteractionAnchor(), Is.SameAs(ladder.leafs1));
    }

    [TearDown]
    public void TearDown()
    {
        LocalPlayerContext.ClearIfMatch(player != null ? player.transform : null, "Ladder endpoint test cleanup");
        if (previousRoot != null) LocalPlayerContext.SetLocalCharacter(previousRoot, previousSource, previousAuthority);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ladderRoot);
    }
}
