using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// AnimationLab-only authoring entry point for a CameraProfilSO. Playback is
/// implemented in the editor and uses only the preview camera hierarchy.
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraProfilPreviewTool : MonoBehaviour
{
    [SerializeField] private CameraProfilSO cameraProfil;
    [SerializeField, Tooltip("Optional explicit anchor. Defaults to Lucian_Anchor under AnimationLab.")]
    private Transform anchorOverride;
    [SerializeField, Tooltip("Optional explicit preview camera. Defaults to Preview_MainCamera.")]
    private Camera previewCameraOverride;
    [SerializeField, Tooltip("Optional explicit Cinemachine Brain. Defaults to the preview camera's Brain.")]
    private CinemachineBrain previewBrainOverride;

    public CameraProfilSO CameraProfil => cameraProfil;

    public bool TryResolvePreview(out Transform anchor, out Camera previewCamera, out CinemachineBrain previewBrain, out string error)
    {
        anchor = anchorOverride != null ? anchorOverride : FindNamedChild("Lucian_Anchor");
        previewCamera = previewCameraOverride != null ? previewCameraOverride : FindPreviewCamera();
        previewBrain = previewBrainOverride != null
            ? previewBrainOverride
            : previewCamera != null ? previewCamera.GetComponent<CinemachineBrain>() : null;

        if (cameraProfil == null)
        {
            error = "CameraProfilSO manquant.";
            return false;
        }
        if (anchor == null)
        {
            error = "Lucian_Anchor introuvable. Assignez un ancrage de secours.";
            return false;
        }
        if (previewCamera == null)
        {
            error = "Preview_MainCamera introuvable. Assignez une caméra de secours.";
            return false;
        }
        if (previewBrain == null)
        {
            error = "CinemachineBrain introuvable sur la caméra de preview. Assignez-en un de secours.";
            return false;
        }

        error = null;
        return true;
    }

    private Transform FindNamedChild(string childName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == childName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private Camera FindPreviewCamera()
    {
        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i].name == "Preview_MainCamera")
            {
                return cameras[i];
            }
        }

        return cameras.Length > 0 ? cameras[0] : null;
    }
}
