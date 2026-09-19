using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared presentation for world interaction labels.</summary>
[CreateAssetMenu(menuName = "Lit/UI/World Interaction Settings")]
public sealed class WorldInteractionUiSettings : ScriptableObject
{
    [SerializeField] private GameObject interactionBoxPrefab;

    public static GameObject Create(Transform parent, GameObject legacyLabelSource = null)
    {
        var settings = Resources.Load<WorldInteractionUiSettings>("WorldInteractionUiSettings");
        if (settings == null || settings.interactionBoxPrefab == null)
        {
            Debug.LogError("WorldInteractionUiSettings requires the shared UI_World_InteractionBox prefab.");
            return null;
        }

        GameObject instance = Instantiate(settings.interactionBoxPrefab, parent, false);
        // Old references supply only their label; presentation comes from the shared prefab.
        TMP_Text label = instance.GetComponentInChildren<TMP_Text>(true);
        if (label != null && legacyLabelSource != null)
        {
            TMP_Text previous = legacyLabelSource.GetComponentInChildren<TMP_Text>(true);
            Text previousLegacy = legacyLabelSource.GetComponentInChildren<Text>(true);
            string text = previous != null ? previous.text : previousLegacy != null ? previousLegacy.text : null;
            if (!string.IsNullOrWhiteSpace(text))
                label.text = text;
        }

        CanvasGroup group = instance.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        instance.SetActive(true);
        return instance;
    }
}
