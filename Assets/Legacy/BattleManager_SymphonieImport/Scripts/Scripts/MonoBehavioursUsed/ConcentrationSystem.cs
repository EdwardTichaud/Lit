using UnityEngine;

[RequireComponent(typeof(CharacterUnit))]
public class ConcentrationSystem : MonoBehaviour
{
    public float maxConcentration = 100f;
    public float decayPerSecond = 5f;
    [HideInInspector] public float currentConcentration = 0f;

    private CharacterUnit unit;
    private float lastDamageTime;
    private bool wasFull;
    private UnitStateEffects stateEffects;

    public bool IsFull => currentConcentration >= maxConcentration;

    private void Awake()
    {
        unit = GetComponent<CharacterUnit>();
        stateEffects = GetComponent<UnitStateEffects>();
    }

    private void Update()
    {
        if (currentConcentration > 0f && Time.time > lastDamageTime)
        {
            currentConcentration = Mathf.Max(currentConcentration - decayPerSecond * Time.deltaTime, 0f);
            UpdateBar();
        }
        CheckState();
    }

    public void OnDamageTaken(float damage)
    {
        lastDamageTime = Time.time;
        currentConcentration = Mathf.Clamp(currentConcentration + damage * 0.1f, 0f, maxConcentration);
        UpdateBar();
        CheckState();
    }

    public float CalculateBonusDamage(float baseDamage)
    {
        return IsFull ? baseDamage : 0f;
    }

    private void UpdateBar()
    {
        if (unit != null && unit.customBar != null)
            unit.customBar.SetValue(currentConcentration);
    }

    private void CheckState()
    {
        if (!wasFull && IsFull)
        {
            stateEffects?.EnterState();
            wasFull = true;
        }
        else if (wasFull && !IsFull)
        {
            stateEffects?.ExitState();
            wasFull = false;
        }
    }
}
