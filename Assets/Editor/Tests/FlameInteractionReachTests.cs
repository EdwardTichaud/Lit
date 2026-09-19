using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class FlameInteractionReachTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void NonConvexDoorUsesItsSurfaceForFlameReach()
    {
        var root = new GameObject("Door reach test");
        var mesh = new Mesh();
        try
        {
            mesh.vertices = new[] { new Vector3(4, -1, -1), new Vector3(4, 1, -1), new Vector3(4, 0, 1) };
            mesh.triangles = new[] { 0, 1, 2 };
            var collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            Physics.SyncTransforms();
            var influence = new LitInfluenceSource(6f);
            Assert.That(influence.TouchesCollider(null, collider, Vector3.one * 100), Is.True);
            influence.SetRadius(3f);
            Assert.That(influence.TouchesCollider(null, collider, Vector3.one * 100), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void ItemCanUseLitBrazierBeforeReceivingPhysicsNotification()
    {
        var brazier = new GameObject("Brazier reach test");
        var itemRoot = new GameObject("Necklace reach test");
        itemRoot.SetActive(false);
        brazier.transform.position = new Vector3(10000, 10000, 10000);
        itemRoot.transform.position = brazier.transform.position + Vector3.right * 4;
        try
        {
            var flame = brazier.AddComponent<Flame>();
            var item = itemRoot.AddComponent<InteractableItem>();
            typeof(InteractableItem).GetField("requireLitInfluenceForInteraction", Private).SetValue(item, true);
            typeof(InteractableItem).GetField("reactToFlameInfluence", Private).SetValue(item, true);
            var canInteract = typeof(InteractableItem).GetMethod("CanInteractInCurrentInfluence", Private);
            typeof(Flame).GetField("isLit", Private).SetValue(flame, true);
            Assert.That(canInteract.Invoke(item, null), Is.EqualTo(true));
            typeof(Flame).GetField("externalSuppression", Private).SetValue(flame, true);
            Assert.That(canInteract.Invoke(item, null), Is.EqualTo(false));
            typeof(Flame).GetField("externalSuppression", Private).SetValue(flame, false);
            flame.enabled = false;
            Assert.That(canInteract.Invoke(item, null), Is.EqualTo(false));
            flame.enabled = true;
            itemRoot.transform.position += Vector3.right * 20;
            Assert.That(canInteract.Invoke(item, null), Is.EqualTo(false));
        }
        finally
        {
            Object.DestroyImmediate(itemRoot);
            Object.DestroyImmediate(brazier);
        }
    }

    [Test]
    public void MaelleBrazierKeepsItsInfluenceWhenEnemiesAreNearby()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/District_1/District_1_Rooms_Flammes.unity");
        try
        {
            AncientFlameEnemyBlocker blocker = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (AncientFlameEnemyBlocker candidate in root.GetComponentsInChildren<AncientFlameEnemyBlocker>(true))
                {
                    if (candidate.GetComponent<Flame>()?.FlameId == "scene-flame:Maison:E6C2F108")
                    {
                        blocker = candidate;
                        break;
                    }
                }
            }

            Assert.That(blocker, Is.Not.Null);
            Assert.That(new SerializedObject(blocker).FindProperty("ignoreEnemySuppression").boolValue, Is.True);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void MaelleDoorHasAnAuthoredOutlineTarget()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/District_1/District_1_Interactives.unity");
        try
        {
            Door door = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Door candidate in root.GetComponentsInChildren<Door>(true))
                {
                    if (candidate.name == "Moon_Room_Door_Maelle")
                    {
                        door = candidate;
                        break;
                    }
                }
            }

            Assert.That(door, Is.Not.Null);
            Assert.That(door.GetComponent<RuntimeOutlineTarget>(), Is.Not.Null);
            Assert.That(door.GetComponent<Renderer>(), Is.Not.Null);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
