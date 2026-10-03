using UnityEngine;

/// <summary>Reserved per-material-slot shader contract. Never modifies shared materials.</summary>
public static class LitTacticalMaskProperties
{
    public static readonly int Version = Shader.PropertyToID("_LitMaskVersion");
    public static readonly int Enabled = Shader.PropertyToID("_LitMaskEnabled");
    public static readonly int[] Vectors = {
        Shader.PropertyToID("_LitMaskTarget0"), Shader.PropertyToID("_LitMaskTarget1"),
        Shader.PropertyToID("_LitMaskSettings"), Shader.PropertyToID("_LitMaskCameraPosition"),
        Shader.PropertyToID("_LitMaskCameraForward") };
    public struct Data
    {
        public Vector4 target0, target1, settings, cameraPosition, cameraForward;
        public void Write(MaterialPropertyBlock block)
        {
            block.SetFloat(Enabled, 1);
            block.SetVector(Vectors[0], target0); block.SetVector(Vectors[1], target1);
            block.SetVector(Vectors[2], settings); block.SetVector(Vectors[3], cameraPosition);
            block.SetVector(Vectors[4], cameraForward);
        }
    }
    public static bool Supports(Material material)
    {
        if (material == null || material.shader == null || !material.shader.isSupported ||
            !material.HasProperty(Version) || material.GetFloat(Version) < 1 || !material.HasProperty(Enabled)) return false;
        foreach (int property in Vectors) if (!material.HasProperty(property)) return false;
        return true;
    }
}
