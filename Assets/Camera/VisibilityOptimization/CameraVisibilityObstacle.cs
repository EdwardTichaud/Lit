using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraVisibilityObstacle : MonoBehaviour, ICameraVisibilityObstacle
{
    [Header("Camera-player visibility")]
    [SerializeField] private bool preserveForCameraFade = true;
    [SerializeField] private bool neverCullWhenBetweenCameraAndPlayer = true;
    [SerializeField] private bool includeChildRenderers = true;
    [SerializeField, Tooltip("Desactive si cet objet ne doit jamais etre pris comme obstacle XRay, meme s'il est sur le bon layer.")]
    private bool usableByCameraFade = true;

    public bool PreserveForCameraFade => preserveForCameraFade;
    public bool NeverCullWhenBetweenCameraAndPlayer => neverCullWhenBetweenCameraAndPlayer;
    public bool IncludeChildRenderers => includeChildRenderers;
    public bool UsableByCameraFade => usableByCameraFade;
}
