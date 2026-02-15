using UnityEngine;

// Point d'ancrage pour afficher un feedback de skill check.
public class SkillCheckFeedbackAnchor : MonoBehaviour
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

    public void Show(Skill skill, int roll, int modifier, int total, bool success)
    {
        if (feedbackPrefab == null)
        {
            return;
        }

        // Instancie le feedback et l'initialise si possible.
        Transform parent = feedbackParent != null ? feedbackParent : null;
        Vector3 position = transform.position + offset;
        GameObject instance = Instantiate(feedbackPrefab, position, Quaternion.identity, parent);

        SkillCheckFeedback feedback = instance.GetComponent<SkillCheckFeedback>();
        if (feedback != null)
        {
            feedback.Initialize(skill, roll, modifier, total, success);
            return;
        }

        if (fallbackLifetime > 0f)
        {
            Destroy(instance, fallbackLifetime);
        }
    }
}
