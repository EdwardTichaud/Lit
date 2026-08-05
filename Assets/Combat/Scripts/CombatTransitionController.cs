using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Role: affiche une transition visuelle/audio lors de l'entree et sortie de combat.
// Usage: appele par CombatSessionManager autour du changement de presentation joueur/ennemi.
// Responsibilities: creer son UI runtime, jouer SFX/musique, executer l'action quand l'ecran est couvert.
// Dependencies: AudioManager, TMP, Unity UI.
// Precautions: coveredAction est critique; elle doit etre appelee meme si la transition est interrompue.
/// <summary>
/// Controleur singleton des transitions entre exploration et combat.
/// </summary>
public sealed class CombatTransitionController : MonoBehaviour
{
    /// <summary>
    /// Instance singleton de transition de combat.
    /// </summary>
    public static CombatTransitionController Instance { get; private set; }

    [Header("Timing")]
    /// <summary>
    /// Duree de la transition d'entree en combat.
    /// </summary>
    [SerializeField, Min(0.2f)] private float enterDuration = 1.45f;
    /// <summary>
    /// Moment normalise ou l'action couverte est executee pendant l'entree.
    /// </summary>
    [SerializeField, Range(0.05f, 0.95f)] private float enterCoverNormalizedTime = 0.38f;
    /// <summary>
    /// Duree de la transition de sortie de combat.
    /// </summary>
    [SerializeField, Min(0.2f)] private float exitDuration = 1.05f;
    /// <summary>
    /// Moment normalise ou l'action couverte est executee pendant la sortie.
    /// </summary>
    [SerializeField, Range(0.05f, 0.95f)] private float exitCoverNormalizedTime = 0.44f;

    private CanvasGroup canvasGroup;
    private RectTransform visualRoot;
    private Image background;
    private Image topLetterbox;
    private Image bottomLetterbox;
    private Image leftSlash;
    private Image rightSlash;
    private Image scanLine;
    private Image flash;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI subtitleText;
    private Coroutine transitionRoutine;
    private Action pendingCoveredAction;
    private int musicOverrideToken;
    private int gameOverMusicOverrideToken;
    private bool combatSessionMusicActive;
    private readonly HashSet<CombatAggroEnemy> proximityMusicOwners = new HashSet<CombatAggroEnemy>();
    private readonly List<CombatAggroEnemy> proximityMusicRemovalBuffer = new List<CombatAggroEnemy>();
    private Sprite solidSprite;

    /// <summary>
    /// Retourne l'instance existante ou cree un controleur runtime minimal.
    /// </summary>
    public static CombatTransitionController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<CombatTransitionController>();
#else
        Instance = FindAnyObjectByType<CombatTransitionController>();
#endif
        if (Instance != null)
        {
            return Instance;
        }

