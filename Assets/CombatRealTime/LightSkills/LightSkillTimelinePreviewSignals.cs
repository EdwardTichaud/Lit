using UnityEngine;

/// <summary>Visible feedback for Signal markers while authoring in AnimationLab.</summary>
[DisallowMultipleComponent]
public sealed class LightSkillTimelinePreviewSignals : MonoBehaviour
{
    [SerializeField] private string lastSignal;
    public string LastSignal => lastSignal;
    public void RecordSignal(string signal) => Report(signal == "StartSequence" ? "Start" : signal);

    public void HandleStartSequence() => Report("Start");
    public void HandleRearShot() => Report("RearShot");
    public void HandleImpulse() => Report("Impulse");
    public void HandleSpawnProjectile() => Report("SpawnProjectile");
    public void HandleLaunchProjectile() => Report("LaunchProjectile");
    public void HandleSpawnImpactVfx() => Report("SpawnImpactVfx");
    public void HandleResolveDamage() => Report("ResolveDamage");

    private void Report(string signal)
    {
        lastSignal = signal;
        Debug.Log("[LightSkill Preview] " + signal, this);
    }
}
