using UnityEngine;

/// <summary>Visible feedback for Signal markers while authoring in AnimationLab.</summary>
[DisallowMultipleComponent]
public sealed class LightSkillTimelinePreviewSignals : MonoBehaviour
{
    [SerializeField] private string lastSignal;
    public string LastSignal => lastSignal;

    public void StartSequence() => Report("Start");
    public void RearShot() => Report("RearShot");
    public void Impulse() => Report("Impulse");
    public void SpawnProjectile() => Report("SpawnProjectile");
    public void LaunchProjectile() => Report("LaunchProjectile");
    public void SpawnImpactVfx() => Report("SpawnImpactVfx");
    public void ResolveDamage() => Report("ResolveDamage");

    private void Report(string signal)
    {
        lastSignal = signal;
        Debug.Log("[LightSkill Preview] " + signal, this);
    }
}
