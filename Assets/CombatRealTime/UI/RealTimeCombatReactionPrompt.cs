using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RealTimeCombatReactionPrompt : MonoBehaviour
{
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private Image[] reactionIcons;
    [SerializeField] private Sprite counterIcon;
    [SerializeField] private Sprite dodgeIcon;
    [SerializeField] private Sprite jumpIcon;
    [SerializeField] private Vector3 enemyOffset = new Vector3(0f, 2.25f, 0f);
    [SerializeField] private bool faceCamera = true;
    [SerializeField, Min(0.01f)] private float visualSharpness = 18f;

    private Transform target;
    private float targetAlpha;
    private float targetScale = 1f;

    private void Awake()
    {
        if (promptText == null) promptText = GetComponentInChildren<TMP_Text>(true);
        if (visualRoot == null) visualRoot = promptText != null ? promptText.gameObject : gameObject;
        if (canvasGroup == null) canvasGroup = visualRoot.GetComponent<CanvasGroup>();
        if (worldCanvas == null) worldCanvas = GetComponent<Canvas>();
        if (reactionIcons == null || reactionIcons.Length == 0) reactionIcons = visualRoot.GetComponentsInChildren<Image>(true);
        SetVisible(false, instant: true);
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.TransformPoint(enemyOffset);
            if (faceCamera && Camera.main != null)
            {
                Vector3 direction = transform.position - Camera.main.transform.position;
                if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        if (worldCanvas != null)
        {
            worldCanvas.overrideSorting = true;
            worldCanvas.sortingOrder = 1000;
            if (worldCanvas.renderMode == RenderMode.WorldSpace && Camera.main != null)
            {
                worldCanvas.worldCamera = Camera.main;
            }
        }

        float blend = 1f - Mathf.Exp(-visualSharpness * Time.unscaledDeltaTime);
        if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, blend);
        if (visualRoot != null) visualRoot.transform.localScale = Vector3.Lerp(visualRoot.transform.localScale, Vector3.one * targetScale, blend);
    }

    public void BeginTelegraph(Transform enemy, SkillSO skill)
    {
        target = enemy;
        Populate(skill);
        ApplyColor(skill != null ? skill.ReactionTelegraph.threatColor : Color.magenta);
        SetVisible(enemy != null, instant: false);
        targetAlpha = 0.42f;
        targetScale = 0.78f;
    }

    public void OpenPerfectWindow(Transform enemy, SkillSO skill)
    {
        target = enemy;
        Populate(skill);
        ApplyColor(skill != null ? skill.ReactionTelegraph.perfectWindowColor : Color.cyan);
        SetVisible(enemy != null, instant: false);
        targetAlpha = 1f;
        targetScale = 1.16f;
    }

    public void Resolve(bool succeeded)
    {
        targetAlpha = 0f;
        targetScale = succeeded ? 1.28f : 0.9f;
        target = null;
    }

    public void Clear()
    {
        target = null;
        SetVisible(false, instant: false);
    }

    private void Populate(SkillSO skill)
    {
        IReadOnlyList<RealTimeCombatReaction> reactions = skill != null ? skill.AcceptedEnemyReactions : null;
        if (promptText != null)
        {
            List<string> labels = new List<string>();
            if (reactions != null)
            {
                for (int i = 0; i < reactions.Count; i++) labels.Add(LabelFor(reactions[i]));
            }
            promptText.text = string.Join("   ", labels);
        }

        for (int i = 0; i < reactionIcons.Length; i++)
        {
            if (reactionIcons[i] == null)
            {
                continue;
            }

            bool visible = reactions != null && i < reactions.Count;
            reactionIcons[i].gameObject.SetActive(visible);
            if (visible) reactionIcons[i].sprite = SpriteFor(reactions[i]);
        }
    }

    private void SetVisible(bool visible, bool instant)
    {
        targetAlpha = visible ? targetAlpha : 0f;
        if (canvasGroup != null && instant) canvasGroup.alpha = targetAlpha;
        if (visualRoot != null && visualRoot != gameObject) visualRoot.SetActive(true);
    }

    private void ApplyColor(Color color)
    {
        if (promptText != null) promptText.color = color;
        for (int i = 0; i < reactionIcons.Length; i++)
        {
            if (reactionIcons[i] != null) reactionIcons[i].color = color;
        }
    }

    private Sprite SpriteFor(RealTimeCombatReaction reaction)
    {
        return reaction == RealTimeCombatReaction.Counter ? counterIcon :
            reaction == RealTimeCombatReaction.Dodge ? dodgeIcon :
            reaction == RealTimeCombatReaction.Jump ? jumpIcon : null;
    }

    private static string LabelFor(RealTimeCombatReaction reaction)
    {
        return reaction == RealTimeCombatReaction.Counter ? "SOUTH" :
            reaction == RealTimeCombatReaction.Dodge ? "EAST" :
            reaction == RealTimeCombatReaction.Jump ? "NORTH" : string.Empty;
    }
}
