using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TorchLightReceiver))]
public class TreasureFinder : MonoBehaviour
{
    [SerializeField] private TorchVisionDefinition requiredVision;
    [SerializeField] private TorchLightReceiver torchReceiver;

    public Vector3 FinderPosition => transform.position;
    public TorchVisionDefinition RequiredVision => requiredVision;

    private void Awake()
    {
        CacheComponents();
        ConfigureTorchSource();
    }

    private void OnEnable()
    {
        CacheComponents();
        ConfigureTorchSource();
    }

    private void OnValidate()
    {
        CacheComponents();
        ConfigureTorchSource();
    }

    public void ConfigureRuntime(TorchVisionDefinition targetVision)
    {
        requiredVision = targetVision;
        CacheComponents();
        ConfigureTorchSource();
    }

    private void CacheComponents()
    {
        if (torchReceiver == null)
        {
            torchReceiver = GetComponent<TorchLightReceiver>();
        }

        if (torchReceiver == null)
        {
            torchReceiver = gameObject.AddComponent<TorchLightReceiver>();
        }
    }

    private void ConfigureTorchSource()
    {
        if (torchReceiver == null)
        {
            return;
        }

        torchReceiver.ConfigureWorldTorchSource(requiredVision, countsAsEquipped: true);
    }
}
