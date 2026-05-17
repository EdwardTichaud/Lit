using UnityEngine;
using TMPro;

// Cible d'affichage: affiche l'annee canonique diffusee par BraseroDisplayManager.
[DisallowMultipleComponent]
public class BraseroYearDisplay : MonoBehaviour, IBraseroDisplayTarget
{
    [Header("References")]
    [Tooltip("Texte cible (TMP).")]
    public TMP_Text textTarget;

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
        ResolveTextTarget();
        BraseroDisplayManager.Register(this);
    }

    private void OnDisable()
    {
        BraseroDisplayManager.Unregister(this);
    }

    public void ApplyBraseroDisplay(BraseroDisplaySnapshot snapshot)
    {
        UpdateText(snapshot.CurrentYear, snapshot.LitCount);
    }

    public void UpdateText()
    {
        ApplyBraseroDisplay(BraseroDisplayManager.GetCurrentSnapshot());
    }

    private void ResolveTextTarget()
    {
        if (textTarget == null)
        {
            textTarget = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void UpdateText(int year, int litCount)
    {
        ResolveTextTarget();
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
            ResolveTextTarget();
            UpdateText();
        }
    }
#endif
}
