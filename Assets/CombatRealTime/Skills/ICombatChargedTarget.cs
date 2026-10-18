using UnityEngine;

public interface ICombatChargedTarget
{
    Transform ChargedRoot { get; }
    bool ChargedAlive { get; }
    bool ChargedProtected { get; }
    int ChargedGeneration { get; }
    bool CanReceiveChargedLaunch { get; }
    bool IsChargedAirborne { get; }
    bool BeginChargedLaunch(ChargedSkillSO skill);
    void BeginChargedFloat(float holdSeconds);
    void ReleaseChargedHold();
}

public static class CombatChargedTargets
{
    public static ICombatChargedTarget Resolve(ICombatTarget target) => target?.Root != null
        ? target.Root.GetComponent<ICombatChargedTarget>() : null;
}
