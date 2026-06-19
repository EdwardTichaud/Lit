using UnityEngine;

// Optional shader global bridge for spatial data only. The canonical gameplay age
// and _AgeAmount runtime value come from AgeManager, driven only by AncientFlame.
public class GlobalAgeZone : MonoBehaviour
{
    public float radius = 5f;
    public float softness = 2f;
    [Tooltip("Legacy preview value. _AgeAmount is driven by AgeManager at runtime.")]
    [Range(0f, 666f)] public float amount = AgeManager.DefaultStartYear;

    [SerializeField, Tooltip("Desactive par defaut: AgeManager pilote le gameplay temporel et _AgeAmount.")]
    private bool writeShaderGlobals;
    [SerializeField, Tooltip("Conserve pour compatibilite scene; _AgeAmount n'est plus ecrit ici.")]
    private bool useAgeManager = true;

    private void Update()
    {
        if (!writeShaderGlobals)
        {
            return;
        }

        Shader.SetGlobalVector("_AgeCenter", transform.position);
        Shader.SetGlobalFloat("_AgeRadius", radius);
        Shader.SetGlobalFloat("_AgeSoftness", softness);
    }
}
