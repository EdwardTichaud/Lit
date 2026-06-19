using UnityEngine;
using TMPro;

// Cible d'affichage: affiche l'annee canonique diffusee par AncientFlameDisplayManager.
[DisallowMultipleComponent]
public class AncientFlameYearDisplay : MonoBehaviour, IAncientFlameDisplayTarget
{
    [Header("References")]
    [Tooltip("Texte cible (TMP).")]
    public TMP_Text textTarget;

    [Header("Format")]
    [Tooltip("Prefixe affiche avant l'annee.")]
    public string prefix = "An ";
    [Tooltip("Suffixe affiche apres l'annee.")]
    public string suffix = "";
    [Tooltip("Affiche le nombre de flames allumes.")]
    public bool includeLitCount = false;
    [Tooltip("Format additionnel pour le nombre allume.")]
    public string litCountFormat = " ({0} allumes)";

    private void OnEnable()
    {
        ResolveTextTarget();
        AncientFlameDisplayManager.Register(this);
    }

    private void OnDisable()
    {
        AncientFlameDisplayManager.Unregister(this);
    }

    public void ApplyAncientFlameDisplay(AncientFlameDisplaySnapshot snapshot)
    {
        UpdateText(snapshot.CurrentYear, snapshot.LitCount);
    }

    public void UpdateText()
    {
        ApplyAncientFlameDisplay(AncientFlameDisplayManager.GetCurrentSnapshot());
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
