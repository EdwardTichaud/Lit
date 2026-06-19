using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Controle l'affichage des charges de Munin sur une UI deja presente dans la scene.
[DisallowMultipleComponent]
public class MuninUI : MonoBehaviour
{
    [Header("Behaviour")]
    [SerializeField, Tooltip("Masque l'UI quand aucun Munin local n'est resolu.")]
    private bool hideWhenNoMunin = true;
    [SerializeField, Tooltip("Masque l'UI si les charges sont desactivees sur Munin.")]
    private bool hideWhenChargesDisabled = true;
    [SerializeField, Min(0.02f), Tooltip("Intervalle de resolution du personnage local.")]
    private float resolveInterval = 0.2f;

    [Header("Display")]
    [SerializeField, Tooltip("Format du texte. {0}=charges restantes, {1}=charges max.")]
    private string chargesFormat = "Munin {0}/{1}";
    [SerializeField, Tooltip("Texte affiche si les charges sont desactivees.")]
    private string disabledChargesText = "Munin --";
    [SerializeField, Tooltip("Couleur du texte quand il reste des charges.")]
    private Color textColor = new Color(0.92f, 0.94f, 1f, 1f);
    [SerializeField, Tooltip("Couleur du texte quand il ne reste plus de charge.")]
    private Color emptyTextColor = new Color(1f, 0.42f, 0.34f, 1f);
    [SerializeField, Tooltip("Couleur du flash quand une action est refusee.")]
    private Color rejectedTextColor = new Color(1f, 0.86f, 0.28f, 1f);
    [SerializeField, Tooltip("Couleur de fond du bloc UI.")]
    private Color backgroundColor = new Color(0.03f, 0.035f, 0.05f, 0.72f);
    [SerializeField, Tooltip("Couleur de la barre de charges.")]
    private Color fillColor = new Color(0.48f, 0.66f, 1f, 1f);
    [SerializeField, Tooltip("Couleur de la barre quand elle est vide.")]
    private Color emptyFillColor = new Color(0.62f, 0.18f, 0.14f, 1f);
    [SerializeField, Tooltip("Couleur de flash de la barre quand une action est refusee.")]
    private Color rejectedFillColor = new Color(1f, 0.66f, 0.08f, 1f);
    [SerializeField, Tooltip("Couleur discrete lors d'une recharge liee a la memoire.")]
    private Color rewardedColor = new Color(0.56f, 0.86f, 1f, 1f);
    [SerializeField, Tooltip("Couleur discrete lors d'une depense de charge.")]
    private Color spentColor = new Color(0.82f, 0.72f, 1f, 1f);
    [SerializeField, Min(0.01f), Tooltip("Duree du flash quand une action est refusee.")]
    private float rejectedFlashDuration = 0.35f;
    [SerializeField, Min(0.01f), Tooltip("Duree du feedback doux de gain ou de depense.")]
    private float chargeChangeFlashDuration = 0.3f;

    [Header("References")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private TMP_Text chargesText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private ShakeUI rejectedChargeShake;

    private MuninController boundMunin;
    private Transform boundCharacter;
    private float nextResolveTime;
    private float rejectedFlashRemaining;
    private float rewardedFlashRemaining;
    private float spentFlashRemaining;

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveAssignedReferences();
    }

    private void OnValidate()
    {
        ResolveAssignedReferences();
    }
#endif

    private void Awake()
    {
        ResolveAssignedReferences();
        ResolveLocalMunin();
        Refresh();
    }

    private void OnEnable()
    {
        LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;
        ResolveLocalMunin();
        Refresh();
    }

    private void OnDisable()
    {
        LocalPlayerContext.LocalCharacterChanged -= OnLocalCharacterChanged;
        BindMunin(null, null);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextResolveTime)
        {
            nextResolveTime = Time.unscaledTime + Mathf.Max(0.02f, resolveInterval);
            ResolveLocalMunin();
        }

        if (rejectedFlashRemaining > 0f)
        {
            rejectedFlashRemaining = Mathf.Max(0f, rejectedFlashRemaining - Time.unscaledDeltaTime);
            ApplyColors();
        }

