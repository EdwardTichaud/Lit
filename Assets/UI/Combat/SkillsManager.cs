using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillsManager : MonoBehaviour
{
    public const int MaxEquippedSkills = 8;

    [SerializeField] private SquadCharacterController playerController;
    [SerializeField] private SkillSO[] equippedSkills = new SkillSO[MaxEquippedSkills];
    [SerializeField] private List<BasicSkillsSO> basicSkills = new List<BasicSkillsSO>();

    public event Action<IReadOnlyList<SkillSO>> EquippedSkillsChanged;

    public IReadOnlyList<SkillSO> KnownSkills => playerController != null && playerController.CharacterData != null && playerController.CharacterData.combatSkills != null
        ? playerController.CharacterData.combatSkills
        : Array.Empty<SkillSO>();

    public IReadOnlyList<SkillSO> EquippedSkills => equippedSkills;
    public IReadOnlyList<BasicSkillsSO> BasicSkills => basicSkills;
    public SkillSO AnimationEventSkill { get; private set; }

    private int nextBasicSkillIndex;

    private void Awake()
    {
        EnsureSlotCount();
        ResolveReferences();
        RefreshSkills(forceNotification: true);
    }

    public void RefreshSkills(bool forceNotification = false)
    {
        EnsureSlotCount();
        ResolveReferences();
        if (RemoveUnknownSkills() || forceNotification)
        {
            EquippedSkillsChanged?.Invoke(equippedSkills);
        }
    }

    public bool IsKnown(SkillSO skill)
    {
        return skill != null && playerController != null && playerController.CharacterData != null && playerController.CharacterData.combatSkills != null && playerController.CharacterData.combatSkills.Contains(skill);
    }

    public SkillSO GetEquippedSkill(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < MaxEquippedSkills ? equippedSkills[slotIndex] : null;
    }

    public bool EquipSkill(SkillSO skill)
    {
        for (int i = 0; i < MaxEquippedSkills; i++)
        {
            if (equippedSkills[i] == null)
            {
                return EquipSkillAt(i, skill);
            }
        }

        return false;
    }

    public bool EquipSkillAt(int slotIndex, SkillSO skill)
    {
        if (slotIndex < 0 || slotIndex >= MaxEquippedSkills || !IsKnown(skill))
        {
            return false;
        }

        for (int i = 0; i < MaxEquippedSkills; i++)
        {
            if (i != slotIndex && equippedSkills[i] == skill)
            {
                return false;
            }
        }

        if (equippedSkills[slotIndex] == skill)
        {
            return true;
        }

        equippedSkills[slotIndex] = skill;
        EquippedSkillsChanged?.Invoke(equippedSkills);
        return true;
    }

    public void UnequipSkillAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxEquippedSkills)
        {
            return;
        }

        if (equippedSkills[slotIndex] == null)
        {
            return;
        }

        equippedSkills[slotIndex] = null;
        EquippedSkillsChanged?.Invoke(equippedSkills);
    }

    public bool TryReserveNextBasicSkill(out BasicSkillsSO skill)
    {
        skill = null;
        if (basicSkills == null || basicSkills.Count == 0)
        {
            return false;
        }

        for (int offset = 0; offset < basicSkills.Count; offset++)
        {
            int index = (nextBasicSkillIndex + offset) % basicSkills.Count;
            if (basicSkills[index] != null)
            {
                skill = basicSkills[index];
                nextBasicSkillIndex = (index + 1) % basicSkills.Count;
                return true;
            }
        }

        return false;
    }

    public void ResetBasicSkillCombo()
    {
        nextBasicSkillIndex = 0;
    }

    public void SetAnimationEventSkill(SkillSO skill)
    {
        AnimationEventSkill = skill;
    }

    private void OnValidate()
    {
        EnsureSlotCount();
    }

    private void EnsureSlotCount()
    {
        if (equippedSkills != null && equippedSkills.Length == MaxEquippedSkills)
        {
            return;
        }

        SkillSO[] normalized = new SkillSO[MaxEquippedSkills];
        if (equippedSkills != null)
        {
            Array.Copy(equippedSkills, normalized, Mathf.Min(equippedSkills.Length, MaxEquippedSkills));
        }

        equippedSkills = normalized;
    }

    private void ResolveReferences()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<SquadCharacterController>(FindObjectsInactive.Include);
        }
    }

    private bool RemoveUnknownSkills()
    {
        if (playerController == null || playerController.CharacterData == null)
        {
            return false;
        }

        bool changed = false;
        for (int i = 0; i < MaxEquippedSkills; i++)
        {
            if (equippedSkills[i] != null && !IsKnown(equippedSkills[i]))
            {
                equippedSkills[i] = null;
                changed = true;
            }
        }
        return changed;
    }
}
