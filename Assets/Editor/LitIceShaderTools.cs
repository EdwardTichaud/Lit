using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal static class LitIceShaderInstaller
{
    // Keep every Lit Ice variant in one validation pass so graph failures are visible immediately.
    private const string GraphPath = "Assets/Materials/IceShader/ShaderGraph_LitIceFrostedEdges.shadergraph";
    private const string MaterialPath = "Assets/Materials/IceShader/Material_LitIceFrostedEdges.mat";
    private const string GraphV2Path = "Assets/Materials/IceShader/ShaderGraph_LitIceFrostedEdges_v2.shadergraph";
    private const string MaterialV2Path = "Assets/Materials/IceShader/Material_LitIceFrostedEdges_v2.mat";
    private const string GraphV3Path = "Assets/Materials/IceShader/ShaderGraph_LitIceFrostedEdges_v3.shadergraph";
    private const string MaterialV3Path = "Assets/Materials/IceShader/Material_LitIceFrostedEdges_v3.mat";
    private const string StatusPath = "Temp/LitIceShaderGraphStatus.txt";
    private static bool s_Running;

    [InitializeOnLoadMethod]
    private static void ScheduleImport()
    {
        EditorApplication.delayCall += ImportAndCreateMaterial;
    }

    [MenuItem("Lit/Shadergraph/Reimport Shader Graph")]
    private static void ImportAndCreateMaterial()
    {
        if (s_Running)
            return;

        s_Running = true;
        try
        {
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            if (shader == null)
                throw new InvalidOperationException("Shader Graph import returned a null shader.");

            if (ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Shader Graph imported, but HDRP reported a shader compilation error.");

            AssetDatabase.ImportAsset(GraphV2Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Shader shaderV2 = AssetDatabase.LoadAssetAtPath<Shader>(GraphV2Path);
            if (shaderV2 == null)
                throw new InvalidOperationException("Shader Graph v2 import returned a null shader.");

            if (ShaderUtil.ShaderHasError(shaderV2))
                throw new InvalidOperationException("Shader Graph v2 imported, but HDRP reported a shader compilation error.");

            AssetDatabase.ImportAsset(GraphV3Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Shader shaderV3 = AssetDatabase.LoadAssetAtPath<Shader>(GraphV3Path);
            if (shaderV3 == null)
                throw new InvalidOperationException("Shader Graph v3 import returned a null shader.");

            if (ShaderUtil.ShaderHasError(shaderV3))
                throw new InvalidOperationException("Shader Graph v3 imported, but HDRP reported a shader compilation error.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Material_LitIceFrostedEdges" };
                ApplyRecommendedPresetValues(material);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            Material materialV2 = AssetDatabase.LoadAssetAtPath<Material>(MaterialV2Path);
            if (materialV2 == null)
            {
                materialV2 = new Material(material) { name = "Material_LitIceFrostedEdges_v2" };
                materialV2.shader = shaderV2;
                ApplyV2Defaults(materialV2);
                AssetDatabase.CreateAsset(materialV2, MaterialV2Path);
            }
            else if (materialV2.shader != shaderV2)
            {
                materialV2.shader = shaderV2;
                EditorUtility.SetDirty(materialV2);
            }

            Material materialV3 = AssetDatabase.LoadAssetAtPath<Material>(MaterialV3Path);
            if (materialV3 == null)
            {
                materialV3 = new Material(materialV2) { name = "Material_LitIceFrostedEdges_v3" };
                materialV3.shader = shaderV3;
                ApplyV3Defaults(materialV3);
                AssetDatabase.CreateAsset(materialV3, MaterialV3Path);
            }
            else if (materialV3.shader != shaderV3)
            {
                materialV3.shader = shaderV3;
                EditorUtility.SetDirty(materialV3);
            }

            AssetDatabase.SaveAssets();
            WriteStatus("OK");
            Debug.Log("[Lit Ice] Shader Graphs v1/v2/v3 and their materials imported successfully.");
        }
        catch (Exception exception)
        {
            WriteStatus(exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            s_Running = false;
        }
    }

    [MenuItem("Lit/Shadergraph/Apply Recommended Ice Preset")]
    private static void ApplyRecommendedPresetToExistingMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogError($"[Lit Ice] Material not found at {MaterialPath}. Reimport the Shader Graph first.");
            return;
        }

        Undo.RecordObject(material, "Apply recommended Lit Ice preset");
        ApplyRecommendedPresetValues(material);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        Debug.Log("[Lit Ice] Recommended ice preset applied to Material_LitIceFrostedEdges.");
    }

    private static void ApplyRecommendedPresetValues(Material material)
    {
        SetColor(material, "_IceDeepColor", new Color(0.015f, 0.11f, 0.26f, 1f));
        SetColor(material, "_FrostColor", new Color(1.0f, 1.3f, 1.8f, 1f));
        SetColor(material, "_CrackColor", new Color(0.8f, 1.2f, 1.7f, 1f));
        SetFloat(material, "_IceScale", 1.8f);
        SetFloat(material, "_FrostWidth", 0.22f);
        SetFloat(material, "_Transparency", 0.93f);
        SetFloat(material, "_NormalStrength", 0.32f);
        SetFloat(material, "_EdgeSensitivity", 3.5f);
        SetVector(material, "_NoiseOffset", Vector4.zero);
        SetFloat(material, "_MicroScale", 7.0f);
        SetFloat(material, "_CrackWidth", 0.045f);
        SetFloat(material, "_FresnelPower", 3.0f);
        SetFloat(material, "_FresnelIntensity", 0.0f);
        SetFloat(material, "_EmissionIntensity", 3.0f);
        SetFloat(material, "_EdgeBakedBoost", 0.9f);
        SetFloat(material, "_Smoothness", 0.88f);
        SetFloat(material, "_Metallic", 0.0f);
    }

    private static void ApplyV2Defaults(Material material)
    {
        SetColor(material, "_BaseColor", Color.white);
        SetFloat(material, "_BaseNormalStrength", 1.0f);
        SetFloat(material, "_BaseSmoothness", 0.5f);
        SetFloat(material, "_BaseMetallic", 0.0f);
        SetFloat(material, "_UseBaseRoughnessTexture", 0.0f);
        SetFloat(material, "_UseBaseMetallicTexture", 0.0f);
        SetFloat(material, "_UseBaseOcclusionTexture", 0.0f);
        SetFloat(material, "_UseScaleTiling", 0.0f);
        SetFloat(material, "_TilingMultiplier", 1.0f);
        SetVector(material, "_FlameCenter", Vector4.zero);
        SetFloat(material, "_FlameInfluenceRadius", 0.0f);
        SetFloat(material, "_TransitionSoftness", 0.5f);
        SetFloat(material, "_TransitionProgress", 1.0f);
    }

    private static void ApplyV3Defaults(Material material)
    {
        ApplyV2Defaults(material);
        // V3 maps the complete inspector range 0..10 to the normalized frost width used by the core shader.
        SetFloat(material, "_FrostWidth", 2.2f);
        SetFloat(material, "_IceReliefNormalStrength", 0.75f);
        SetFloat(material, "_IceReliefRoughnessInfluence", 0.4f);
        SetFloat(material, "_TextureEdgeStrength", 1.0f);
        SetFloat(material, "_TextureEdgeWidth", 1.5f);
        SetFloat(material, "_TextureEdgeThreshold", 0.25f);
        SetFloat(material, "_TextureEdgeNormalInfluence", 1.0f);
        SetFloat(material, "_TextureEdgeRoughnessInfluence", 0.5f);
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private static void SetColor(Material material, string property, Color value)
    {
        if (material.HasProperty(property)) material.SetColor(property, value);
    }

    private static void SetVector(Material material, string property, Vector4 value)
    {
        if (material.HasProperty(property)) material.SetVector(property, value);
    }

    private static void WriteStatus(string contents)
    {
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), StatusPath), contents);
    }
}

