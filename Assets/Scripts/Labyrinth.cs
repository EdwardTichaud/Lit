using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Labyrinth", menuName = "Scriptable Objects/Labyrinth")]
// Donnees d'un labyrinthe + etat persistant.
public class Labyrinth : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Identifiant stable utilise pour la sauvegarde.")]
    public string labyrinthId = "labyrinth";

    [Header("Scene References")]
    [Tooltip("Root en scene du labyrinthe (optionnel).")]
    public GameObject labyrinthRoot;
    [Tooltip("Prefab a instancier pour ce labyrinthe.")]
    public GameObject labyrinthPrefab;

    [Header("State")]
    [SerializeField, Tooltip("Etat persistant stocke en JSON.")]
    private LabyrinthState state = new LabyrinthState();

    public LabyrinthState State
    {
        get
        {
            EnsureState();
            return state;
        }
    }

    public void ClearState()
    {
        state = new LabyrinthState();
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        return State.GetBool(key, defaultValue);
    }

    public void SetBool(string key, bool value)
    {
        State.SetBool(key, value);
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        return State.GetInt(key, defaultValue);
    }

    public void SetInt(string key, int value)
    {
        State.SetInt(key, value);
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        return State.GetFloat(key, defaultValue);
    }

    public void SetFloat(string key, float value)
    {
        State.SetFloat(key, value);
    }

    public string GetString(string key, string defaultValue = "")
    {
        return State.GetString(key, defaultValue);
    }

    public void SetString(string key, string value)
    {
        State.SetString(key, value);
    }

    public Vector3 GetVector3(string key, Vector3 defaultValue)
    {
        return State.GetVector3(key, defaultValue);
    }

    public void SetVector3(string key, Vector3 value)
    {
        State.SetVector3(key, value);
    }

    public string ToStateJson()
    {
        EnsureState();
        return JsonUtility.ToJson(state);
    }

    public void LoadStateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        state = JsonUtility.FromJson<LabyrinthState>(json) ?? new LabyrinthState();
    }

    private void EnsureState()
    {
        // S'assure que l'etat est toujours valide.
        if (state == null)
        {
            state = new LabyrinthState();
        }
    }
}

[System.Serializable]
// Etat persistant pour un labyrinthe (stocke par cle/valeur).
public class LabyrinthState
{
    [System.Serializable]
    public class BoolEntry
    {
        [Tooltip("Cle de la valeur booleenne.")]
        public string key;
        [Tooltip("Valeur booleenne.")]
        public bool value;
    }

    [System.Serializable]
    public class IntEntry
    {
        [Tooltip("Cle de la valeur entiere.")]
        public string key;
        [Tooltip("Valeur entiere.")]
        public int value;
    }

    [System.Serializable]
    public class FloatEntry
    {
        [Tooltip("Cle de la valeur float.")]
        public string key;
        [Tooltip("Valeur float.")]
        public float value;
    }

    [System.Serializable]
    public class StringEntry
    {
        [Tooltip("Cle de la valeur string.")]
        public string key;
        [Tooltip("Valeur string.")]
        public string value;
    }

    [System.Serializable]
    public class Vector3Entry
    {
        [Tooltip("Cle de la valeur Vector3.")]
        public string key;
        [Tooltip("Valeur Vector3.")]
        public Vector3 value;
    }

    [Tooltip("Liste des valeurs booleennes.")]
    public List<BoolEntry> bools = new List<BoolEntry>();
    [Tooltip("Liste des valeurs entieres.")]
    public List<IntEntry> ints = new List<IntEntry>();
    [Tooltip("Liste des valeurs flottantes.")]
    public List<FloatEntry> floats = new List<FloatEntry>();
    [Tooltip("Liste des valeurs string.")]
    public List<StringEntry> strings = new List<StringEntry>();
    [Tooltip("Liste des valeurs Vector3.")]
    public List<Vector3Entry> vectors = new List<Vector3Entry>();

    public bool GetBool(string key, bool defaultValue = false)
    {
        BoolEntry entry = FindEntry(bools, key);
        return entry != null ? entry.value : defaultValue;
    }

    public void SetBool(string key, bool value)
    {
        BoolEntry entry = GetOrCreateEntry(bools, key);
        entry.value = value;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        IntEntry entry = FindEntry(ints, key);
        return entry != null ? entry.value : defaultValue;
    }

    public void SetInt(string key, int value)
    {
        IntEntry entry = GetOrCreateEntry(ints, key);
        entry.value = value;
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        FloatEntry entry = FindEntry(floats, key);
        return entry != null ? entry.value : defaultValue;
    }

    public void SetFloat(string key, float value)
    {
        FloatEntry entry = GetOrCreateEntry(floats, key);
        entry.value = value;
    }

    public string GetString(string key, string defaultValue = "")
    {
        StringEntry entry = FindEntry(strings, key);
        return entry != null ? entry.value : defaultValue;
    }

    public void SetString(string key, string value)
    {
        StringEntry entry = GetOrCreateEntry(strings, key);
        entry.value = value;
    }

    public Vector3 GetVector3(string key, Vector3 defaultValue)
    {
        Vector3Entry entry = FindEntry(vectors, key);
        return entry != null ? entry.value : defaultValue;
    }

    public void SetVector3(string key, Vector3 value)
    {
        Vector3Entry entry = GetOrCreateEntry(vectors, key);
        entry.value = value;
    }

    private static T FindEntry<T>(List<T> list, string key) where T : class
    {
        if (list == null)
        {
            return null;
        }

        for (int i = 0; i < list.Count; i++)
        {
            T entry = list[i];
            if (entry == null)
            {
                continue;
            }

            string entryKey = GetKey(entry);
            if (entryKey == key)
            {
                return entry;
            }
        }

        return null;
    }

    private static T GetOrCreateEntry<T>(List<T> list, string key) where T : class, new()
    {
        if (list == null)
        {
            return new T();
        }

        T entry = FindEntry(list, key);
        if (entry != null)
        {
            return entry;
        }

        entry = new T();
        SetKey(entry, key);
        list.Add(entry);
        return entry;
    }

    private static string GetKey(object entry)
    {
        if (entry is BoolEntry boolEntry)
        {
            return boolEntry.key;
        }
        if (entry is IntEntry intEntry)
        {
            return intEntry.key;
        }
        if (entry is FloatEntry floatEntry)
        {
            return floatEntry.key;
        }
        if (entry is StringEntry stringEntry)
        {
            return stringEntry.key;
        }
        if (entry is Vector3Entry vectorEntry)
        {
            return vectorEntry.key;
        }
        return null;
    }

    private static void SetKey(object entry, string key)
    {
        if (entry is BoolEntry boolEntry)
        {
            boolEntry.key = key;
        }
        else if (entry is IntEntry intEntry)
        {
            intEntry.key = key;
        }
        else if (entry is FloatEntry floatEntry)
        {
            floatEntry.key = key;
        }
        else if (entry is StringEntry stringEntry)
        {
            stringEntry.key = key;
        }
        else if (entry is Vector3Entry vectorEntry)
        {
            vectorEntry.key = key;
        }
    }
}
