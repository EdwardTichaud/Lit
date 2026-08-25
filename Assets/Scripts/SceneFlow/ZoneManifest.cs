using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Description d'une zone. La scene principale est chargee comme scene additive
/// pendant le voyage depuis le hub. Les scenes de chargement sont requises
/// avant de rendre la main au joueur; les scenes post-chargement arrivent
/// ensuite progressivement.
/// </summary>
[CreateAssetMenu(menuName = "Lit/Scenes/Zone Manifest", fileName = "ZoneManifest")]
public sealed class ZoneManifest : ScriptableObject
{
    [Header("Scenes")]
#if UNITY_EDITOR
    [Tooltip("Scene Core contenant le spawn, les collisions et les systemes indispensables.")]
    public SceneAsset primaryScene;
    [Tooltip("Scenes chargees sous l'ecran de chargement avant de rendre la main au joueur.")]
    public List<SceneAsset> loadingScenes = new List<SceneAsset>();
    [Tooltip("Scenes chargees une par une apres la disparition de l'ecran de chargement.")]
    public List<SceneAsset> postLoadingScenes = new List<SceneAsset>();
#endif

    [SerializeField, HideInInspector] private string primarySceneName;
    [SerializeField] private string loadingMessage = "Chargement...";
    [SerializeField, HideInInspector, FormerlySerializedAs("additionalSceneNames")]
    private List<string> loadingSceneNames = new List<string>();
    [SerializeField, HideInInspector] private List<string> postLoadingSceneNames = new List<string>();
    [SerializeField, HideInInspector] private bool editorSceneReferencesMigrated;

    public string PrimarySceneName => primarySceneName;
    public string LoadingMessage => loadingMessage;
    /// <summary>Scenes obligatoires avant le fondu de sortie.</summary>
    public IReadOnlyList<string> LoadingSceneNames => loadingSceneNames;
    /// <summary>Scenes chargees apres le retour du joueur en jeu.</summary>
    public IReadOnlyList<string> PostLoadingSceneNames => postLoadingSceneNames;

    public bool IsValid => !string.IsNullOrWhiteSpace(primarySceneName);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!editorSceneReferencesMigrated)
        {
            primaryScene = FindSceneAsset(primarySceneName);
            PopulateSceneAssets(loadingScenes, loadingSceneNames);
            PopulateSceneAssets(postLoadingScenes, postLoadingSceneNames);
            editorSceneReferencesMigrated = true;
        }

        primarySceneName = GetSceneName(primaryScene);
        CopySceneNames(loadingScenes, loadingSceneNames);
        CopySceneNames(postLoadingScenes, postLoadingSceneNames);
    }

    private static void PopulateSceneAssets(List<SceneAsset> destination, List<string> sceneNames)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        for (int i = 0; i < sceneNames.Count; i++)
        {
            SceneAsset sceneAsset = FindSceneAsset(sceneNames[i]);
            if (sceneAsset != null)
            {
                destination.Add(sceneAsset);
            }
        }
    }

    private static void CopySceneNames(List<SceneAsset> source, List<string> destination)
    {
        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            string sceneName = GetSceneName(source[i]);
            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                destination.Add(sceneName);
            }
        }
    }

    private static string GetSceneName(SceneAsset sceneAsset)
    {
        return sceneAsset == null ? string.Empty : sceneAsset.name;
    }

    private static SceneAsset FindSceneAsset(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return null;
        }

        string[] guids = AssetDatabase.FindAssets("t:Scene");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (System.String.Equals(
                    System.IO.Path.GetFileNameWithoutExtension(path),
                    sceneName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            }
        }

        return null;
    }
#endif
}
