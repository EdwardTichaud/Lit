using System;
using UnityEngine;

public class CombatHealth : MonoBehaviour
{
    [SerializeField, Min(1), Tooltip("PV max utilises par le combat.")]
    private int maxHp = 8;
    [SerializeField, Min(0), Tooltip("PV courants. 0 au demarrage est remplace par les PV max.")]
    private int currentHp;
    [SerializeField, Tooltip("Reinitialise les PV au Start si currentHp vaut 0.")]
    private bool initializeEmptyHealthToMax = true;

    public event Action<CombatHealth> HealthChanged;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public bool IsDead => currentHp <= 0;

    private void Awake()
    {
        if (initializeEmptyHealthToMax && currentHp <= 0)
        {
            currentHp = maxHp;
        }

        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
    }

    public void SetHealth(int current, int max)
    {
        maxHp = Mathf.Max(1, max);
        currentHp = Mathf.Clamp(current, 0, maxHp);
        HealthChanged?.Invoke(this);
    }

    public int ApplyDamage(int amount)
    {
        int sanitized = Mathf.Max(0, amount);
        if (sanitized <= 0 || currentHp <= 0)
        {
            return 0;
        }

        int before = currentHp;
        currentHp = Mathf.Max(0, currentHp - sanitized);
        HealthChanged?.Invoke(this);
        return before - currentHp;
    }

    public void RestoreToMax()
    {
        currentHp = maxHp;
        HealthChanged?.Invoke(this);
    }
}
