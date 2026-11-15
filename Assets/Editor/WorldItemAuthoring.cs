using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Category = InteractableItem.InteractableCategory;
using Placement = SceneMarker.ItemPlacement;

/// <summary>Editor-only linking. No existing object is recreated or given a new identity.</summary>
internal static class WorldItemAuthoring
{
    internal static bool TryCategory(Item item, Placement placement, out Category category, out string reason)
    {
        category = default;
        if (item == null) { reason = "Assigne un Item."; return false; }
        if (placement == Placement.Unspecified) { reason = "Choisis explicitement l’usage monde."; return false; }
        if (item.IsReadableStab() && placement != Placement.FixedDocument)
        { reason = "Une stèle doit rester un document fixe."; return false; }
        switch (placement)
        {
            case Placement.FixedDocument when item.IsReadable(): category = Category.ReadableItem; break;
            case Placement.Container when !item.IsReadable(): category = Category.Container; break;
            case Placement.Recoverable: category = Category.RecoverableItem; break;
            default: reason = "Cet usage ne correspond pas au type de l’Item."; return false;
        }
        reason = null;
        return true;
    }

    internal static bool TryBake(SceneMarker marker, out string reason)
    {
        if (marker == null || !marker.UsesItem || !CanEdit(marker.gameObject))
        { reason = "Sélectionne un marker Item dans une scène ou en mode prefab, hors Play."; return false; }
        if (!TryCategory(marker.Item, marker.Placement, out Category category, out reason)) return false;
        GameObject root = marker.gameObject;
        var existing = root.GetComponentsInChildren<InteractableItem>(true);
        GameObject source = marker.Item.ResolveWorldPrefab();

        if (marker.HasBakedItemLink)
        {
            var target = marker.BakedItemInteraction;
            if (target == null || existing.Length != 1 || existing[0] != target)
            { reason = "Lien baked cassé ou ambigu. Rien n’a été recréé."; return false; }
            if (target.representedItem != marker.Item ||
                (marker.BakedItemSourcePrefab != null && marker.BakedItemSourcePrefab != source))
            { reason = "Le changement de définition ou de modèle exige une migration explicite. L’instance est conservée."; return false; }
            if (!CanPreserveContents(target, category, marker.Item, out reason)) return false;
            if (target.interactableCategory != category)
            {
                Undo.RecordObject(target, "Actualiser l’usage monde");
                WorldPickupUtility.ApplyWorldBinding(target, marker.Item, category);
                RecordChange(target);
            }
            return true;
        }

        if (existing.Length != 0 || root.transform.childCount != 0 || root.GetComponent<NetworkObject>() != null)
        { reason = "Ce marker n’est pas vide. Utilise le raccord assisté pour conserver l’instance existante."; return false; }
        if (HasSiblingWithName(root, root.name))
        { reason = "Donne un nom unique à ce nouveau marker avant de le baker. Un chemin partagé peut modifier les identifiants de scène."; return false; }
        if (source == null || !PrefabUtility.IsPartOfPrefabAsset(source))
        { reason = "Assigne un prefab monde valide sur l’Item."; return false; }
        if (!HasUniqueExplicitId(marker.Item, out reason)) return false;

        var sourceInteractions = source.GetComponentsInChildren<InteractableItem>(true);
        var sourceNetwork = source.GetComponentsInChildren<NetworkObject>(true);
        if (sourceInteractions.Length > 1 || sourceNetwork.Length > 1 ||
            sourceInteractions.Any(i => i.gameObject != source) || sourceNetwork.Any(n => n.gameObject != source))
        { reason = "Plusieurs racines possibles dans le prefab. Prépare une racine d’interaction explicite."; return false; }
        if (sourceInteractions.Length == 1 && !CanPreserveContents(sourceInteractions[0], category, marker.Item, out reason))
            return false;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Baker un Item");
        var instance = PrefabUtility.InstantiatePrefab(source, root.scene) as GameObject;
        if (instance == null) { reason = "Impossible d’instancier le prefab monde."; return false; }
        Undo.RegisterCreatedObjectUndo(instance, "Baker un Item");
        Undo.SetTransformParent(instance.transform, root.transform, "Raccorder le modèle");
        Undo.RecordObject(instance.transform, "Poser le modèle");
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = source.transform.localRotation;
        instance.transform.localScale = source.transform.localScale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

        // An authored network/interaction root remains the only one. A pure visual
        // prefab uses the marker root, as in the original bake.
        GameObject interactionRoot = sourceNetwork.Length != 0 || sourceInteractions.Length != 0 ? instance : root;
        if (!interactionRoot.TryGetComponent(out NetworkObject _)) Undo.AddComponent<NetworkObject>(interactionRoot);
        bool created = !interactionRoot.TryGetComponent(out InteractableItem interaction);
        if (created) interaction = Undo.AddComponent<InteractableItem>(interactionRoot);
        Undo.RecordObjects(new Object[] { interaction, marker }, "Baker un Item");
        WorldPickupUtility.ApplyWorldBinding(interaction, marker.Item, category);
        if (created && category == Category.ReadableItem) interaction.allowTake = false;
        if (interaction.interactionTrigger == null)
        {
            var collider = CharacterInteractionDetection.ResolveInteractionCollider(interaction, null);
            if (collider == null)
            {
                var box = Undo.AddComponent<BoxCollider>(interactionRoot);
                Undo.RecordObject(box, "Dimensionner le collider");
                if (WorldPickupUtility.TryCalculateBounds(instance, out Bounds bounds))
                {
                    var transform = interactionRoot.transform;
                    box.center = transform.InverseTransformPoint(bounds.center);
                    // Convert world AABB conservatively to local size for rotated markers.
                    var x = transform.InverseTransformVector(Vector3.right * bounds.extents.x);
                    var y = transform.InverseTransformVector(Vector3.up * bounds.extents.y);
                    var z = transform.InverseTransformVector(Vector3.forward * bounds.extents.z);
                    box.size = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                        Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2f;
                }
                else box.size = Vector3.one * .1f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(box);
                collider = box;
            }
            interaction.interactionTrigger = collider;
        }
        marker.SetBakedItemLink(interaction, source, marker.Placement);
        RecordChange(interaction);
        RecordChange(marker);
        Undo.CollapseUndoOperations(group);
        return true;
    }

