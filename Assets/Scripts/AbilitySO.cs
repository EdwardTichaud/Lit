using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Guaranteed exploration capability. Assets never hold player runtime state.</summary>
[CreateAssetMenu(fileName = "Ability", menuName = "Lit/Abilities/Ability")]
public sealed class AbilitySO : ScriptableObject
{
    [Tooltip("Identifiant stable de sauvegarde. Ne pas renommer apres publication.")]
    public string abilityId;
    [FormerlySerializedAs("skillName")] public string displayName;
    [TextArea]
    public string description;
    public Sprite icon;
    [Header("Deblocage")]
    public bool availableAtStart;
    [Tooltip("Optionnel : cycle reel a terminer, ou jalon reel ci-dessous. Ignore les simulations Dev.")]
    public CycleDefinition unlockCycle;
    public string unlockStepId;
    [Tooltip("Connaissance reelle requise en plus du cycle, si assignee.")]
    public KnowledgeSO unlockKnowledge;
    [HideInInspector] public string legacySkillId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public bool CanUnlockFromProgression()
    {
        if (availableAtStart) return true;
        if (unlockCycle == null && unlockKnowledge == null) return false;
        if (unlockKnowledge != null && (KnowledgeManager.Instance == null ||
            !KnowledgeManager.Instance.HasKnowledge(unlockKnowledge))) return false;
        if (unlockCycle == null) return true;
        var progression = CycleProgressionService.Instance;
        return progression != null && (string.IsNullOrWhiteSpace(unlockStepId)
            ? progression.IsCompleted(unlockCycle) : progression.IsStepCompleted(unlockCycle, unlockStepId));
    }
}
