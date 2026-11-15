#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Category = InteractableItem.InteractableCategory;
using Placement = SceneMarker.ItemPlacement;

public sealed class WorldItemAuthoringTests
{
    [TestCase(Item.ReadableKind.Book, Placement.Unspecified, false)]
    [TestCase(Item.ReadableKind.Book, Placement.FixedDocument, true)]
    [TestCase(Item.ReadableKind.Parchment, Placement.Recoverable, true)]
    [TestCase(Item.ReadableKind.Stab, Placement.Recoverable, false)]
    [TestCase(Item.ReadableKind.Stab, Placement.FixedDocument, true)]
    [TestCase(Item.ReadableKind.None, Placement.FixedDocument, false)]
    [TestCase(Item.ReadableKind.None, Placement.Container, true)]
    public void WorldRoleRequiresAnExplicitCompatibleChoice(Item.ReadableKind kind, Placement role, bool expected)
    {
        var item = ScriptableObject.CreateInstance<Item>();
        try
        {
            item.readableKind = kind;
            Assert.That(WorldItemAuthoring.TryCategory(item, role, out _, out _), Is.EqualTo(expected));
        }
        finally { Object.DestroyImmediate(item); }
    }

    [Test]
    public void BakeTwiceKeepsTheInstanceAndIdentityAndRefusesToClearData()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        string folder = "Assets/__WorldItemFixture_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        try
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(visual, scene);
            var prefab = PrefabUtility.SaveAsPrefabAsset(visual, folder + "/Visual.prefab");
            Object.DestroyImmediate(visual);
            var item = ScriptableObject.CreateInstance<Item>();
            item.itemId = Guid.NewGuid().ToString("N");
            item.worldPrefab = prefab;
            item.readableKind = Item.ReadableKind.Book;
            AssetDatabase.CreateAsset(item, folder + "/TestBook.asset");

            var root = new GameObject("Name stays stable");
            SceneManager.MoveGameObjectToScene(root, scene);
            var marker = root.AddComponent<SceneMarker>();
            marker.SetItem(item);
            var properties = new SerializedObject(marker);
            properties.FindProperty("itemPlacement").enumValueIndex = (int)Placement.Recoverable;
            properties.ApplyModifiedPropertiesWithoutUndo();
            uint identity = NetcodeSceneIdUtility.GetStableId(root.transform);
            Assert.That(WorldItemAuthoring.TryBake(marker, out string error), Is.True, error);
            var interaction = marker.BakedItemInteraction;
            interaction.maxStoredQuantity = 17;
            interaction.lockId = "Fixture lock";
            interaction.storedItems.Add(new InteractableItem.LootItemEntry { item = item, quantity = 2 });
            Assert.That(WorldItemAuthoring.TryBake(marker, out error), Is.True, error);
            Assert.That(marker.BakedItemInteraction, Is.EqualTo(interaction));
            Assert.That(root.transform.childCount, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<InteractableItem>(true).Length, Is.EqualTo(1));
            Assert.That(NetcodeSceneIdUtility.GetStableId(root.transform), Is.EqualTo(identity));
            Assert.That(interaction.maxStoredQuantity, Is.EqualTo(17));
            Assert.That(interaction.lockId, Is.EqualTo("Fixture lock"));

            properties.Update();
            properties.FindProperty("itemPlacement").enumValueIndex = (int)Placement.FixedDocument;
            properties.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(WorldItemAuthoring.TryBake(marker, out _), Is.False);
            Assert.That(interaction.interactableCategory, Is.EqualTo(Category.RecoverableItem));
            Assert.That(interaction.storedItems[0].quantity, Is.EqualTo(2));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            AssetDatabase.DeleteAsset(folder);
        }
    }

    [Test]
    public void AdoptionPreviewIsReadOnlyAndApplyDoesNotChangeTheLegacyInstance()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var item = ScriptableObject.CreateInstance<Item>();
        try
        {
            var root = new GameObject("Existing chest");
            SceneManager.MoveGameObjectToScene(root, scene);
            var chest = root.AddComponent<InteractableItem>();
            chest.representedItem = item;
            chest.isLocked = true;
            chest.storedItems.Add(new InteractableItem.LootItemEntry { item = item, quantity = 8 });
            uint identity = NetcodeSceneIdUtility.GetStableId(root.transform);
            string before = EditorJsonUtility.ToJson(chest);
            Assert.That(WorldItemAuthoring.TryAdopt(chest, false, out string preview), Is.True, preview);
            Assert.That(root.GetComponent<SceneMarker>(), Is.Null);
            Assert.That(WorldItemAuthoring.TryAdopt(chest, true, out preview), Is.True, preview);
            Assert.That(root.GetComponent<SceneMarker>().BakedItemInteraction, Is.EqualTo(chest));
            Assert.That(NetcodeSceneIdUtility.GetStableId(root.transform), Is.EqualTo(identity));
            Assert.That(EditorJsonUtility.ToJson(chest), Is.EqualTo(before));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); Object.DestroyImmediate(item); }
    }

    [Test]
    public void FixedDocumentDoesNotExposeHistoricalLootThroughItemRules()
    {
        var root = new GameObject("Fixed document fixture");
        var item = ScriptableObject.CreateInstance<Item>();
        try
        {
            item.allowTakeFromContainers = item.allowDepositToContainers = true;
            var document = root.AddComponent<InteractableItem>();
            WorldPickupUtility.ApplyWorldBinding(document, item, Category.ReadableItem);
            document.storedItems.Add(new InteractableItem.LootItemEntry { item = item, quantity = 3 });
            Assert.That(item.CanTakeFromContainer(document), Is.False);
            Assert.That(item.CanDepositToContainer(document), Is.False);
            Assert.That(document.storedItems[0].quantity, Is.EqualTo(3));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(item); }
    }
}
#endif
