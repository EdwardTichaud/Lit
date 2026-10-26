using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Gestionnaire de sessions/sauvegardes (menu principal).
public class SaveSessionManager : MonoBehaviour
{
    public const string DefaultSavesRootFolder = "Saves";
    public static SaveSessionManager Instance { get; private set; }

    [SerializeField] private string menuSceneName = "MainMenu";
    [SerializeField] private string savesRootFolder = DefaultSavesRootFolder;
    [SerializeField] private string sessionMetaFileName = "session.json";
    [SerializeField] private string saveMetaFileName = "meta.json";

    public event Action CatalogChanged;
    public string LastError { get; private set; }

    public string CurrentSessionId { get; private set; }
    public string CurrentSessionName { get; private set; }
    public string CurrentSaveId { get; private set; }
    public string CurrentSaveName { get; private set; }
    public float CurrentPlaytimeSeconds { get; private set; }
    public SaveSessionType CurrentSessionType { get; private set; }

    private bool trackingPlaytime;
    // Published with the character/world transaction, never when a sequence ends.
    private readonly HashSet<string> runtimeStorySequenceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool runtimeStorySequencesInitialized;
    private List<SaveSessionInfo> sessionsCache = new List<SaveSessionInfo>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Evite de detruire un GameObject qui porte d'autres managers (ex: GameManager).
            if (TryDestroySelfOnly())
            {
                return;
            }

