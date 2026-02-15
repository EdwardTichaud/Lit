using System.Collections.Generic;
using UnityEngine;

// Runtime singleton qui applique la combustion amelioree sur la squad.
public class ImprovedCombustionRuntime : MonoBehaviour
{
    private class RuntimeEntry
    {
        public int addedSeconds;
        public float interval;
        public bool requireTorch;
        public float timer;
    }

    private static ImprovedCombustionRuntime instance;
    private readonly Dictionary<ImprovedCombustionEffect, RuntimeEntry> entries = new Dictionary<ImprovedCombustionEffect, RuntimeEntry>();

    public static ImprovedCombustionRuntime GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject runner = new GameObject("ImprovedCombustionRuntime");
        instance = runner.AddComponent<ImprovedCombustionRuntime>();
        DontDestroyOnLoad(runner);
        return instance;
    }

    public void Register(ImprovedCombustionEffect effect, int addedSeconds, float interval, bool requireTorch)
    {
        if (effect == null)
        {
            return;
        }

        if (!entries.TryGetValue(effect, out RuntimeEntry entry))
        {
            entry = new RuntimeEntry();
            entries[effect] = entry;
        }

        entry.addedSeconds = Mathf.Max(0, addedSeconds);
        entry.interval = Mathf.Max(0.01f, interval);
        entry.requireTorch = requireTorch;
        if (entry.timer > entry.interval)
        {
            entry.timer = 0f;
        }
    }

    private void Update()
    {
        if (entries.Count == 0)
        {
            return;
        }

        float delta = Time.deltaTime;
        foreach (KeyValuePair<ImprovedCombustionEffect, RuntimeEntry> pair in entries)
        {
            RuntimeEntry entry = pair.Value;
            if (entry == null || entry.interval <= 0f || entry.addedSeconds <= 0)
            {
                continue;
            }

            entry.timer += delta;
            if (entry.timer < entry.interval)
            {
                continue;
            }

            int ticks = Mathf.FloorToInt(entry.timer / entry.interval);
            entry.timer -= ticks * entry.interval;

            ApplyToSquad(entry, ticks);
        }
    }

    private void ApplyToSquad(RuntimeEntry entry, int ticks)
    {
        if (ticks <= 0)
        {
            return;
        }

        SquadManager manager = SquadManager.Instance;
        if (manager == null || manager.squadCharacters == null || manager.squadCharacters.Count == 0)
        {
            return;
        }

        int totalSeconds = entry.addedSeconds * ticks;
        for (int i = 0; i < manager.squadCharacters.Count; i++)
        {
            GameObject character = manager.squadCharacters[i];
            if (character == null)
            {
                continue;
            }

            SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
            if (controller == null)
            {
                continue;
            }

            if (entry.requireTorch && !controller.HasTorchItem)
            {
                continue;
            }

            controller.AddTorchSeconds(totalSeconds);
        }
    }
}
