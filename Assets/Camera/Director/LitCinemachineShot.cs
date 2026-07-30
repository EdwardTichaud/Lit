using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Scene-facing Cinemachine shot. Add it beside a CinemachineCamera and invoke
/// Activate/Release from a trigger, Timeline signal or gameplay event.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
public sealed class LitCinemachineShot : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField, Min(0f)] private float blendSeconds = 0.35f;
    [SerializeField] private bool releaseWhenDisabled = true;

    private void Reset()
    {
        cinemachineCamera = GetComponent<CinemachineCamera>();
    }

    private void Awake()
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
        }
    }

    private void OnDisable()
    {
        if (releaseWhenDisabled && LitCameraDirector.Instance != null
            && LitCameraDirector.Instance.ActiveCinemachineCamera == cinemachineCamera)
        {
            LitCameraDirector.Instance.ReleaseCinemachine();
        }
    }

    public void Activate()
    {
        LitCameraDirector.EnsureInstance()?.ActivateCinemachine(cinemachineCamera, blendSeconds);
    }

    public void Release()
    {
        if (LitCameraDirector.Instance != null
            && LitCameraDirector.Instance.ActiveCinemachineCamera == cinemachineCamera)
        {
            LitCameraDirector.Instance.ReleaseCinemachine();
        }
    }

}
