using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cree sans risque les coquilles de phases d'une scene. Aucun GameObject
/// n'est deplace automatiquement : les references de scenes Unity doivent
/// etre reparties depuis l'editeur, avec l'espace de travail ouvert.
/// </summary>
public sealed class ScenePhaseScaffoldWindow : EditorWindow
{
    private SceneAsset sourceScene;

    [MenuItem("Lit/Scenes/Creer des phases de scene")]
    private static void Open()
    {
        GetWindow<ScenePhaseScaffoldWindow>("Phases de scene");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Creation de phases", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Cette operation cree des scenes additives vides a cote de la scene source. " +
            "Elle ne deplace aucun objet et ne casse donc aucune reference.",
            MessageType.Info);

        sourceScene = (SceneAsset)EditorGUILayout.ObjectField(
            "Scene source",
            sourceScene,
            typeof(SceneAsset),
            false);

        using (new EditorGUI.DisabledScope(sourceScene == null))
        {
            if (GUILayout.Button("Creer Critical, Loading et PostLoading"))
            {
                CreateScaffolds();
            }
        }
    }

    private void CreateScaffolds()
    {
        string sourcePath = AssetDatabase.GetAssetPath(sourceScene);
        string directory = Path.GetDirectoryName(sourcePath);
        string baseName = Path.GetFileNameWithoutExtension(sourcePath);
        string[] names =
        {
            baseName + "_Critical",
            baseName + "_Loading_Environment_Near",
            baseName + "_Loading_Lights_Core",
            baseName + "_PostLoading_Environment_Far",
            baseName + "_PostLoading_Lights_Decorative",
            baseName + "_PostLoading_NPCs_VFX"
        };

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            int created = 0;
            for (int i = 0; i < names.Length; i++)
            {
                string destination = Path.Combine(directory, names[i] + ".unity").Replace('\\', '/');
                if (File.Exists(destination))
                {
                    continue;
                }

                Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(newScene, destination);
                created++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ScenePhaseScaffold] {created} phase(s) creee(s) pour {baseName}. Ouvre ensuite l'espace de scenes pour repartir les objets.");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }
}
