using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CounterSkillWheel : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private CounterSkillWheelSlot[] slots;
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;

    private int selectedIndex = -1;
    public bool IsOpen { get; private set; }
    public CounterSkillSO SelectedSkill => selectedIndex >= 0 && selectedIndex < slots.Length ? slots[selectedIndex].Skill : null;

    private void Awake()
    {
        ResolveReferences();
        SetVisible(false);
    }

    public bool Open(IReadOnlyList<CounterSkillSO> skills)
    {
        ResolveReferences();
        if (slots == null || slots.Length == 0 || skills == null || skills.Count == 0)
        {
            return false;
        }

        int count = Mathf.Min(slots.Length, skills.Count);
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].SetSkill(i < count ? skills[i] : null);
        }

        IsOpen = true;
        selectedIndex = FindFirstVisibleSlot();
        ApplySelection();
        SetVisible(true);
        return selectedIndex >= 0;
    }

    public void Close()
    {
        IsOpen = false;
        selectedIndex = -1;
        ApplySelection();
        SetVisible(false);
    }

    public void Navigate(Vector2 direction)
    {
        if (!IsOpen || direction.sqrMagnitude < 0.0001f) return;

        Vector2 normalized = direction.normalized;
        float bestScore = float.NegativeInfinity;
        int best = selectedIndex;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || !slots[i].gameObject.activeInHierarchy || slots[i].transform is not RectTransform rect) continue;
            Vector2 slotDirection = rect.anchoredPosition;
            if (slotDirection.sqrMagnitude < 0.0001f) continue;
            float score = Vector2.Dot(normalized, slotDirection.normalized);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        if (best >= 0)
        {
            selectedIndex = best;
            ApplySelection();
        }
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (slots == null || slots.Length == 0) slots = GetComponentsInChildren<CounterSkillWheelSlot>(true);
    }

    private int FindFirstVisibleSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].gameObject.activeInHierarchy) return i;
        }
        return -1;
    }

    private void ApplySelection()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i]?.SetSelected(IsOpen && i == selectedIndex);
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? visibleAlpha : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
