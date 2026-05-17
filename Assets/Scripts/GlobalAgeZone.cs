using UnityEngine;

// Ancien pont shader global. Il est conserve pour ne pas casser les scenes qui
// le referencent, mais la logique canonique passe par AgeManager et les torches
// locales via LocalRuntimeAgeTrigger.
public class GlobalAgeZone : MonoBehaviour
{
    public float radius = 5f;
    public float softness = 2f;
    [Range(0f, 666f)] public float amount = AgeManager.DefaultStartYear;

    [SerializeField, Tooltip("Desactive par defaut: les torches locales ecrivent les materiaux concernes.")]
    private bool writeShaderGlobals;
    [SerializeField, Tooltip("Si les globals sont actifs, utilise l'age courant d'AgeManager.")]
    private bool useAgeManager = true;

    private void Update()
    {
        if (!writeShaderGlobals)
        {
            return;
        }

        float resolvedAmount = amount;
        if (useAgeManager && AgeManager.ActiveInstance != null)
        {
            resolvedAmount = AgeManager.ActiveInstance.CurrentYear;
        }

        Shader.SetGlobalVector("_AgeCenter", transform.position);
        Shader.SetGlobalFloat("_AgeRadius", radius);
        Shader.SetGlobalFloat("_AgeSoftness", softness);
        Shader.SetGlobalFloat("_AgeAmount", resolvedAmount);
    }
}
