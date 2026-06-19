using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Pilote le decor 3D du titre a partir de la derniere sauvegarde jouee.
[ExecuteAlways]
[DisallowMultipleComponent]
public class MainMenuTitleDecorController : MonoBehaviour
{
    private enum ProgressTier
    {
        NoSave,
        Early,
        Mid,
        Late
    }

    [Header("Save Source")]
    [SerializeField] private string savesRootFolder = "Saves";
    [SerializeField] private string saveMetaFileName = "meta.json";
    [SerializeField] private string characterStateFileName = "CharacterState.json";
    [SerializeField, Min(0.25f)] private float runtimeRefreshInterval = 2f;

    [Header("Decor States")]
    [SerializeField] private GameObject noSaveRoot;
    [SerializeField] private GameObject earlyProgressRoot;
    [SerializeField] private GameObject midProgressRoot;
    [SerializeField] private GameObject lateProgressRoot;

    [Header("Lighting")]
    [SerializeField] private Light ambientLight;
    [SerializeField] private Light accentLight;
    [SerializeField] private Renderer[] progressTintRenderers = Array.Empty<Renderer>();
    [SerializeField] private Color noSaveTint = new Color(0.25f, 0.28f, 0.34f, 1f);
    [SerializeField] private Color earlyTint = new Color(0.55f, 0.42f, 0.28f, 1f);
    [SerializeField] private Color midTint = new Color(0.4f, 0.56f, 0.52f, 1f);
    [SerializeField] private Color lateTint = new Color(0.9f, 0.72f, 0.42f, 1f);

    private readonly List<SaveCandidate> saveCandidates = new List<SaveCandidate>();
    private MaterialPropertyBlock propertyBlock;
    private float nextRefreshTime;
    private string lastAppliedSaveDirectory;
    private long lastAppliedSaveTicks;