        GameObject host = new GameObject("CombatTransitionController");
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<CombatTransitionController>();
        return Instance;
    }

    /// <summary>
    /// Lance la transition d'entree en combat.
    /// </summary>
    public void PlayEnterTransition(Action coveredAction = null)
    {
        BeginCombatEntryAudioAndMusic();
        StartTransition(EnterRoutine, coveredAction);
    }

    /// <summary>
    /// Demarre les sons et la musique d'entree combat sans jouer l'ancienne transition visuelle.
    /// </summary>
    public void BeginCombatEntryAudioAndMusic()
    {
        AudioManager manager = AudioManager.EnsureInstance();
        manager.PlayUiClip(manager.ResolveCombatAudioClip(CombatAudioCue.EnterTransition));
        manager.PlayUiClip(manager.ResolveCombatAudioClip(CombatAudioCue.Accent));
        ForceReleaseGameOverMusicOverride();
        combatSessionMusicActive = true;
        EnsureCombatMusicOverride(manager);
    }

    /// <summary>
    /// Lance la transition de sortie vers l'exploration.
    /// </summary>
    public void PlayExitTransition(Action coveredAction = null)
    {
        AudioManager manager = AudioManager.EnsureInstance();
        manager.PlayUiClip(manager.ResolveCombatAudioClip(CombatAudioCue.ExitTransition));
        ForceReleaseGameOverMusicOverride();
        combatSessionMusicActive = false;
        ReleaseCombatMusicOverrideIfUnused();

        StartTransition(ExitRoutine, coveredAction);
    }

    /// <summary>
    /// Remplace localement la musique de combat par la musique de Game Over pendant une defaite.
    /// </summary>
    public void PlayGameOverResolutionMusic()
    {
        if (gameOverMusicOverrideToken != 0)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        AudioClipSO musicClip = manager.ResolveCombatAudioClip(CombatAudioCue.GameOverMusic);
        gameOverMusicOverrideToken = manager.PushCombatMusicOverride(musicClip);
    }

    /// <summary>
    /// Active localement la musique de combat quand le joueur local approche d'un ennemi, sans demarrer de session.
    /// </summary>
    public void BeginProximityCombatMusic(CombatAggroEnemy owner)
    {
        if (owner == null)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        PruneProximityMusicOwners();
        proximityMusicOwners.Add(owner);
        EnsureCombatMusicOverride(manager);
    }

    /// <summary>
    /// Relache la demande locale de musique de combat d'un ennemi proche.
    /// </summary>
    public void EndProximityCombatMusic(CombatAggroEnemy owner)
    {
        if (owner == null)
        {
            return;
        }

        proximityMusicOwners.Remove(owner);
        ReleaseCombatMusicOverrideIfUnused();
    }

    private void Awake()
    {
        // Unity appelle Awake au chargement; le singleton doit exister avant les demandes de transition.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RuntimePersistenceUtility.DontDestroyOnLoadRoot(gameObject);
    }

    private void OnDestroy()
    {
        // Si l'objet est detruit pendant une transition, on execute quand meme l'action critique.
        InvokePendingCoveredAction();
        ForceReleaseGameOverMusicOverride();
        ForceReleaseCombatMusicOverride();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureCombatMusicOverride(AudioManager manager = null)
    {
        if (musicOverrideToken != 0)
        {
            return;
        }

        if (manager == null)
        {
            manager = AudioManager.EnsureInstance();
        }

        AudioClipSO musicClip = manager.ResolveCombatAudioClip(CombatAudioCue.CombatMusic);
        musicOverrideToken = manager.PushCombatMusicOverride(musicClip);
    }

    private void ReleaseCombatMusicOverrideIfUnused()
    {
        PruneProximityMusicOwners();
        if (combatSessionMusicActive || proximityMusicOwners.Count > 0)
        {
            return;
        }

        ForceReleaseCombatMusicOverride();
    }

    private void ForceReleaseCombatMusicOverride()
    {
        if (musicOverrideToken == 0 || AudioManager.Instance == null)
        {
            musicOverrideToken = 0;
            return;
        }

        AudioManager.Instance.PopMusicOverride(musicOverrideToken);
        musicOverrideToken = 0;
    }

    private void ForceReleaseGameOverMusicOverride()
    {
        if (gameOverMusicOverrideToken == 0 || AudioManager.Instance == null)
        {
            gameOverMusicOverrideToken = 0;
            return;
        }

        AudioManager.Instance.PopMusicOverride(gameOverMusicOverrideToken);
        gameOverMusicOverrideToken = 0;
    }

    private void PruneProximityMusicOwners()
    {
        if (proximityMusicOwners.Count == 0)
        {
            return;
        }

        proximityMusicRemovalBuffer.Clear();
        foreach (CombatAggroEnemy owner in proximityMusicOwners)
        {
            if (owner == null)
            {
                proximityMusicRemovalBuffer.Add(owner);
            }
        }

        for (int i = 0; i < proximityMusicRemovalBuffer.Count; i++)
        {
            proximityMusicOwners.Remove(proximityMusicRemovalBuffer[i]);
        }

        proximityMusicRemovalBuffer.Clear();
    }

    private void StartTransition(Func<IEnumerator> routineFactory, Action coveredAction)
    {
        InvokePendingCoveredAction();
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        pendingCoveredAction = coveredAction;
        EnsureVisuals();
        visualRoot.gameObject.SetActive(true);
        transitionRoutine = StartCoroutine(routineFactory());
    }

    private IEnumerator EnterRoutine()
    {
        titleText.text = "COMBAT";
        subtitleText.text = "ENGAGEMENT";

        float duration = Mathf.Max(0.2f, enterDuration);
        float coverAt = duration * Mathf.Clamp01(enterCoverNormalizedTime);
        bool covered = false;
        float time = 0f;

        while (time < duration)
        {
            float n = time / duration;
            if (!covered && time >= coverAt)
            {
                covered = true;
                // A cet instant l'ecran est cense couvrir le changement de placement.
                InvokePendingCoveredAction();
            }

            ApplyEnterVisual(n);
            time += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!covered)
        {
            InvokePendingCoveredAction();
        }

        HideVisuals();
    }

    private IEnumerator ExitRoutine()
    {
        titleText.text = "RETOUR";
        subtitleText.text = "EXPLORATION";

        float duration = Mathf.Max(0.2f, exitDuration);
        float coverAt = duration * Mathf.Clamp01(exitCoverNormalizedTime);
        bool covered = false;
        float time = 0f;

        while (time < duration)
        {
            float n = time / duration;
            if (!covered && time >= coverAt)
            {
                covered = true;
                // Meme principe que l'entree: on cache le changement de retour exploration.
                InvokePendingCoveredAction();
            }

            ApplyExitVisual(n);
            time += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!covered)
        {
            InvokePendingCoveredAction();
        }

        HideVisuals();
    }

    private void ApplyEnterVisual(float n)
    {
        float fadeIn = Ease(Mathf.Clamp01(n / 0.24f));
        float reveal = Ease(Mathf.Clamp01((n - 0.64f) / 0.36f));
        float hold = Mathf.Clamp01(Mathf.Min(fadeIn, 1f - reveal));
        float slash = Ease(Mathf.Clamp01(n / 0.42f));
        float title = Ease(Mathf.Clamp01((n - 0.18f) / 0.22f)) * (1f - Ease(Mathf.Clamp01((n - 0.72f) / 0.2f)));
        float flashAmount = Mathf.Clamp01(1f - Mathf.Abs(n - 0.38f) / 0.08f);

        SetCommonVisuals(hold, slash, title, flashAmount, new Color(0.72f, 0.05f, 0.02f, 1f));
    }

    private void ApplyExitVisual(float n)
    {
        float fadeIn = Ease(Mathf.Clamp01(n / 0.2f));
        float reveal = Ease(Mathf.Clamp01((n - 0.56f) / 0.44f));
        float hold = Mathf.Clamp01(Mathf.Min(fadeIn, 1f - reveal));
        float slash = Ease(Mathf.Clamp01(n / 0.36f));
        float title = Ease(Mathf.Clamp01((n - 0.12f) / 0.24f)) * (1f - Ease(Mathf.Clamp01((n - 0.64f) / 0.2f)));
        float flashAmount = Mathf.Clamp01(1f - Mathf.Abs(n - 0.44f) / 0.1f);

        SetCommonVisuals(hold, slash, title, flashAmount, new Color(0.03f, 0.32f, 0.44f, 1f));
    }

    private void SetCommonVisuals(float hold, float slash, float title, float flashAmount, Color accent)
    {
        canvasGroup.alpha = Mathf.Clamp01(Mathf.Max(hold, flashAmount * 0.6f));
        canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.01f;

        background.color = new Color(0.005f, 0.004f, 0.006f, 0.82f * hold);
        topLetterbox.color = new Color(0f, 0f, 0f, 0.95f * hold);
        bottomLetterbox.color = topLetterbox.color;

        float letterboxOffset = Mathf.Lerp(180f, 0f, hold);
        topLetterbox.rectTransform.anchoredPosition = new Vector2(0f, letterboxOffset);
        bottomLetterbox.rectTransform.anchoredPosition = new Vector2(0f, -letterboxOffset);

        Color slashColor = accent;
        slashColor.a = 0.92f * hold;
        leftSlash.color = slashColor;
        rightSlash.color = new Color(0f, 0f, 0f, 0.86f * hold);

        float slashTravel = Mathf.Lerp(-1200f, 0f, slash);
        leftSlash.rectTransform.anchoredPosition = new Vector2(slashTravel - 180f, 0f);
        rightSlash.rectTransform.anchoredPosition = new Vector2(-slashTravel + 180f, 0f);

        Color scanColor = accent;
        scanColor.a = 0.7f * Mathf.Clamp01(title + flashAmount);
        scanLine.color = scanColor;
        scanLine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-900f, 900f, slash), 0f);

        flash.color = new Color(1f, 0.92f, 0.72f, 0.34f * flashAmount);

        titleText.alpha = title;
        subtitleText.alpha = title * 0.85f;
        titleText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Ease(title));
        subtitleText.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-44f, -64f, Ease(title)));
    }

    private void HideVisuals()
    {
        transitionRoutine = null;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        visualRoot.gameObject.SetActive(false);
    }

    private void InvokePendingCoveredAction()
    {
        Action action = pendingCoveredAction;
        pendingCoveredAction = null;
        action?.Invoke();
    }

    private void EnsureVisuals()
    {
        if (visualRoot != null)
        {
            return;
        }

        solidSprite = CreateSolidSprite();

        GameObject canvasObject = new GameObject("CombatTransitionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        visualRoot = canvasObject.transform as RectTransform;
        Stretch(visualRoot);

        background = AddImage("Backdrop", visualRoot, Color.clear);
        Stretch(background.rectTransform);

        topLetterbox = AddImage("TopLetterbox", visualRoot, Color.black);
        ConfigureBand(topLetterbox.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 110f));

        bottomLetterbox = AddImage("BottomLetterbox", visualRoot, Color.black);
        ConfigureBand(bottomLetterbox.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f));

        leftSlash = AddImage("CombatSlash_A", visualRoot, Color.clear);
        ConfigureSlash(leftSlash.rectTransform, new Vector2(880f, 1500f), -12f);

        rightSlash = AddImage("CombatSlash_B", visualRoot, Color.clear);
        ConfigureSlash(rightSlash.rectTransform, new Vector2(820f, 1500f), -12f);

        scanLine = AddImage("ScanLine", visualRoot, Color.clear);
        RectTransform scan = scanLine.rectTransform;
        scan.anchorMin = new Vector2(0.5f, 0.5f);
        scan.anchorMax = new Vector2(0.5f, 0.5f);
        scan.pivot = new Vector2(0.5f, 0.5f);
        scan.sizeDelta = new Vector2(340f, 5f);
        scan.localRotation = Quaternion.Euler(0f, 0f, -12f);

        titleText = AddText("CombatTitle", visualRoot, 96f, FontStyles.Bold);
        subtitleText = AddText("CombatSubtitle", visualRoot, 24f, FontStyles.UpperCase);

        flash = AddImage("Flash", visualRoot, Color.clear);
        Stretch(flash.rectTransform);

        visualRoot.gameObject.SetActive(false);
    }

    private Image AddImage(string objectName, Transform parent, Color color)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.sprite = solidSprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI AddText(string objectName, Transform parent, float fontSize, FontStyles style)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.92f, 0.78f, 1f);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 140f);
        rect.anchoredPosition = objectName == "CombatTitle" ? Vector2.zero : new Vector2(0f, -64f);
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void ConfigureBand(RectTransform rect, Vector2 pivot, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, pivot.y);
        rect.anchorMax = new Vector2(1f, pivot.y);
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }

    private static void ConfigureSlash(RectTransform rect, Vector2 size, float rotationZ)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
    }

    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static Sprite CreateSolidSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
