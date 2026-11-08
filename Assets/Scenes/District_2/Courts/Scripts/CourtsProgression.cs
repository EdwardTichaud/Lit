using UnityEngine;

/// <summary>Only projects persistent cycle state onto the authored access objects.</summary>
public sealed class CourtsProgression : MonoBehaviour
{
    public CycleDefinition family;
    public CycleDefinition visits;
    public CycleDefinition finale;
    public GameObject commonAccessBarrier;
    public GameObject visitsBarrier;
    public GameObject nextZonePortal;
    private float nextCheck;

    private void OnEnable() => Apply();
    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .2f;
        Apply();
    }
    private void Apply()
    {
        var service = CycleProgressionService.Instance;
        bool ready = service != null && service.IsReady;
        SetActive(commonAccessBarrier, !ready || !service.IsCompleted(family));
        SetActive(visitsBarrier, !ready || !service.IsCompleted(visits));
        SetActive(nextZonePortal, ready && service.IsCompleted(finale));
    }
    private static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value) target.SetActive(value);
    }
}
