using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public sealed class XRayCameraSync : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;

    private Camera xrayCamera;

    private void Reset()
    {
        xrayCamera = GetComponent<Camera>();
        RemoveAudioListener();
    }

    private void Awake()
    {
        xrayCamera = GetComponent<Camera>();
        RemoveAudioListener();
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null || xrayCamera == null)
        {
            return;
        }

        Transform source = mainCamera.transform;
        Transform destination = xrayCamera.transform;
        destination.SetPositionAndRotation(source.position, source.rotation);

        xrayCamera.fieldOfView = mainCamera.fieldOfView;
        xrayCamera.nearClipPlane = mainCamera.nearClipPlane;
        xrayCamera.farClipPlane = mainCamera.farClipPlane;
        xrayCamera.orthographic = mainCamera.orthographic;
        xrayCamera.orthographicSize = mainCamera.orthographicSize;
        xrayCamera.projectionMatrix = mainCamera.projectionMatrix;
    }

    public void SetMainCamera(Camera cameraToFollow)
    {
        mainCamera = cameraToFollow;
    }

    private void RemoveAudioListener()
    {
        AudioListener listener = GetComponent<AudioListener>();
        if (listener != null)
        {
            if (Application.isPlaying)
            {
                Destroy(listener);
            }
            else
            {
                DestroyImmediate(listener);
            }
        }
    }

    private void OnValidate()
    {
        xrayCamera = GetComponent<Camera>();
        RemoveAudioListener();
    }
}
