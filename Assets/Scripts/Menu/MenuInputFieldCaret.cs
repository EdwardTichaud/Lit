using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Affiche un petit curseur clignotant apres le texte d'un TMP_InputField.
[DisallowMultipleComponent]
public class MenuInputFieldCaret : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private RectTransform caretRect;
    [SerializeField] private Image caretImage;
    [SerializeField] private Color caretColor = Color.white;
    [SerializeField, Min(0.05f)] private float blinkRate = 0.6f;
    [SerializeField, Min(1f)] private float caretWidth = 2f;
    [SerializeField] private float heightPadding = 2f;
    [SerializeField] private bool showWhenUnfocused = true;
    [SerializeField] private bool useUnscaledTime = true;

    private TMP_Text textComponent;
    private float blinkTimer;
    private bool caretVisible = true;

    private void Awake()
    {
        Resolve();
        EnsureCaret();
        UpdateCaretVisual(true);
    }

    private void OnEnable()
    {
        Resolve();
        EnsureCaret();
        blinkTimer = 0f;
        caretVisible = true;
        UpdateCaretVisual(true);
    }

    private void Update()
    {
        if (inputField == null || textComponent == null)
        {
            return;
        }

        bool shouldShow = showWhenUnfocused || inputField.isFocused;
        if (!shouldShow)
        {
            SetCaretVisible(false);
            return;
        }

        UpdateCaretPosition();
        UpdateBlink();
    }

    public void Bind(TMP_InputField field)
    {
        inputField = field;
        Resolve();
        EnsureCaret();
        UpdateCaretVisual(true);
    }

    private void Resolve()
    {
        if (inputField == null)
        {
            inputField = GetComponent<TMP_InputField>();
        }

        textComponent = inputField != null ? inputField.textComponent : null;
    }

    private void EnsureCaret()
    {
        if (textComponent == null)
        {
            return;
        }

        if (caretRect == null)
        {
            Transform existing = textComponent.transform.Find("ManualCaret");
            if (existing != null)
            {
                caretRect = existing as RectTransform;
            }
        }

        if (caretRect == null)
        {
            GameObject caret = new GameObject("ManualCaret", typeof(RectTransform), typeof(Image));
            caret.transform.SetParent(textComponent.transform, false);
            caretRect = caret.GetComponent<RectTransform>();
        }

        if (caretImage == null && caretRect != null)
        {
            caretImage = caretRect.GetComponent<Image>();
        }

        if (caretImage != null)
        {
            caretImage.color = caretColor;
            caretImage.raycastTarget = false;
        }

        if (caretRect != null)
        {
            caretRect.pivot = new Vector2(0f, 0.5f);
            caretRect.anchorMin = new Vector2(0.5f, 0.5f);
            caretRect.anchorMax = new Vector2(0.5f, 0.5f);
        }
    }

    private void UpdateCaretPosition()
    {
        if (textComponent == null || caretRect == null)
        {
            return;
        }

        textComponent.ForceMeshUpdate();
        TMP_TextInfo info = textComponent.textInfo;
        if (info == null || info.lineCount == 0)
        {
            return;
        }

        float x;
        float y;
        float height;

        if (info.characterCount > 0)
        {
            TMP_CharacterInfo ch = info.characterInfo[info.characterCount - 1];
            x = ch.topRight.x;
            y = (ch.ascender + ch.descender) * 0.5f;
            height = Mathf.Abs(ch.ascender - ch.descender);
        }
        else
        {
            TMP_LineInfo line = info.lineInfo[0];
            x = line.lineExtents.min.x;
            y = (line.ascender + line.descender) * 0.5f;
            height = Mathf.Abs(line.ascender - line.descender);
        }

        height = Mathf.Max(4f, height + heightPadding);
        caretRect.sizeDelta = new Vector2(caretWidth, height);
        caretRect.localPosition = new Vector3(x, y, 0f);
    }

    private void UpdateBlink()
    {
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        blinkTimer += delta;
        if (blinkTimer >= blinkRate)
        {
            blinkTimer = 0f;
            caretVisible = !caretVisible;
            UpdateCaretVisual(false);
        }
    }

    private void UpdateCaretVisual(bool forceVisible)
    {
        if (caretImage == null)
        {
            return;
        }

        bool visible = forceVisible || caretVisible;
        SetCaretVisible(visible);
    }

    private void SetCaretVisible(bool visible)
    {
        if (caretImage != null)
        {
            Color color = caretImage.color;
            color.a = visible ? caretColor.a : 0f;
            caretImage.color = color;
        }
    }
}
