using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Affiche une jauge circulaire permettant de passer le tour.
/// </summary>
public class PassTurnUI : MonoBehaviour
{
    public static PassTurnUI Instance { get; private set; }

    [Header("UI Elements")]
    public Image progressImage;
    public TextMeshProUGUI label;

    [Header("Settings")]
    [Tooltip("Vitesse à laquelle la jauge revient à zéro lorsqu'aucune entrée n'est détectée.")]
    public float resetSpeed = 2f;

    private Coroutine resetRoutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Réinitialise l'instance statique lorsque cet objet est détruit.
    /// Cela évite de conserver une référence vers un objet Unity détruit
    /// qui provoquerait ensuite des MissingReferenceException.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        Hide();
    }

    /// <summary>
    /// Affiche la jauge et réinitialise l'avancement.
    /// </summary>
    public void Show()
    {
        transform.GetChild(0).gameObject.SetActive(true);
        SetProgress(0f);
    }

    /// <summary>
    /// Masque la jauge.
    /// </summary>
    public void Hide()
    {
        transform.GetChild(0).gameObject.SetActive(false);
    }

    /// <summary>
    /// Met à jour la progression entre 0 et 1.
    /// </summary>
    public void SetProgress(float t)
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }
        if (progressImage != null)
            progressImage.fillAmount = Mathf.Clamp01(t);
    }

    /// <summary>
    /// Lance la diminution progressive de la jauge jusqu'à 0.
    /// </summary>
    public void ResetProgressSmooth()
    {
        if (resetRoutine != null)
            StopCoroutine(resetRoutine);

        resetRoutine = StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        while (progressImage != null && progressImage.fillAmount > 0f)
        {
            progressImage.fillAmount = Mathf.Max(0f, progressImage.fillAmount - Time.unscaledDeltaTime * resetSpeed);
            yield return null;
        }
        resetRoutine = null;
    }
}
