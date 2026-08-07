using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CounterSkillWheelSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Range(0f, 1f)] private float unselectedAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 1f;
    [SerializeField, Min(1f)] private float selectedScale = 1.5f;
    [SerializeField, Min(0f)] private float visualLerpSpeed = 22f;

    private CounterSkillSO skill;
    private Vector3 baseScale;
    private float targetAlpha;
    private float targetScale = 1f;

    public CounterSkillSO Skill => skill;

    private void Awake()
    {
        baseScale = transform.localScale;
        ResolveReferences();
        Refresh();
    }

    private void Update()
    {
        float blend = 1f - Mathf.Exp(-visualLerpSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * targetScale, blend);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, blend);
        }
    }

    public void SetSkill(CounterSkillSO value)
    {
        skill = value;
        ResolveReferences();
        Refresh();
        gameObject.SetActive(skill != null);
    }

    public void SetSelected(bool selected)
    {
        targetScale = selected ? selectedScale : 1f;
        targetAlpha = selected ? selectedAlpha : unselectedAlpha;
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (iconImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].gameObject != gameObject && images[i].name.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    iconImage = images[i];
                    break;
                }
            }
        }
        if (skillNameText == null) skillNameText = GetComponentInChildren<TMP_Text>(true);
    }

    private void Refresh()
    {
        if (iconImage != null)
        {
            iconImage.sprite = skill != null ? skill.Icon : null;
            iconImage.enabled = skill != null && skill.Icon != null;
        }
        if (skillNameText != null) skillNameText.text = skill != null ? skill.DisplayName : string.Empty;
    }
}
