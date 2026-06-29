using UnityEngine;

[DisallowMultipleComponent]
public class SurfaceProvider : MonoBehaviour
{
    [SerializeField] private SurfaceDefinition surface;

    public SurfaceDefinition Surface => surface;
}
