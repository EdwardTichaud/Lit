using UnityEngine;

[DisallowMultipleComponent]
public class TorchLightReceiver : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private Light targetLight;
    [SerializeField] private bool searchInChildren = true;
    [SerializeField] private bool disableColorTemperatureWhenColored = true;

    [Header("Owner")]
    [SerializeField] private SquadCharacterController owner;
    [SerializeField] private bool searchOwnerInParents = true;

    private Color defaultColor;
    private bool defaultUseColorTemperature;
    private float defaultColorTemperature;
    private bool hasDefault;

    private void Awake()
    {
        CacheLight();
        CacheOwner();
    }

    private void OnEnable()
    {
        CacheLight();
        CacheOwner();
        TorchVisionSystem.GetOrCreate();
        TorchVisionSystem.VisionChanged += OnVisionChanged;
        ApplyVision(GetOwnerVision());
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

    private void CacheOwner()
    {
        if (owner != null)
        {
            return;
        }

        if (searchOwnerInParents)
        {
            owner = GetComponentInParent<SquadCharacterController>(true);
        }
    }

    private TorchVisionDefinition GetOwnerVision()
    {
        if (owner == null)
        {
            return null;
        }

        return TorchVisionSystem.GetVisionFor(owner);
    }

    private void OnVisionChanged(SquadCharacterController controller, TorchVisionDefinition previous, TorchVisionDefinition current)
    {
        if (controller != owner)
        {
            return;
        }

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
