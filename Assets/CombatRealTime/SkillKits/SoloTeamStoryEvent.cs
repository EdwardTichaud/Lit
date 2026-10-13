using UnityEngine;

/// <summary>Bind these methods to existing narrative UnityEvents.</summary>
public sealed class SoloTeamStoryEvent : MonoBehaviour
{
    [SerializeField] private CombatSkillKit member;
    [SerializeField, Range(0, 3)] private int slot;
    public void RecruitMember() => SoloTeamProgression.Resolve()?.RecruitMember(member);
    public void UnlockSkill() => SoloTeamProgression.Resolve()?.UnlockSkill(member, slot);
}
