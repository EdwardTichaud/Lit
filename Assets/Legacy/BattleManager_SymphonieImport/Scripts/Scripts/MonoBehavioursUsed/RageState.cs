using UnityEngine;

[RequireComponent(typeof(CharacterUnit))]
public class RageState : UnitStateEffects
{
    private bool isEnraged;

    private CharacterUnit unit;

    protected override void Awake()
    {
        base.Awake();
        unit = GetComponent<CharacterUnit>();
    }

    public void OnRageChanged(float currentRage)
    {
        if (!isEnraged && currentRage >= GetMaxRage())
            EnterRage();
        else if (isEnraged && currentRage <= GetBaseRage())
            ExitRage();
    }

    private float GetMaxRage() => unit != null && unit.Data != null ? unit.Data.maxRage : 100f;
    private float GetBaseRage() => unit != null && unit.Data != null ? unit.Data.baseRage : 0f;

    private void EnterRage()
    {
        isEnraged = true;
        EnterState();
    }

    private void ExitRage()
    {
        isEnraged = false;
        ExitState();
    }
}
