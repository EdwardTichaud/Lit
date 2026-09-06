using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class SkillsManager : MonoBehaviour
{
    public const int MaxEquippedSkills = 8;

    [SerializeField] private SquadCharacterController playerController;
    [SerializeField] private SkillSO[] equippedSkills = new SkillSO[MaxEquippedSkills];
    [FormerlySerializedAs("basicSkills")]
    [Tooltip("Combo de base utilise lorsque Lucian est au sol. Les BasicSkills Airborne sont refuses ici.")]
    [SerializeField] private List<BasicSkillsSO> groundBasicSkills = new List<BasicSkillsSO>();
    [Tooltip("Combo de base utilise lorsque Lucian est en l'air. Les BasicSkills Grounded sont refuses ici.")]
    [SerializeField] private List<BasicSkillsSO> airBasicSkills = new List<BasicSkillsSO>();

    public event Action<IReadOnlyList<SkillSO>> EquippedSkillsChanged;

    private readonly List<SkillSO> knownSkillsView = new List<SkillSO>();
    public IReadOnlyList<SkillSO> KnownSkills
    {
        get
        {
            knownSkillsView.Clear();
            if (playerController != null && playerController.CharacterData != null && playerController.CharacterData.combatSkills != null)
                knownSkillsView.AddRange(playerController.CharacterData.combatSkills);
            NinaSharedSkills.AppendTo(knownSkillsView);
            return knownSkillsView;
        }
    }

    public IReadOnlyList<SkillSO> EquippedSkills => equippedSkills;
    public IReadOnlyList<BasicSkillsSO> GroundBasicSkills => GetBasicSkillList(BasicSkillContext.Grounded);
    public IReadOnlyList<BasicSkillsSO> AirBasicSkills => GetBasicSkillList(BasicSkillContext.Airborne);
    public SkillSO AnimationEventSkill { get; private set; }

    private int nextGroundBasicSkillIndex;
    private int nextAirBasicSkillIndex;

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
        if (skill == null || playerController == null) return false;
        foreach (var known in KnownSkills) if (known == skill) return true;
        return false;
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

    public bool TryReserveNextBasicSkill(BasicSkillContext context, out BasicSkillsSO skill)
    {
        skill = null;
        IReadOnlyList<BasicSkillsSO> skills = GetBasicSkillList(context);
        if (skills == null || skills.Count == 0)
        {
            return false;
        }

        int nextIndex = GetNextBasicSkillIndex(context);
        for (int offset = 0; offset < skills.Count; offset++)
        {
            int index = (nextIndex + offset) % skills.Count;
            BasicSkillsSO candidate = skills[index];
            if (candidate != null && candidate.Context == context)
            {
                skill = candidate;
                SetNextBasicSkillIndex(context, (index + 1) % skills.Count);
                return true;
            }
        }

        return false;
    }

    public void ResetBasicSkillCombo(BasicSkillContext context)
    {
        SetNextBasicSkillIndex(context, 0);
    }

    public void ResetAllBasicSkillCombos()
    {
        nextGroundBasicSkillIndex = 0;
        nextAirBasicSkillIndex = 0;
    }

    public void SetAnimationEventSkill(SkillSO skill)
    {
        AnimationEventSkill = skill;
    }

    private void OnValidate()
    {
        EnsureSlotCount();
        ValidateBasicSkillContexts(groundBasicSkills, BasicSkillContext.Grounded, "Ground Basic Skills");
        ValidateBasicSkillContexts(airBasicSkills, BasicSkillContext.Airborne, "Air Basic Skills");

        if (playerController != null && playerController.CharacterData != null)
        {
            ValidateBasicSkillContexts(playerController.CharacterData.groundBasicSkills, BasicSkillContext.Grounded, "CharacterData Ground Basic Skills");
            ValidateBasicSkillContexts(playerController.CharacterData.airBasicSkills, BasicSkillContext.Airborne, "CharacterData Air Basic Skills");
        }
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
        // The manager lives in Bootstrap while Lucian can be respawned or
        // swapped. The combat PlayerRoot is the authoritative owner of the
        // BasicSkills currently being requested.
        Transform activePlayerRoot = RealTimeCombatManager.Instance != null
            ? RealTimeCombatManager.Instance.PlayerRoot
            : null;
        if (activePlayerRoot != null)
        {
            SquadCharacterController activeController = activePlayerRoot.GetComponentInChildren<SquadCharacterController>(true);
            if (activeController != null)
            {
                playerController = activeController;
                return;
            }
        }

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

    private IReadOnlyList<BasicSkillsSO> GetBasicSkillList(BasicSkillContext context)
    {
        // The local character can spawn after this manager. Resolve again at
        // selection time so the combo always comes from the active CharacterData.
        ResolveReferences();
        CharacterData characterData = playerController != null ? playerController.CharacterData : null;
        if (characterData != null && HasCharacterBasicSkillConfiguration(characterData))
        {
            return context == BasicSkillContext.Airborne
                ? characterData.airBasicSkills
                : characterData.groundBasicSkills;
        }

        // Compatibility for scenes that have not yet migrated their authoring
        // data to CharacterData. New characters should use CharacterData only.
        return context == BasicSkillContext.Airborne ? airBasicSkills : groundBasicSkills;
    }

    private static bool HasCharacterBasicSkillConfiguration(CharacterData characterData)
    {
        return characterData != null &&
               ((characterData.groundBasicSkills != null && characterData.groundBasicSkills.Count > 0) ||
                (characterData.airBasicSkills != null && characterData.airBasicSkills.Count > 0));
    }

    private int GetNextBasicSkillIndex(BasicSkillContext context)
    {
        return context == BasicSkillContext.Airborne ? nextAirBasicSkillIndex : nextGroundBasicSkillIndex;
    }

    private void SetNextBasicSkillIndex(BasicSkillContext context, int value)
    {
        if (context == BasicSkillContext.Airborne)
        {
            nextAirBasicSkillIndex = value;
            return;
        }

        nextGroundBasicSkillIndex = value;
    }

    private void ValidateBasicSkillContexts(
        List<BasicSkillsSO> skills,
        BasicSkillContext expectedContext,
        string listName)
    {
        if (skills == null)
        {
            return;
        }

        foreach (BasicSkillsSO skill in skills)
        {
            if (skill != null && skill.Context != expectedContext)
            {
                Debug.LogWarning(
                    "[SkillsManager] '" + skill.name + "' est configure " + skill.Context +
                    " mais se trouve dans '" + listName + "'. Il sera ignore.",
                    this);
            }
        }
    }
}
