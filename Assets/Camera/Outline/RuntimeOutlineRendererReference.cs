using UnityEngine;

/// <summary>
/// Optional explicit outline target for an interactive object. When present,
/// RuntimeOutlineUtility uses only this renderer and never searches the child
/// hierarchy for outline targets.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Lit/Outline Renderer Reference")]
public sealed class RuntimeOutlineRendererReference : MonoBehaviour
{
    [Tooltip("Unique renderer used by the runtime interaction outline. When assigned, child renderers are ignored.")]
    public Renderer outlineRenderer;
}
