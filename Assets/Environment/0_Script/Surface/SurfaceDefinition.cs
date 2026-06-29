using UnityEngine;

[CreateAssetMenu(fileName = "SurfaceDefinition", menuName = "Scriptable Objects/Surfaces/Surface Definition")]
public class SurfaceDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string surfaceId = "default";
    [SerializeField] private string displayName = "Default";

    [Header("Footsteps")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.8f;
    [SerializeField, Min(0f)] private float footstepPitchMin = 0.95f;
    [SerializeField, Min(0f)] private float footstepPitchMax = 1.05f;

    [Header("Future Effects")]
    [SerializeField] private AudioClip[] impactClips;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private GameObject decalPrefab;

    public string SurfaceId => surfaceId;
    public string DisplayName => displayName;
    public float FootstepVolume => Mathf.Clamp01(footstepVolume);
    public AudioClip[] FootstepClips => footstepClips;
    public AudioClip[] ImpactClips => impactClips;
    public GameObject ImpactVfxPrefab => impactVfxPrefab;
    public GameObject DecalPrefab => decalPrefab;
    public bool HasFootstepClips => footstepClips != null && footstepClips.Length > 0;

    public AudioClip GetRandomFootstepClip(AudioClip previousClip = null)
    {
        return GetRandomClip(footstepClips, previousClip);
    }

    public float GetRandomFootstepPitch()
    {
        float min = Mathf.Max(0f, footstepPitchMin);
        float max = Mathf.Max(min, footstepPitchMax);
        return Mathf.Approximately(min, max) ? min : Random.Range(min, max);
    }

    private static AudioClip GetRandomClip(AudioClip[] clips, AudioClip previousClip)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        AudioClip fallback = null;
        int validClipCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null)
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

        AudioClip selected = null;
        for (int i = 0; i < 8; i++)
        {
            selected = clips[Random.Range(0, clips.Length)];
            if (selected != null && selected != previousClip)
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
