using System;
using System.Collections.Generic;
using UnityEngine;

public class TorchVisionSystem : MonoBehaviour
{
    private static TorchVisionSystem instance;

    public static event Action<SquadCharacterController, TorchVisionDefinition, TorchVisionDefinition> VisionChanged;
    public static event Action<SquadCharacterController, bool> TorchStateChanged;

    private struct VisionEntry
    {
        public TorchVisionDefinition Vision;
        public float RemainingDuration;
    }

    private readonly Dictionary<SquadCharacterController, VisionEntry> visions = new Dictionary<SquadCharacterController, VisionEntry>();
    private readonly Dictionary<SquadCharacterController, bool> torchStates = new Dictionary<SquadCharacterController, bool>();

    public static TorchVisionDefinition CurrentVision
    {
        get
        {
            if (instance == null)
            {
                return null;
            }

            return instance.GetVisionForInternal(GetCurrentController());
        }
    }

    public static TorchVisionDefinition GetVisionFor(SquadCharacterController controller)
    {
        return instance != null ? instance.GetVisionForInternal(controller) : null;
    }

    public static bool IsTorchEquipped()
    {
        return IsTorchEquipped(GetCurrentController());
    }

    public static bool IsTorchEquipped(SquadCharacterController controller)
    {
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
        SquadCharacterController controller = GetCurrentController();
        if (controller == null)
        {
            return false;
        }

        return SetVisionFor(controller, vision, durationSeconds);
    }

    public static bool SetVisionFor(SquadCharacterController controller, TorchVisionDefinition vision, float durationSeconds = 0f)
    {
        if (controller == null)
        {
            return false;
        }

        return GetOrCreate().SetVisionInternal(controller, vision, durationSeconds);
    }

    public static void ClearVision()
    {
        SquadCharacterController controller = GetCurrentController();
        if (controller != null)
        {
            ClearVisionFor(controller);
        }
    }

    public static void ClearVisionFor(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return;
        }

        if (instance == null)
        {
            return;
        }

        instance.SetVisionInternal(controller, null, 0f);
    }

    public static void GetVisionActivity(TorchVisionDefinition vision, bool requireTorchEquipped, out bool hasAnyVision, out bool hasMatchingVision)
    {
        if (instance == null)
        {
            hasAnyVision = false;
            hasMatchingVision = false;
            return;
        }

        instance.GetVisionActivityInternal(vision, requireTorchEquipped, out hasAnyVision, out hasMatchingVision);
    }

    private static SquadCharacterController GetCurrentController()
    {
        SquadManager manager = SquadManager.Instance;
        if (manager == null || manager.currentCharacter == null)
        {
            return null;
        }

        return manager.currentCharacter.GetComponent<SquadCharacterController>();
    }

    private static bool IsControllerActive(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return false;
        }

        IReadOnlyList<SquadCharacterController> active = SquadCharacterController.ActiveCharacters;
        if (active == null)
        {
            return false;
        }

        for (int i = 0; i < active.Count; i++)
        {
            if (active[i] == controller)
            {
                return true;
            }
        }

        return false;
    }

    private TorchVisionDefinition GetVisionForInternal(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return null;
        }

        if (visions.TryGetValue(controller, out VisionEntry entry))
        {
            return entry.Vision;
        }

        return null;
    }

    private bool SetVisionInternal(SquadCharacterController controller, TorchVisionDefinition vision, float durationSeconds)
    {
        TorchVisionDefinition previous = GetVisionForInternal(controller);
        bool changed = previous != vision;

        VisionEntry entry = new VisionEntry
        {
            Vision = vision,
            RemainingDuration = durationSeconds > 0f ? durationSeconds : 0f
        };
        visions[controller] = entry;

        if (changed)
        {
            VisionChanged?.Invoke(controller, previous, vision);
        }

        return true;
    }

    private void Update()
    {
        RefreshTorchStates();
        UpdateVisionDurations(Time.deltaTime);
        CleanupDestroyedControllers();
    }

    private void RefreshTorchStates()
    {
        IReadOnlyList<SquadCharacterController> active = SquadCharacterController.ActiveCharacters;
        if (active == null)
        {
            return;
        }

        HashSet<SquadCharacterController> activeSet = new HashSet<SquadCharacterController>();

        for (int i = 0; i < active.Count; i++)
        {
            SquadCharacterController controller = active[i];
            if (controller == null)
            {
                continue;
            }

            activeSet.Add(controller);

            bool equipped = controller.IsTorchEquipped;
            if (!torchStates.TryGetValue(controller, out bool cached) || cached != equipped)
            {
                torchStates[controller] = equipped;
                TorchStateChanged?.Invoke(controller, equipped);
            }
        }

        if (torchStates.Count == 0)
        {
            return;
        }

        List<SquadCharacterController> toRemove = null;
        foreach (KeyValuePair<SquadCharacterController, bool> pair in torchStates)
        {
            SquadCharacterController controller = pair.Key;
            if (controller == null || !activeSet.Contains(controller))
            {
                if (pair.Value)
                {
                    TorchStateChanged?.Invoke(controller, false);
                }

                if (toRemove == null)
                {
                    toRemove = new List<SquadCharacterController>();
                }

                toRemove.Add(controller);
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                torchStates.Remove(toRemove[i]);
            }
        }
    }

    private void UpdateVisionDurations(float deltaTime)
    {
        if (visions.Count == 0)
        {
            return;
        }

        List<SquadCharacterController> toClear = null;

        foreach (KeyValuePair<SquadCharacterController, VisionEntry> pair in visions)
        {
            VisionEntry entry = pair.Value;
            if (entry.RemainingDuration <= 0f)
            {
                continue;
            }

            entry.RemainingDuration -= deltaTime;
            if (entry.RemainingDuration <= 0f)
            {
                if (toClear == null)
                {
                    toClear = new List<SquadCharacterController>();
                }

                toClear.Add(pair.Key);
            }
            else
            {
                visions[pair.Key] = entry;
            }
        }

        if (toClear == null)
        {
            return;
        }

        for (int i = 0; i < toClear.Count; i++)
        {
            SquadCharacterController controller = toClear[i];
            if (controller != null)
            {
                SetVisionInternal(controller, null, 0f);
            }
        }
    }

    private void CleanupDestroyedControllers()
    {
        if (visions.Count == 0)
        {
            return;
        }

        List<SquadCharacterController> toRemove = null;
        foreach (KeyValuePair<SquadCharacterController, VisionEntry> pair in visions)
        {
            if (pair.Key != null)
            {
                continue;
            }

            if (toRemove == null)
            {
                toRemove = new List<SquadCharacterController>();
            }

            toRemove.Add(pair.Key);
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            visions.Remove(toRemove[i]);
        }
    }

    private void GetVisionActivityInternal(TorchVisionDefinition vision, bool requireTorchEquipped, out bool hasAnyVision, out bool hasMatchingVision)
    {
        hasAnyVision = false;
        hasMatchingVision = false;

        if (visions.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<SquadCharacterController, VisionEntry> pair in visions)
        {
            SquadCharacterController controller = pair.Key;
            if (controller == null)
            {
                continue;
            }

            if (!IsControllerActive(controller))
            {
                continue;
            }

            if (requireTorchEquipped && !controller.IsTorchEquipped)
            {
                continue;
            }

            TorchVisionDefinition activeVision = pair.Value.Vision;
            if (activeVision == null)
            {
                continue;
            }

            hasAnyVision = true;

            if (vision != null && activeVision == vision)
            {
                hasMatchingVision = true;
                return;
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