internal static class LitIceEdgeMaskBaker
{
    private const float EdgeAngleDegrees = 32f;
    private const string BakedVersionMarker = "IceEdgesV3_";
    private const string OutputFolder = "Assets/Materials/IceShader/BakedMeshes";
    private const string IceMaterialPath = "Assets/Materials/IceShader/Material_LitIceFrostedEdges.mat";
    private const string IceMaterialV2Path = "Assets/Materials/IceShader/Material_LitIceFrostedEdges_v2.mat";
    private const string IceMaterialV3Path = "Assets/Materials/IceShader/Material_LitIceFrostedEdges_v3.mat";
    private const string IceShaderV3Path = "Assets/Materials/IceShader/ShaderGraph_LitIceFrostedEdges_v3.shadergraph";
    private const string BakeAllMenu = "Lit/Shadergraph/Bake Edge Mask On All Material_LitIceFrostedEdges";

    internal sealed class BakeSummary
    {
        public bool UsedSelection;
        public int CandidateRendererCount;
        public int UpdatedRendererCount;
        public int CreatedMeshCount;
        public int AlreadyBakedCount;
        public int MissingMeshCount;
        public int ErrorCount;

        public string ToDisplayMessage()
        {
            if (CandidateRendererCount == 0)
                return "Aucun renderer chargé n’utilise ce matériau.";

            string scope = UsedSelection ? "sélection" : "scènes chargées";
            string message = $"Bake ({scope}) : {UpdatedRendererCount} renderer(s) mis à jour, "
                           + $"{CreatedMeshCount} mesh(s) créé(s), {AlreadyBakedCount} déjà baké(s).";
            if (MissingMeshCount > 0)
                message += $" {MissingMeshCount} renderer(s) sans mesh.";
            if (ErrorCount > 0)
                message += $" {ErrorCount} erreur(s), voir la Console.";
            return message;
        }
    }

