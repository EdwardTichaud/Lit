using UnityEngine;

[DisallowMultipleComponent]
public sealed class UIWorldVfxCameraService : MonoBehaviour
{
    public static UIWorldVfxCameraService Instance { get; private set; }

    [SerializeField] private Camera vfxCamera;
    [SerializeField] private Transform vfxRoot;
    [SerializeField] private string visualEffectLayer = "VisualEffect_ScreenSpace";

    public Camera Camera => vfxCamera;
    public Transform VfxRoot => vfxRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (vfxCamera == null)
        {
            Transform authoredCamera = transform.Find("UIWorldVFXCamera");
            vfxCamera = authoredCamera != null ? authoredCamera.GetComponent<Camera>() : null;
        }
        if (vfxCamera == null) vfxCamera = GetComponentInChildren<Camera>(true);
        if (vfxRoot == null && vfxCamera != null) vfxRoot = vfxCamera.transform;
        if (vfxCamera == null) return;
        vfxCamera.enabled = true;
        vfxCamera.clearFlags = CameraClearFlags.Depth;
        vfxCamera.cullingMask = LayerMask.GetMask(visualEffectLayer);
        vfxCamera.useOcclusionCulling = false;
        AudioListener audioListener = vfxCamera.GetComponent<AudioListener>();
        if (audioListener != null) audioListener.enabled = false;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
