using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    private static DamagePopupManager _instance;

    /// <summary>
    /// Accès global au gestionnaire. Cherche automatiquement une instance
    /// existante dans la scène si aucune n'a encore été assignée.
    /// </summary>
    public static DamagePopupManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<DamagePopupManager>(); // Migration vers la nouvelle API de recherche d'objets
            return _instance;
        }
    }

    [SerializeField] private GameObject damagePopupPrefab;
    [SerializeField] private Camera battleCamera;
    [SerializeField] private int prewarmCount = 10;
    private Canvas localCanvas;

    private TMP_FontAsset popupFont;
    private float popupFontSize = 150f;
    private float popupTextScale = 1f;
    private Vector3 popupOffset = new Vector3(0f, 2f, 0f);
    private bool popupOffsetUsesCameraAxes = false;
    private bool usePooling = true;

    [Header("Couleurs")]
    [SerializeField] private Color damageColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color healColor = new Color(0.35f, 1f, 0.55f, 1f);
    [SerializeField] private Color buffColor = new Color(0.4f, 0.75f, 1f, 1f);
    [SerializeField] private Color debuffColor = new Color(1f, 0.6f, 0.2f, 1f);
    [SerializeField] private Color statusColor = new Color(1f, 0.85f, 0.45f, 1f);
    [Header("Interception")]
    [SerializeField] private Color interceptionPositiveColor = new Color(0.3f, 1f, 0.4f, 1f);
    [SerializeField] private Color interceptionNegativeColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private float interceptionPopupDuration = 2f;

    private readonly Queue<DamagePopup> popupPool = new();

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            // Évite les doublons au démarrage
            Destroy(gameObject);
            return;
        }

        // S'assure que l'objet ne possède pas d'échelle étrange héritée d'un prefab
        transform.localScale = Vector3.one;

        localCanvas = GetComponent<Canvas>();
        if (localCanvas != null)
        {
            Canvas parentCanvas = ResolveParentCanvas(localCanvas);
            if (parentCanvas != null && parentCanvas.renderMode == RenderMode.WorldSpace)
                localCanvas.enabled = false;
        }

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

        if (battleCamera == null)
            battleCamera = Camera.main;
    }

    private Canvas ResolveParentCanvas(Canvas local)
    {
        Canvas[] canvases = GetComponentsInParent<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || canvas == local)
                continue;
            if (!canvas.enabled)
                continue;
            return canvas;
        }

        return null;
    }


    private void PrewarmPool()
    {
        if (!usePooling || damagePopupPrefab == null || prewarmCount <= 0)
            return;

        for (int i = popupPool.Count; i < prewarmCount; i++)
        {
            DamagePopup popup = CreatePopupInstance();
            if (popup == null)
                break;

            popup.gameObject.SetActive(false);
            popupPool.Enqueue(popup);
        }
    }

    private DamagePopup CreatePopupInstance()
    {
        if (damagePopupPrefab == null)
            return null;

        GameObject popupObject = Instantiate(damagePopupPrefab, transform, false);
        DamagePopup popupScript = popupObject.GetComponent<DamagePopup>();
        if (popupScript == null)
        {
            Debug.LogError("[DamagePopupManager] Le prefab ne contient pas de composant DamagePopup.");
            Destroy(popupObject);
            return null;
        }

        popupScript.SetOwner(this);
        popupObject.SetActive(false);
        return popupScript;
    }

    private DamagePopup GetPopup()
    {
        if (!usePooling)
            return CreatePopupInstance();

        if (popupPool.Count > 0)
            return popupPool.Dequeue();

        return CreatePopupInstance();
    }

    internal void ApplyStyle(DamagePopup popup)
    {
        if (popup == null)
            return;

        float safeFontSize = Mathf.Max(0.1f, popupFontSize);
        float safeScale = Mathf.Max(0.001f, popupTextScale);
        popup.ApplyStyle(popupFont, safeFontSize, safeScale, popupOffset, popupOffsetUsesCameraAxes);
    }

    public void SetDisplaySettings(TMP_FontAsset font, float fontSize, float textScale, Vector3 offset, bool offsetUsesCameraAxes, bool poolingEnabled)
    {
        popupFont = font;
        popupFontSize = fontSize;
        popupTextScale = textScale;
        popupOffset = offset;
        popupOffsetUsesCameraAxes = offsetUsesCameraAxes;
        usePooling = poolingEnabled;

        if (!usePooling && popupPool.Count > 0)
        {
            while (popupPool.Count > 0)
            {
                var pooledPopup = popupPool.Dequeue();
                if (pooledPopup != null)
                    Destroy(pooledPopup.gameObject);
            }
        }
    }

    public void SetColorSettings(Color damage, Color heal, Color buff, Color debuff, Color status, Color interceptionPositive, Color interceptionNegative)
    {
        damageColor = damage;
        healColor = heal;
        buffColor = buff;
        debuffColor = debuff;
        statusColor = status;
        interceptionPositiveColor = interceptionPositive;
        interceptionNegativeColor = interceptionNegative;
    }

    public void ReleasePopup(DamagePopup popup)
    {
        if (popup == null)
            return;

        if (!usePooling)
        {
            Destroy(popup.gameObject);
            return;
        }

        popup.gameObject.SetActive(false);
        popup.transform.SetParent(transform, false);
        popupPool.Enqueue(popup);
    }

    /// <summary>
    /// Crée un popup de dégâts au-dessus de la cible.
    /// </summary>
    /// <param name="target">Transform à suivre.</param>
    /// <param name="amount">Montant de dégâts.</param>
    public void ShowDamage(Transform target, int amount)
    {
        if (amount <= 0)
            return;

        ShowPopup(target, amount.ToString(), damageColor);
    }

    public void ShowHeal(Transform target, int amount)
    {
        int rounded = Mathf.RoundToInt(Mathf.Abs(amount));
        if (rounded <= 0)
            return;

        ShowPopup(target, "+" + rounded, healColor);
    }

    public void ShowBuff(Transform target, BuffStatType stat, int amount, bool isPercentage)
    {
        if (amount == 0 || stat == BuffStatType.None)
            return;

        string value = Mathf.Abs(amount).ToString();
        if (isPercentage)
            value += "%";

        string text = $"+{value} {FormatStatName(stat)}";
        ShowPopup(target, text, buffColor);
    }

    public void ShowDebuff(Transform target, DebuffStatType stat, int amount, bool isPercentage)
    {
        if (amount == 0 || stat == DebuffStatType.None)
            return;

        string value = Mathf.Abs(amount).ToString();
        if (isPercentage)
            value += "%";

        string text = $"-{value} {FormatStatName((BuffStatType)stat)}";
        ShowPopup(target, text, debuffColor);
    }

    public void ShowStatus(Transform target, string label, bool isPositive)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        ShowPopup(target, label, isPositive ? buffColor : statusColor);
    }

    public void ShowInterceptionOutcome(Transform target, string label, bool isPositive)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        Color color = isPositive ? interceptionPositiveColor : interceptionNegativeColor;
        float duration = Mathf.Max(0.1f, interceptionPopupDuration);
        ShowPopup(target, label, color, duration);
    }

    private void ShowPopup(Transform target, string text, Color color, float durationOverride = -1f)
    {
        if (damagePopupPrefab == null)
        {
            Debug.LogWarning("[DamagePopupManager] Prefab manquant.");
            return;
        }

        if (target == null)
            return;

        ResolveBattleCamera();

        DamagePopup popupScript = GetPopup();
        if (popupScript == null)
            return;

        popupScript.transform.SetParent(transform, false);
        popupScript.gameObject.SetActive(true);
        popupScript.SetOwner(this);
        popupScript.Initialize(text, target, battleCamera, color, durationOverride);
    }

    private static string FormatStatName(BuffStatType stat)
    {
        return stat switch
        {
            BuffStatType.Strength => "Force",
            BuffStatType.Defense => "Defense",
            BuffStatType.Initiative => "Initiative",
            BuffStatType.MaxHP => "PV max",
            BuffStatType.Power => "Puissance",
            BuffStatType.CriticalRate => "Critique",
            _ => "Stat"
        };
    }
}
