using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lit.Traversal;

[InitializeOnLoad]
public static class GrappleAuthoring
{
    public const string ScenePath = "Assets/Scenes/Reve/Reve_Limbes_Progression.unity";
    public const string Root = "Library/GrappleValidation";
    static GrappleAuthoring() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string request = Root + "/Install.request";
        if (!File.Exists(request)) return;
        File.Delete(request);
        try { Install(); File.WriteAllText(Root + "/Install.result", "PASS " + DateTime.UtcNow.ToString("O")); }
        catch (Exception e) { File.WriteAllText(Root + "/Install.result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Lit/Traversal/Install Limbes Grapple")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save unsaved scene voluntarily before installing grapple.");
        const string abilityPath = "Assets/Resources/Abilities/Ability_Bondisseur.asset";
        var ability = AssetDatabase.LoadAssetAtPath<AbilitySO>(abilityPath);
        if (ability == null)
        {
            ability = ScriptableObject.CreateInstance<AbilitySO>();
            ability.abilityId = "bondisseur"; ability.displayName = "Bondisseur";
            ability.description = "Utilise les ancrages révélés par Observateur. En coopération, prépare la traversée pour toute l’équipe.";
            // No invented campaign reward: acquired through the existing explicit AbilityUnlock event.
            AssetDatabase.CreateAsset(ability, abilityPath);
        }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var progression = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LimbesZoneProgression>(true)).Single();
            var access = progression.arenaAccess;
            var existing = access.GetComponentsInChildren<GrappleAnchor>(true);
            if (existing.Length > 0)
            {
                var outbound = existing.Single(a => a.anchorId == "limbes.statue.aller");
                outbound.landing.position = new Vector3(610, 129.43f, 114);
                outbound.transform.position = outbound.landing.position + Vector3.up * 4;
                foreach (Transform child in access.transform)
                    if (child.name.StartsWith("Pont_final_", StringComparison.Ordinal)) child.gameObject.SetActive(true);
                var step = access.transform.Find("Palier_depart_grappin");
                if (step != null && step.TryGetComponent<UnityEngine.AI.NavMeshObstacle>(out var oldCarve))
                    UnityEngine.Object.DestroyImmediate(oldCarve);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                return;
            }
            // Preserve the existing walking route until Bondisseur's campaign acquisition is authored.
            // Both routes remain behind the same five-cycle gate.
            Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Reve/Completion/Data/LimbesExpansionStone.mat");
            Material glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Reve/Lighting/Limbes_Lueur.mat");
            var launch = Marker(access.transform, "Depart_grappin_statue", new Vector3(604, 129.43f, 89));
            var arrival = Marker(access.transform, "Reception_grappin_statue", new Vector3(610, 129.43f, 114));
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Palier_depart_grappin";
            SceneManager.MoveGameObjectToScene(floor, scene); floor.transform.SetParent(access.transform, false);
            floor.transform.position = new Vector3(604, 129, 89); floor.transform.localScale = new Vector3(6, .8f, 6);
            if (stone != null) floor.GetComponent<Renderer>().sharedMaterial = stone;
            CreateAnchor(access.transform, "limbes.statue.aller", launch, arrival, glow);
            CreateAnchor(access.transform, "limbes.statue.retour", arrival, launch, glow);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            var active = setup.FirstOrDefault(s => s.isActive);
            if (active != null) { var original = SceneManager.GetSceneByPath(active.path); if (original.isLoaded) SceneManager.SetActiveScene(original); }
        }
    }
    private static Transform Marker(Transform parent, string name, Vector3 position)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position; return go.transform;
    }
    private static void CreateAnchor(Transform parent, string id, Transform launch, Transform landing, Material material)
    {
        var root = Marker(parent, id, landing.position + Vector3.up * 4);
        var anchor = root.gameObject.AddComponent<GrappleAnchor>(); anchor.anchorId = id; anchor.routeId = "limbes.statue";
        anchor.launch = launch; anchor.landing = landing; anchor.hook = root;
        var mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere); mesh.name = "Ancrage_lumineux";
        mesh.transform.SetParent(root, false); mesh.transform.localScale = Vector3.one * .65f;
        UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
        if (material != null) mesh.GetComponent<Renderer>().sharedMaterial = material;
        anchor.visual = mesh;
    }
}