    internal static bool TryAdopt(InteractableItem interaction, bool apply, out string report)
    {
        if (interaction == null || !CanEdit(interaction.gameObject) || interaction.representedItem == null)
        { report = "Sélectionne une instance d’Item existante hors Play, avec son Item représenté."; return false; }
        GameObject root = interaction.gameObject;
        if (root.GetComponentsInChildren<InteractableItem>(true).Length != 1 ||
            root.GetComponentsInParent<InteractableItem>(true).Length != 1)
        { report = "Racine d’interaction ambiguë : aucun lien ajouté."; return false; }
        var marker = root.GetComponent<SceneMarker>();
        Placement placement = interaction.interactableCategory switch
        {
            Category.Container => Placement.Container,
            Category.RecoverableItem => Placement.Recoverable,
            Category.ReadableItem => Placement.FixedDocument,
            _ => Placement.Unspecified
        };
        if (marker != null && (!marker.UsesItem || marker.HasBakedItemLink ||
            marker.Item != interaction.representedItem || (marker.Placement != Placement.Unspecified && marker.Placement != placement)))
        { report = "Ce marker existe déjà avec un autre contrat. Rien n’a été écrasé."; return false; }
        if (!TryCategory(interaction.representedItem, placement, out _, out report) ||
            !CanPreserveContents(interaction, interaction.interactableCategory, interaction.representedItem, out report)) return false;
        report = $"Objet : {root.name}\nItem : {interaction.representedItem.name}\nUsage conservé : {interaction.interactableCategory}\n" +
            $"Identité de scène conservée : {NetcodeSceneIdUtility.GetStableId(root.transform):X8}\n" +
            "Seule modification : ajout ou renseignement du lien SceneMarker. Aucun déplacement, renommage ou changement de contenu.";
        if (!apply) return true;
        if (marker == null) marker = Undo.AddComponent<SceneMarker>(root);
        Undo.RecordObject(marker, "Raccorder un Item existant");
        marker.SetItem(interaction.representedItem);
        marker.SetBakedItemLink(interaction, null, placement);
        RecordChange(marker);
        return true;
    }

    internal static bool HasUniqueExplicitId(Item definition, out string reason)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.itemId))
        { reason = "Un nouveau placement exige un identifiant Item explicite."; return false; }
        foreach (var asset in AllItems())
            if (asset != definition && ItemIdUtils.GetItemId(asset) == definition.itemId)
            { reason = $"L’ID « {definition.itemId} » est aussi utilisé par {AssetDatabase.GetAssetPath(asset)}."; return false; }
        reason = null;
        return true;
    }

    internal static Item[] AllItems() => AssetDatabase.FindAssets("t:Item", new[] { "Assets" })
        .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Item>().ToArray();

    internal static bool CanPreserveContents(InteractableItem interaction, Category category, Item item, out string reason)
    {
        var entries = interaction.storedItems;
        if (category == Category.ReadableItem && entries != null && entries.Count > 0)
        { reason = "Le document fixe contient des données de loot. Elles sont conservées : corrige le conflit manuellement."; return false; }
        if (category == Category.RecoverableItem && item.IsReadable() && entries != null &&
            entries.Any(e => e != null && e.item != null && e.item != item))
        { reason = "Ce document contient d’autres Items. Le bake ne modifiera pas son contenu."; return false; }
        reason = null;
        return true;
    }

    private static bool CanEdit(GameObject root) =>
        !EditorApplication.isPlayingOrWillChangePlaymode && root.scene.IsValid() && !EditorUtility.IsPersistent(root);

    internal static void NameNewMarker(GameObject root)
    {
        string name = root.name;
        int suffix = 1;
        while (HasSiblingWithName(root, root.name)) root.name = $"{name} ({suffix++})";
    }

    private static bool HasSiblingWithName(GameObject root, string name)
    {
        var parent = root.transform.parent;
        if (parent == null) return root.scene.GetRootGameObjects().Any(go => go != root && go.name == name);
        for (int i = 0; i < parent.childCount; i++)
        {
            var sibling = parent.GetChild(i).gameObject;
            if (sibling != root && sibling.name == name) return true;
        }
        return false;
    }

    private static void RecordChange(Component component)
    {
        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorUtility.SetDirty(component);
        EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }

    [MenuItem("Lit/Objets monde/Raccorder la sélection…")]
    private static void AdoptSelected()
    {
        var interaction = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<InteractableItem>() : null;
        if (!TryAdopt(interaction, false, out string report))
        { EditorUtility.DisplayDialog("Raccord non appliqué", report, "Fermer"); return; }
        if (!EditorUtility.DisplayDialog("Aperçu du raccord", report, "Appliquer", "Annuler")) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Raccorder un Item existant");
        if (!TryAdopt(interaction, true, out report)) Debug.LogWarning(report, interaction);
        Undo.CollapseUndoOperations(group);
    }
}
