using System;
using UnityEngine;

public class TorchVisionSystem : MonoBehaviour
{
    private static TorchVisionSystem instance;

    public static event Action<TorchVisionDefinition, TorchVisionDefinition> VisionChanged;
    public static event Action<bool> TorchStateChanged;

    [SerializeField] private TorchVisionDefinition currentVision;
    [SerializeField] private float remainingDuration;
    [SerializeField] private bool cachedTorchEquipped;
    [SerializeField] private bool hasCachedTorchState;

    public static TorchVisionDefinition CurrentVision => instance != null ? instance.currentVision : null;

    public static bool IsTorchEquipped()
    {
        SquadManager manager = SquadManager.Instance;
        if (manager == null || manager.currentCharacter == null)
        {
            return false;
        }

        SquadCharacterController controller = manager.currentCharacter.GetComponent<SquadCharacterController>();
        return controller != null && controller.IsTorchEquipped;
    }

    public static TorchVisionSystem GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject host = new GameObject("TorchVisionSystem");
        instance = host.AddComponent<TorchVisionSystem>();
        DontDestroyOnLoad(host);
        return instance;
    }

    public static bool SetVision(TorchVisionDefinition vision, float durationSeconds = 0f)
    {
        return GetOrCreate().SetVisionInternal(vision, durationSeconds);
    }

    public static void ClearVision()
    {
        SetVision(null, 0f);
    }

    private bool SetVisionInternal(TorchVisionDefinition vision, float durationSeconds)
    {
        TorchVisionDefinition previous = currentVision;
        bool changed = previous != vision;

        currentVision = vision;
        remainingDuration = durationSeconds > 0f ? durationSeconds : 0f;

        if (changed)
        {
            VisionChanged?.Invoke(previous, currentVision);
        }

        return true;
    }

    private void Update()
    {
        bool torchEquipped = IsTorchEquipped();
        if (!hasCachedTorchState || torchEquipped != cachedTorchEquipped)
        {
            cachedTorchEquipped = torchEquipped;
            hasCachedTorchState = true;
            TorchStateChanged?.Invoke(torchEquipped);
        }

        if (remainingDuration > 0f)
        {
            remainingDuration -= Time.deltaTime;
            if (remainingDuration <= 0f)
            {
                SetVisionInternal(null, 0f);
            }
        }
    }

    private void OnDisable()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
