using UnityEngine;
using TMPro;

/// <summary>
/// Popup visuel indiquant un gain d'harmonique au-dessus d'une unité.
/// Fonctionne sur le même principe que <see cref="DamagePopup"/>.
/// </summary>
public class AddHarmonicPopup : MonoBehaviour
{
    [Header("Animation")]
    public float floatSpeed = 1f;      // Vitesse d'ascension
    public float duration = 1.5f;      // Durée avant disparition
    public Vector3 offset = new(0, 2f, 0); // Décalage initial par rapport à l'unité

    [Header("Références")]
    public TextMeshProUGUI textMesh;   // Référence au texte affiché

    private float elapsed = 0f;        // Temps écoulé depuis l'apparition
    private float floatOffset = 0f;    // Décalage vertical progressif
    private Camera battleCamera;            // Caméra utilisée pour la conversion Monde/Ecran
    private CanvasGroup canvasGroup;   // Pour faire disparaître progressivement le popup
    private Transform target;          // Unité suivie
    private AddHarmonicPopupManager owner;

    public void SetOwner(AddHarmonicPopupManager manager)
    {
        owner = manager;
    }

    /// <summary>
    /// Initialise le popup avec la quantité d'harmonique gagnée et la cible à suivre.
    /// </summary>
    /// <param name="amount">Montant à afficher (typiquement +1).</param>
    /// <param name="followTarget">Transform de l'unité concernée.</param>
    public void Initialize(int amount, Transform followTarget, Camera cameraOverride)
    {
        // Récupère automatiquement la référence du texte si elle n'a pas été assignée dans l'inspecteur.
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TextMeshProUGUI>();
            if (textMesh == null)
            {
                // Impossible d'afficher le popup sans texte, on log l'erreur et on quitte.
                Debug.LogError("[AddHarmonicPopup] Aucun TextMeshProUGUI trouvé sur le prefab.");
                Release();
                return;
            }
        }

        textMesh.text = "+" + amount.ToString();

        if (cameraOverride != null)
        {
            battleCamera = cameraOverride;
        }
        else
        {
            // Dans certaines scènes de tests simplifiées, la BattleCamera n'est pas instanciée :
            // on sécurise donc sa récupération pour éviter une NullReference qui crasherait le popup.
            GameObject battleCameraGO = GameObject.FindGameObjectWithTag("BattleCamera");
            if (battleCameraGO == null)
            {
                Debug.LogWarning("[AddHarmonicPopup] Aucune BattleCamera trouvée : initialisation du popup annulée pour cette scène simplifiée.");
                Release();
                return;
            }

            // La caméra est indispensable pour convertir la position monde en position écran du popup.
            battleCamera = battleCameraGO.GetComponent<Camera>();
            if (battleCamera == null)
            {
                Debug.LogWarning("[AddHarmonicPopup] Le GameObject BattleCamera n'a pas de composant Camera : initialisation du popup annulée.");
                Release();
                return;
            }
        }

        canvasGroup ??= GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        target = followTarget;
        elapsed = 0f;
        floatOffset = 0f;
        UpdatePosition();
    }

    void Update()
    {
        // Utilise le temps non-scalé afin que l'animation reste fluide même lorsque le combat est ralenti ou en pause.
        floatOffset += floatSpeed * Time.unscaledDeltaTime;
        UpdatePosition();

        // Même logique pour le fondu : on ne dépend plus du Time.timeScale global.
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= duration)
        {
            // Fondu puis destruction
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, (elapsed - duration) / duration);
            if (canvasGroup.alpha <= 0f)
                Release();
        }
    }

    /// <summary>
    /// Met à jour la position écran en fonction de la cible suivie.
    /// </summary>
    private void UpdatePosition()
    {
        if (battleCamera == null || target == null)
            return;

        Vector3 worldPos = target.position + offset + Vector3.up * floatOffset;
        Vector3 screenPos = battleCamera.WorldToScreenPoint(worldPos);
        screenPos.x = Mathf.Clamp(screenPos.x, 0f, Screen.width);
        screenPos.y = Mathf.Clamp(screenPos.y, 0f, Screen.height);
        transform.position = screenPos;
    }

    private void Release()
    {
        if (owner != null)
            owner.ReleasePopup(this);
        else
            Destroy(gameObject);
    }
}
