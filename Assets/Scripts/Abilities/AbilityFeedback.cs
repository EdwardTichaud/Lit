using TMPro;
using UnityEngine;

// Exploration ability feedback; no dice, statistics or difficulty class.
public class AbilityFeedback : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Texte UI (ScreenSpace).")]
    public TextMeshProUGUI textUi;
    [Tooltip("Texte world (WorldSpace).")]
    public TextMeshPro textWorld;

    [Header("Style")]
    [Tooltip("Couleur en cas de reussite.")]
    public Color successColor = Color.white;
    [Tooltip("Couleur en cas d'echec.")]
    public Color failureColor = new Color(1f, 0.3f, 0.3f, 1f);
    [Tooltip("Duree de vie du feedback (0 = infini).")]
    public float lifetime = 1.6f;

    private void Awake()
    {
        if (textUi == null)
        {
            textUi = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (textWorld == null)
        {
            textWorld = GetComponentInChildren<TextMeshPro>(true);
        }
    }

    public void Initialize(AbilitySO ability, bool success)
    {
        string text = ability != null ? ability.DisplayName : "Capacite";
        if (!success) text += " : non equipee";
        ApplyText(text, success ? successColor : failureColor);

        if (lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void ApplyText(string text, Color color)
    {
        if (textUi != null)
        {
            textUi.text = text;
            textUi.color = color;
        }

        if (textWorld != null)
        {
            textWorld.text = text;
            textWorld.color = color;
        }
    }
}
