using UnityEngine;

/// <summary>
/// One BlendShape target weight inside a facial preset.
/// </summary>
[System.Serializable]
public class FacialBlendShapeWeight
{
    [Tooltip("Exact BlendShape name as reported by the SkinnedMeshRenderer mesh.")]
    public string blendShapeName;

    [Range(0f, 100f), Tooltip("Target BlendShape weight.")]
    public float weight;
}
