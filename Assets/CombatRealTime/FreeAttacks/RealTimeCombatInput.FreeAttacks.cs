using UnityEngine;

public sealed partial class RealTimeCombatInput
{
    private bool explorationAttackEnabled;
    private void RefreshFreeAttackMode(InputMode mode) => RefreshFreeAttackBindings();

    private void RefreshFreeAttackBindings()
    {
        var manager = GetComponent<RealTimeCombatManager>();
        bool free = manager != null && manager.CanReceiveFreeAttackInput && InputModeCoordinator.CurrentMode == InputMode.Exploration;
        if (free)
        {
            if (!ResolveActions()) return;
            Subscribe();
            basicAttackAction?.Enable();
            actionMap?.FindAction("SelectSkillKit", false)?.Enable();
        }
        else if (explorationAttackEnabled && InputModeCoordinator.CurrentMode != InputMode.Combat)
        {
            basicAttackAction?.Disable();
            actionMap?.FindAction("SelectSkillKit", false)?.Disable();
            CancelBufferedBasicSkills();
        }
        explorationAttackEnabled = free;
    }
}
