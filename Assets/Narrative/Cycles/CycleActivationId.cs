using UnityEngine;

/// <summary>Visual scene adapter for a persistent activation reward.</summary>
[DisallowMultipleComponent]
public sealed class CycleActivationId : MonoBehaviour
{
    [Tooltip("Same stable identifier configured by the Activation reward.")]
    public string activationId;
    [Tooltip("Object to switch. Keep this component on an always-active parent.")]
    public GameObject target;
    [Tooltip("When false, the target is shown until the activation is obtained, then hidden.")]
    public bool activeWhenSet = true;
    private WorldRulesStateManager rules;
    private float nextCheck;

    private void OnEnable() { rules = FindAnyObjectByType<WorldRulesStateManager>(); Apply(); }
    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .25f;
        if (rules == null) rules = FindAnyObjectByType<WorldRulesStateManager>();
        Apply();
    }
    private void Apply()
    {
        if (rules == null || string.IsNullOrWhiteSpace(activationId)) return;
        bool active = rules.TryGetBool(activationId, out bool enabled) && enabled;
        if (!active && rules.TryGetInt(activationId, out int integer)) active = integer != 0;
        if (!active && rules.TryGetString(activationId, out string value)) active = value == "true" || value == "1";
        bool targetActive = activeWhenSet ? active : !active;
        if (target != null && target.activeSelf != targetActive) target.SetActive(targetActive);
    }
}
