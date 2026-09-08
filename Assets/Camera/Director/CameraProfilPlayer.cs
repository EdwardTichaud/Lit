using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Plays one local camera profile at a time. It delegates camera ownership to
/// LitCameraDirector and never writes the Main Camera transform directly.
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraProfilPlayer : MonoBehaviour
{
    private CinemachineCamera profileCamera;
    private Coroutine playbackRoutine;

    public bool IsPlaying => playbackRoutine != null;

    /// <summary>Cancels this local profile and immediately restores UCC ownership.</summary>
    public void Cancel()
    {
        StopPlayback(releaseCamera: true);
    }

    public static CameraProfilPlayer GetOrCreate(LitCameraDirector director)
    {
        if (director == null)
        {
            return null;
        }

        CameraProfilPlayer player = director.GetComponent<CameraProfilPlayer>();
        return player != null ? player : director.gameObject.AddComponent<CameraProfilPlayer>();
    }

    public bool Play(CameraProfilSO profile, Transform anchor)
    {
        if (profile == null || anchor == null)
        {
            return false;
        }

        LitCameraDirector director = GetComponent<LitCameraDirector>();
        Camera controlledCamera = director != null ? director.ControlledCamera : null;
        if (director == null || controlledCamera == null)
        {
            return false;
        }

        StopPlayback(releaseCamera: true);
        EnsureProfileCamera();
        if (profileCamera == null)
        {
            return false;
        }

        profileCamera.enabled = true;
        playbackRoutine = StartCoroutine(PlayRoutine(director, controlledCamera, profile, anchor));
        return true;
    }

    private void OnDisable()
    {
        StopPlayback(releaseCamera: true);
    }

    private void OnDestroy()
    {
        StopPlayback(releaseCamera: true);
        if (profileCamera != null)
        {
            Destroy(profileCamera.gameObject);
            profileCamera = null;
        }
    }

    private IEnumerator PlayRoutine(
        LitCameraDirector director,
        Camera controlledCamera,
        CameraProfilSO profile,
        Transform anchor)
    {
        Vector3 sourcePosition = controlledCamera.transform.position;
        Quaternion sourceRotation = controlledCamera.transform.rotation;
        float sourceFieldOfView = controlledCamera.fieldOfView;

        SetProfilePose(anchor, profile, 0f);
        profileCamera.Lens.FieldOfView = sourceFieldOfView;
        if (!director.ActivateCinemachine(profileCamera, 0f))
        {
            playbackRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < profile.startLerp && anchor != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = DurationProgress(elapsed, profile.startLerp);
            GetProfilePose(anchor, profile, 0f, out Vector3 destination, out Quaternion destinationRotation);
            profileCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(sourcePosition, destination, t),
                Quaternion.Slerp(sourceRotation, destinationRotation, t));
            profileCamera.Lens.FieldOfView = Mathf.Lerp(sourceFieldOfView, profile.fieldOfView, DurationProgress(elapsed, profile.fieldOfViewLerp));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < profile.duration && anchor != null)
        {
            elapsed += Time.unscaledDeltaTime;
            SetProfilePose(anchor, profile, elapsed);
            profileCamera.Lens.FieldOfView = Mathf.Lerp(sourceFieldOfView, profile.fieldOfView,
                DurationProgress(profile.startLerp + elapsed, profile.fieldOfViewLerp));
            yield return null;
        }

        Vector3 returnPosition = profileCamera.transform.position;
        Quaternion returnRotation = profileCamera.transform.rotation;
        float returnFieldOfView = profileCamera.Lens.FieldOfView;
        elapsed = 0f;
        while (elapsed < profile.endLerp)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = DurationProgress(elapsed, profile.endLerp);
            profileCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(returnPosition, sourcePosition, t),
                Quaternion.Slerp(returnRotation, sourceRotation, t));
            profileCamera.Lens.FieldOfView = Mathf.Lerp(returnFieldOfView, sourceFieldOfView, t);
            yield return null;
        }

        if (director.ActiveCinemachineCamera == profileCamera)
        {
            director.ReleaseCinemachine();
        }

        profileCamera.enabled = false;
        playbackRoutine = null;
    }

    private void StopPlayback(bool releaseCamera)
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        LitCameraDirector director = GetComponent<LitCameraDirector>();
        if (releaseCamera && director != null && director.ActiveCinemachineCamera == profileCamera)
        {
            director.ReleaseCinemachine();
        }

        if (profileCamera != null)
        {
            profileCamera.enabled = false;
        }
    }

    private void EnsureProfileCamera()
    {
        if (profileCamera != null)
        {
            return;
        }

        GameObject profileCameraObject = new GameObject("Camera Profil Runtime");
        profileCameraObject.hideFlags = HideFlags.DontSave;
        profileCamera = profileCameraObject.AddComponent<CinemachineCamera>();
        profileCamera.enabled = false;
    }

    private void SetProfilePose(Transform anchor, CameraProfilSO profile, float holdElapsed)
    {
        GetProfilePose(anchor, profile, holdElapsed, out Vector3 position, out Quaternion rotation);
        profileCamera.transform.SetPositionAndRotation(position, rotation);
    }

    private static void GetProfilePose(Transform anchor, CameraProfilSO profile, float holdElapsed, out Vector3 position, out Quaternion rotation)
    {
        Vector3 localPositionOffset = profile.positionOffset + profile.movementSpeed * holdElapsed;
        position = anchor.TransformPoint(localPositionOffset);
        Vector3 focusPoint = anchor.TransformPoint(profile.offset);
        Vector3 toFocus = focusPoint - position;
        Quaternion lookAtAnchor = toFocus.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toFocus.normalized, anchor.up)
            : anchor.rotation;
        rotation = lookAtAnchor;
    }

    private static float DurationProgress(float elapsed, float duration)
    {
        return duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
    }
}