        if (rewardedFlashRemaining > 0f || spentFlashRemaining > 0f)
        {
            rewardedFlashRemaining = Mathf.Max(0f, rewardedFlashRemaining - Time.unscaledDeltaTime);
            spentFlashRemaining = Mathf.Max(0f, spentFlashRemaining - Time.unscaledDeltaTime);
            ApplyColors();
        }
    }

    private void OnLocalCharacterChanged(Transform characterRoot)
    {
        ResolveLocalMunin();
        Refresh();
    }

    private void ResolveLocalMunin()
    {
        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        Transform characterRoot = controlled != null ? controlled.transform : null;
        if (characterRoot == boundCharacter && boundMunin != null)
        {
            return;
        }

        MuninController munin = controlled != null
            ? controlled.GetComponentInChildren<MuninController>(true)
            : null;
        BindMunin(characterRoot, munin);
    }

    private void BindMunin(Transform characterRoot, MuninController munin)
    {
        if (boundMunin == munin && boundCharacter == characterRoot)
        {
            return;
        }

        if (boundMunin != null)
        {
            boundMunin.ChargesChanged -= OnMuninChargesChanged;
            boundMunin.ChargeUseRejected -= OnMuninChargeUseRejected;
            boundMunin.ChargesSpent -= OnMuninChargesSpent;
            boundMunin.ChargeRewardReceived -= OnMuninChargeRewardReceived;
        }

        boundCharacter = characterRoot;
        boundMunin = munin;
        rejectedFlashRemaining = 0f;
        rewardedFlashRemaining = 0f;
        spentFlashRemaining = 0f;

        if (boundMunin != null)
        {
            boundMunin.ChargesChanged += OnMuninChargesChanged;
            boundMunin.ChargeUseRejected += OnMuninChargeUseRejected;
            boundMunin.ChargesSpent += OnMuninChargesSpent;
            boundMunin.ChargeRewardReceived += OnMuninChargeRewardReceived;
        }

        Refresh();
    }

    private void OnMuninChargesChanged(MuninController munin, int current, int max)
    {
        Refresh();
    }

    private void OnMuninChargeUseRejected(MuninController munin)
    {
        rejectedFlashRemaining = Mathf.Max(rejectedFlashRemaining, rejectedFlashDuration);
        if (rejectedChargeShake != null)
        {
            rejectedChargeShake.Shake();
        }

        Refresh();
    }

    private void OnMuninChargesSpent(MuninController munin, int amount)
    {
        spentFlashRemaining = Mathf.Max(spentFlashRemaining, chargeChangeFlashDuration);
        Refresh();
    }

    private void OnMuninChargeRewardReceived(MuninController munin, int amount, string reason)
    {
        rewardedFlashRemaining = Mathf.Max(rewardedFlashRemaining, chargeChangeFlashDuration);
        Refresh();
    }

    private void Refresh()
    {
        ResolveAssignedReferences();

        bool hasMunin = boundMunin != null;
        bool chargesDisabled = hasMunin && !boundMunin.ChargesEnabled;
        bool visible = hasMunin || !hideWhenNoMunin;
        if (chargesDisabled && hideWhenChargesDisabled)
        {
            visible = false;
        }

        SetVisible(visible);
        if (!visible)
        {
            return;
        }

        int current = hasMunin ? boundMunin.ChargesRemaining : 0;
        int max = hasMunin ? boundMunin.MaxCharges : 0;
        if (chargesText != null)
        {
            chargesText.text = chargesDisabled
                ? disabledChargesText
                : string.Format(chargesFormat, current, max);
        }

        ApplyFill(current, max, chargesDisabled);
        ApplyColors();
    }

    private void SetVisible(bool visible)
    {
        if (rootGroup != null)
        {
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }
    }

    private void ApplyFill(int current, int max, bool chargesDisabled)
    {
        if (fillImage == null)
        {
            return;
        }

        float ratio = chargesDisabled ? 1f : (max > 0 ? Mathf.Clamp01((float)current / max) : 0f);
        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(ratio, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    private void ApplyColors()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }

        bool rejectedFlash = rejectedFlashRemaining > 0f;
        bool rewardedFlash = rewardedFlashRemaining > 0f;
        bool spentFlash = spentFlashRemaining > 0f;
        bool empty = boundMunin != null && boundMunin.ChargesEnabled && boundMunin.ChargesRemaining <= 0;
        Color resolvedTextColor = rejectedFlash
            ? rejectedTextColor
            : rewardedFlash
                ? rewardedColor
                : spentFlash
                    ? spentColor
                    : empty ? emptyTextColor : textColor;
        Color resolvedFillColor = rejectedFlash
            ? rejectedFillColor
            : rewardedFlash
                ? rewardedColor
                : spentFlash
                    ? spentColor
                    : empty ? emptyFillColor : fillColor;

        if (chargesText != null)
        {
            chargesText.color = resolvedTextColor;
        }

        if (fillImage != null)
        {
            fillImage.color = resolvedFillColor;
        }
    }

    private void ResolveAssignedReferences()
    {
        if (rootRect == null)
        {
            rootRect = transform as RectTransform;
        }

        if (rootGroup == null && rootRect != null)
        {
            rootGroup = rootRect.GetComponent<CanvasGroup>();
        }

        if (chargesText == null && rootRect != null)
        {
            chargesText = rootRect.GetComponentInChildren<TMP_Text>(true);
        }

        if (backgroundImage == null && rootRect != null)
        {
            backgroundImage = rootRect.GetComponent<Image>();
        }

        if (fillImage == null && rootRect != null)
        {
            Image[] images = rootRect.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image == null || image == backgroundImage)
                {
                    continue;
                }

                if (image.name.ToLowerInvariant().Contains("fill"))
                {
                    fillImage = image;
                    break;
                }
            }
        }

        if (rejectedChargeShake == null && rootRect != null)
        {
            rejectedChargeShake = rootRect.GetComponentInChildren<ShakeUI>(true);
        }
    }
}
