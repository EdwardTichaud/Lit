using UnityEngine;
using TMPro;

// Affiche l'annee courante calculee par le BraseroTimeManager.
[DisallowMultipleComponent]
public class BraseroYearDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Manager des braseros.")]
    public BraseroTimeManager timeManager;
    [Tooltip("Texte cible (TMP).")]
    public TMP_Text textTarget;
    [Tooltip("Cherche automatiquement un manager si non assigne.")]
    public bool autoFindManager = true;

    [Header("Format")]
    [Tooltip("Prefixe affiche avant l'annee.")]
    public string prefix = "An ";
    [Tooltip("Suffixe affiche apres l'annee.")]
    public string suffix = "";
    [Tooltip("Affiche le nombre de braseros allumes.")]
    public bool includeLitCount = false;
    [Tooltip("Format additionnel pour le nombre allume.")]
    public string litCountFormat = " ({0} allumes)";

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        UpdateText();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (textTarget == null)
        {
            textTarget = GetComponentInChildren<TMP_Text>(true);
        }

        if (timeManager == null && autoFindManager)
        {
            timeManager = FindObjectOfType<BraseroTimeManager>();
        }
    }

    private void Subscribe()
    {
        if (timeManager == null)
        {
            return;
        }

        timeManager.TimeChanged += OnTimeChanged;
    }

    private void Unsubscribe()
    {
        if (timeManager == null)
        {
            return;
        }

        timeManager.TimeChanged -= OnTimeChanged;
    }

    private void OnTimeChanged(int year, int litCount)
    {
        UpdateText(year, litCount);
    }

    public void UpdateText()
    {
        if (timeManager == null)
        {
            UpdateText(0, 0);
            return;
        }

        UpdateText(timeManager.CurrentYear, timeManager.LitCount);
    }

    private void UpdateText(int year, int litCount)
    {
        if (textTarget == null)
        {
            return;
        }

        string text = prefix + year + suffix;
        if (includeLitCount)
        {
            text += string.Format(litCountFormat, litCount);
        }

        textTarget.text = text;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ResolveReferences();
            UpdateText();
        }
    }
#endif
}
