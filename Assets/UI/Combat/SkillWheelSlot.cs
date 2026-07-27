using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class SkillWheelSlot : MonoBehaviour
{
    [SerializeField] private SkillSO skill;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Range(0f, 1f)] private float unselectedAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 1f;
    [SerializeField, Min(1f)] private float selectedScale = 1.5f;
    [SerializeField, Min(0f)] private float visualLerpSpeed = 22f;

    private Vector3 baseScale;
    private float targetAlpha = 1f;
    private float targetScale = 1f;

    public SkillSO AssignedSkill => skill;

    public void SetSkill(SkillSO value)
    {
        ResolveVisualReferences();
        skill = value;
        Refresh();
        gameObject.SetActive(skill != null);
    }

    public void SetSelection(bool selected, bool wheelIsActive)
    {
        targetScale = selected ? selectedScale : 1f;
        targetAlpha = wheelIsActive ? (selected ? selectedAlpha : unselectedAlpha) : 1f;
    }

    private void Awake()
    {
        baseScale = transform.localScale;
        ResolveVisualReferences();

        SetSelection(false, false);
        Refresh();
    }

    private void Update()
    {
        float lerp = 1f - Mathf.Exp(-visualLerpSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * targetScale, lerp);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, lerp);
        }
    }

    private void OnValidate()
    {
        ResolveVisualReferences();

        Refresh();
    }

    private void ResolveVisualReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (iconImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && image.gameObject != gameObject &&
                    image.name.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    iconImage = image;
                    break;
                }
            }
        }

        if (skillNameText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0)
            {
                skillNameText = texts[0];
            }
        }
    }

    private void Refresh()
    {
        if (iconImage != null)
        {
            iconImage.sprite = skill != null ? skill.Icon : null;
            iconImage.enabled = skill != null && skill.Icon != null;
        }

        if (skillNameText != null)
        {
            skillNameText.text = skill != null ? skill.SkillName : string.Empty;
        }
    }
}
