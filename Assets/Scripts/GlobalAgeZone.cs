using UnityEngine;

public class GlobalAgeZone : MonoBehaviour
{
    public float radius = 5f;
    public float softness = 2f;
    [Range(0f, 1f)] public float amount = 1f;

    void Update()
    {
        Shader.SetGlobalVector("_AgeCenter", transform.position);
        Shader.SetGlobalFloat("_AgeRadius", radius);
        Shader.SetGlobalFloat("_AgeSoftness", softness);
        Shader.SetGlobalFloat("_AgeAmount", amount);
    }
}