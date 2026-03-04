using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Gestionnaire de sessions/sauvegardes (menu principal).
public class SaveSessionManager : MonoBehaviour
{
    public static SaveSessionManager Instance { get; private set; }

    [SerializeField] private string menuSceneName = "MainMenu";
    [SerializeField] private string savesRootFolder = "Saves";
    [SerializeField] private string sessionMetaFileName = "session.json";
    [SerializeField] private string saveMetaFileName = "meta.json";

    public string CurrentSessionId { get; private set; }
    public string CurrentSessionName { get; private set; }
    public string CurrentSaveId { get; private set; }
    public string CurrentSaveName { get; private set; }
    public float CurrentPlaytimeSeconds { get; private set; }

    private bool trackingPlaytime;
    private List<SaveSessionInfo> sessionsCache = new List<SaveSessionInfo>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        }
        else if (HasActiveSave)
        {
            StartPlaytimeTracking();
        }
    }

    public bool HasActiveSave => !string.IsNullOrWhiteSpace(CurrentSessionId) && !string.IsNullOrWhiteSpace(CurrentSaveId);

    public void SetMenuSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        menuSceneName = sceneName;
    }

    public IReadOnlyList<SaveSessionInfo> Sessions => sessionsCache;

    public void ReloadSessions()
    {
        sessionsCache = LoadSessions();
    }

    public SaveSessionInfo CreateSession(string sessionName)
    {
        string name = string.IsNullOrWhiteSpace(sessionName) ? "Nouvelle partie" : sessionName.Trim();
        string sessionId = GenerateId();
        string sessionPath = GetSessionPath(sessionId);
        Directory.CreateDirectory(sessionPath);

        SessionMeta meta = new SessionMeta
        {
            sessionId = sessionId,
            sessionName = name,
            createdAtUtcTicks = DateTime.UtcNow.Ticks
        };
        WriteJson(GetSessionMetaPath(sessionId), meta);

        SaveSessionInfo info = new SaveSessionInfo
        {
            sessionId = sessionId,
            sessionName = name,
            createdAtUtcTicks = meta.createdAtUtcTicks,
            saves = new List<SaveSlotInfo>()
        };

        sessionsCache.Insert(0, info);
        return info;
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

        SaveMeta meta = new SaveMeta
        {
            sessionId = sessionId,
            sessionName = ResolveSessionName(sessionId),
            saveId = saveId,
            saveName = name,
            savedAtUtcTicks = DateTime.UtcNow.Ticks,
            playTimeSeconds = CurrentPlaytimeSeconds,
            sceneName = SceneManager.GetActiveScene().name
        };
        WriteJson(GetSaveMetaPath(sessionId, saveId), meta);

        SaveSlotInfo slot = new SaveSlotInfo
        {
            sessionId = sessionId,
            sessionName = meta.sessionName,
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
        CurrentPlaytimeSeconds = meta != null ? meta.playTimeSeconds : 0f;
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
        if (!HasActiveSave)
        {
            return;
        }

        string metaPath = GetSaveMetaPath(CurrentSessionId, CurrentSaveId);
        SaveMeta meta = ReadJson<SaveMeta>(metaPath) ?? new SaveMeta();
        meta.sessionId = CurrentSessionId;
        meta.sessionName = ResolveSessionName(CurrentSessionId);
        meta.saveId = CurrentSaveId;
        meta.saveName = ResolveSaveName(CurrentSessionId, CurrentSaveId);
        meta.savedAtUtcTicks = DateTime.UtcNow.Ticks;
        meta.playTimeSeconds = CurrentPlaytimeSeconds;
        meta.sceneName = sceneName;
        WriteJson(metaPath, meta);
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

    private string ResolveSaveName(string sessionId, string saveId)
    {
        SaveMeta meta = ReadJson<SaveMeta>(GetSaveMetaPath(sessionId, saveId));
        if (meta != null && !string.IsNullOrWhiteSpace(meta.saveName))
        {
            return meta.saveName;
        }

        return saveId;
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
            SaveSessionInfo session = new SaveSessionInfo
            {
                sessionId = sessionId,
                sessionName = sessionMeta != null && !string.IsNullOrWhiteSpace(sessionMeta.sessionName) ? sessionMeta.sessionName : sessionId,
                createdAtUtcTicks = sessionMeta != null ? sessionMeta.createdAtUtcTicks : 0,
                saves = new List<SaveSlotInfo>()
            };

            string[] saveDirs = Directory.GetDirectories(sessionPath);
            for (int j = 0; j < saveDirs.Length; j++)
            {
                string savePath = saveDirs[j];
                string saveId = Path.GetFileName(savePath);
                SaveMeta saveMeta = ReadJson<SaveMeta>(GetSaveMetaPath(sessionId, saveId));
                if (saveMeta == null)
                {
                    DateTime fallbackTime = Directory.GetLastWriteTimeUtc(savePath);
                    saveMeta = new SaveMeta
                    {
                        sessionId = sessionId,
                        sessionName = session.sessionName,
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
                    saveId = saveId,
                    saveName = saveMeta.saveName,
                    savedAtUtcTicks = saveMeta.savedAtUtcTicks,
                    playTimeSeconds = saveMeta.playTimeSeconds,
                    sceneName = saveMeta.sceneName,
                    directoryPath = savePath
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
        if (string.IsNullOrWhiteSpace(path) || data == null)
        {
            return;
        }

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
        }
        catch (Exception)
        {
        }
    }

    private static T ReadJson<T>(string path) where T : class
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
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
public class SaveSessionInfo
{
    public string sessionId;
    public string sessionName;
    public long createdAtUtcTicks;
    public List<SaveSlotInfo> saves = new List<SaveSlotInfo>();
}

[System.Serializable]
public class SaveSlotInfo
{
    public string sessionId;
    public string sessionName;
    public string saveId;
    public string saveName;
    public long savedAtUtcTicks;
    public float playTimeSeconds;
    public string sceneName;
    public string directoryPath;
}

[System.Serializable]
public class SessionMeta
{
    public string sessionId;
    public string sessionName;
    public long createdAtUtcTicks;
}

[System.Serializable]
public class SaveMeta
{
    public string sessionId;
    public string sessionName;
    public string saveId;
    public string saveName;
    public long savedAtUtcTicks;
    public float playTimeSeconds;
    public string sceneName;
}
