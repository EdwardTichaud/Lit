using UnityEngine;

/// <summary>Opt-in variation of the existing confirmed contact sound, never an extra audio layer.</summary>
[DisallowMultipleComponent]
public sealed class CombatContactAudio : MonoBehaviour
{
    public Vector2 lightPitch = new Vector2(1.02f, 1.08f);
    public Vector2 heavyPitch = new Vector2(.88f, .94f);
    [Min(0)] public float lightDamage = 5;
    [Min(.01f)] public float heavyDamage = 15;
    public float LastBasePitch { get; private set; } = 1;
    public int ContactSoundsPlayed { get; private set; }

    public float ResolvePitch(float damage, int generation)
    {
        // Presentation variation must not consume Unity's shared random stream used by enemy decisions.
        uint hash = unchecked((uint)generation * 747796405u + 2891336453u);
        hash = ((hash >> (int)((hash >> 28) + 4)) ^ hash) * 277803737u;
        float variation = ((hash >> 22) ^ hash) / (float)uint.MaxValue;
        float weight = Mathf.InverseLerp(lightDamage, Mathf.Max(lightDamage + .01f, heavyDamage), damage);
        var range = Vector2.Lerp(lightPitch, heavyPitch, weight);
        return Mathf.Clamp(Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), variation), .5f, 1.5f);
    }

    public float ResolvePitch(SkillSO skill, int generation) => skill == null ? 1 :
        ResolvePitch(skill is ChargedSkillSO ? heavyDamage : skill.Damages, generation);

    public bool TryPlay(AudioClipSO clip, Vector3 position, SkillSO skill, int generation)
    {
        if (!isActiveAndEnabled || skill == null || clip == null || clip.audioClip == null) return false;
        var manager = AudioManager.Instance != null ? AudioManager.Instance : AudioManager.EnsureInstance();
        if (manager == null) return false;
        LastBasePitch = ResolvePitch(skill, generation);
        if (manager.PlayClip(clip, position, 1, LastBasePitch) == null) return false;
        ContactSoundsPlayed++;
        return true;
    }
}
