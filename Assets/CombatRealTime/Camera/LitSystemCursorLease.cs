using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared scoped cursor ownership for the existing pointer UI and tactical camera.</summary>
public static class LitSystemCursorLease
{
    private struct Entry { public object owner; public bool visible; public CursorLockMode mode; public int priority; }
    private static readonly List<Entry> entries = new List<Entry>();
    private static bool previousVisible;
    private static CursorLockMode previousMode;
    public static void Acquire(object owner, bool visible, CursorLockMode mode, int priority = 0)
    {
        foreach (var entry in entries) if (ReferenceEquals(entry.owner, owner)) return;
        if (entries.Count == 0) { previousVisible = Cursor.visible; previousMode = Cursor.lockState; }
        int index = entries.Count;
        while (index > 0 && entries[index - 1].priority > priority) index--;
        entries.Insert(index, new Entry { owner = owner, visible = visible, mode = mode, priority = priority });
        Apply();
    }
    public static void Release(object owner)
    {
        bool removed = false;
        for (int i = entries.Count - 1; i >= 0; i--)
            if (ReferenceEquals(entries[i].owner, owner)) { entries.RemoveAt(i); removed = true; }
        if (removed) Apply();
    }
    private static void Apply()
    {
        Cursor.lockState = entries.Count == 0 ? previousMode : entries[entries.Count - 1].mode;
        Cursor.visible = entries.Count == 0 ? previousVisible : entries[entries.Count - 1].visible;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { if (entries.Count > 0) { entries.Clear(); Apply(); } }
}
