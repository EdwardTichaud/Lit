using UnityEngine;

[RequireComponent(typeof(CharacterUnit))]
public class RageSystem : MonoBehaviour
{
    private CharacterUnit unit;
    private RageState rageState;

    private void Awake()
    {
        unit = GetComponent<CharacterUnit>();
        rageState = GetComponent<RageState>();
    }

    public void AddRage(float damage)
    {
        if (unit == null || unit.Data == null) return;
        unit.currentRage = Mathf.Clamp(unit.currentRage + damage, unit.Data.baseRage, unit.Data.maxRage);
        if (unit.customBar != null)
            unit.customBar.SetValue(unit.currentRage);
        rageState?.OnRageChanged(unit.currentRage);
    }

    public void ConsumeRage()
    {
        if (unit == null || unit.Data == null) return;
        unit.currentRage = unit.Data.baseRage;
        if (unit.customBar != null)
            unit.customBar.SetValue(unit.currentRage);
        rageState?.OnRageChanged(unit.currentRage);
    }

    public bool IsEnraged => unit != null && unit.Data != null && unit.currentRage >= unit.Data.maxRage;

    public float CalculateBonusDamage()
    {
        if (unit == null || unit.Data == null) return 0;
        return Mathf.RoundToInt(unit.currentRage * unit.Data.rageDamageMultiplier);
    }
}
