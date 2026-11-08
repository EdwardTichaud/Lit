using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class RetainedMarkerRestoreTests
{
    private GameObject registryRoot, markerRoot;
    private CharacterData data;
    [UnityTest] public IEnumerator RestoringAnInactiveBakedActorRetainsItsPersistentIdentity()
    {
        yield return new EnterPlayMode();
        Assert.That(GameFlowService.Instance == null || !GameFlowService.Instance.HasGameplaySession, Is.True, "Do not mutate a real gameplay session.");
        var registry = NetworkObjectRegistry.Instance;
        if (registry == null)
        {
            registryRoot = new GameObject("Owned retained-corpse registry"); registry = registryRoot.AddComponent<NetworkObjectRegistry>();
        }
        markerRoot = new GameObject("Owned retained marker"); markerRoot.SetActive(false);
        data = ScriptableObject.CreateInstance<CharacterData>(); data.characterId = "test.retained.marker"; data.hp = 100; data.isEnemy = false;
        var actor = new GameObject("Owned baked actor"); actor.transform.SetParent(markerRoot.transform, false);
        actor.AddComponent<NetworkObject>(); var health = actor.AddComponent<CharacterInfo>(); health.SetCharacterData(data);
        var marker = markerRoot.AddComponent<SceneMarker>(); marker.SetCharacterData(data); marker.SetBakedCharacterInstance(actor);
        typeof(SceneMarker).GetField("markerId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(marker, "test.retained.marker");
        markerRoot.SetActive(true); yield return null;
        var persistent = actor.GetComponent<PersistentNetworkObject>();
        Assert.That(persistent, Is.Not.Null); Assert.That(registry.TryGet(marker.MarkerId, out var initial), Is.True); Assert.That(initial, Is.SameAs(persistent));
        var snapshot = persistent.CaptureSnapshot(null); snapshot.Transform.ActiveSelf = false;
        persistent.ApplyTransformState(snapshot);
        Assert.That(registry.TryGet(marker.MarkerId, out var duringRestore), Is.True, "Transform/active phase must not remove the identity before gameplay providers.");
        Assert.That(duringRestore, Is.SameAs(persistent));
        marker.ApplyPersistedState(actor.transform.position, actor.transform.rotation, Vector3.one, 0, 100, false);
        yield return null;
        Assert.That(actor.activeSelf, Is.False); Assert.That(health.CurrentHp, Is.Zero);
        Assert.That(registry.TryGet(marker.MarkerId, out var restored), Is.True, "Inactive actor must remain addressable during final reference restoration.");
        Assert.That(restored, Is.SameAs(persistent));
        marker.ApplyPersistedState(actor.transform.position, actor.transform.rotation, Vector3.one, 0, 100, false);
        Assert.That(registry.TryGet(marker.MarkerId, out var again), Is.True); Assert.That(again, Is.SameAs(persistent));
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (markerRoot != null) Object.DestroyImmediate(markerRoot);
        if (registryRoot != null) Object.DestroyImmediate(registryRoot);
        if (data != null) Object.DestroyImmediate(data);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
