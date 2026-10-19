using UnityEngine;

// Existing prefab references are preserved through the script GUID.
public class AbilityFeedbackAnchor : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab de feedback a instancier.")]
    public GameObject feedbackPrefab;
    [Tooltip("Offset world par rapport a l'ancre.")]
    public Vector3 offset = new Vector3(0f, 2.1f, 0f);
    [Tooltip("Parent pour instancier le feedback.")]
    public Transform feedbackParent;
    [Tooltip("Duree de vie fallback si aucun script n'est trouve.")]
    public float fallbackLifetime = 1.6f;

    public void Show(AbilitySO ability, bool success)
    {
        if (feedbackPrefab == null)
        {
            return;
        }

        // Instancie le feedback et l'initialise si possible.
        Transform parent = feedbackParent != null ? feedbackParent : null;
        Vector3 position = transform.position + offset;
        GameObject instance = Instantiate(feedbackPrefab, position, Quaternion.identity, parent);

        AbilityFeedback feedback = instance.GetComponent<AbilityFeedback>();
        if (feedback != null)
        {
            feedback.Initialize(ability, success);
            return;
        }

        if (fallbackLifetime > 0f)
        {
            Destroy(instance, fallbackLifetime);
        }
    }
}
