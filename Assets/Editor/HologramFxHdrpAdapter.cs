#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class HologramFxHdrpAdapter : EditorWindow
{
    private DefaultAsset sourceFolder;

    private string outputRoot = "Assets/HologramFX_HDRP";

    private Vector2 scroll;

    private readonly List<ReportEntry> report = new();

    private enum Severity
    {
        Info,
        Success,
        Warning,
        Error
    }

    [Serializable]
    private class ReportEntry
    {
        public Severity severity;
        public string asset;
        public string message;

        public ReportEntry(
            Severity severity,
            string asset,
            string message)
        {
            this.severity = severity;
            this.asset = asset;
            this.message = message;
        }
    }

    [Serializable]
    private class ShaderGraphInfo
    {
        public string sourcePath;
        public string copyPath;
        public string shaderName;

        public List<string> materialPaths = new();
        public List<string> exposedProperties = new();

        public bool appearsBuiltIn;
        public bool appearsHDRP;
    }

    private readonly List<ShaderGraphInfo> graphInfos = new();

    [MenuItem("Tools/Hologram FX/HDRP Adapter V2")]
    public static void Open()
    {
        GetWindow<HologramFxHdrpAdapter>(
            "Hologram FX → HDRP");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField(
            "Hologram FX → HDRP Adapter V2",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Cible : Unity 6000.4 / HDRP 17.4.\n\n" +
            "Le pack original n'est jamais modifié.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Core Built-In",
            sourceFolder,
            typeof(DefaultAsset),
            false);

        outputRoot = EditorGUILayout.TextField(
            "Sortie HDRP",
            outputRoot);

        EditorGUILayout.Space(8);

        DrawPipelineStatus();

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(sourceFolder == null))
        {
            if (GUILayout.Button(
                    "1. Analyser Core Built-In",
                    GUILayout.Height(30)))
            {
                Analyze();
            }

            if (GUILayout.Button(
                    "2. Créer les copies HDRP",
                    GUILayout.Height(34)))
            {
                PrepareCopies();
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button(
                    "3. Vérifier les Shader Graphs convertis",
                    GUILayout.Height(30)))
            {
                VerifyConvertedGraphs();
            }

            if (GUILayout.Button(
                    "4. Reconnecter les matériaux",
                    GUILayout.Height(34)))
            {
                ReconnectMaterials();
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button(
                    "Ouvrir le dossier HDRP",
                    GUILayout.Height(25)))
            {
                SelectOutputFolder();
            }
        }

        EditorGUILayout.Space(12);

        DrawReport();
    }

    // ============================================================
    // PIPELINE
    // ============================================================

    private void DrawPipelineStatus()
    {
        if (IsHDRPActive())
        {
            EditorGUILayout.HelpBox(
                "HDRP détecté comme pipeline actif.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "HDRP n'est pas détecté comme pipeline actif.",
                MessageType.Error);
        }
    }

    private static bool IsHDRPActive()
    {
        RenderPipelineAsset pipeline =
            GraphicsSettings.currentRenderPipeline;

        if (pipeline == null)
            return false;

        string typeName =
            pipeline.GetType().FullName ?? "";

        return typeName.Contains(
            "HDRenderPipelineAsset",
            StringComparison.OrdinalIgnoreCase);
    }

    // ============================================================
    // ANALYSE
    // ============================================================

    private void Analyze()
    {
        ClearState();

        string root = GetSourcePath();

        if (!ValidateSource(root))
            return;

        Add(
            Severity.Info,
            root,
            "Analyse de Core Built-In.");

        FindShaderGraphs(root);
        FindMaterialsUsingGraphs(root);
        AnalyzeGraphFiles();

        Add(
            Severity.Success,
            root,
            $"{graphInfos.Count} Shader Graph(s) détecté(s).");

        Repaint();
    }

    private void FindShaderGraphs(string root)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:Shader",
                new[] { root });

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            if (!path.EndsWith(
                    ".shadergraph",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Shader shader =
                AssetDatabase.LoadAssetAtPath<Shader>(path);

            ShaderGraphInfo info =
                new ShaderGraphInfo
                {
                    sourcePath = path,
                    shaderName =
                        shader != null
                            ? shader.name
                            : Path.GetFileNameWithoutExtension(path)
                };

            graphInfos.Add(info);

            Add(
                Severity.Info,
                path,
                $"Shader Graph trouvé : {info.shaderName}");
        }
    }

    private void FindMaterialsUsingGraphs(string root)
    {
        string[] materialGuids =
            AssetDatabase.FindAssets(
                "t:Material",
                new[] { root });

        foreach (string guid in materialGuids)
        {
            string materialPath =
                AssetDatabase.GUIDToAssetPath(guid);

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    materialPath);

            if (material == null ||
                material.shader == null)
            {
                continue;
            }

            string shaderPath =
                AssetDatabase.GetAssetPath(
                    material.shader);

            ShaderGraphInfo graph =
                graphInfos.FirstOrDefault(
                    g => g.sourcePath == shaderPath);

            if (graph == null)
                continue;

            graph.materialPaths.Add(
                materialPath);
        }

        foreach (ShaderGraphInfo graph in graphInfos)
        {
            Add(
                Severity.Info,
                graph.sourcePath,
                $"{graph.materialPaths.Count} matériau(x) utilisent ce graph.");
        }
    }

    private void AnalyzeGraphFiles()
    {
        foreach (ShaderGraphInfo info in graphInfos)
        {
            string absolute =
                ToAbsolutePath(info.sourcePath);

            if (!File.Exists(absolute))
            {
                Add(
                    Severity.Error,
                    info.sourcePath,
                    "Fichier Shader Graph introuvable.");

                continue;
            }

            string text =
                File.ReadAllText(absolute);

            info.appearsBuiltIn =
                ContainsAny(
                    text,
                    "BuiltInTarget",
                    "BuiltInSubTarget",
                    "BuiltIn.ShaderGraph");

            info.appearsHDRP =
                ContainsAny(
                    text,
                    "HDTarget",
                    "HDLitSubTarget",
                    "HDUnlitSubTarget",
                    "HighDefinition.ShaderGraph");

            info.exposedProperties =
                ExtractPropertyReferenceNames(text);

            string pipelineStatus;

            if (info.appearsBuiltIn &&
                info.appearsHDRP)
            {
                pipelineStatus =
                    "Built-In + HDRP détectés.";
            }
            else if (info.appearsBuiltIn)
            {
                pipelineStatus =
                    "Built-In uniquement.";
            }
            else if (info.appearsHDRP)
            {
                pipelineStatus =
                    "HDRP détecté.";
            }
            else
            {
                pipelineStatus =
                    "Pipeline non déterminé.";
            }

            Add(
                info.appearsHDRP
                    ? Severity.Success
                    : Severity.Warning,
                info.sourcePath,
                pipelineStatus);

            if (info.exposedProperties.Count > 0)
            {
                Add(
                    Severity.Info,
                    info.sourcePath,
                    "Propriétés trouvées : " +
                    string.Join(
                        ", ",
                        info.exposedProperties.Take(20)));
            }
        }
    }

    // ============================================================
    // COPIES HDRP
    // ============================================================

    private void PrepareCopies()
    {
        ClearState();

        string root = GetSourcePath();

        if (!ValidateSource(root))
            return;

        if (!IsHDRPActive())
        {
            Add(
                Severity.Error,
                "",
                "HDRP n'est pas actif.");

            return;
        }

        AnalyzeSilently(root);

        EnsureFolder(outputRoot);

        string graphRoot =
            outputRoot + "/ShaderGraphs";

        string materialRoot =
            outputRoot + "/Materials";

        EnsureFolder(graphRoot);
        EnsureFolder(materialRoot);

        CopyGraphs(root, graphRoot);
        CopyMaterials(root, materialRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WriteInstructionsFile();

        Add(
            Severity.Success,
            outputRoot,
            "Copies HDRP préparées.");

        Add(
            Severity.Info,
            outputRoot,
            "Étape suivante : convertir les copies de Shader Graph " +
            "dans Graph Settings > Active Targets.");

        Repaint();
    }

    private void CopyGraphs(
        string sourceRoot,
        string destinationRoot)
    {
        foreach (ShaderGraphInfo info in graphInfos)
        {
            string relative =
                GetRelativeAssetPath(
                    sourceRoot,
                    info.sourcePath);

            string destination =
                destinationRoot + "/" + relative;

            EnsureParentFolder(destination);

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    destination) != null)
            {
                info.copyPath = destination;

                Add(
                    Severity.Info,
                    destination,
                    "Copie existante conservée.");

                continue;
            }

            bool success =
                AssetDatabase.CopyAsset(
                    info.sourcePath,
                    destination);

            if (!success)
            {
                Add(
                    Severity.Error,
                    info.sourcePath,
                    "Échec de copie.");

                continue;
            }

            info.copyPath = destination;

            Add(
                Severity.Success,
                destination,
                "Shader Graph HDRP de travail créé.");
        }
    }

    private void CopyMaterials(
        string sourceRoot,
        string destinationRoot)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:Material",
                new[] { sourceRoot });

        foreach (string guid in guids)
        {
            string sourcePath =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            string relative =
                GetRelativeAssetPath(
                    sourceRoot,
                    sourcePath);

            string destination =
                destinationRoot + "/" + relative;

            EnsureParentFolder(destination);

            if (AssetDatabase.LoadAssetAtPath<Material>(
                    destination) != null)
            {
                continue;
            }

            bool success =
                AssetDatabase.CopyAsset(
                    sourcePath,
                    destination);

            Add(
                success
                    ? Severity.Success
                    : Severity.Error,
                destination,
                success
                    ? "Material copié."
                    : "Échec de copie du material.");
        }
    }

    // ============================================================
    // VERIFICATION HDRP
    // ============================================================

    private void VerifyConvertedGraphs()
    {
        report.Clear();

        string graphRoot =
            outputRoot + "/ShaderGraphs";

        if (!AssetDatabase.IsValidFolder(graphRoot))
        {
            Add(
                Severity.Error,
                graphRoot,
                "Aucun dossier ShaderGraphs HDRP.");

            return;
        }

        string[] files =
            Directory.GetFiles(
                graphRoot,
                "*.shadergraph",
                SearchOption.AllDirectories);

        int hdrpCount = 0;

        foreach (string raw in files)
        {
            string path =
                NormalizeAssetPath(raw);

            string text =
                File.ReadAllText(
                    ToAbsolutePath(path));

            bool hdrp =
                ContainsAny(
                    text,
                    "HDTarget",
                    "HDLitSubTarget",
                    "HDUnlitSubTarget",
                    "HighDefinition.ShaderGraph");

            if (hdrp)
            {
                hdrpCount++;

                Add(
                    Severity.Success,
                    path,
                    "Target HDRP détecté.");
            }
            else
            {
                Add(
                    Severity.Warning,
                    path,
                    "HDRP non détecté. Ouvre ce graph et ajoute HDRP dans Active Targets.");
            }
        }

        Add(
            hdrpCount == files.Length
                ? Severity.Success
                : Severity.Warning,
            graphRoot,
            $"{hdrpCount}/{files.Length} graph(s) semblent cibler HDRP.");

        Repaint();
    }

    // ============================================================
    // MATERIAL RECONNECTION
    // ============================================================

    private void ReconnectMaterials()
    {
        report.Clear();

        string sourceRoot =
            GetSourcePath();

        if (!ValidateSource(sourceRoot))
            return;

        string graphRoot =
            outputRoot + "/ShaderGraphs";

        string materialRoot =
            outputRoot + "/Materials";

        if (!AssetDatabase.IsValidFolder(graphRoot) ||
            !AssetDatabase.IsValidFolder(materialRoot))
        {
            Add(
                Severity.Error,
                outputRoot,
                "Les copies HDRP n'ont pas été créées.");

            return;
        }

        AnalyzeSilently(sourceRoot);

        Dictionary<string, Shader> convertedShaders =
            BuildConvertedShaderMap(
                sourceRoot,
                graphRoot);

        string[] materialGuids =
            AssetDatabase.FindAssets(
                "t:Material",
                new[] { sourceRoot });

        int connected = 0;

        foreach (string guid in materialGuids)
        {
            string originalMaterialPath =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            Material original =
                AssetDatabase.LoadAssetAtPath<Material>(
                    originalMaterialPath);

            if (original == null ||
                original.shader == null)
            {
                continue;
            }

            string originalShaderPath =
                AssetDatabase.GetAssetPath(
                    original.shader);

            if (!convertedShaders.TryGetValue(
                    originalShaderPath,
                    out Shader newShader))
            {
                continue;
            }

            string relative =
                GetRelativeAssetPath(
                    sourceRoot,
                    originalMaterialPath);

            string copiedMaterialPath =
                materialRoot + "/" + relative;

            Material copied =
                AssetDatabase.LoadAssetAtPath<Material>(
                    copiedMaterialPath);

            if (copied == null)
                continue;

            MaterialSnapshot snapshot =
                CaptureAllProperties(copied);

            copied.shader = newShader;

            RestoreCompatibleProperties(
                copied,
                snapshot);

            EditorUtility.SetDirty(copied);

            connected++;

            Add(
                Severity.Success,
                copiedMaterialPath,
                $"Reconnecté à {newShader.name}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Add(
            Severity.Success,
            materialRoot,
            $"{connected} matériau(x) reconnecté(s).");

        Repaint();
    }

    private Dictionary<string, Shader>
        BuildConvertedShaderMap(
            string sourceRoot,
            string graphRoot)
    {
        Dictionary<string, Shader> result =
            new Dictionary<string, Shader>();

        foreach (ShaderGraphInfo info in graphInfos)
        {
            string relative =
                GetRelativeAssetPath(
                    sourceRoot,
                    info.sourcePath);

            string convertedPath =
                graphRoot + "/" + relative;

            Shader converted =
                AssetDatabase.LoadAssetAtPath<Shader>(
                    convertedPath);

            if (converted == null)
            {
                Add(
                    Severity.Warning,
                    convertedPath,
                    "Shader généré introuvable.");

                continue;
            }

            result[info.sourcePath] =
                converted;
        }

        return result;
    }

    // ============================================================
    // MATERIAL PROPERTY PRESERVATION
    // ============================================================

    private class MaterialSnapshot
    {
        public readonly Dictionary<string, float> floats = new();
        public readonly Dictionary<string, Color> colors = new();
        public readonly Dictionary<string, Vector4> vectors = new();
        public readonly Dictionary<string, Texture> textures = new();
        public readonly Dictionary<string, Vector2> textureScale = new();
        public readonly Dictionary<string, Vector2> textureOffset = new();
    }

    private MaterialSnapshot CaptureAllProperties(
        Material material)
    {
        MaterialSnapshot snapshot =
            new MaterialSnapshot();

        Shader shader = material.shader;

        if (shader == null)
            return snapshot;

        int count =
            ShaderUtil.GetPropertyCount(shader);

        for (int i = 0; i < count; i++)
        {
            string name =
                ShaderUtil.GetPropertyName(shader, i);

            ShaderUtil.ShaderPropertyType type =
                ShaderUtil.GetPropertyType(
                    shader,
                    i);

            try
            {
                switch (type)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        snapshot.colors[name] =
                            material.GetColor(name);
                        break;

                    case ShaderUtil.ShaderPropertyType.Vector:
                        snapshot.vectors[name] =
                            material.GetVector(name);
                        break;

                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        snapshot.floats[name] =
                            material.GetFloat(name);
                        break;

                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        snapshot.textures[name] =
                            material.GetTexture(name);

                        snapshot.textureScale[name] =
                            material.GetTextureScale(name);

                        snapshot.textureOffset[name] =
                            material.GetTextureOffset(name);
                        break;
                }
            }
            catch
            {
                // Ignore une propriété non lisible.
            }
        }

        return snapshot;
    }

    private void RestoreCompatibleProperties(
        Material material,
        MaterialSnapshot snapshot)
    {
        foreach (var pair in snapshot.floats)
        {
            if (material.HasProperty(pair.Key))
                material.SetFloat(
                    pair.Key,
                    pair.Value);
        }

        foreach (var pair in snapshot.colors)
        {
            if (material.HasProperty(pair.Key))
                material.SetColor(
                    pair.Key,
                    pair.Value);
        }

        foreach (var pair in snapshot.vectors)
        {
            if (material.HasProperty(pair.Key))
                material.SetVector(
                    pair.Key,
                    pair.Value);
        }

        foreach (var pair in snapshot.textures)
        {
            if (!material.HasProperty(pair.Key))
                continue;

            material.SetTexture(
                pair.Key,
                pair.Value);

            if (snapshot.textureScale.TryGetValue(
                    pair.Key,
                    out Vector2 scale))
            {
                material.SetTextureScale(
                    pair.Key,
                    scale);
            }

            if (snapshot.textureOffset.TryGetValue(
                    pair.Key,
                    out Vector2 offset))
            {
                material.SetTextureOffset(
                    pair.Key,
                    offset);
            }
        }
    }

    // ============================================================
    // PROPERTY EXTRACTION
    // ============================================================

    private List<string> ExtractPropertyReferenceNames(
        string text)
    {
        HashSet<string> names =
            new HashSet<string>();

        const string token =
            "\"m_ReferenceName\":";

        int index = 0;

        while (true)
        {
            index =
                text.IndexOf(
                    token,
                    index,
                    StringComparison.Ordinal);

            if (index < 0)
                break;

            int quoteStart =
                text.IndexOf(
                    '"',
                    index + token.Length);

            if (quoteStart < 0)
                break;

            int quoteEnd =
                text.IndexOf(
                    '"',
                    quoteStart + 1);

            if (quoteEnd < 0)
                break;

            string value =
                text.Substring(
                    quoteStart + 1,
                    quoteEnd - quoteStart - 1);

            if (!string.IsNullOrWhiteSpace(value))
                names.Add(value);

            index = quoteEnd + 1;
        }

        return names
            .OrderBy(x => x)
            .ToList();
    }

    // ============================================================
    // INSTRUCTIONS
    // ============================================================

    private void WriteInstructionsFile()
    {
        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine(
            "HOLOGRAM FX PACK → HDRP 17.4");
        sb.AppendLine(
            "================================");
        sb.AppendLine();

        sb.AppendLine(
            "Unity : 6000.4.x");
        sb.AppendLine(
            "HDRP  : 17.4.x");
        sb.AppendLine();

        sb.AppendLine(
            "CONVERSION DES SHADER GRAPHS");
        sb.AppendLine(
            "-----------------------------");

        sb.AppendLine();

        sb.AppendLine(
            "Pour chaque fichier sous ShaderGraphs :");
        sb.AppendLine();

        sb.AppendLine(
            "1. Ouvrir le Shader Graph.");
        sb.AppendLine(
            "2. Ouvrir Graph Inspector.");
        sb.AppendLine(
            "3. Graph Settings.");
        sb.AppendLine(
            "4. Active Targets > +.");
        sb.AppendLine(
            "5. Ajouter HDRP.");
        sb.AppendLine(
            "6. Choisir Unlit pour commencer.");
        sb.AppendLine(
            "7. Surface Type = Transparent.");
        sb.AppendLine(
            "8. Conserver le Built-In Target provisoirement.");
        sb.AppendLine(
            "9. Vérifier les blocks devenus gris/incompatibles.");
        sb.AppendLine(
            "10. Save Asset.");
        sb.AppendLine();

        sb.AppendLine(
            "IMPORTANT : pour l'effet souvenir, HDRP Unlit est " +
            "préférable au Lit pour la première conversion.");
        sb.AppendLine();

        foreach (ShaderGraphInfo graph in graphInfos)
        {
            sb.AppendLine(
                "- " + Path.GetFileNameWithoutExtension(
                    graph.sourcePath));

            if (graph.exposedProperties.Count > 0)
            {
                sb.AppendLine(
                    "  Properties: " +
                    string.Join(
                        ", ",
                        graph.exposedProperties));
            }
        }

        string path =
            outputRoot +
            "/HDRP_CONVERSION_INSTRUCTIONS.txt";

        File.WriteAllText(
            ToAbsolutePath(path),
            sb.ToString());

        AssetDatabase.ImportAsset(path);
    }

    // ============================================================
    // INTERNAL ANALYSE
    // ============================================================

    private void AnalyzeSilently(string root)
    {
        graphInfos.Clear();

        FindShaderGraphsSilent(root);
        FindMaterialsUsingGraphsSilent(root);
        AnalyzeGraphFilesSilent();
    }

    private void FindShaderGraphsSilent(
        string root)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:Shader",
                new[] { root });

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            if (!path.EndsWith(
                    ".shadergraph",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Shader shader =
                AssetDatabase.LoadAssetAtPath<Shader>(
                    path);

            graphInfos.Add(
                new ShaderGraphInfo
                {
                    sourcePath = path,
                    shaderName =
                        shader != null
                            ? shader.name
                            : Path.GetFileNameWithoutExtension(path)
                });
        }
    }

    private void FindMaterialsUsingGraphsSilent(
        string root)
    {
        string[] materialGuids =
            AssetDatabase.FindAssets(
                "t:Material",
                new[] { root });

        foreach (string guid in materialGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    path);

            if (material == null ||
                material.shader == null)
            {
                continue;
            }

            string shaderPath =
                AssetDatabase.GetAssetPath(
                    material.shader);

            ShaderGraphInfo graph =
                graphInfos.FirstOrDefault(
                    g => g.sourcePath == shaderPath);

            graph?.materialPaths.Add(path);
        }
    }

    private void AnalyzeGraphFilesSilent()
    {
        foreach (ShaderGraphInfo info in graphInfos)
        {
            string absolute =
                ToAbsolutePath(info.sourcePath);

            if (!File.Exists(absolute))
                continue;

            string text =
                File.ReadAllText(absolute);

            info.appearsBuiltIn =
                ContainsAny(
                    text,
                    "BuiltInTarget",
                    "BuiltInSubTarget",
                    "BuiltIn.ShaderGraph");

            info.appearsHDRP =
                ContainsAny(
                    text,
                    "HDTarget",
                    "HDLitSubTarget",
                    "HDUnlitSubTarget",
                    "HighDefinition.ShaderGraph");

            info.exposedProperties =
                ExtractPropertyReferenceNames(text);
        }
    }

    // ============================================================
    // UI REPORT
    // ============================================================

    private void DrawReport()
    {
        if (report.Count == 0)
            return;

        EditorGUILayout.LabelField(
            $"Rapport ({report.Count})",
            EditorStyles.boldLabel);

        scroll =
            EditorGUILayout.BeginScrollView(
                scroll);

        foreach (ReportEntry entry in report)
        {
            MessageType type =
                entry.severity switch
                {
                    Severity.Warning =>
                        MessageType.Warning,

                    Severity.Error =>
                        MessageType.Error,

                    _ =>
                        MessageType.Info
                };

            string text =
                string.IsNullOrEmpty(entry.asset)
                    ? entry.message
                    : entry.asset +
                      "\n" +
                      entry.message;

            EditorGUILayout.HelpBox(
                text,
                type);
        }

        EditorGUILayout.EndScrollView();
    }

    private void Add(
        Severity severity,
        string asset,
        string message)
    {
        report.Add(
            new ReportEntry(
                severity,
                asset,
                message));
    }

    private void ClearState()
    {
        report.Clear();
        graphInfos.Clear();
    }

    // ============================================================
    // PATHS
    // ============================================================

    private string GetSourcePath()
    {
        if (sourceFolder == null)
            return null;

        return AssetDatabase.GetAssetPath(
            sourceFolder);
    }

    private bool ValidateSource(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Add(
                Severity.Error,
                "",
                "Sélectionne Core Built-In.");

            return false;
        }

        if (!AssetDatabase.IsValidFolder(path))
        {
            Add(
                Severity.Error,
                path,
                "Le chemin n'est pas un dossier Unity valide.");

            return false;
        }

        return true;
    }

    private void SelectOutputFolder()
    {
        DefaultAsset folder =
            AssetDatabase.LoadAssetAtPath<DefaultAsset>(
                outputRoot);

        if (folder != null)
        {
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }
    }

    private static string NormalizeAssetPath(
        string path)
    {
        return path.Replace("\\", "/");
    }

    private static string GetRelativeAssetPath(
        string root,
        string fullPath)
    {
        root =
            NormalizeAssetPath(root)
                .TrimEnd('/');

        fullPath =
            NormalizeAssetPath(fullPath);

        if (fullPath.StartsWith(root + "/"))
        {
            return fullPath.Substring(
                root.Length + 1);
        }

        return Path.GetFileName(fullPath);
    }

    private static string ToAbsolutePath(
        string assetPath)
    {
        string root =
            Directory.GetParent(
                Application.dataPath)
            ?.FullName ?? "";

        return Path.Combine(
            root,
            assetPath);
    }

    private static void EnsureParentFolder(
        string assetPath)
    {
        string directory =
            Path.GetDirectoryName(
                    assetPath)
                ?.Replace("\\", "/");

        if (!string.IsNullOrEmpty(directory))
            EnsureFolder(directory);
    }

    private static void EnsureFolder(
        string folder)
    {
        folder =
            NormalizeAssetPath(folder)
                .TrimEnd('/');

        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts =
            folder.Split('/');

        string current =
            parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next =
                current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]);
            }

            current = next;
        }
    }

    private static bool ContainsAny(
        string input,
        params string[] values)
    {
        return values.Any(
            value =>
                input.IndexOf(
                    value,
                    StringComparison.OrdinalIgnoreCase)
                >= 0);
    }
}

#endif