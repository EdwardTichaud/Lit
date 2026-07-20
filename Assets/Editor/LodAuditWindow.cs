using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Audit non destructif des meshes presents dans les scenes ouvertes.
/// Il sert a prioriser les vrais candidats aux LOD : un mesh lourd, tres
/// instancie et qui n'est couvert par aucun LODGroup.
/// </summary>
public sealed class LodAuditWindow : EditorWindow
{
    private sealed class Record
    {
        public Mesh mesh;
        public string assetPath;
        public long triangles;
        public int instanceCount;
        public int lodCoveredInstances;
        public readonly List<string> sampleObjects = new List<string>();

        public int MissingLodInstances => Mathf.Max(0, instanceCount - lodCoveredInstances);
        public long MissingLodTriangleCost => triangles * MissingLodInstances;
    }

    private readonly List<Record> records = new List<Record>();
    private Vector2 scrollPosition;
    private string sceneSummary = "Aucune analyse effectuee.";
    private bool showOnlyMissingLod = true;

    [MenuItem("Lit/Performance/LOD Audit")]
    public static void Open()
    {
        GetWindow<LodAuditWindow>("LOD Audit");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Audit des LOD", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Analyse les scenes actuellement ouvertes. Aucun asset n'est modifie. "
            + "Ouvre Maison ou un District, puis lance l'analyse.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Analyser les scenes ouvertes", GUILayout.Height(28f)))
            {
                AnalyzeOpenScenes();
            }

            using (new EditorGUI.DisabledScope(records.Count == 0))
            {
                if (GUILayout.Button("Afficher dans la Console", GUILayout.Height(28f)))
                {
                    PrintReport();
                }
            }
        }

        showOnlyMissingLod = EditorGUILayout.ToggleLeft("Afficher seulement les meshes sans LOD", showOnlyMissingLod);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(sceneSummary, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(6f);

        DrawHeader();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (Record record in records)
        {
            if (showOnlyMissingLod && record.MissingLodInstances == 0)
            {
                continue;
            }

            DrawRecord(record);
        }
        EditorGUILayout.EndScrollView();
    }

    private static void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Mesh", GUILayout.Width(220f));
            GUILayout.Label("Triangles", GUILayout.Width(80f));
            GUILayout.Label("Instances", GUILayout.Width(65f));
            GUILayout.Label("Sans LOD", GUILayout.Width(72f));
            GUILayout.Label("Impact estime", GUILayout.Width(105f));
        }
    }

    private static void DrawRecord(Record record)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            string label = string.IsNullOrEmpty(record.assetPath)
                ? record.mesh.name + " (mesh de scene)"
                : record.mesh.name;
            if (GUILayout.Button(label, EditorStyles.linkLabel, GUILayout.Width(220f)))
            {
                Selection.activeObject = record.mesh;
                EditorGUIUtility.PingObject(record.mesh);
            }

            GUILayout.Label(FormatNumber(record.triangles), GUILayout.Width(80f));
            GUILayout.Label(record.instanceCount.ToString(), GUILayout.Width(65f));
            GUILayout.Label(record.MissingLodInstances.ToString(), GUILayout.Width(72f));
            GUILayout.Label(FormatNumber(record.MissingLodTriangleCost), GUILayout.Width(105f));
        }

        if (record.sampleObjects.Count > 0)
        {
            EditorGUILayout.LabelField("     Exemples : " + string.Join(", ", record.sampleObjects), EditorStyles.miniLabel);
        }
    }

    private void AnalyzeOpenScenes()
    {
        records.Clear();
        Dictionary<int, Record> byMeshId = new Dictionary<int, Record>();
        HashSet<int> lodRendererIds = CollectLodRenderers();
        List<string> analyzedScenes = new List<string>();

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
            {
                continue;
            }

            analyzedScenes.Add(scene.name);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = GetMesh(renderer);
                    if (mesh == null)
                    {
                        continue;
                    }

                    int meshId = mesh.GetInstanceID();
                    if (!byMeshId.TryGetValue(meshId, out Record record))
                    {
                        record = new Record
                        {
                            mesh = mesh,
                            assetPath = AssetDatabase.GetAssetPath(mesh),
                            triangles = GetTriangleCount(mesh)
                        };
                        byMeshId.Add(meshId, record);
                        records.Add(record);
                    }

                    record.instanceCount++;
                    if (lodRendererIds.Contains(renderer.GetInstanceID()))
                    {
                        record.lodCoveredInstances++;
                    }

                    if (record.sampleObjects.Count < 3)
                    {
                        record.sampleObjects.Add(renderer.gameObject.name);
                    }
                }
            }
        }

        records.Sort((left, right) => right.MissingLodTriangleCost.CompareTo(left.MissingLodTriangleCost));
        int missing = records.Sum(record => record.MissingLodInstances);
        sceneSummary = analyzedScenes.Count == 0
            ? "Aucune scene ouverte."
            : $"Scenes : {string.Join(", ", analyzedScenes)} | {records.Count} meshes uniques | {missing} instances sans LOD.";
        Repaint();
    }

    private static HashSet<int> CollectLodRenderers()
    {
        HashSet<int> result = new HashSet<int>();
        foreach (LODGroup group in Resources.FindObjectsOfTypeAll<LODGroup>())
        {
            if (group == null || !group.gameObject.scene.isLoaded)
            {
                continue;
            }

            foreach (LOD lod in group.GetLODs())
            {
                foreach (Renderer renderer in lod.renderers)
                {
                    if (renderer != null)
                    {
                        result.Add(renderer.GetInstanceID());
                    }
                }
            }
        }

        return result;
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer is MeshRenderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        if (renderer is SkinnedMeshRenderer skinnedRenderer)
        {
            return skinnedRenderer.sharedMesh;
        }

        return null;
    }

    private static long GetTriangleCount(Mesh mesh)
    {
        long indexCount = 0;
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            indexCount += (long)mesh.GetIndexCount(subMesh);
        }

        return indexCount / 3L;
    }

    private void PrintReport()
    {
        IEnumerable<Record> missingRecords = records.Where(record => record.MissingLodInstances > 0).Take(50);
        List<string> lines = new List<string>
        {
            "[LOD Audit] Priorites sans LOD (impact = triangles x instances sans LOD)"
        };

        foreach (Record record in missingRecords)
        {
            lines.Add(
                $"- {record.mesh.name}: triangles={record.triangles}, instances={record.instanceCount}, "
                + $"sansLOD={record.MissingLodInstances}, impact={record.MissingLodTriangleCost}, asset={record.assetPath}");
        }

        Debug.Log(string.Join("\n", lines));
    }

    private static string FormatNumber(long value)
    {
        return value.ToString("N0");
    }
}
