using TMPro;
using UnityEngine;

/// <summary>Optional label for a selected world interaction, using the common UI prefab.</summary>
[DisallowMultipleComponent]
public sealed class WorldInteractionPrompt : MonoBehaviour
{
    public MonoBehaviour target;
    public string label = "Interagir";
    public Vector3 worldOffset = new Vector3(0, 1.7f, 0);

    private GameObject instance;
    private TMP_Text text;
    public bool IsVisible => instance != null && instance.activeSelf;

    private void LateUpdate()
    {
        var interaction = target != null && target.isActiveAndEnabled ? target as ICharacterDetectedInteractable : null;
        var camera = Camera.main;
        bool visible = interaction != null && camera != null && !InputFocusStack.HasAnyFocus() &&
            !RuntimeOutlineSelectionManager.IsSuspended && RuntimeOutlineSelectionManager.IsActiveInteractable(interaction);
        if (!visible)
        {
            if (instance != null) instance.SetActive(false);
            return;
        }
        if (instance == null)
        {
            instance = WorldInteractionUiSettings.Create(null);
            if (instance == null) return;
            // Preserve the shared prefab's world scale on scaled model roots.
            instance.transform.SetParent(transform, true);
            text = instance.GetComponentInChildren<TMP_Text>(true);
        }
        if (text != null) text.text = label;
        var anchor = interaction.GetInteractionAnchor();
        instance.transform.position = (anchor != null ? anchor.position : target.transform.position) + worldOffset;
        Vector3 direction = instance.transform.position - camera.transform.position;
        if (direction.sqrMagnitude > .0001f) instance.transform.rotation = Quaternion.LookRotation(direction, camera.transform.up);
        instance.SetActive(true);
    }

    private void OnDisable()
    {
        if (instance != null)
        {
            instance.SetActive(false);
            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
        }
        instance = null;
        text = null;
    }
}
