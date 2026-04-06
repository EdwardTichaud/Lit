using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Authoring simple pour poser dans une scene un item recuperable.
[DisallowMultipleComponent]
[AddComponentMenu("Lit/World Pickup Authoring")]
public class WorldPickupAuthoring : MonoBehaviour
{
    [Header("Pickup")]
    [Tooltip("Item donne au joueur quand il le recupere.")]
    public Item item;
    [Min(1)]
    [Tooltip("Quantite donnee lors de la recuperation.")]
    public int quantity = 1;
    [Tooltip("Item affiche dans le panneau local (laisser vide pour reutiliser l'item ramasse).")]
    public Item displayItemOverride;
    [Tooltip("Le pickup disparait lorsqu'il est vide.")]
    public bool destroyWhenEmpty = true;
    [Tooltip("Autorise la recuperation par le joueur.")]
    public bool collectable = true;

    [Header("Interaction")]
    [Tooltip("Trigger d'interaction optionnel. Si vide, un BoxCollider trigger est cree automatiquement.")]
    public Collider interactionTrigger;

    [Header("Editor")]
    [Tooltip("Applique automatiquement la configuration dans l'editeur.")]
    public bool autoApplyInEditor = true;

#if UNITY_EDITOR
    private bool editorApplyQueued;
#endif

    private void Awake()
    {
        ApplyPickupSetup();
    }

    private void Reset()
    {
        quantity = 1;
        destroyWhenEmpty = true;
        collectable = true;
#if UNITY_EDITOR
        QueueEditorApply();
#endif
    }

    [ContextMenu("Apply Pickup Setup")]
    public void ApplyPickupSetup()
    {
        InteractableItem container = WorldPickupUtility.EnsurePickupInfrastructure(gameObject);
        if (container == null)
        {
            return;
        }

        if (item == null)
        {
            if (interactionTrigger == null)
            {
                interactionTrigger = container.interactionTrigger != null
                    ? container.interactionTrigger
                    : WorldPickupUtility.EnsureTriggerCollider(gameObject);
            }

            if (interactionTrigger != null)
            {
                container.interactionTrigger = interactionTrigger;
            }

            return;
        }

        WorldPickupUtility.ConfigureLootContainer(
            container,
            item,
            quantity,
            destroyWhenEmpty,
            collectable,
            displayItemOverride != null ? displayItemOverride : item,
            interactionTrigger);

        interactionTrigger = container.interactionTrigger;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying || !autoApplyInEditor)
        {
            return;
        }

        QueueEditorApply();
    }

    private void QueueEditorApply()
    {
        if (editorApplyQueued)
        {
            return;
        }

        editorApplyQueued = true;
        EditorApplication.delayCall += ApplyQueuedEditorSetup;
    }

    private void ApplyQueuedEditorSetup()
    {
        editorApplyQueued = false;
        if (this == null)
        {
            return;
        }

        ApplyPickupSetup();
        EditorUtility.SetDirty(this);
        EditorUtility.SetDirty(gameObject);
        if (interactionTrigger != null)
        {
            EditorUtility.SetDirty(interactionTrigger);
        }

        if (gameObject.scene.IsValid() && !EditorUtility.IsPersistent(gameObject))
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }
#endif
}
