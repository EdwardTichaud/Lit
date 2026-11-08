using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

/// <summary>Repairs only a broken override with a valid prefab source, or an unambiguous identity-space collision mesh.</summary>
public static class SceneIntegrityRepair
{
    private static readonly List<string> changes = new();
    public static void RunBatch()
    {
        try { Run(); SceneIntegrityAudit.Run(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    [MenuItem("Lit/Validation/Repair Verified Scene References")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scenes before repair.");
        changes.Clear();
        var scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        try
        {
            foreach (string path in AssetDatabase.GetDependencies(scenes, true).Distinct().Where(p => p.EndsWith(".prefab")))
            {
                Debug.Log("[SceneIntegrityRepair] Inspect prefab " + path);
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (RepairHierarchy(prefab, path)) PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            foreach (string path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                foreach (var root in scene.GetRootGameObjects()) changed |= RepairHierarchy(root, path);
                if (changed) EditorSceneManager.SaveScene(scene);
            }
            foreach (string path in AssetDatabase.GetDependencies(scenes, true).Distinct().Where(p => p.EndsWith(".mat")))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && (material.shader == null || material.shader.name == "Hidden/InternalErrorShader") &&
                    path.StartsWith("Assets/Characters/1_Squad/Lucian/Lucian_CC5_Embed/Materials/", StringComparison.Ordinal))
                {
                    string backup = "Library/SceneIntegrity/MaterialBackups/" + material.name + ".mat";
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    LucianCC5HqIntegration.ConfigureMaterialForHq(material, material);
                    const string texturesRoot = "Assets/Characters/1_Squad/Lucian/Lucian_CC5_Embed/Lucian_CC5_Character_Model.fbm/";
                    foreach (var slot in new[] { ("_BaseColorMap", "_Diffuse.png"), ("_NormalMap", "_Normal.png") })
                    {
                        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturesRoot + material.name + slot.Item2);
                        if (texture != null && material.HasProperty(slot.Item1) && material.GetTexture(slot.Item1) == null)
                            material.SetTexture(slot.Item1, texture);
                    }
                    if (material.name.IndexOf("cornea", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        material.SetFloat("_SurfaceType", 1);
                        Color tint = material.GetColor("_BaseColor"); tint.a = Mathf.Min(tint.a, .12f);
                        material.SetColor("_BaseColor", tint);
                    }
                    UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
                    EditorUtility.SetDirty(material);
                    changes.Add(path + " | recover missing character shader with existing HDRP material conversion, preserving saved maps and alpha");
                }
                if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader") continue;
                var data = new SerializedObject(material);
                var textures = data.FindProperty("m_SavedProperties.m_TexEnvs");
                bool changed = false;
                for (int i = textures.arraySize - 1; i >= 0; i--)
                {
                    var entry = textures.GetArrayElementAtIndex(i);
                    string key = entry.FindPropertyRelative("first").stringValue;
                    var reference = entry.FindPropertyRelative("second.m_Texture");
                    if (Broken(reference) && !material.HasProperty(key))
                    {
                        textures.DeleteArrayElementAtIndex(i); changed = true;
                        changes.Add(path + " | remove obsolete missing texture property " + key);
                    }
                }
                if (changed) { data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(material); }
            }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            Directory.CreateDirectory("Library/SceneIntegrity");
            File.WriteAllLines("Library/SceneIntegrity/deterministic-repairs.txt", changes);
            if (setup.Any(s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        Debug.Log("[SceneIntegrityRepair] Applied " + changes.Count + " verified repairs.");
    }
    internal static bool RepairHierarchy(GameObject root, string path)
    {
        bool changed = false;
        // This authored root only groups its 31 emitting children; it has no emission or material.
        if (path == "Assets/Environment/3_VFX/VFX_Rain/VFX_RainSplash.prefab" &&
            root.TryGetComponent<ParticleSystem>(out var rainContainer) && !rainContainer.emission.enabled &&
            root.GetComponentsInChildren<ParticleSystem>(true).Length > 1 &&
            root.TryGetComponent<ParticleSystemRenderer>(out var containerRenderer) &&
            containerRenderer.sharedMaterial == null && containerRenderer.renderMode != ParticleSystemRenderMode.None)
        {
            containerRenderer.renderMode = ParticleSystemRenderMode.None;
            EditorUtility.SetDirty(containerRenderer); changed = true;
            changes.Add(path + " | non-emitting root explicitly uses Render Mode None; child rain systems preserved");
        }
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            if (component is ParticleSystemRenderer particle && particle.renderMode != ParticleSystemRenderMode.Mesh)
            {
                var particleData = new SerializedObject(particle);
                bool particleChanged = false;
                foreach (string field in new[] { "m_Mesh", "m_Mesh1", "m_Mesh2", "m_Mesh3" })
                {
                    var mesh = particleData.FindProperty(field);
                    if (!Broken(mesh)) continue;
                    mesh.objectReferenceValue = null; particleChanged = true;
                    changes.Add(path + " | " + particle.name + " | remove broken unused mesh slot in " + particle.renderMode + " mode");
                }
                if (particleChanged) { particleData.ApplyModifiedPropertiesWithoutUndo(); changed = true; }
            }
            if (component is LineRenderer line && line.sharedMaterial == null && line.GetComponentInParent<FlameGuidanceArcRenderer>() != null)
            {
                const string materialPath = "Assets/Gameplay/Temporal/FlameGuidance/Materials/FlameGuidanceArc.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    EnsureFolder(Path.GetDirectoryName(materialPath).Replace('\\', '/'));
                    material = new Material(Shader.Find("Sprites/Default")) { name = "FlameGuidanceArc" };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                line.sharedMaterial = material; EditorUtility.SetDirty(line); changed = true;
                changes.Add(path + " | " + line.name + " | authored guidance material; runtime reuses these lines");
            }
            var materialReferences = new SerializedObject(component);
            var referenceIterator = materialReferences.GetIterator();
            bool replacedMaterial = false;
            while (referenceIterator.Next(true))
            {
                if (referenceIterator.propertyType != SerializedPropertyType.ObjectReference ||
                    referenceIterator.objectReferenceValue is not Material currentMaterial ||
                    AssetDatabase.GetAssetPath(currentMaterial) != "Packages/com.unity.probuilder/Content/Resources/Materials/ProBuilderDefault.mat" ||
                    currentMaterial.shader != null && currentMaterial.shader.name != "Hidden/InternalErrorShader") continue;
                var replacement = GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.defaultMaterial : null;
                if (replacement == null || replacement.shader == null || replacement.shader.name == "Hidden/InternalErrorShader") continue;
                referenceIterator.objectReferenceValue = replacement; replacedMaterial = true;
                changes.Add(path + " | " + component.name + " | replace obsolete ProBuilder default with active render pipeline default");
            }
            if (replacedMaterial) { materialReferences.ApplyModifiedPropertiesWithoutUndo(); changed = true; }
            if (component is MeshCollider collider && collider.sharedMesh == null)
            {
                // A mesh on a transformed child would require a baked conversion, never a direct assignment.
                var filters = collider.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null &&
                    (collider.transform.worldToLocalMatrix * f.transform.localToWorldMatrix).ValidTRS() &&
                    Identity(collider.transform.worldToLocalMatrix * f.transform.localToWorldMatrix)).ToArray();
                if (filters.Length == 1)
                {
                    collider.sharedMesh = filters[0].sharedMesh; EditorUtility.SetDirty(collider); changed = true;
                    changes.Add(path + " | " + collider.name + " | collision mesh from identity-space visual " + filters[0].sharedMesh.name);
                }
            }
            if (!PrefabUtility.IsPartOfPrefabInstance(component)) continue;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(component);
            if (source == null) continue;
            var current = new SerializedObject(component);
            var original = new SerializedObject(source);
            var property = current.GetIterator();
            var revert = new List<string>();
            while (property.Next(true))
            {
                if (!property.prefabOverride || !Broken(property)) continue;
                var baseline = original.FindProperty(property.propertyPath);
                if (baseline != null && baseline.propertyType == SerializedPropertyType.ObjectReference && baseline.objectReferenceValue != null)
                    revert.Add(property.propertyPath);
            }
            foreach (string field in revert)
            {
                PrefabUtility.RevertPropertyOverride(new SerializedObject(component).FindProperty(field), InteractionMode.AutomatedAction);
                changes.Add(path + " | " + component.name + " | revert broken override " + field + " to valid prefab source"); changed = true;
            }
        }
        return changed;
    }
    private static bool Broken(SerializedProperty property) => property != null && property.propertyType == SerializedPropertyType.ObjectReference &&
        property.objectReferenceValue == null && property.objectReferenceEntityIdValue != default;
    private static bool Identity(Matrix4x4 matrix)
    {
        for (int i = 0; i < 16; i++) if (Mathf.Abs(matrix[i] - Matrix4x4.identity[i]) > .0001f) return false;
        return true;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
