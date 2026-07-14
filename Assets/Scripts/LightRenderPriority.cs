using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightRenderPriority : MonoBehaviour
{
    [SerializeField] private bool critical;
    [SerializeField, Range(-100, 100)] private int priority;

    public bool Critical => critical;
    public int Priority => priority;
}
