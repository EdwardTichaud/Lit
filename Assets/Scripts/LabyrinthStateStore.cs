using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Gere la persistence des labyrinthes (etat par fichier JSON).
public class LabyrinthStateStore : MonoBehaviour
{
    [Header("Labyrinths")]
    [Tooltip("Liste des labyrinthes a sauvegarder/charger.")]
    public List<Labyrinth> labyrinths = new List<Labyrinth>();

    [Header("Persistence")]
    [Tooltip("Charge automatiquement au Start.")]
    public bool loadOnStart = true;
    [Tooltip("Sauvegarde automatiquement au OnDisable.")]
    public bool saveOnDisable = true;

    private void Start()
    {
        if (loadOnStart)
        {
            // Lecture des fichiers de sauvegarde.
            LoadAll();
        }
    }

    private void OnDisable()
    {
        if (saveOnDisable)
        {
            // Sauvegarde tous les labyrinthes connus.
            SaveAll();
        }
    }

    public void SaveAll()
    {
        if (labyrinths == null)
        {
            return;
        }

        for (int i = 0; i < labyrinths.Count; i++)
        {
            Save(labyrinths[i]);
        }
    }

    public void LoadAll()
    {
        if (labyrinths == null)
        {
            return;
        }

        for (int i = 0; i < labyrinths.Count; i++)
        {
            Load(labyrinths[i]);
        }
    }

    public void Save(Labyrinth labyrinth)
    {
        if (labyrinth == null)
        {
            return;
        }

        string path = GetPath(labyrinth);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string json = labyrinth.ToStateJson();
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            File.WriteAllText(path, json);
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"LabyrinthStateStore: echec d'ecriture {path}. {ex.Message}");
        }
    }

    public void Load(Labyrinth labyrinth)
    {
        if (labyrinth == null)
        {
            return;
        }

        string path = GetPath(labyrinth);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            labyrinth.LoadStateJson(json);
        }
        catch (IOException ex)
        {
            Debug.LogWarning($"LabyrinthStateStore: echec de lecture {path}. {ex.Message}");
        }
    }

    private string GetPath(Labyrinth labyrinth)
    {
        string id = !string.IsNullOrWhiteSpace(labyrinth.labyrinthId)
            ? labyrinth.labyrinthId
            : labyrinth.name;

        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            id = id.Replace(c, '_');
        }

        return Path.Combine(Application.persistentDataPath, $"LabyrinthState_{id}.json");
    }
}
