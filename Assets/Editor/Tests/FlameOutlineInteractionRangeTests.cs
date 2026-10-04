using NUnit.Framework;
using UnityEngine;

public sealed class FlameOutlineInteractionRangeTests
{
    [Test]
    public void DirectFlameOutlineRangeMatchesItsInteractionRange()
    {
        GameObject flameObject = new GameObject("Flame interaction range test");
        GameObject player = new GameObject("Player interaction range test");
        try
        {
            Flame flame = flameObject.AddComponent<Flame>();
            flame.ConfigureDirectInteraction();
            player.transform.position = flameObject.transform.position + Vector3.right * 1.05f;

            Assert.That(flame.IsCharacterWithinInteractionDistance(player.transform), Is.False,
                "A direct Flame must not be outlined beyond the distance at which Interact can light it.");

            player.transform.position = flameObject.transform.position + Vector3.right * 0.95f;
            Assert.That(flame.IsCharacterWithinInteractionDistance(player.transform), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(flameObject);
        }
    }
}
