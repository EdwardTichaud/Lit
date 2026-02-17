using System.Collections;
using TMPro;
using UnityEngine;

// Affiche des messages courts (fallbacks, infos) avec fondu.
public class InfoBoxUI : MonoBehaviour
{
    public static InfoBoxUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("Root de l'InfoBox.")]
    public GameObject infoBoxRoot;
    [Tooltip("Frame de l'InfoBox.")]
    public GameObject infoBoxFrame;
    [Tooltip("Texte affiche.")]
    public TextMeshProUGUI infoText;

    [Header("Behavior")]
    [Tooltip("Duree par defaut d'affichage.")]
    public float defaultDuration = 1.2f;
    [Tooltip("Duree du fondu.")]
    public float fadeDuration = 0.25f;
    [Tooltip("Masque le texte quand il est vide.")]
    public bool hideWhenEmpty = true;
    [Tooltip("Desactive le GameObject apres le fondu.")]
    public bool setInactiveOnHide = true;
    [Tooltip("Auto-resout les references au Awake/OnEnable.")]
    public bool autoFindOnAwake = true;
    [Tooltip("Ne pas detruire au changement de scene.")]
    public bool dontDestroyOnLoad = false;

    private Coroutine hideRoutine;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (autoFindOnAwake)
        {
            ResolveReferences();
        }

        InitializeCanvasGroup();
    }

    private void OnEnable()
    {
        if (autoFindOnAwake)
        {
            ResolveReferences();
        }

        InitializeCanvasGroup();
    }

    public static bool TryShow(string message)
    {
        return TryShow(message, 0f);
    }

    public static bool TryShow(string message, float duration)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        InfoBoxUI ui = Instance;
        if (ui == null)
        {
#if UNITY_2023_1_OR_NEWER
            ui = FindFirstObjectByType<InfoBoxUI>();
#else
            ui = FindObjectOfType<InfoBoxUI>();
#endif
        }

        if (ui == null)
        {
            GameObject runner = new GameObject("InfoBoxUI_Runtime");
            ui = runner.AddComponent<InfoBoxUI>();
        }

        return ui.ShowMessage(message, duration);
    }

    public static float GetDefaultDuration()
    {
        InfoBoxUI ui = Instance;
        if (ui == null)
        {
#if UNITY_2023_1_OR_NEWER
            ui = FindFirstObjectByType<InfoBoxUI>();
#else
            ui = FindObjectOfType<InfoBoxUI>();
#endif
        }

        if (ui == null)
        {
            return 1.2f;
        }

        return ui.defaultDuration;
    }

    public bool ShowMessage(string message, float duration)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        // Prepare l'UI puis lance la coroutine de fade.
        ResolveReferences();
        if (infoText == null)
        {
            return false;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        SetVisible(true);
        infoText.text = message;
        infoText.gameObject.SetActive(true);
        InitializeCanvasGroup();
        SetCanvasAlpha(0f);

        float wait = duration > 0f ? duration : defaultDuration;
        hideRoutine = StartCoroutine(ShowAndHideRoutine(wait));

        return true;
    }

    private IEnumerator ShowAndHideRoutine(float duration)
    {
        float fadeIn = Mathf.Max(0f, fadeDuration);
        float hold = Mathf.Max(0f, duration);
        float fadeOut = Mathf.Max(0f, fadeDuration);

        if (fadeIn > 0f)
        {
            yield return FadeAlpha(0f, 1f, fadeIn);
        }
        else
        {
            SetCanvasAlpha(1f);
        }

        float time = 0f;
        while (time < hold)
        {
            time += Time.unscaledDeltaTime;
            yield return null;
        }

        if (fadeOut > 0f)
        {
            yield return FadeAlpha(1f, 0f, fadeOut);
        }
        else
        {
            SetCanvasAlpha(0f);
        }

        if (hideWhenEmpty && infoText != null)
        {
            infoText.text = string.Empty;
        }

        if (setInactiveOnHide)
        {
            SetVisible(false);
        }

        hideRoutine = null;
    }

    private void ResolveReferences()
    {
        if (infoText == null)
        {
            infoText = FindText("InfoBox_Text");
        }

        if (infoBoxFrame == null && infoText != null && infoText.transform.parent != null)
        {
            infoBoxFrame = infoText.transform.parent.gameObject;
        }

        if (infoBoxRoot == null)
        {
            if (infoBoxFrame != null && infoBoxFrame.transform.parent != null)
            {
                infoBoxRoot = infoBoxFrame.transform.parent.gameObject;
            }
            else
            {
                infoBoxRoot = GameObject.Find("InfoBox");
            }
        }

        if (infoBoxFrame == null)
        {
            infoBoxFrame = GameObject.Find("InfoBox_Frame");
        }

        if (infoText == null)
        {
            infoText = FindText("InfoBox_Text");
        }

        InitializeCanvasGroup();
    }

    private TextMeshProUGUI FindText(string name)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            TextMeshProUGUI[] allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            for (int i = 0; i < allTexts.Length; i++)
            {
                TextMeshProUGUI tmp = allTexts[i];
                if (tmp != null && tmp.gameObject.name == name)
                {
                    return tmp;
                }
            }

            return null;
        }

        return obj.GetComponent<TextMeshProUGUI>();
    }

    private void SetVisible(bool visible)
    {
        if (infoBoxRoot != null)
        {
            infoBoxRoot.SetActive(visible);
            return;
        }

        if (infoBoxFrame != null)
        {
            infoBoxFrame.SetActive(visible);
        }

        if (infoText != null)
        {
            infoText.gameObject.SetActive(visible);
        }
    }

    private void InitializeCanvasGroup()
    {
        canvasGroup = GetCanvasGroup();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private CanvasGroup GetCanvasGroup()
    {
        if (canvasGroup != null)
        {
            return canvasGroup;
        }

        GameObject target = infoBoxRoot != null
            ? infoBoxRoot
            : infoBoxFrame != null
                ? infoBoxFrame
                : infoText != null
                    ? infoText.gameObject
                    : null;

        if (target == null)
        {
            return null;
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        canvasGroup = group;
        return canvasGroup;
    }

    private void SetCanvasAlpha(float alpha)
    {
        CanvasGroup group = GetCanvasGroup();
        if (group == null)
        {
            return;
        }

        group.alpha = alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        CanvasGroup group = GetCanvasGroup();
        if (group == null)
        {
            yield break;
        }

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.alpha = to;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
