using UnityEngine;

[CreateAssetMenu(fileName = "SurfaceDefinition", menuName = "Scriptable Objects/Surfaces/Surface Definition")]
public class SurfaceDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string surfaceId = "default";
    [SerializeField] private string displayName = "Default";

    [Header("Footsteps")]
    [SerializeField] private AudioClipSO[] footstepClips;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.8f;
    [SerializeField, Min(0f)] private float footstepPitchMin = 0.95f;
    [SerializeField, Min(0f)] private float footstepPitchMax = 1.05f;

    [Header("Future Effects")]
    [SerializeField] private AudioClipSO[] impactClips;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private GameObject decalPrefab;

    public string SurfaceId => surfaceId;
    public string DisplayName => displayName;
    public float FootstepVolume => Mathf.Clamp01(footstepVolume);
    public AudioClipSO[] FootstepClips => footstepClips;
    public AudioClipSO[] ImpactClips => impactClips;
    public GameObject ImpactVfxPrefab => impactVfxPrefab;
    public GameObject DecalPrefab => decalPrefab;
    public bool HasFootstepClips => HasValidClip(footstepClips);
    public bool HasImpactClips => HasValidClip(impactClips);

    public AudioClipSO GetRandomFootstepClip(AudioClipSO previousClip = null)
    {
        return GetRandomClip(footstepClips, previousClip);
    }

    public AudioClipSO GetRandomImpactClip(AudioClipSO previousClip = null)
    {
        return GetRandomClip(impactClips, previousClip);
    }

    public float GetRandomFootstepPitch()
    {
        float min = Mathf.Max(0f, footstepPitchMin);
        float max = Mathf.Max(min, footstepPitchMax);
        return Mathf.Approximately(min, max) ? min : Random.Range(min, max);
    }

    private static bool HasValidClip(AudioClipSO[] clips)
    {
        if (clips == null)
        {
            return false;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].audioClip != null)
            {
                return true;
            }
        }

        return false;
    }

    private static AudioClipSO GetRandomClip(AudioClipSO[] clips, AudioClipSO previousClip)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        AudioClipSO fallback = null;
        int validClipCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClipSO clip = clips[i];
            if (clip == null || clip.audioClip == null)
            {
                continue;
            }

            fallback = clip;
            validClipCount++;
        }

        if (validClipCount == 0)
        {
            return null;
        }

        if (validClipCount == 1)
        {
            return fallback;
        }

        AudioClipSO selected = null;
        for (int i = 0; i < 8; i++)
        {
            selected = clips[Random.Range(0, clips.Length)];
            if (selected != null && selected.audioClip != null && selected != previousClip)
            {
                return selected;
            }
        }

        return fallback;
    }

    private void OnValidate()
    {
        footstepVolume = Mathf.Clamp01(footstepVolume);
        footstepPitchMin = Mathf.Max(0f, footstepPitchMin);
        footstepPitchMax = Mathf.Max(footstepPitchMin, footstepPitchMax);
    }
}