            Destroy(gameObject);
            return;
        }

        Instance = this;
        RuntimePersistenceUtility.DontDestroyOnLoadRoot(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        ReloadSessions();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private bool TryDestroySelfOnly()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        if (behaviours != null && behaviours.Length > 1)
        {
            Destroy(this);
            return true;
        }

        return false;
    }

    private void Update()
    {
        if (!trackingPlaytime)
        {
            return;
        }

        CurrentPlaytimeSeconds += Time.unscaledDeltaTime;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.Equals(scene.name, menuSceneName, StringComparison.OrdinalIgnoreCase))
        {
            StopPlaytimeTracking();
            GameplayRuntimeReset.ResetForMenuScene($"menu_scene_loaded:{scene.name}");
        }
        else if (HasActiveSave)
        {
            StartPlaytimeTracking();
        }
    }

    public bool HasActiveSave => !string.IsNullOrWhiteSpace(CurrentSessionId) && !string.IsNullOrWhiteSpace(CurrentSaveId);

    public string GetActiveSaveSceneName()
    {
        if (!HasActiveSave)
        {
            return null;
        }

        SaveMeta meta = ReadJson<SaveMeta>(GetSaveMetaPath(CurrentSessionId, CurrentSaveId));
        return meta != null ? meta.sceneName : null;
    }

    public void SetMenuSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        menuSceneName = sceneName;
    }

    public IReadOnlyList<SaveSessionInfo> Sessions => sessionsCache;

    public IReadOnlyList<SaveSessionInfo> GetSessionsByType(SaveSessionType sessionType)
    {
        if (sessionsCache == null || sessionsCache.Count == 0)
        {
            return Array.Empty<SaveSessionInfo>();
        }

        List<SaveSessionInfo> results = new List<SaveSessionInfo>(sessionsCache.Count);
        for (int i = 0; i < sessionsCache.Count; i++)
        {
            SaveSessionInfo session = sessionsCache[i];
            if (session != null && session.sessionType == sessionType)
            {
                results.Add(session);
            }
        }

        return results;
    }

    public void ReloadSessions()
    {
        try { sessionsCache = LoadSessions(); LastError = string.Empty; }
        catch (Exception ex) { LastError = "Impossible de lire les sauvegardes."; Debug.LogWarning(ex.Message, this); }
        CatalogChanged?.Invoke();
    }

    public void SetCurrentSessionType(SaveSessionType sessionType)
    {
        CurrentSessionType = sessionType;
    }

    public bool DeleteSave(string sessionId, string saveId, bool deleteSessionIfEmpty = true)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(saveId))
        {
            return false;
        }

        string savePath = GetSavePath(sessionId, saveId);
        if (string.IsNullOrWhiteSpace(savePath))
        {
            return false;
        }

        try
        {
            if (Directory.Exists(savePath))
            {
                Directory.Delete(savePath, true);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SaveSessionManager: echec suppression sauvegarde {savePath}. {ex.Message}");
            return false;
        }

        SaveSessionInfo session = GetSession(sessionId);
        if (session != null && session.saves != null)
        {
            session.saves.RemoveAll(s => s != null && s.saveId == saveId);
        }

        if (deleteSessionIfEmpty && session != null && (session.saves == null || session.saves.Count == 0))
        {
            DeleteSessionInternal(sessionId);
        }

        if (CurrentSessionId == sessionId && CurrentSaveId == saveId)
        {
            ClearActiveSave();
        }

        CatalogChanged?.Invoke();
        return true;
    }

    public bool DeleteSession(string sessionId)
    {
        return DeleteSessionInternal(sessionId);
    }

    public SaveSessionInfo CreateSession(string sessionName, SaveSessionType sessionType)
    {
        string name = string.IsNullOrWhiteSpace(sessionName) ? "Nouvelle partie" : sessionName.Trim();
        string sessionId = GenerateId();
        string sessionPath = GetSessionPath(sessionId);
        Directory.CreateDirectory(sessionPath);

        SessionMeta meta = new SessionMeta
        {
            sessionId = sessionId,
            sessionName = name,
            createdAtUtcTicks = DateTime.UtcNow.Ticks,
            sessionType = sessionType
        };
        try { WriteJson(GetSessionMetaPath(sessionId), meta); }
        catch
        {
            if (Directory.Exists(sessionPath) && Directory.GetFileSystemEntries(sessionPath).Length == 0)
                Directory.Delete(sessionPath);
            throw;
        }

        SaveSessionInfo info = new SaveSessionInfo
        {
            sessionId = sessionId,
            sessionName = name,
            createdAtUtcTicks = meta.createdAtUtcTicks,
            sessionType = sessionType,
            saves = new List<SaveSlotInfo>()
        };

        sessionsCache.Insert(0, info);
        CatalogChanged?.Invoke();
        return info;
    }

    /// <summary>
    /// Renames a session and every slot metadata entry that represents it.
    /// This keeps the load browser coherent even before the next full save.
    /// </summary>
    public bool RenameActiveSession(string sessionName)
    {
        return HasActiveSave && RenameSession(CurrentSessionId, sessionName);
    }

    public bool RenameSession(string sessionId, string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        string name = string.IsNullOrWhiteSpace(sessionName) ? "Nouvelle partie" : sessionName.Trim();
        string sessionMetaPath = GetSessionMetaPath(sessionId);
        SessionMeta sessionMeta = ReadJson<SessionMeta>(sessionMetaPath);
        if (sessionMeta == null)
        {
            LastError = "Session introuvable.";
            return false;
        }

        try
        {
            sessionMeta.sessionName = name;
            WriteJson(sessionMetaPath, sessionMeta);

            string sessionPath = GetSessionPath(sessionId);
            if (Directory.Exists(sessionPath))
            {
                string[] saveDirectories = Directory.GetDirectories(sessionPath);
                for (int i = 0; i < saveDirectories.Length; i++)
                {
                    string saveMetaPath = Path.Combine(saveDirectories[i], saveMetaFileName);
                    SaveMeta saveMeta = ReadJson<SaveMeta>(saveMetaPath);
                    if (saveMeta == null)
                    {
                        continue;
                    }

                    saveMeta.sessionName = name;
                    WriteJson(saveMetaPath, saveMeta);
                }
            }

            SaveSessionInfo session = GetSession(sessionId);
            if (session != null)
            {
                session.sessionName = name;
                if (session.saves != null)
                {
                    for (int i = 0; i < session.saves.Count; i++)
                    {
                        if (session.saves[i] != null)
                        {
                            session.saves[i].sessionName = name;
                        }
                    }
                }
            }

            if (string.Equals(CurrentSessionId, sessionId, StringComparison.Ordinal))
            {
                CurrentSessionName = name;
            }

            LastError = string.Empty;
            CatalogChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            LastError = "Impossible de renommer la partie.";
            Debug.LogWarning("SaveSessionManager: echec du renommage de session. " + ex.Message, this);
            return false;
        }
    }

    public SaveSlotInfo CreateSave(string sessionId, string saveName)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        string name = string.IsNullOrWhiteSpace(saveName) ? "Sauvegarde" : saveName.Trim();
        string saveId = GenerateId();
        string savePath = GetSavePath(sessionId, saveId);
        Directory.CreateDirectory(savePath);

        SaveSessionType sessionType = ResolveSessionType(sessionId);
        SaveMeta meta = new SaveMeta
        {
            sessionId = sessionId,
            sessionName = ResolveSessionName(sessionId),
            sessionType = sessionType,
            saveId = saveId,
            saveName = name,
            savedAtUtcTicks = DateTime.UtcNow.Ticks,
            playTimeSeconds = CurrentSessionId == sessionId ? CurrentPlaytimeSeconds : 0f,
            sceneName = SceneManager.GetActiveScene().name,
            completedStorySequenceIds = CopyActiveStorySequenceCompletions(sessionId)
        };
        try { WriteJson(GetSaveMetaPath(sessionId, saveId), meta); }
        catch
        {
            if (Directory.Exists(savePath) && Directory.GetFileSystemEntries(savePath).Length == 0)
                Directory.Delete(savePath);
            throw;
        }

        SaveSlotInfo slot = new SaveSlotInfo
        {
            sessionId = sessionId,
            sessionName = meta.sessionName,
            sessionType = sessionType,
            saveId = saveId,
            saveName = name,
            savedAtUtcTicks = meta.savedAtUtcTicks,
            playTimeSeconds = meta.playTimeSeconds,
            sceneName = meta.sceneName,
            directoryPath = savePath
        };

        SaveSessionInfo session = GetSession(sessionId);
        if (session != null)
        {
            session.saves.Insert(0, slot);
        }

        CatalogChanged?.Invoke();
        return slot;
    }

    public void SetActiveSave(string sessionId, string saveId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(saveId))
        {
            return;
        }

        CurrentSessionId = sessionId;
        CurrentSaveId = saveId;
        CurrentSessionName = ResolveSessionName(sessionId);
        CurrentSaveName = ResolveSaveName(sessionId, saveId);

        SaveMeta meta = ReadJson<SaveMeta>(GetSaveMetaPath(sessionId, saveId));
        LoadRuntimeStorySequences(meta);
        CurrentPlaytimeSeconds = meta != null ? meta.playTimeSeconds : 0f;
        CurrentSessionType = meta != null ? meta.sessionType : ResolveSessionType(sessionId);
    }

    public string GetActiveSaveFilePath(string fileName)
    {
        if (!HasActiveSave || string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string savePath = GetSavePath(CurrentSessionId, CurrentSaveId);
        if (string.IsNullOrWhiteSpace(savePath))
        {
            return null;
        }

        return Path.Combine(savePath, fileName);
    }

    public void RecordSaveMetadata(string sceneName)
    {
        if (TryPrepareSaveMetadata(sceneName, out string path, out byte[] bytes))
            SaveMetadataWriter.WriteBytesAtomic(path, bytes);
    }

    public bool TryPrepareSaveMetadata(string sceneName, out string path, out byte[] bytes)
    {
        path = null;
        bytes = null;
        if (!HasActiveSave)
        {
            return false;
        }

        string metaPath = GetSaveMetaPath(CurrentSessionId, CurrentSaveId);
        SaveMetadataWriter.RecoverPendingBatch(Path.GetDirectoryName(metaPath));
        SaveMeta meta = ReadJson<SaveMeta>(metaPath) ?? new SaveMeta();
        meta.sessionId = CurrentSessionId;
        meta.sessionName = ResolveSessionName(CurrentSessionId);
        meta.sessionType = ResolveSessionType(CurrentSessionId);
        meta.saveId = CurrentSaveId;
        meta.saveName = ResolveSaveName(CurrentSessionId, CurrentSaveId);
        meta.savedAtUtcTicks = DateTime.UtcNow.Ticks;
        meta.playTimeSeconds = CurrentPlaytimeSeconds;
        meta.sceneName = sceneName;
        EnsureRuntimeStorySequences();
        meta.completedStorySequenceIds = GetRuntimeStorySequenceIds();
        path = metaPath;
        bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(meta, true));
        return true;
    }

    public bool IsStorySequenceCompleted(string sequenceId)
    {
        if (!HasActiveSave || string.IsNullOrWhiteSpace(sequenceId))
        {
            return false;
        }

        EnsureRuntimeStorySequences();
        return runtimeStorySequenceIds.Contains(sequenceId.Trim());
    }

    public bool MarkStorySequenceCompleted(string sequenceId)
    {
        if (!HasActiveSave || string.IsNullOrWhiteSpace(sequenceId))
        {
            return false;
        }

        EnsureRuntimeStorySequences();
        runtimeStorySequenceIds.Add(sequenceId.Trim());
        return true;
    }

    public bool ResetStorySequenceCompletion(string sequenceId)
    {
        if (!HasActiveSave || string.IsNullOrWhiteSpace(sequenceId))
        {
            return false;
        }

        EnsureRuntimeStorySequences();
        runtimeStorySequenceIds.Remove(sequenceId.Trim());
        return true;
    }

    public int ResetAllStorySequenceCompletions()
    {
        if (!HasActiveSave)
        {
            return 0;
        }

        EnsureRuntimeStorySequences();
        int removed = runtimeStorySequenceIds.Count;
        runtimeStorySequenceIds.Clear();
        return removed;
    }

    public void StartPlaytimeTracking()
    {
        if (!HasActiveSave)
        {
            return;
        }

        trackingPlaytime = true;
    }

    public void StopPlaytimeTracking()
    {
        trackingPlaytime = false;
    }

    public string GetSavesRoot()
    {
        return Path.Combine(Application.persistentDataPath, savesRootFolder);
    }

    public SaveSessionInfo GetSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        for (int i = 0; i < sessionsCache.Count; i++)
        {
            if (sessionsCache[i].sessionId == sessionId)
            {
                return sessionsCache[i];
            }
        }

        return null;
    }

    private bool DeleteSessionInternal(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        string sessionPath = GetSessionPath(sessionId);
        if (string.IsNullOrWhiteSpace(sessionPath))
        {
            return false;
        }

        try
        {
            if (Directory.Exists(sessionPath))
            {
                Directory.Delete(sessionPath, true);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SaveSessionManager: echec suppression session {sessionPath}. {ex.Message}");
            return false;
        }

        if (sessionsCache != null)
        {
            sessionsCache.RemoveAll(s => s != null && s.sessionId == sessionId);
        }

        if (CurrentSessionId == sessionId)
        {
            ClearActiveSave();
        }

        CatalogChanged?.Invoke();
        return true;
    }

    public void ClearActiveSave()
    {
        ResetRuntimeStorySequenceState();
        CurrentSessionId = null;
        CurrentSessionName = null;
        CurrentSaveId = null;
        CurrentSaveName = null;
        CurrentPlaytimeSeconds = 0f;
        CurrentSessionType = SaveSessionType.Solo;
    }

    private string ResolveSessionName(string sessionId)
    {
        SessionMeta meta = ReadJson<SessionMeta>(GetSessionMetaPath(sessionId));
        if (meta != null && !string.IsNullOrWhiteSpace(meta.sessionName))
        {
            return meta.sessionName;
        }

        SaveSessionInfo session = GetSession(sessionId);
        if (session != null && !string.IsNullOrWhiteSpace(session.sessionName))
        {
            return session.sessionName;
        }

        return sessionId;
    }

    private SaveSessionType ResolveSessionType(string sessionId)
    {
        SessionMeta meta = ReadJson<SessionMeta>(GetSessionMetaPath(sessionId));
        if (meta != null)
        {
            return meta.sessionType;
        }

        SaveSessionInfo session = GetSession(sessionId);
        if (session != null)
        {
            return session.sessionType;
        }

        return SaveSessionType.Solo;
    }

    private string ResolveSaveName(string sessionId, string saveId)
    {
        SaveMeta meta = ReadJson<SaveMeta>(GetSaveMetaPath(sessionId, saveId));
        if (meta != null && !string.IsNullOrWhiteSpace(meta.saveName))
        {
            return meta.saveName;
        }

        return saveId;
    }

    private SaveMeta CreateActiveSaveMeta()
    {
        return new SaveMeta
        {
            sessionId = CurrentSessionId,
            sessionName = ResolveSessionName(CurrentSessionId),
            sessionType = ResolveSessionType(CurrentSessionId),
            saveId = CurrentSaveId,
            saveName = ResolveSaveName(CurrentSessionId, CurrentSaveId),
            savedAtUtcTicks = DateTime.UtcNow.Ticks,
            playTimeSeconds = CurrentPlaytimeSeconds,
            sceneName = SceneManager.GetActiveScene().name,
            completedStorySequenceIds = new List<string>()
        };
    }

    private List<string> CopyActiveStorySequenceCompletions(string targetSessionId)
    {
        List<string> result = new List<string>();
        if (!HasActiveSave ||
            !string.Equals(CurrentSessionId, targetSessionId, StringComparison.Ordinal))
        {
            return result;
        }

        EnsureRuntimeStorySequences();
        return GetRuntimeStorySequenceIds();
    }

    public void ResetRuntimeStorySequenceState()
    {
        runtimeStorySequenceIds.Clear();
        runtimeStorySequencesInitialized = false;
    }

    private void EnsureRuntimeStorySequences()
    {
        if (!runtimeStorySequencesInitialized)
            LoadRuntimeStorySequences(HasActiveSave
                ? ReadJson<SaveMeta>(GetSaveMetaPath(CurrentSessionId, CurrentSaveId)) : null);
    }

    private void LoadRuntimeStorySequences(SaveMeta meta)
    {
        runtimeStorySequenceIds.Clear();
        if (meta?.completedStorySequenceIds != null)
        {
            foreach (string id in meta.completedStorySequenceIds)
                if (!string.IsNullOrWhiteSpace(id)) runtimeStorySequenceIds.Add(id.Trim());
        }
        runtimeStorySequencesInitialized = true;
    }

    private List<string> GetRuntimeStorySequenceIds()
    {
        var ids = new List<string>(runtimeStorySequenceIds);
        ids.Sort(StringComparer.OrdinalIgnoreCase);
        return ids;
    }

    private List<SaveSessionInfo> LoadSessions()
    {
        List<SaveSessionInfo> results = new List<SaveSessionInfo>();
        string root = GetSavesRoot();
        if (!Directory.Exists(root))
        {
            return results;
        }

        string[] sessionDirs = Directory.GetDirectories(root);
        for (int i = 0; i < sessionDirs.Length; i++)
        {
            string sessionPath = sessionDirs[i];
            if (string.IsNullOrWhiteSpace(sessionPath))
            {
                continue;
            }

            string sessionId = Path.GetFileName(sessionPath);
            SessionMeta sessionMeta = ReadJson<SessionMeta>(GetSessionMetaPath(sessionId));
            SaveSessionType sessionType = sessionMeta != null ? sessionMeta.sessionType : SaveSessionType.Solo;
            SaveSessionInfo session = new SaveSessionInfo
            {
                sessionId = sessionId,
                sessionName = sessionMeta != null && !string.IsNullOrWhiteSpace(sessionMeta.sessionName) ? sessionMeta.sessionName : sessionId,
                createdAtUtcTicks = sessionMeta != null ? sessionMeta.createdAtUtcTicks : 0,
                sessionType = sessionType,
                saves = new List<SaveSlotInfo>()
            };

            string[] saveDirs = Directory.GetDirectories(sessionPath);
            for (int j = 0; j < saveDirs.Length; j++)
            {
                string savePath = saveDirs[j];
                string saveId = Path.GetFileName(savePath);
                SaveMeta saveMeta = ReadJson<SaveMeta>(GetSaveMetaPath(sessionId, saveId));
                bool validMetadata = saveMeta != null && saveMeta.sessionId == sessionId && saveMeta.saveId == saveId;
                if (saveMeta == null)
                {
                    DateTime fallbackTime = Directory.GetLastWriteTimeUtc(savePath);
                    saveMeta = new SaveMeta
                    {
                        sessionId = sessionId,
                        sessionName = session.sessionName,
                        sessionType = sessionType,
                        saveId = saveId,
                        saveName = saveId,
                        savedAtUtcTicks = fallbackTime.Ticks,
                        playTimeSeconds = 0f,
                        sceneName = string.Empty
                    };
                }

                SaveSlotInfo slot = new SaveSlotInfo
                {
                    sessionId = sessionId,
                    sessionName = session.sessionName,
                    sessionType = session.sessionType,
                    saveId = saveId,
                    saveName = saveMeta.saveName,
                    savedAtUtcTicks = Math.Max(0, Math.Min(DateTime.MaxValue.Ticks, saveMeta.savedAtUtcTicks)),
                    playTimeSeconds = float.IsNaN(saveMeta.playTimeSeconds) || float.IsInfinity(saveMeta.playTimeSeconds) ? 0 : Mathf.Clamp(saveMeta.playTimeSeconds, 0, 3155760000f),
                    sceneName = saveMeta.sceneName,
                    directoryPath = savePath,
                    validMetadata = validMetadata
                };
                session.saves.Add(slot);
            }

            session.saves.Sort((a, b) => b.savedAtUtcTicks.CompareTo(a.savedAtUtcTicks));
            results.Add(session);
        }

        results.Sort((a, b) =>
        {
            long aTime = a.saves.Count > 0 ? a.saves[0].savedAtUtcTicks : a.createdAtUtcTicks;
            long bTime = b.saves.Count > 0 ? b.saves[0].savedAtUtcTicks : b.createdAtUtcTicks;
            return bTime.CompareTo(aTime);
        });

        return results;
    }

    private string GetSessionPath(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        return Path.Combine(GetSavesRoot(), sessionId);
    }

    private string GetSessionMetaPath(string sessionId)
    {
        string sessionPath = GetSessionPath(sessionId);
        return string.IsNullOrWhiteSpace(sessionPath) ? null : Path.Combine(sessionPath, sessionMetaFileName);
    }

    private string GetSavePath(string sessionId, string saveId)
    {
        string sessionPath = GetSessionPath(sessionId);
        if (string.IsNullOrWhiteSpace(sessionPath) || string.IsNullOrWhiteSpace(saveId))
        {
            return null;
        }

        return Path.Combine(sessionPath, saveId);
    }

    private string GetSaveMetaPath(string sessionId, string saveId)
    {
        string savePath = GetSavePath(sessionId, saveId);
        return string.IsNullOrWhiteSpace(savePath) ? null : Path.Combine(savePath, saveMetaFileName);
    }

    private static string GenerateId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static void WriteJson<T>(string path, T data) where T : class
    {
        if (string.IsNullOrWhiteSpace(path) || data == null) throw new ArgumentException("MÃÆ’©tadonnÃÆ’©es manquantes.");
        SaveMetadataWriter.RecoverPendingBatch(Path.GetDirectoryName(path));
        SaveMetadataWriter.WriteAtomic(path, JsonUtility.ToJson(data, true));
    }

    public bool TryCreateNewGame(string name, SaveSessionType type, string initialName, out SaveSlotInfo save, out string error)
    {
        save = null;
        error = string.Empty;
        SaveSessionInfo session = null;
        try
        {
            session = CreateSession(name, type);
            save = CreateSave(session.sessionId, initialName);
            if (save == null) throw new IOException("Emplacement initial indisponible.");
            return true;
        }
        catch (Exception ex)
        {
            if (session != null) DeleteSessionInternal(session.sessionId);
            error = "Impossible d’enregistrer cette partie. VÃÆ’©rifiez l’espace disque et les droits du dossier de sauvegarde.";
            LastError = error;
            Debug.LogWarning("SaveSessionManager: " + ex.Message, this);
            save = null;
            return false;
        }
    }

    private static T ReadJson<T>(string path) where T : class
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            SaveMetadataWriter.RecoverPendingBatch(Path.GetDirectoryName(path));
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

[System.Serializable]
public enum SaveSessionType
{
    Solo = 0,
    Multiplayer = 1
}

[System.Serializable]
public class SaveSessionInfo
{
    public string sessionId;
    public string sessionName;
    public long createdAtUtcTicks;
    public SaveSessionType sessionType;
    public List<SaveSlotInfo> saves = new List<SaveSlotInfo>();
}

[System.Serializable]
public class SaveSlotInfo
{
    public string sessionId;
    public string sessionName;
    public SaveSessionType sessionType;
    public string saveId;
    public string saveName;
    public long savedAtUtcTicks;
    public float playTimeSeconds;
    public string sceneName;
    public string directoryPath;
    public bool validMetadata = true;
}

[System.Serializable]
public class SessionMeta
{
    public string sessionId;
    public string sessionName;
    public long createdAtUtcTicks;
    public SaveSessionType sessionType;
}

[System.Serializable]
public class SaveMeta
{
    public string sessionId;
    public string sessionName;
    public SaveSessionType sessionType;
    public string saveId;
    public string saveName;
    public long savedAtUtcTicks;
    public float playTimeSeconds;
    public string sceneName;
    public List<string> completedStorySequenceIds = new List<string>();
}
