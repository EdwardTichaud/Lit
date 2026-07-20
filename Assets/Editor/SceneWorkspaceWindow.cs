using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ouvre les scenes de Assets/Scenes sous forme d'espaces de travail.
/// Les scenes partageant un meme prefixe de zone (par exemple District_1_)
/// sont ajoutees ensemble en multi-scenes pour faciliter l'edition.
/// Cet outil est exclusivement editor : il ne modifie jamais les Build
/// Settings, les manifests, ni le chargement runtime.
/// </summary>
public sealed class SceneWorkspaceWindow : EditorWindow
{
    private const string ScenesRoot = "Assets/Scenes";
    private static readonly string[] PhaseMarkers = { "_Critical", "_Loading", "_PostLoading" };

    private string search = string.Empty;
    private Vector2 scrollPosition;
    private List<SceneWorkspace> workspaces = new List<SceneWorkspace>();

    [MenuItem("Lit/Scenes/Ajouter des espaces de scenes")]
    public static void OpenWindow()
    {
        SceneWorkspaceWindow window = GetWindow<SceneWorkspaceWindow>("Espaces de scenes");
        window.minSize = new Vector2(420f, 260f);
        window.Refresh();
        window.Show();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnProjectChange()
    {
        Refresh();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Espaces de scenes", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Toutes les scenes partageant le meme prefixe de zone s'ajoutent ensemble.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            search = EditorGUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"));
            if (GUILayout.Button("Actualiser", EditorStyles.miniButton, GUILayout.Width(82f)))
            {
                Refresh();
            }
        }

        EditorGUILayout.Space(6f);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (SceneWorkspace workspace in workspaces)
        {
            if (!MatchesSearch(workspace))
            {
                continue;
            }

            DrawWorkspace(workspace);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawWorkspace(SceneWorkspace workspace)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(workspace.DisplayName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Ajouter", GUILayout.Width(76f)))
                {
                    OpenWorkspace(workspace);
                }
            }

            foreach (SceneEntry entry in workspace.Scenes)
            {
                EditorGUILayout.LabelField("• " + entry.PhaseLabel + ": " + entry.Path, EditorStyles.miniLabel);
            }
        }
    }

    private void Refresh()
    {
        workspaces = BuildWorkspaces();
    }

    private static List<SceneWorkspace> BuildWorkspaces()
    {
        Dictionary<string, List<SceneEntry>> groups = new Dictionary<string, List<SceneEntry>>(StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { ScenesRoot });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string sceneName = Path.GetFileNameWithoutExtension(path);
            string workspaceName = GetWorkspaceName(sceneName, out string phaseLabel, out int order);
            string key = Path.GetDirectoryName(path) + "|" + workspaceName;

            if (!groups.TryGetValue(key, out List<SceneEntry> entries))
            {
                entries = new List<SceneEntry>();
                groups.Add(key, entries);
            }

            entries.Add(new SceneEntry(path, phaseLabel, order));
        }

        return groups
            .Select(pair => new SceneWorkspace(pair.Value))
            .OrderBy(workspace => workspace.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetWorkspaceName(string sceneName, out string phaseLabel, out int order)
    {
        for (int i = 0; i < PhaseMarkers.Length; i++)
        {
            string marker = PhaseMarkers[i];
            int markerIndex = sceneName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex > 0)
            {
                phaseLabel = sceneName.Substring(markerIndex + 1).Replace("_", " / ");
                order = i;
                return sceneName.Substring(0, markerIndex);
            }
        }

        phaseLabel = "Scene";
        order = PhaseMarkers.Length;
        return sceneName;
    }

    private bool MatchesSearch(SceneWorkspace workspace)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return workspace.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
               workspace.Scenes.Any(entry => entry.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void OpenWorkspace(SceneWorkspace workspace)
    {
        for (int i = 0; i < workspace.Scenes.Count; i++)
        {
            SceneEntry entry = workspace.Scenes[i];
            Scene openedScene = SceneManager.GetSceneByPath(entry.Path);
            if (!openedScene.IsValid() || !openedScene.isLoaded)
            {
                EditorSceneManager.OpenScene(entry.Path, OpenSceneMode.Additive);
            }
        }
    }

    private sealed class SceneWorkspace
    {
        public SceneWorkspace(List<SceneEntry> scenes)
        {
            Scenes = scenes.OrderBy(entry => entry.Order).ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).ToList();
            DisplayName = GetWorkspaceName(Path.GetFileNameWithoutExtension(Scenes[0].Path), out _, out _);
        }

        public string DisplayName { get; }
        public List<SceneEntry> Scenes { get; }
    }

    private sealed class SceneEntry
    {
        public SceneEntry(string path, string phaseLabel, int order)
        {
            Path = path;
            PhaseLabel = phaseLabel;
            Order = order;
        }

        public string Path { get; }
        public string PhaseLabel { get; }
        public int Order { get; }
    }
}
