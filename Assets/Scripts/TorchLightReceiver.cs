using UnityEngine;

[DisallowMultipleComponent]
public class TorchLightReceiver : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private Light targetLight;
    [SerializeField] private bool searchInChildren = true;
    [SerializeField] private bool disableColorTemperatureWhenColored = true;

    private Color defaultColor;
    private bool defaultUseColorTemperature;
    private float defaultColorTemperature;
    private bool hasDefault;

    private void Awake()
    {
        CacheLight();
    }

    private void OnEnable()
    {
        CacheLight();
        TorchVisionSystem.GetOrCreate();
        TorchVisionSystem.VisionChanged += OnVisionChanged;
        ApplyVision(TorchVisionSystem.CurrentVision);
    }

    private void OnDisable()
    {
        TorchVisionSystem.VisionChanged -= OnVisionChanged;
    }

    private void CacheLight()
    {
        if (targetLight == null)
        {
            targetLight = searchInChildren ? GetComponentInChildren<Light>(true) : GetComponent<Light>();
        }

        if (targetLight == null || hasDefault)
        {
            return;
        }

        defaultColor = targetLight.color;
        defaultUseColorTemperature = targetLight.useColorTemperature;
        defaultColorTemperature = targetLight.colorTemperature;
        hasDefault = true;
    }

    private void OnVisionChanged(TorchVisionDefinition previous, TorchVisionDefinition current)
    {
        ApplyVision(current);
    }

    private void ApplyVision(TorchVisionDefinition vision)
    {
        if (targetLight == null)
        {
            return;
        }

        if (vision == null || vision.useDefaultLightSettings)
        {
            if (hasDefault)
            {
                targetLight.color = defaultColor;
                targetLight.useColorTemperature = defaultUseColorTemperature;
                targetLight.colorTemperature = defaultColorTemperature;
            }
            return;
        }

        targetLight.color = vision.lightColor;

        if (disableColorTemperatureWhenColored)
        {
            targetLight.useColorTemperature = false;
        }
    }
}
