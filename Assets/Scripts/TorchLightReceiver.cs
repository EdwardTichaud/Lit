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

    public SquadCharacterController Owner => owner;

    public Color CurrentTorchColor
    {
        get
        {
            CacheLight();

            if (targetLight != null)
            {
                return targetLight.color;
            }

            TorchVisionDefinition vision = GetOwnerVision();
            if (vision != null && !vision.useDefaultLightSettings)
            {
                return vision.lightColor;
            }

            return hasDefault ? defaultColor : Color.white;
        }
    }

    public Vector3 TorchWorldPosition
    {
        get
        {
            if (targetLight != null)
            {
                return targetLight.transform.position;
            }

            return transform.position;
        }
    }

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
        TorchVisionSystem.RegisterTorchSource(this);
        TorchVisionSystem.VisionChanged += OnVisionChanged;
        ApplyVision(GetOwnerVision());
    }

    private void OnDisable()
    {
        TorchVisionSystem.UnregisterTorchSource(this);
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

    public bool TryGetTorchSourceInfo(
        out SquadCharacterController controller,
        out TorchVisionDefinition vision,
        out bool torchEquipped,
        out Vector3 position)
    {
        CacheOwner();
        CacheLight();

        controller = owner;
        vision = GetOwnerVision();
        torchEquipped = owner != null && owner.IsTorchEquipped;
        position = TorchWorldPosition;

        return controller != null;
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
