using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(
    fileName = "LegacyBuildingSystemSettings",
    menuName = "Lit/Legacy/Building System Settings")]
public sealed class LegacyBuildingSystemSettings : ScriptableObject
{
    [Tooltip("Reactive le gameplay de construction legacy. Laisser desactive tant que ce systeme n'est pas remis dans le scope.")]
    public bool systemEnabled;

    [Tooltip("Conserve les anciennes constructions dans les sauvegardes JSON sans les instancier.")]
    public bool preserveLegacySaveData = true;

    [Tooltip("Conserve les snapshots Netcode des constructions sans les reconstruire.")]
    public bool preserveLegacyWorldSnapshots = true;
}

// Point d'activation unique du systeme Building legacy.
// Les donnees restent compatibles avec les anciennes sauvegardes, mais aucun
// gameplay de construction ne doit s'executer lorsque Enabled vaut false.
public static class LegacyBuildingSystem
{
    public const string DisabledMessage = "Construction legacy désactivée.";

    private const string ResourcesPath = "LegacyBuildingSystemSettings";
    private static LegacyBuildingSystemSettings settings;

    public static bool Enabled => Settings != null && Settings.systemEnabled;
    public static bool PreserveLegacySaveData => Settings == null || Settings.preserveLegacySaveData;
    public static bool PreserveLegacyWorldSnapshots => Settings == null || Settings.preserveLegacyWorldSnapshots;

    private static LegacyBuildingSystemSettings Settings
    {
        get
        {
            if (settings == null)
            {
                settings = Resources.Load<LegacyBuildingSystemSettings>(ResourcesPath);
            }

            return settings;
        }
    }

    public static bool IsBuildingItemAvailable(Item item)
    {
        return item == null || !item.isBuilding || Enabled;
    }

    public static bool IsBuildingSnapshot(PersistentObjectSnapshot snapshot)
    {
        return snapshot != null &&
               !string.IsNullOrWhiteSpace(snapshot.RuntimePrefabId) &&
               snapshot.RuntimePrefabId.StartsWith(
                   PersistentWorldSceneInstaller.BuildingPrefabPrefix,
                   System.StringComparison.Ordinal);
    }

    public static bool ShouldSkipRuntimeSnapshot(PersistentObjectSnapshot snapshot)
    {
        return !Enabled && IsBuildingSnapshot(snapshot);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyInitialSceneState()
    {
        ApplySceneState();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneState();
    }

    public static void ApplySceneState()
    {
        bool active = Enabled;
        SetComponentsEnabled(Object.FindObjectsByType<BuilderController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None), active);
        SetBuildingInteractablesEnabled(Object.FindObjectsByType<BuildingInfoInteractable>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None), active);
        SetPanelsEnabled(Object.FindObjectsByType<BuildingPanelController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None), active);
        SetPanelsEnabled(Object.FindObjectsByType<CraftingConstructionPanel>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None), active);
    }

    private static void SetComponentsEnabled<T>(T[] components, bool active)
        where T : Behaviour
    {
        if (components == null)
        {
            return;
        }

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null)
            {
                components[i].enabled = active;
            }
        }
    }

    private static void SetBuildingInteractablesEnabled(
        BuildingInfoInteractable[] components,
        bool active)
    {
        if (components == null)
        {
            return;
        }

        for (int i = 0; i < components.Length; i++)
        {
            BuildingInfoInteractable component = components[i];
            if (component != null && component.IsLegacyBuilding)
            {
                component.enabled = active;
            }
        }
    }

    private static void SetPanelsEnabled(BuildingPanelController[] panels, bool active)
    {
        if (panels == null)
        {
            return;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            BuildingPanelController panel = panels[i];
            if (panel == null)
            {
                continue;
            }

            panel.enabled = active;
            if (!active && panel.buildingPanel != null)
            {
                panel.buildingPanel.SetActive(false);
            }
        }
    }

    private static void SetPanelsEnabled(CraftingConstructionPanel[] panels, bool active)
    {
        if (panels == null)
        {
            return;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            CraftingConstructionPanel panel = panels[i];
            if (panel == null)
            {
                continue;
            }

            panel.enabled = active;
            if (!active && panel.craftingPanel != null)
            {
                panel.craftingPanel.SetActive(false);
            }
        }
    }
}
