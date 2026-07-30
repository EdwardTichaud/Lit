using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(ProximitySceneVolume))]
public sealed class ProximitySceneVolumeEditor : Editor
{
    private const string DefaultFolder = "Assets/Scenes/District_1";

    [MenuItem("Lit/Scenes/Creer une cellule de proximite depuis la selection")]
    private static void CreateCellFromSelection()
    {
        GameObject host = Selection.activeGameObject;
        if (host == null || !host.scene.IsValid() || !host.scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Cellule de proximite", "Selectionne un GameObject de la scene Critical qui servira de point de proximite.", "OK");
            return;
        }

        string folder = Directory.Exists(DefaultFolder) ? DefaultFolder : "Assets/Scenes";
        string path = EditorUtility.SaveFilePanelInProject(
            "Creer une cellule de proximite decorative",
            host.scene.name + "_Proximity_Decor",
            "unity",
            "La scene creee doit contenir uniquement du decor local.",
            folder);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Scene cellScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SaveScene(cellScene, path);
        EditorSceneManager.CloseScene(cellScene, true);

        ProximitySceneVolume volume = host.GetComponent<ProximitySceneVolume>();
        if (volume == null)
        {
            volume = Undo.AddComponent<ProximitySceneVolume>(host);
        }

        SerializedObject serializedVolume = new SerializedObject(volume);
        serializedVolume.FindProperty("sceneAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        serializedVolume.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(host.scene);
        Selection.activeObject = volume;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Cellule locale : decor, lumieres, VFX et ambiance uniquement. " +
            "N'y place ni NetworkObject, ni ennemi/PNJ, ni portail, ni collision indispensable.",
            MessageType.Info);

        if (GUILayout.Button("Creer une sous-scene decorative"))
        {
            Selection.activeGameObject = ((ProximitySceneVolume)target).gameObject;
            CreateCellFromSelection();
        }
    }
}
