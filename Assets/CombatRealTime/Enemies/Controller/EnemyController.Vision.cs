using UnityEngine;

public sealed partial class EnemyController
{
    [SerializeField, Tooltip("Origine du regard. Sans reference, utilise la racine du personnage.")]
    private Transform visionOrigin;
    private CharacterVisionSettings fallbackVision;
    private float? visionDistanceOverride;
    private CharacterVisionSettings VisionSettings => Health != null && Health.CharacterData != null
        ? Health.CharacterData.vision : fallbackVision ??= new CharacterVisionSettings();
    public float MaximumDistance => visionDistanceOverride ?? VisionSettings.maximumDistance;

    public bool TryEvaluate(Transform target, out float distance, out float angle, out string reason)
    {
        return VisionSettings.TryEvaluate(visionOrigin != null ? visionOrigin : transform, target,
            MaximumDistance, VisionSettings.fieldOfViewDegrees, out distance, out angle, out reason);
    }

    public bool CanSenseNearby(Transform target, float radius)
    {
        return radius > 0f && VisionSettings.TryEvaluate(visionOrigin != null ? visionOrigin : transform,
            target, radius, 360f, out _, out _, out _);
    }

    // Temporary instance override never changes the shared CharacterData asset.
    public void SetMaximumDistance(float distance) => visionDistanceOverride = Mathf.Max(0.1f, distance);
    public bool CanSee(Transform target) => TryEvaluate(target, out _, out _, out _);
}
