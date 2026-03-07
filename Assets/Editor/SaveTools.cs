using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Utilitaires d'edition pour nettoyer rapidement les sauvegardes locales.
public static class SaveTools
{
    private const string DefaultSaveFileName = "CharacterState.json";
    private const string DefaultSavesFolderName = "Saves";

    [MenuItem("Lit/Saves/Clear All Saves", priority = 10)]
    public static void ClearAllSaves()
    {
        string persistentPath = Application.persistentDataPath;
        if (string.IsNullOrWhiteSpace(persistentPath))
        {
            Debug.LogWarning("SaveTools: persistentDataPath introuvable.");
            return;
        }

        string message =
            "Supprimer toutes les sauvegardes locales ?\n\n" +
            "- CharacterState.json\n" +
            "- Dossier Saves\n\n" +
            $"Chemin: {persistentPath}";

        if (!EditorUtility.DisplayDialog("Clear Saves", message, "Supprimer", "Annuler"))
        {
            return;
        }

        int deletedFiles = 0;
        int deletedDirs = 0;

        deletedFiles += TryDeleteFile(Path.Combine(persistentPath, DefaultSaveFileName));
        deletedDirs += TryDeleteDirectory(Path.Combine(persistentPath, DefaultSavesFolderName));

        AssetDatabase.Refresh();
        Debug.Log($"SaveTools: sauvegardes nettoyees. Fichiers supprimes: {deletedFiles}, dossiers supprimes: {deletedDirs}. Path: {persistentPath}");
    }

    [MenuItem("Lit/Saves/Open Persistent Data Path", priority = 11)]
    public static void OpenPersistentDataPath()
    {
        string persistentPath = Application.persistentDataPath;
        if (string.IsNullOrWhiteSpace(persistentPath))
        {
            Debug.LogWarning("SaveTools: persistentDataPath introuvable.");
            return;
        }

        EditorUtility.RevealInFinder(persistentPath);
    }

    private static int TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return 1;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Debug.LogWarning($"SaveTools: echec suppression fichier {path}. {ex.Message}");
        }

        return 0;
    }

    private static int TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                return 1;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Debug.LogWarning($"SaveTools: echec suppression dossier {path}. {ex.Message}");
        }

        return 0;
    }
}
