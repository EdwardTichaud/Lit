using System.Collections.Generic;

// Registre local des triggers reseau (par ID stable).
public static class NetcodeTriggerRegistry
{
    private static readonly Dictionary<uint, ReturnHomeTrigger> returnHomeTriggers = new Dictionary<uint, ReturnHomeTrigger>();
    private static readonly Dictionary<uint, HubCompanionSwapTrigger> hubSwapTriggers = new Dictionary<uint, HubCompanionSwapTrigger>();
    private static readonly Dictionary<uint, LabyrinthStartTrigger> labyrinthTriggers = new Dictionary<uint, LabyrinthStartTrigger>();

    public static void Register(ReturnHomeTrigger trigger, uint id)
    {
        if (trigger == null || id == 0u)
        {
            return;
        }

        returnHomeTriggers[id] = trigger;
    }

    public static void Unregister(ReturnHomeTrigger trigger, uint id)
    {
        if (trigger == null || id == 0u)
        {
            return;
        }

        if (returnHomeTriggers.TryGetValue(id, out ReturnHomeTrigger existing) && existing == trigger)
        {
            returnHomeTriggers.Remove(id);
        }
    }

    public static bool TryGetReturnHome(uint id, out ReturnHomeTrigger trigger)
    {
        return returnHomeTriggers.TryGetValue(id, out trigger);
    }

    public static void Register(HubCompanionSwapTrigger trigger, uint id)
    {
        if (trigger == null || id == 0u)
        {
            return;
        }

        hubSwapTriggers[id] = trigger;
    }

    public static void Unregister(HubCompanionSwapTrigger trigger, uint id)
    {
        if (trigger == null || id == 0u)
        {
            return;
        }

        if (hubSwapTriggers.TryGetValue(id, out HubCompanionSwapTrigger existing) && existing == trigger)
        {
            hubSwapTriggers.Remove(id);
        }
    }

    public static bool TryGetHubSwap(uint id, out HubCompanionSwapTrigger trigger)
    {
        return hubSwapTriggers.TryGetValue(id, out trigger);
    }

    public static void Register(LabyrinthStartTrigger trigger, uint id)
    {
        if (trigger == null || id == 0u)
        {
            return;
        }

        labyrinthTriggers[id] = trigger;
    }

    public static void Unregister(LabyrinthStartTrigger trigger, uint id)
    {
        if (trigger == null || id == 0u)
        {
            return;
        }

        if (labyrinthTriggers.TryGetValue(id, out LabyrinthStartTrigger existing) && existing == trigger)
        {
            labyrinthTriggers.Remove(id);
        }
    }

    public static bool TryGetLabyrinth(uint id, out LabyrinthStartTrigger trigger)
    {
        return labyrinthTriggers.TryGetValue(id, out trigger);
    }
}