    [MenuItem("Lit/Shadergraph/Bake Edge Mask On Selected Meshes", true)]
    private static bool CanBakeSelection()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go.GetComponentInChildren<MeshFilter>(true) != null
                || go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                return true;
        }
        return false;
    }

    [MenuItem("Lit/Shadergraph/Bake Edge Mask On Selected Meshes")]
    private static void BakeSelection()
    {
        Renderer[] renderers = Selection.gameObjects
            .SelectMany(go => go.GetComponentsInChildren<Renderer>(true))
            .Distinct()
            .ToArray();
        BakeSummary summary = BakeRenderers(renderers, true);
        Debug.Log($"[Lit Ice] {summary.ToDisplayMessage()}");
    }

    [MenuItem(BakeAllMenu, true)]
    private static bool CanBakeAllMaterialUsers()
    {
        return AssetDatabase.LoadAssetAtPath<Material>(IceMaterialPath) != null
            || AssetDatabase.LoadAssetAtPath<Material>(IceMaterialV2Path) != null
            || AssetDatabase.LoadAssetAtPath<Material>(IceMaterialV3Path) != null
            || AssetDatabase.LoadAssetAtPath<Shader>(IceShaderV3Path) != null;
    }

    [MenuItem(BakeAllMenu)]
    private static void BakeAllMaterialUsers()
    {
        Material iceMaterial = AssetDatabase.LoadAssetAtPath<Material>(IceMaterialPath);
        Material iceMaterialV2 = AssetDatabase.LoadAssetAtPath<Material>(IceMaterialV2Path);
        Material iceMaterialV3 = AssetDatabase.LoadAssetAtPath<Material>(IceMaterialV3Path);
        Shader iceShaderV3 = AssetDatabase.LoadAssetAtPath<Shader>(IceShaderV3Path);
        if (iceMaterial == null && iceMaterialV2 == null && iceMaterialV3 == null && iceShaderV3 == null)
        {
            Debug.LogError($"[Lit Ice] No v1/v2/v3 Lit Ice material was found.");
            return;
        }

        var iceMaterials = new HashSet<Material>();
        if (iceMaterial != null) iceMaterials.Add(iceMaterial);
        if (iceMaterialV2 != null) iceMaterials.Add(iceMaterialV2);
        if (iceMaterialV3 != null) iceMaterials.Add(iceMaterialV3);

        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>()
            .Where(renderer => IsEditableSceneRenderer(renderer)
                && UsesAnyMaterial(renderer, iceMaterials, iceShaderV3))
            .ToArray();
        BakeSummary summary = BakeRenderers(renderers, false);
        Debug.Log($"[Lit Ice] Global bake complete. {summary.ToDisplayMessage()}");
    }

    internal static BakeSummary BakeForMaterials(IEnumerable<Material> materials)
    {
        var materialSet = new HashSet<Material>(materials.Where(material => material != null));
        Renderer[] selectedRenderers = Selection.gameObjects
            .SelectMany(go => go.GetComponentsInChildren<Renderer>(true))
            .Where(renderer => IsEditableSceneRenderer(renderer)
                && UsesAnyMaterial(renderer, materialSet))
            .Distinct()
            .ToArray();

        bool useSelection = selectedRenderers.Length > 0;
        Renderer[] renderers = useSelection
            ? selectedRenderers
            : Resources.FindObjectsOfTypeAll<Renderer>()
                .Where(renderer => IsEditableSceneRenderer(renderer)
                    && UsesAnyMaterial(renderer, materialSet))
                .Distinct()
                .ToArray();

        return BakeRenderers(renderers, useSelection);
    }

    private static BakeSummary BakeRenderers(IEnumerable<Renderer> renderers, bool usedSelection)
    {
        EnsureOutputFolder();
        Renderer[] candidates = renderers
            .Where(IsEditableSceneRenderer)
            .Distinct()
            .ToArray();
        var summary = new BakeSummary
        {
            UsedSelection = usedSelection,
            CandidateRendererCount = candidates.Length
        };
        var bakedBySource = new Dictionary<Mesh, Mesh>();

        foreach (Renderer renderer in candidates)
        {
            Mesh source = GetSharedMesh(renderer);
            if (source == null)
            {
                summary.MissingMeshCount++;
                continue;
            }

            if (IsCurrentBakedMesh(source))
            {
                summary.AlreadyBakedCount++;
                continue;
            }

            try
            {
                if (!bakedBySource.TryGetValue(source, out Mesh baked))
                {
                    baked = SaveBakedMesh(BakeMesh(source));
                    bakedBySource.Add(source, baked);
                    summary.CreatedMeshCount++;
                }

                Undo.RecordObject(renderer, "Bake Lit Ice edge mask");
                AssignSharedMesh(renderer, baked);
                EditorUtility.SetDirty(renderer);
                summary.UpdatedRendererCount++;
            }
            catch (Exception exception)
            {
                summary.ErrorCount++;
                Debug.LogError(
                    $"[Lit Ice] Could not bake '{renderer.name}' ({source.name}): {exception.Message}",
                    renderer);
            }
        }

        AssetDatabase.SaveAssets();
        return summary;
    }

    private static bool IsEditableSceneRenderer(Renderer renderer)
    {
        return renderer != null
            && !EditorUtility.IsPersistent(renderer)
            && renderer.gameObject.scene.IsValid()
            && renderer.gameObject.scene.isLoaded;
    }

    private static bool UsesAnyMaterial(Renderer renderer, HashSet<Material> materials,
        Shader additionalShader = null)
    {
        foreach (Material sharedMaterial in renderer.sharedMaterials)
            if (sharedMaterial != null
                && (materials.Contains(sharedMaterial)
                    || (additionalShader != null && sharedMaterial.shader == additionalShader)))
                return true;
        return false;
    }

    private static Mesh GetSharedMesh(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned)
            return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private static void AssignSharedMesh(Renderer renderer, Mesh mesh)
    {
        if (renderer is SkinnedMeshRenderer skinned)
        {
            skinned.sharedMesh = mesh;
            return;
        }

        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter != null)
        {
            Undo.RecordObject(filter, "Bake Lit Ice edge mask");
            filter.sharedMesh = mesh;
            EditorUtility.SetDirty(filter);
        }
    }

    private static Mesh BakeMesh(Mesh source)
    {
        Vector3[] vertices = source.vertices;
        float threshold = Mathf.Cos(EdgeAngleDegrees * Mathf.Deg2Rad);
        float tolerance = Mathf.Max(source.bounds.size.magnitude * 0.00001f, 0.000001f);

        var submeshTriangles = new List<int[]>(source.subMeshCount);
        var geometricEdges = new Dictionary<GeometricEdgeKey, EdgeRecord>();
        int triangleIndexCount = 0;

        for (int submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            if (source.GetTopology(submesh) != MeshTopology.Triangles)
                throw new InvalidOperationException(
                    $"Submesh {submesh} of '{source.name}' is not made of triangles.");

            int[] triangles = source.GetTriangles(submesh);
            submeshTriangles.Add(triangles);
            triangleIndexCount += triangles.Length;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                Vector3 faceNormal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).normalized;
                RegisterEdge(a, b, vertices, tolerance, faceNormal, threshold, geometricEdges);
                RegisterEdge(b, c, vertices, tolerance, faceNormal, threshold, geometricEdges);
                RegisterEdge(c, a, vertices, tolerance, faceNormal, threshold, geometricEdges);
            }
        }

        // A unique vertex is required for every triangle corner. RGB then carries
        // signed barycentric coordinates: a positive channel means that the edge
        // opposite that corner is a real stone edge; a negative one suppresses an
        // ordinary triangulation diagonal. This creates a thin pixel-width line
        // even when the original stone face has no interior vertices.
        var newToOld = new List<int>(triangleIndexCount);
        var bakedVertices = new List<Vector3>(triangleIndexCount);
        var bakedColors = new List<Color>(triangleIndexCount);
        var bakedSubmeshTriangles = new List<int[]>(source.subMeshCount);

        for (int submesh = 0; submesh < submeshTriangles.Count; submesh++)
        {
            int[] triangles = submeshTriangles[submesh];
            int[] bakedTriangles = new int[triangles.Length];
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                bool edgeAB = IsSelectedEdge(a, b, vertices, tolerance, geometricEdges);
                bool edgeBC = IsSelectedEdge(b, c, vertices, tolerance, geometricEdges);
                bool edgeCA = IsSelectedEdge(c, a, vertices, tolerance, geometricEdges);

                AddBakedCorner(a, new Color(edgeBC ? 1f : -1f, 0f, 0f, 0.25f),
                    vertices, newToOld, bakedVertices, bakedColors, bakedTriangles, i);
                AddBakedCorner(b, new Color(0f, edgeCA ? 1f : -1f, 0f, 0.25f),
                    vertices, newToOld, bakedVertices, bakedColors, bakedTriangles, i + 1);
                AddBakedCorner(c, new Color(0f, 0f, edgeAB ? 1f : -1f, 0.25f),
                    vertices, newToOld, bakedVertices, bakedColors, bakedTriangles, i + 2);
            }
            bakedSubmeshTriangles.Add(bakedTriangles);
        }

        var mesh = new Mesh
        {
            // Keep the internal name short as rebaking an already baked mesh
            // must never recursively append the complete source name.
            name = BakedVersionMarker + "Pending",
            indexFormat = triangleIndexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
            subMeshCount = source.subMeshCount
        };
        mesh.SetVertices(bakedVertices);
        mesh.SetColors(bakedColors);

        for (int submesh = 0; submesh < bakedSubmeshTriangles.Count; submesh++)
            mesh.SetTriangles(bakedSubmeshTriangles[submesh], submesh, false);

        CopyVertexAttributes(source, mesh, newToOld);
        CopyBlendShapes(source, mesh, newToOld);
        mesh.bindposes = source.bindposes;
        mesh.bounds = source.bounds;
        return mesh;
    }

    private static bool IsCurrentBakedMesh(Mesh mesh)
    {
        return mesh != null
            && mesh.name.StartsWith(BakedVersionMarker, StringComparison.Ordinal);
    }

    private static void RegisterEdge(int a, int b, Vector3[] vertices, float tolerance,
        Vector3 faceNormal, float threshold, Dictionary<GeometricEdgeKey, EdgeRecord> edges)
    {
        var key = new GeometricEdgeKey(vertices[a], vertices[b], tolerance);
        if (!edges.TryGetValue(key, out EdgeRecord record))
            edges.Add(key, record = new EdgeRecord(faceNormal));
        record.AddFace(faceNormal, threshold);
    }

    private static bool IsSelectedEdge(int a, int b, Vector3[] vertices, float tolerance,
        Dictionary<GeometricEdgeKey, EdgeRecord> edges)
    {
        return edges[new GeometricEdgeKey(vertices[a], vertices[b], tolerance)].IsSelected;
    }

    private static void AddBakedCorner(int sourceIndex, Color edgeData, Vector3[] vertices,
        List<int> newToOld, List<Vector3> bakedVertices, List<Color> bakedColors,
        int[] bakedTriangles, int triangleIndex)
    {
        int bakedIndex = bakedVertices.Count;
        newToOld.Add(sourceIndex);
        bakedVertices.Add(vertices[sourceIndex]);
        bakedColors.Add(edgeData);
        bakedTriangles[triangleIndex] = bakedIndex;
    }

    private static void CopyVertexAttributes(Mesh source, Mesh destination, List<int> newToOld)
    {
        Vector3[] sourceNormals = source.normals;
        if (sourceNormals.Length == source.vertexCount)
        {
            var values = new List<Vector3>(newToOld.Count);
            foreach (int index in newToOld) values.Add(sourceNormals[index]);
            destination.SetNormals(values);
        }
        else destination.RecalculateNormals();

        Vector4[] sourceTangents = source.tangents;
        if (sourceTangents.Length == source.vertexCount)
        {
            var values = new List<Vector4>(newToOld.Count);
            foreach (int index in newToOld) values.Add(sourceTangents[index]);
            destination.SetTangents(values);
        }

        for (int channel = 0; channel < 8; channel++)
        {
            var sourceUvs = new List<Vector4>();
            source.GetUVs(channel, sourceUvs);
            if (sourceUvs.Count != source.vertexCount)
                continue;
            var bakedUvs = new List<Vector4>(newToOld.Count);
            foreach (int index in newToOld) bakedUvs.Add(sourceUvs[index]);
            destination.SetUVs(channel, bakedUvs);
        }

        BoneWeight[] sourceWeights = source.boneWeights;
        if (sourceWeights.Length == source.vertexCount)
        {
            var bakedWeights = new BoneWeight[newToOld.Count];
            for (int i = 0; i < bakedWeights.Length; i++) bakedWeights[i] = sourceWeights[newToOld[i]];
            destination.boneWeights = bakedWeights;
        }
    }

    private static void CopyBlendShapes(Mesh source, Mesh destination, List<int> newToOld)
    {
        if (source.blendShapeCount == 0)
            return;

        var deltaVertices = new Vector3[source.vertexCount];
        var deltaNormals = new Vector3[source.vertexCount];
        var deltaTangents = new Vector3[source.vertexCount];
        for (int shape = 0; shape < source.blendShapeCount; shape++)
        for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
        {
            source.GetBlendShapeFrameVertices(shape, frame, deltaVertices, deltaNormals, deltaTangents);
            var bakedVertices = new Vector3[newToOld.Count];
            var bakedNormals = new Vector3[newToOld.Count];
            var bakedTangents = new Vector3[newToOld.Count];
            for (int i = 0; i < newToOld.Count; i++)
            {
                int sourceIndex = newToOld[i];
                bakedVertices[i] = deltaVertices[sourceIndex];
                bakedNormals[i] = deltaNormals[sourceIndex];
                bakedTangents[i] = deltaTangents[sourceIndex];
            }
            destination.AddBlendShapeFrame(
                source.GetBlendShapeName(shape),
                source.GetBlendShapeFrameWeight(shape, frame),
                bakedVertices, bakedNormals, bakedTangents);
        }
    }

    private static void SaveAndAssign(Mesh mesh, Action<Mesh> assign)
    {
        Mesh savedMesh = SaveBakedMesh(mesh);
        assign(savedMesh);
        Selection.activeObject = savedMesh;
    }

    private static Mesh SaveBakedMesh(Mesh mesh)
    {
        // Object and imported mesh names can be very long. Concatenating them on
        // every rebake eventually exceeds Windows/Git path limits, so file names
        // use a compact random identifier instead. Unity references the asset by
        // its .meta GUID, not by this human-readable file name.
        string shortId = Guid.NewGuid().ToString("N").Substring(0, 12);
        mesh.name = BakedVersionMarker + shortId;
        string path = $"{OutputFolder}/{mesh.name}.asset";
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials/IceShader"))
            AssetDatabase.CreateFolder("Assets/Materials", "IceShader");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Materials/IceShader", "BakedMeshes");
    }

    private sealed class EdgeRecord
    {
        private int m_FaceCount;
        private Vector3 m_FirstFaceNormal;
        private bool m_HasHardAngle;

        public EdgeRecord(Vector3 firstFaceNormal)
        {
            m_FirstFaceNormal = firstFaceNormal;
        }

        // Only an open boundary or a genuine hard angle is an edge. Imported
        // meshes frequently duplicate vertices along UV seams; treating those
        // duplicate topology records as edges exposed triangulation diagonals.
        public bool IsSelected => m_FaceCount == 1 || m_HasHardAngle;

        public void AddFace(Vector3 faceNormal, float threshold)
        {
            if (m_FaceCount == 0)
                m_FirstFaceNormal = faceNormal;
            else if (Vector3.Dot(m_FirstFaceNormal, faceNormal) < threshold)
                m_HasHardAngle = true;
            m_FaceCount++;
        }
    }

    private readonly struct GeometricEdgeKey : IEquatable<GeometricEdgeKey>
    {
        private readonly PositionKey m_A;
        private readonly PositionKey m_B;

        public GeometricEdgeKey(Vector3 a, Vector3 b, float tolerance)
        {
            var keyA = new PositionKey(a, tolerance);
            var keyB = new PositionKey(b, tolerance);
            if (keyA.CompareTo(keyB) <= 0) { m_A = keyA; m_B = keyB; }
            else { m_A = keyB; m_B = keyA; }
        }

        public bool Equals(GeometricEdgeKey other) => m_A.Equals(other.m_A) && m_B.Equals(other.m_B);
        public override bool Equals(object obj) => obj is GeometricEdgeKey other && Equals(other);
        public override int GetHashCode() => (m_A.GetHashCode() * 397) ^ m_B.GetHashCode();
    }

    private readonly struct PositionKey : IEquatable<PositionKey>, IComparable<PositionKey>
    {
        private readonly int m_X, m_Y, m_Z;
        public PositionKey(Vector3 position, float tolerance)
        {
            m_X = Mathf.RoundToInt(position.x / tolerance);
            m_Y = Mathf.RoundToInt(position.y / tolerance);
            m_Z = Mathf.RoundToInt(position.z / tolerance);
        }
        public int CompareTo(PositionKey other)
        {
            int x = m_X.CompareTo(other.m_X);
            if (x != 0) return x;
            int y = m_Y.CompareTo(other.m_Y);
            return y != 0 ? y : m_Z.CompareTo(other.m_Z);
        }
        public bool Equals(PositionKey other) => m_X == other.m_X && m_Y == other.m_Y && m_Z == other.m_Z;
        public override bool Equals(object obj) => obj is PositionKey other && Equals(other);
        public override int GetHashCode() => ((m_X * 397) ^ m_Y) * 397 ^ m_Z;
    }
}
