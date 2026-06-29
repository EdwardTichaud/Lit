using UnityEngine;

public class CombatSkyboxManager : MonoBehaviour
{
    public static CombatSkyboxManager Instance { get; private set; }

    [Header("Skybox Materials")]
    [SerializeField] private Material battleSkybox;

    private Material defaultSkybox;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        defaultSkybox = RenderSettings.skybox;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            RestoreDefaultSkybox();
            Instance = null;
        }
    }

    public void ApplyBattleSkybox()
    {
        if (battleSkybox != null)
        {
            RenderSettings.skybox = battleSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }

    public void RestoreDefaultSkybox()
    {
        if (defaultSkybox != null)
        {
            RenderSettings.skybox = defaultSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }
}
