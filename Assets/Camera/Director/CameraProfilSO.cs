using UnityEngine;

/// <summary>
/// Authoring data for a short, local gameplay-camera presentation.
/// The offset and speeds are expressed in the anchor's local space.
/// </summary>
[CreateAssetMenu(fileName = "CameraProfil", menuName = "Lit/Camera/Camera Profil")]
public sealed class CameraProfilSO : ScriptableObject
{
    [Tooltip("Initial camera position relative to the anchor, in the anchor's local space.")]
    public Vector3 offset = new Vector3(2f, 1.8f, -3.5f);

    [Tooltip("Continuous local-space displacement in units per second during the hold.")]
    public Vector3 movementSpeed;

    [Tooltip("Continuous local view rotation in degrees per second during the hold.")]
    public Vector3 rotationSpeed;

    [Min(0f), Tooltip("Real-time blend from the current UCC pose into this profile.")]
    public float startLerp = 0.5f;

    [Min(0f), Tooltip("Real-time duration during which movement and rotation speeds are applied.")]
    public float duration = 1f;

    [Min(0f), Tooltip("Real-time blend from this profile back to the memorised UCC pose.")]
    public float endLerp = 0.5f;

    [Range(1f, 179f), Tooltip("Target field of view used by this profile.")]
    public float fieldOfView = 60f;

    [Min(0f), Tooltip("Real-time FOV interpolation duration from the source pose.")]
    public float fieldOfViewLerp = 0.5f;
}
