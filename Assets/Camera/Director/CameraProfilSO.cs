using UnityEngine;

/// <summary>
/// Authoring data for a short, local gameplay-camera presentation.
/// The offset and speeds are expressed in the anchor's local space.
/// </summary>
[CreateAssetMenu(fileName = "CameraProfil", menuName = "Lit/Camera/Camera Profil")]
public sealed class CameraProfilSO : ScriptableObject
{
    [Tooltip("Initial camera position relative to the focus, in the focus local space.")]
    public Vector3 positionOffset = new Vector3(2f, 1.8f, -3.5f);

    [Tooltip("Point looked at by the camera, relative to the focus in its local space.")]
    public Vector3 offset;

    [Tooltip("Continuous local-space displacement in units per second during the hold.")]
    public Vector3 movementSpeed;

    [Min(0f), Tooltip("Real-time blend from the current UCC pose into this profile.")]
    public float startLerp = 0.5f;

    [Min(0f), Tooltip("Real-time duration during which the local movement speed is applied.")]
    public float duration = 1f;

    [Min(0f), Tooltip("Real-time blend from this profile back to the memorised UCC pose.")]
    public float endLerp = 0.5f;

    [Range(1f, 179f), Tooltip("Target field of view used by this profile.")]
    public float fieldOfView = 60f;

    [Min(0f), Tooltip("Real-time FOV interpolation duration from the source pose.")]
    public float fieldOfViewLerp = 0.5f;

    [Header("Munin UI Effect")]
    [Tooltip("HDR intensity applied to MuninUIEffects/LightFrost when this profile starts.")]
    public float muninUiHdrIntensityStart = -10f;

    [Tooltip("HDR intensity reached by MuninUIEffects/LightFrost during this profile.")]
    public float muninUiHdrIntensityEnd = 10f;

    [Min(0f), Tooltip("Real-time interpolation duration for the Munin UI HDR intensity.")]
    public float muninUiHdrIntensityLerp = 1f;
}
