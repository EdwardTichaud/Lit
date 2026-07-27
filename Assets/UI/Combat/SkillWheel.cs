using System.Collections.Generic;
using UnityEngine;

public sealed class SkillWheel : MonoBehaviour
{
    [SerializeField] private SkillWheelSlot[] slots;
    [SerializeField] private SkillsManager skillsManager;

    private int selectedSlotIndex = -1;

    public int SlotCount => slots != null ? slots.Length : 0;
    public int SelectedSlotIndex => selectedSlotIndex;

    public SkillSO GetSkill(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < SlotCount && slots[slotIndex] != null
            ? slots[slotIndex].AssignedSkill
            : null;
    }

    public bool SetSkill(int slotIndex, SkillSO skill)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount || slots[slotIndex] == null)
        {
            return false;
        }

        slots[slotIndex].SetSkill(skill);
        return true;
    }

    public int SelectFromDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f || slots == null)
        {
            return selectedSlotIndex;
        }

        Vector2 normalizedDirection = direction.normalized;
        float bestScore = float.NegativeInfinity;
        int bestIndex = selectedSlotIndex;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || !slots[i].gameObject.activeInHierarchy || slots[i].transform is not RectTransform rectTransform)
            {
                continue;
            }

            Vector2 slotDirection = rectTransform.anchoredPosition;
            if (slotDirection.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            float score = Vector2.Dot(normalizedDirection, slotDirection.normalized);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            SetSelectedSlot(bestIndex);
        }

        return selectedSlotIndex;
    }

    public void SetSelectedSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount || slots[slotIndex] == null || !slots[slotIndex].gameObject.activeInHierarchy)
        {
            slotIndex = FindFirstVisibleSlot();
            if (slotIndex < 0)
            {
                ClearSelection();
                return;
            }
        }

        selectedSlotIndex = slotIndex;
        ApplySelection(true);
    }

    public void ClearSelection()
    {
        selectedSlotIndex = -1;
        ApplySelection(false);
    }

    private void Awake()
    {
        ResolveSlots();
        ClearSelection();
    }

    private void Start()
    {
        BindSkillsManager();
    }

    private void OnEnable()
    {
        BindSkillsManager();
    }

    private void OnDisable()
    {
        UnbindSkillsManager();
        ClearSelection();
    }

    private void OnValidate()
    {
        ResolveSlots();
    }

    private void ResolveSlots()
    {
        if (slots == null || slots.Length == 0)
        {
            slots = GetComponentsInChildren<SkillWheelSlot>(true);
        }
    }

    private void BindSkillsManager()
    {
        if (skillsManager == null)
        {
            skillsManager = FindAnyObjectByType<SkillsManager>(FindObjectsInactive.Include);
        }

        if (skillsManager == null)
        {
            return;
        }

        skillsManager.EquippedSkillsChanged -= ApplySkills;
        skillsManager.EquippedSkillsChanged += ApplySkills;
        ApplySkills(skillsManager.EquippedSkills);
    }

    private void UnbindSkillsManager()
    {
        if (skillsManager != null)
        {
            skillsManager.EquippedSkillsChanged -= ApplySkills;
        }
    }

    private void ApplySkills(IReadOnlyList<SkillSO> skills)
    {
        if (slots == null || skills == null)
        {
            return;
        }

        int count = Mathf.Min(slots.Length, skills.Count);
        for (int i = 0; i < count; i++)
        {
            SetSkill(i, skills[i]);
        }

        for (int i = count; i < slots.Length; i++)
        {
            SetSkill(i, null);
        }

        SetSelectedSlot(selectedSlotIndex);
    }

    private int FindFirstVisibleSlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] != null && slots[i].gameObject.activeInHierarchy)
            {
                return i;
            }
        }

        return -1;
    }

    private void ApplySelection(bool wheelIsActive)
    {
        if (slots == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slots[i].SetSelection(wheelIsActive && i == selectedSlotIndex, wheelIsActive);
            }
        }
    }
}
