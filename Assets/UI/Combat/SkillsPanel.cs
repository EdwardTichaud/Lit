using UnityEngine;

public sealed class SkillsPanel : MonoBehaviour
{
    [SerializeField] private SkillWheel skillWheel;
    [SerializeField] private SkillsManager skillsManager;

    public SkillWheel SkillWheel => skillWheel;

    public bool AssignSkillToSlot(int slotIndex, SkillSO skill)
    {
        return skillsManager != null && skillsManager.EquipSkillAt(slotIndex, skill);
    }

    private void OnValidate()
    {
        if (skillsManager == null)
        {
            skillsManager = GetComponent<SkillsManager>();
        }
    }
}
