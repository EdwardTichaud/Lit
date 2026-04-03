using System;
using System.Collections.Generic;
using UnityEngine;

public class TorchVisionSystem : MonoBehaviour
{
    private static TorchVisionSystem instance;

    public readonly struct TorchSourceMatch
    {
        public TorchSourceMatch(
            SquadCharacterController controller,
            TorchVisionDefinition vision,
            Vector3 position,
            float distance,
            TorchLightReceiver receiver)
        {
            Controller = controller;
            Vision = vision;
            Position = position;
            Distance = distance;
            Receiver = receiver;
        }

        public SquadCharacterController Controller { get; }
        public TorchVisionDefinition Vision { get; }
        public Vector3 Position { get; }
        public float Distance { get; }
        public TorchLightReceiver Receiver { get; }
    }

    public readonly struct TorchSourceInfo
    {
        public TorchSourceInfo(
            SquadCharacterController controller,
            TorchVisionDefinition vision,
            bool torchEquipped,
            Vector3 position,
            Color color,
            TorchLightReceiver receiver)
        {
            Controller = controller;
            Vision = vision;
            TorchEquipped = torchEquipped;
            Position = position;
            Color = color;
            Receiver = receiver;
        }

        public SquadCharacterController Controller { get; }
        public TorchVisionDefinition Vision { get; }
        public bool TorchEquipped { get; }
        public Vector3 Position { get; }
        public Color Color { get; }
        public TorchLightReceiver Receiver { get; }
    }

    public static event Action<SquadCharacterController, TorchVisionDefinition, TorchVisionDefinition> VisionChanged;
    public static event Action<SquadCharacterController, bool> TorchStateChanged;
    public static event Action TorchSourcesChanged;

    private struct VisionEntry
    {
        public TorchVisionDefinition Vision;
        public float RemainingDuration;
    }

    private readonly Dictionary<SquadCharacterController, VisionEntry> visions = new Dictionary<SquadCharacterController, VisionEntry>();
    private readonly Dictionary<SquadCharacterController, bool> torchStates = new Dictionary<SquadCharacterController, bool>();
    private readonly HashSet<TorchLightReceiver> torchSources = new HashSet<TorchLightReceiver>();

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

    public static void RegisterTorchSource(TorchLightReceiver receiver)
    {
        if (receiver == null)
        {
            return;
        }

        GetOrCreate().RegisterTorchSourceInternal(receiver);
    }

    public static void UnregisterTorchSource(TorchLightReceiver receiver)
    {
        if (receiver == null || instance == null)
        {
            return;
        }

        instance.UnregisterTorchSourceInternal(receiver);
    }

    public static bool TryGetNearestMatchingTorch(
        TorchVisionDefinition vision,
        Vector3 worldPosition,
        float maxDistance,
        bool requireTorchEquipped,
        out TorchSourceMatch match)
    {
        if (instance == null)
        {
            match = default;
            return false;
        }

        return instance.TryGetNearestMatchingTorchInternal(vision, worldPosition, maxDistance, requireTorchEquipped, out match);
    }

    public static void GetTorchSources(List<TorchSourceInfo> results, bool requireTorchEquipped = false)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (instance == null)
        {
            return;
        }

        instance.GetTorchSourcesInternal(results, requireTorchEquipped);
    }

    public static void ResetRuntimeState(string reason = null)
    {
        if (instance == null)
        {
            return;
        }

        instance.ResetRuntimeStateInternal(reason);
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

    private void RegisterTorchSourceInternal(TorchLightReceiver receiver)
    {
        if (receiver == null)
        {
            return;
        }

        if (torchSources.Add(receiver))
        {
            TorchSourcesChanged?.Invoke();
        }
    }

    private void UnregisterTorchSourceInternal(TorchLightReceiver receiver)
    {
        if (receiver == null)
        {
            return;
        }

        if (torchSources.Remove(receiver))
        {
            TorchSourcesChanged?.Invoke();
        }
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

    private bool TryGetNearestMatchingTorchInternal(
        TorchVisionDefinition vision,
        Vector3 worldPosition,
        float maxDistance,
        bool requireTorchEquipped,
        out TorchSourceMatch match)
    {
        match = default;

        if (torchSources.Count == 0)
        {
            return false;
        }

        float bestDistanceSqr = maxDistance > 0f ? maxDistance * maxDistance : float.PositiveInfinity;
        bool found = false;
        List<TorchLightReceiver> staleSources = null;

        foreach (TorchLightReceiver source in torchSources)
        {
            if (source == null)
            {
                if (staleSources == null)
                {
                    staleSources = new List<TorchLightReceiver>();
                }

                staleSources.Add(source);
                continue;
            }

            if (!source.TryGetTorchSourceInfo(
                    out SquadCharacterController controller,
                    out TorchVisionDefinition activeVision,
                    out bool torchEquipped,
                    out Vector3 sourcePosition))
            {
                continue;
            }

            if (controller != null && !IsControllerActive(controller))
            {
                continue;
            }

            if (requireTorchEquipped && !torchEquipped)
            {
                continue;
            }

            if (!IsVisionMatch(vision, activeVision))
            {
                continue;
            }

            float distanceSqr = (sourcePosition - worldPosition).sqrMagnitude;
            if (distanceSqr > bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            match = new TorchSourceMatch(
                controller,
                activeVision,
                sourcePosition,
                Mathf.Sqrt(distanceSqr),
                source);
            found = true;
        }

        if (staleSources != null)
        {
            for (int i = 0; i < staleSources.Count; i++)
            {
                torchSources.Remove(staleSources[i]);
            }
        }

        return found;
    }

    private void GetTorchSourcesInternal(List<TorchSourceInfo> results, bool requireTorchEquipped)
    {
        if (results == null || torchSources.Count == 0)
        {
            return;
        }

        List<TorchLightReceiver> staleSources = null;

        foreach (TorchLightReceiver source in torchSources)
        {
            if (source == null)
            {
                if (staleSources == null)
                {
                    staleSources = new List<TorchLightReceiver>();
                }

                staleSources.Add(source);
                continue;
            }

            if (!source.TryGetTorchSourceInfo(
                    out SquadCharacterController controller,
                    out TorchVisionDefinition activeVision,
                    out bool torchEquipped,
                    out Vector3 sourcePosition))
            {
                continue;
            }

            if (controller != null && !IsControllerActive(controller))
            {
                continue;
            }

            if (requireTorchEquipped && !torchEquipped)
            {
                continue;
            }

            results.Add(new TorchSourceInfo(
                controller,
                activeVision,
                torchEquipped,
                sourcePosition,
                source.CurrentTorchColor,
                source));
        }

        if (staleSources == null)
        {
            return;
        }

        for (int i = 0; i < staleSources.Count; i++)
        {
            torchSources.Remove(staleSources[i]);
        }
    }

    private static bool IsVisionMatch(TorchVisionDefinition requiredVision, TorchVisionDefinition activeVision)
    {
        if (activeVision == null)
        {
            return false;
        }

        return requiredVision == null || activeVision == requiredVision;
    }

    private void ResetRuntimeStateInternal(string reason)
    {
        visions.Clear();
        torchStates.Clear();
        torchSources.Clear();
        TorchSourcesChanged?.Invoke();
    }

    private void OnDisable()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
