using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire responsable d'afficher les popups d'ajout d'harmonique.
/// Inspiré du <see cref="DamagePopupManager"/>.
/// </summary>
public class AddHarmonicPopupManager : MonoBehaviour
{
    private static AddHarmonicPopupManager _instance;

    /// <summary>
    /// Accès global au gestionnaire. Recherche automatiquement une instance dans la scène si nécessaire.
    /// </summary>
    public static AddHarmonicPopupManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<AddHarmonicPopupManager>(); // Utilise la nouvelle API; l'ancienne méthode FindObjectOfType est obsolète
            return _instance;
        }
    }

    [SerializeField] private GameObject addHarmonicPrefab;
    [SerializeField] private Camera battleCamera;
    [SerializeField] private int prewarmCount = 10;

    private readonly Queue<AddHarmonicPopup> popupPool = new();

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        transform.localScale = Vector3.one; // S'assure d'une échelle correcte

        ResolveBattleCamera();
        PrewarmPool();
    }

    private void ResolveBattleCamera()
    {
        if (battleCamera != null)
            return;

        GameObject battleCameraGO = GameObject.FindGameObjectWithTag("BattleCamera");
        if (battleCameraGO != null)
            battleCamera = battleCameraGO.GetComponent<Camera>();
    }

    private void PrewarmPool()
    {
        if (addHarmonicPrefab == null || prewarmCount <= 0)
            return;

        for (int i = popupPool.Count; i < prewarmCount; i++)
        {
            AddHarmonicPopup popup = CreatePopupInstance();
            if (popup == null)
                break;

            popup.gameObject.SetActive(false);
            popupPool.Enqueue(popup);
        }
    }

    private AddHarmonicPopup CreatePopupInstance()
    {
        if (addHarmonicPrefab == null)
            return null;

        GameObject popupObject = Instantiate(addHarmonicPrefab, transform, true);
        AddHarmonicPopup popupScript = popupObject.GetComponent<AddHarmonicPopup>();
        if (popupScript == null)
        {
            Debug.LogError("[AddHarmonicPopupManager] Le prefab ne contient pas de composant AddHarmonicPopup.");
            Destroy(popupObject);
            return null;
        }

        popupScript.SetOwner(this);
        popupObject.SetActive(false);
        return popupScript;
    }

    private AddHarmonicPopup GetPopup()
    {
        if (popupPool.Count > 0)
            return popupPool.Dequeue();

        return CreatePopupInstance();
    }

    public void ReleasePopup(AddHarmonicPopup popup)
    {
        if (popup == null)
            return;

        popup.gameObject.SetActive(false);
        popup.transform.SetParent(transform, true);
        popupPool.Enqueue(popup);
    }

    /// <summary>
    /// Crée un popup de gain d'harmonique au-dessus de la cible.
    /// </summary>
    /// <param name="target">Transform de l'unité concernée.</param>
    /// <param name="amount">Montant d'harmonique à afficher.</param>
    public void ShowAddHarmonic(Transform target, int amount)
    {
        if (addHarmonicPrefab == null)
        {
            Debug.LogWarning("[AddHarmonicPopupManager] Prefab manquant.");
            return;
        }

        ResolveBattleCamera();

        AddHarmonicPopup popupScript = GetPopup();
        if (popupScript == null)
            return;

        popupScript.transform.SetParent(transform, true);
        popupScript.gameObject.SetActive(true);
        popupScript.SetOwner(this);
        popupScript.Initialize(amount, target, battleCamera);
    }
}