    private void OnEnable()
    {
        RefreshDecor();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.25f, runtimeRefreshInterval);
        RefreshDecor();
    }

    [ContextMenu("Refresh Decor")]
    public void RefreshDecor()
    {
        DecorSnapshot snapshot = LoadLatestSnapshot();
        if (Application.isPlaying &&
            string.Equals(snapshot.saveDirectory, lastAppliedSaveDirectory, StringComparison.Ordinal) &&
            snapshot.savedAtUtcTicks == lastAppliedSaveTicks)
        {
            return;
        }

        lastAppliedSaveDirectory = snapshot.saveDirectory;
        lastAppliedSaveTicks = snapshot.savedAtUtcTicks;
        ApplySnapshot(snapshot);
    }

    private DecorSnapshot LoadLatestSnapshot()
    {
        SaveCandidate candidate = FindLatestSave();
        if (!candidate.hasSave)
        {
            return DecorSnapshot.Empty;
        }

        DecorSnapshot snapshot = new DecorSnapshot
        {
            hasSave = true,
            saveDirectory = candidate.directory,
            savedAtUtcTicks = candidate.savedAtUtcTicks,
            sessionName = candidate.meta != null ? candidate.meta.sessionName : string.Empty,
            saveName = candidate.meta != null ? candidate.meta.saveName : string.Empty,
            sceneName = candidate.meta != null ? candidate.meta.sceneName : string.Empty,
            playTimeSeconds = candidate.meta != null ? candidate.meta.playTimeSeconds : 0f
        };

        string characterStatePath = Path.Combine(candidate.directory, characterStateFileName);
        CharacterSaveData characterState = ReadJson<CharacterSaveData>(characterStatePath);
        if (characterState != null)
        {
            snapshot.squadCount = characterState.squadIds != null && characterState.squadIds.Count > 0
                ? characterState.squadIds.Count
                : CountSquadCharacters(characterState);
            snapshot.litFlameCount = CountLitFlames(characterState);
            snapshot.builtConstructionCount = LegacyBuildingSystem.Enabled && characterState.builtConstructions != null
                ? characterState.builtConstructions.Count
                : 0;
            snapshot.readableContentCount = characterState.readableGeneratedContents != null ? characterState.readableGeneratedContents.Count : 0;
        }

        return snapshot;
    }

    private SaveCandidate FindLatestSave()
    {
        saveCandidates.Clear();

        string savesRoot = GetSavesRoot();
        if (string.IsNullOrWhiteSpace(savesRoot) || !Directory.Exists(savesRoot))
        {
            return SaveCandidate.None;
        }

        try
        {
            string[] sessionDirectories = Directory.GetDirectories(savesRoot);
            for (int i = 0; i < sessionDirectories.Length; i++)
            {
                string sessionDirectory = sessionDirectories[i];
                if (string.IsNullOrWhiteSpace(sessionDirectory))
                {
                    continue;
                }

                string[] saveDirectories = Directory.GetDirectories(sessionDirectory);
                for (int j = 0; j < saveDirectories.Length; j++)
                {
                    AddSaveCandidate(saveDirectories[j]);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"MainMenuTitleDecorController: lecture sauvegardes impossible. {ex.Message}", this);
        }

        if (saveCandidates.Count == 0)
        {
            return SaveCandidate.None;
        }

        saveCandidates.Sort((a, b) => b.savedAtUtcTicks.CompareTo(a.savedAtUtcTicks));
        return saveCandidates[0];
    }

    private void AddSaveCandidate(string saveDirectory)
    {
        if (string.IsNullOrWhiteSpace(saveDirectory) || !Directory.Exists(saveDirectory))
        {
            return;
        }

        SaveMeta meta = ReadJson<SaveMeta>(Path.Combine(saveDirectory, saveMetaFileName));
        long ticks = meta != null && meta.savedAtUtcTicks > 0
            ? meta.savedAtUtcTicks
            : Directory.GetLastWriteTimeUtc(saveDirectory).Ticks;

        saveCandidates.Add(new SaveCandidate
        {
            hasSave = true,
            directory = saveDirectory,
            savedAtUtcTicks = ticks,
            meta = meta
        });
    }

    private string GetSavesRoot()
    {
        if (string.IsNullOrWhiteSpace(savesRootFolder))
        {
            return null;
        }

        return Path.Combine(Application.persistentDataPath, savesRootFolder);
    }

    private static T ReadJson<T>(string path) where T : class
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"MainMenuTitleDecorController: JSON invalide {path}. {ex.Message}");
            return null;
        }
    }

    private static int CountSquadCharacters(CharacterSaveData state)
    {
        if (state == null || state.characters == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < state.characters.Count; i++)
        {
            CharacterSaveEntry entry = state.characters[i];
            if (entry != null && entry.inSquad)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountLitFlames(CharacterSaveData state)
    {
        if (state == null || state.flames == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < state.flames.Count; i++)
        {
            FlameSaveEntry entry = state.flames[i];
            if (entry != null && entry.isLit)
            {
                count++;
            }
        }

        return count;
    }

    private void ApplySnapshot(DecorSnapshot snapshot)
    {
        ProgressTier tier = ResolveTier(snapshot);

        SetActive(noSaveRoot, tier == ProgressTier.NoSave);
        SetActive(earlyProgressRoot, tier == ProgressTier.Early);
        SetActive(midProgressRoot, tier == ProgressTier.Mid);
        SetActive(lateProgressRoot, tier == ProgressTier.Late);

        float progress = CalculateProgress01(snapshot);
        Color tint = ResolveTint(tier);
        ApplyTint(tint);

        if (ambientLight != null)
        {
            ambientLight.intensity = Mathf.Lerp(0.25f, 1.35f, progress);
            ambientLight.color = Color.Lerp(noSaveTint, tint, 0.75f);
        }

        if (accentLight != null)
        {
            accentLight.intensity = Mathf.Lerp(0.7f, 3.5f, progress);
            accentLight.color = tint;
        }
    }

    private ProgressTier ResolveTier(DecorSnapshot snapshot)
    {
        if (!snapshot.hasSave)
        {
            return ProgressTier.NoSave;
        }

        float progress = CalculateProgress01(snapshot);
        if (progress < 0.34f)
        {
            return ProgressTier.Early;
        }

        return progress < 0.67f ? ProgressTier.Mid : ProgressTier.Late;
    }

    private static float CalculateProgress01(DecorSnapshot snapshot)
    {
        if (!snapshot.hasSave)
        {
            return 0f;
        }

        float progress = 0.12f;
        progress += Mathf.Clamp01(snapshot.playTimeSeconds / (6f * 3600f)) * 0.25f;
        progress += Mathf.Clamp01(snapshot.litFlameCount / 6f) * 0.22f;
        progress += Mathf.Clamp01(snapshot.builtConstructionCount / 10f) * 0.18f;
        progress += Mathf.Clamp01(snapshot.readableContentCount / 12f) * 0.12f;
        progress += Mathf.Clamp01(snapshot.squadCount / 4f) * 0.11f;

        if (!string.IsNullOrWhiteSpace(snapshot.sceneName) &&
            !string.Equals(snapshot.sceneName, MainMenuController.DefaultMenuSceneName, StringComparison.OrdinalIgnoreCase))
        {
            progress += 0.08f;
        }

        return Mathf.Clamp01(progress);
    }

    private Color ResolveTint(ProgressTier tier)
    {
        switch (tier)
        {
            case ProgressTier.Early:
                return earlyTint;
            case ProgressTier.Mid:
                return midTint;
            case ProgressTier.Late:
                return lateTint;
            default:
                return noSaveTint;
        }
    }

    private void ApplyTint(Color tint)
    {
        if (progressTintRenderers == null || progressTintRenderers.Length == 0)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < progressTintRenderers.Length; i++)
        {
            Renderer target = progressTintRenderers[i];
            if (target == null)
            {
                continue;
            }

            target.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", tint);
            propertyBlock.SetColor("_Color", tint);
            target.SetPropertyBlock(propertyBlock);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private struct SaveCandidate
    {
        public static readonly SaveCandidate None = new SaveCandidate();

        public bool hasSave;
        public string directory;
        public long savedAtUtcTicks;
        public SaveMeta meta;
    }

    private struct DecorSnapshot
    {
        public static readonly DecorSnapshot Empty = new DecorSnapshot();

        public bool hasSave;
        public string saveDirectory;
        public long savedAtUtcTicks;
        public string sessionName;
        public string saveName;
        public string sceneName;
        public float playTimeSeconds;
        public int squadCount;
        public int litFlameCount;
        public int builtConstructionCount;
        public int readableContentCount;
    }
}
