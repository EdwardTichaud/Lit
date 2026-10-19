#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CombatSceneReturnTests
{
    [Test]
    public void ReturnCleanupDoesNotWaitForAnImpossibleLastSceneUnload()
    {
        Assert.That(Application.isPlaying, Is.False);
        Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "Run in the isolated empty-scene test runner.");
        var scene = SceneManager.GetActiveScene();
        string original = scene.name;
        try
        {
            scene.name = "CombatReturnLastSceneFixture";
            Assert.That(SceneManager.GetSceneByName(scene.name).isLoaded, Is.True);
            var iterator = Unload(scene.name);
            Assert.That(iterator.MoveNext(), Is.False, "The next Single load owns retirement of the last scene.");
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
        }
        finally { scene.name = original; }
    }

    [Test]
    public void AlreadyUnloadedSceneCleanupIsIdempotent()
    {
        Assert.That(Unload("__CombatReturnMissingScene__").MoveNext(), Is.False);
        Assert.That(Unload("__CombatReturnMissingScene__").MoveNext(), Is.False);
    }

    private static IEnumerator Unload(string name) => (IEnumerator)typeof(GameFlowService)
        .GetMethod("UnloadSceneIfLoaded", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, new object[] { name });
}
#endif
