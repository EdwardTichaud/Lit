using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static partial class BootstrapDistrictPlayProbe
{
    [Serializable] private sealed class MoonriseRouteSurvey
    {
        public string utc;
        public string scope = "Read-only authored Moonrise survey. Paths use the closed-door baked NavMesh; neither interaction eligibility nor playable duration is certified.";
        public List<string> scenes = new();
        public List<MoonriseSurveyStop> stops = new();
    }
    [Serializable] private sealed class MoonriseSurveyStop
    {
        public string type, scene, hierarchy, identity;
        public bool active, enabled, navSample;
        public Vector3 position, navPosition;
        public List<string> bindings = new();
        public List<string> routes = new();
    }

    public static void SurveyMoonriseRouteBatch()
    {
        var previous = EditorSceneManager.GetSceneManagerSetup();
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
        var survey = new MoonriseRouteSurvey { utc = DateTime.UtcNow.ToString("O") };
        var scenePaths = EditorBuildSettings.scenes.ToDictionary(scene => Path.GetFileNameWithoutExtension(scene.path), scene => scene.path);
        foreach (string name in new[] { manifest.PrimarySceneName }.Concat(manifest.LoadingSceneNames).Distinct())
        {
            if (!scenePaths.TryGetValue(name, out string path)) throw new InvalidOperationException("Missing build scene for route survey: " + name);
            EditorSceneManager.OpenScene(path, survey.scenes.Count == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
            survey.scenes.Add(path);
        }
        Physics.SyncTransforms();
        var data = (NavMeshData)new SerializedObject(manifest).FindProperty("bakedNavMeshData").objectReferenceValue;
        var instance = NavMesh.AddNavMeshData(data);
        try
        {
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            var origins = new[] { (name: "arrival", point: new Vector3(-1.42f, -93.97f, 55.15f)),
                (name: "encounter", point: new Vector3(-9, -94, 43)),
                (name: "ladder1_upper", point: new Vector3(-16.03f, -89.93f, 42.74f)) };
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (behaviour == null || !survey.scenes.Contains(behaviour.gameObject.scene.path)) continue;
                string type = behaviour.GetType().Name;
                if (!(behaviour is ICharacterDetectedInteractable) && !(behaviour is SceneMarker) &&
                    !(behaviour is Lever) && !(behaviour is TwoLeverPuzzle) && !(behaviour is Pivot) &&
                    !type.Contains("Cycle") && !type.Contains("Enigma") && !type.Contains("Portal")) continue;
                var stop = new MoonriseSurveyStop { type = type, scene = behaviour.gameObject.scene.name,
                    identity = GlobalObjectId.GetGlobalObjectIdSlow(behaviour).ToString(),
                    position = behaviour.transform.position, active = behaviour.gameObject.activeInHierarchy, enabled = behaviour.enabled,
                    hierarchy = SurveyHierarchy(behaviour.transform) };
                var serialized = new SerializedObject(behaviour);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    string key = property.propertyPath.ToLowerInvariant();
                    if (!(key.Contains("knowledge") || key.Contains("item") || key.Contains("characterdata") ||
                          key.Contains("definition") || key.Contains("destination") || key.Contains("spawnid") ||
                          key.Contains("lever") || key.Contains("pivot") || key.Contains("targetbindings") ||
                          key == "activeduration" || key == "activationmode" || key == "playonce")) continue;
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                    {
                        string path = AssetDatabase.GetAssetPath(property.objectReferenceValue);
                        if (!string.IsNullOrEmpty(path)) stop.bindings.Add(property.propertyPath + "=" + path);
                        else if (property.objectReferenceValue is Component component)
                            stop.bindings.Add(property.propertyPath + "=" + component.gameObject.scene.name + "/" + SurveyHierarchy(component.transform));
                    }
                    else if (property.propertyType == SerializedPropertyType.String && !string.IsNullOrEmpty(property.stringValue))
                        stop.bindings.Add(property.propertyPath + "=" + property.stringValue);
                    else if (property.propertyType == SerializedPropertyType.Float)
                        stop.bindings.Add(property.propertyPath + "=" + property.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    else if (property.propertyType == SerializedPropertyType.Enum)
                        stop.bindings.Add(property.propertyPath + "=" + property.enumDisplayNames[property.enumValueIndex]);
                    else if (property.propertyType == SerializedPropertyType.Boolean)
                        stop.bindings.Add(property.propertyPath + "=" + property.boolValue);
                }
                stop.navSample = NavMesh.SamplePosition(stop.position, out var sample, 2.5f, filter);
                stop.navPosition = sample.position;
                if (stop.navSample)
                    foreach (var origin in origins)
                    {
                        if (!NavMesh.SamplePosition(origin.point, out var start, .7f, filter)) continue;
                        var path = new NavMeshPath();
                        NavMesh.CalculatePath(start.position, sample.position, filter, path);
                        float length = 0;
                        for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                        stop.routes.Add(origin.name + ":" + path.status + ":" + length.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "m");
                    }
                survey.stops.Add(stop);
            }
            survey.stops = survey.stops.OrderBy(stop => stop.scene).ThenBy(stop => stop.hierarchy).ThenBy(stop => stop.type).ToList();
            Directory.CreateDirectory("Library/MoonriseRoute");
            File.WriteAllText("Library/MoonriseRoute/authored-route-survey.json", JsonUtility.ToJson(survey, true));
        }
        finally { instance.Remove(); EditorSceneManager.RestoreSceneManagerSetup(previous); }
        EditorApplication.Exit(0);
    }

    private static string SurveyHierarchy(Transform root) => root.parent == null ? root.name : SurveyHierarchy(root.parent) + "/" + root.name;
}
