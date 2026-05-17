#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using UnityGLTF;

namespace Lit.EditorTools
{
    public sealed class GlbToFbxConverterWindow : EditorWindow
    {
        private const string WindowTitle = "GLB to FBX";
        private const string DefaultWorkFolder = "Assets/Import/GlbToFbx";

        private UnityEngine.Object sourceGlb;
        private DefaultAsset sourceFolder;
        private DefaultAsset outputFolder;
        private DefaultAsset workFolder;

        private bool outputNextToSource = true;
        private bool overwriteExisting;
        private bool includeSubfolders = true;
        private bool keepImportedPrefab = true;

        private Vector2 scrollPosition;
        private readonly Queue<ConversionRequest> pendingRequests = new Queue<ConversionRequest>();
        private readonly List<string> messages = new List<string>();

        private GLTFEditorImporter activeImporter;
        private ConversionRequest activeRequest;
        private string activeImportedPrefabPath;
        private bool isConverting;

        [MenuItem("Tools/Lit/Conversion/GLB to FBX Converter")]
        public static void OpenWindow()
        {
            var window = GetWindow<GlbToFbxConverterWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(480f, 360f);
            window.Show();
        }

        [MenuItem("Assets/Lit/Convertir GLB en FBX", true)]
        private static bool CanConvertSelectedGlb()
        {
            foreach (var selected in Selection.objects)
            {
                if (IsSupportedSourcePath(AssetDatabase.GetAssetPath(selected)))
                {
                    return true;
                }
            }

            return false;
        }

        [MenuItem("Assets/Lit/Convertir GLB en FBX")]
        private static void ConvertSelectedGlb()
        {
            var selectedPaths = new List<string>();
            foreach (var selected in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(selected);
                if (IsSupportedSourcePath(path))
                {
                    selectedPaths.Add(path);
                }
            }

            if (selectedPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(WindowTitle, "Aucun fichier .glb ou .gltf selectionne.", "OK");
                return;
            }

            var window = GetWindow<GlbToFbxConverterWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.SetDefaults();
            window.outputNextToSource = true;
            window.EnqueueAndStart(selectedPaths);
            window.Show();
        }

        private void OnEnable()
        {
            SetDefaults();
        }

        private void OnDisable()
        {
            ClearProgress();
        }

        private void Update()
        {
            if (activeImporter != null)
            {
                activeImporter.Update();
            }
        }

        private void OnGUI()
        {
            SetDefaults();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            sourceGlb = DrawAssetField("GLB/GLTF", sourceGlb);

            using (new EditorGUI.DisabledScope(sourceGlb == null || isConverting))
            {
                if (GUILayout.Button("Convertir le fichier"))
                {
                    EnqueueAndStart(new[] { AssetDatabase.GetAssetPath(sourceGlb) });
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Batch", EditorStyles.boldLabel);
            sourceFolder = DrawAssetField("Dossier source", sourceFolder);
            includeSubfolders = EditorGUILayout.Toggle("Inclure sous-dossiers", includeSubfolders);

            using (new EditorGUI.DisabledScope(sourceFolder == null || isConverting))
            {
                if (GUILayout.Button("Convertir les GLB du dossier"))
                {
                    EnqueueAndStart(FindSupportedFilesInFolder(AssetDatabase.GetAssetPath(sourceFolder)));
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Sortie", EditorStyles.boldLabel);
            outputNextToSource = EditorGUILayout.Toggle("FBX a cote du GLB", outputNextToSource);

            using (new EditorGUI.DisabledScope(outputNextToSource))
            {
                outputFolder = DrawAssetField("Dossier FBX", outputFolder);
            }

            workFolder = DrawAssetField("Dossier import temporaire", workFolder);
            overwriteExisting = EditorGUILayout.Toggle("Remplacer FBX existant", overwriteExisting);
            keepImportedPrefab = EditorGUILayout.Toggle("Garder prefab importe", keepImportedPrefab);

            EditorGUILayout.Space(8f);
            DrawCurrentState();
            DrawMessages();

            EditorGUILayout.EndScrollView();
        }

        private void SetDefaults()
        {
            if (workFolder == null)
            {
                workFolder = EnsureFolderAsset(DefaultWorkFolder);
            }

            if (outputFolder == null)
            {
                outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
            }
        }

        private static UnityEngine.Object DrawAssetField(string label, UnityEngine.Object asset)
        {
            return EditorGUILayout.ObjectField(label, asset, typeof(UnityEngine.Object), false);
        }

        private static DefaultAsset DrawAssetField(string label, DefaultAsset asset)
        {
            return (DefaultAsset)EditorGUILayout.ObjectField(label, asset, typeof(DefaultAsset), false);
        }

        private void DrawCurrentState()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Conversion active", isConverting);
                EditorGUILayout.IntField("En attente", pendingRequests.Count);
            }

            if (activeRequest != null)
            {
                EditorGUILayout.HelpBox("Conversion en cours : " + activeRequest.SourceAssetPath, MessageType.Info);
            }
        }

        private void DrawMessages()
        {
            if (messages.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Journal", EditorStyles.boldLabel);

            for (var i = messages.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.HelpBox(messages[i], MessageType.None);
            }
        }

        private void EnqueueAndStart(IEnumerable<string> sourcePaths)
        {
            if (isConverting)
            {
                EditorUtility.DisplayDialog(WindowTitle, "Une conversion est deja en cours.", "OK");
                return;
            }

            pendingRequests.Clear();
            messages.Clear();

            foreach (var sourcePath in sourcePaths)
            {
                if (!IsSupportedSourcePath(sourcePath))
                {
                    AddMessage("Ignore : " + sourcePath);
                    continue;
                }

                if (!File.Exists(ProjectRelativeToAbsolutePath(sourcePath)))
                {
                    AddMessage("Fichier introuvable : " + sourcePath);
                    continue;
                }

                var outputPath = GetOutputAssetPath(sourcePath);
                if (string.IsNullOrEmpty(outputPath))
                {
                    AddMessage("Sortie invalide : " + sourcePath);
                    continue;
                }

                if (!overwriteExisting && File.Exists(ProjectRelativeToAbsolutePath(outputPath)))
                {
                    AddMessage("Deja existant, ignore : " + outputPath);
                    continue;
                }

                pendingRequests.Enqueue(new ConversionRequest(
                    sourcePath,
                    outputPath,
                    GetImportFolderAssetPath(sourcePath),
                    GetImportedPrefabPath(sourcePath)));
            }

            if (pendingRequests.Count == 0)
            {
                EditorUtility.DisplayDialog(WindowTitle, "Aucun fichier a convertir.", "OK");
                return;
            }

            isConverting = true;
            StartNextConversion();
        }

        private void StartNextConversion()
        {
            if (pendingRequests.Count == 0)
            {
                FinishAllConversions();
                return;
            }

            activeRequest = pendingRequests.Dequeue();
            activeImportedPrefabPath = activeRequest.ImportedPrefabAssetPath;

            var importFolderAssetPath = activeRequest.ImportFolderAssetPath;
            EnsureFolderAsset(importFolderAssetPath);

            var importer = new GLTFEditorImporter(OnImportProgress, OnImportFinished);
            activeImporter = importer;

            var sourceAbsolutePath = ProjectRelativeToAbsolutePath(activeRequest.SourceAssetPath);
            var importAbsolutePath = ProjectRelativeToAbsolutePath(importFolderAssetPath);
            var prefabName = Path.GetFileNameWithoutExtension(activeRequest.SourceAssetPath);

            AddMessage("Import GLB : " + activeRequest.SourceAssetPath);
            try
            {
                importer.setupForPath(sourceAbsolutePath, importAbsolutePath, prefabName, false);
                importer.Load();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                AddMessage("Erreur import : " + activeRequest.SourceAssetPath + " -> " + exception.Message);
                activeImporter = null;
                activeRequest = null;
                activeImportedPrefabPath = null;
                StartNextConversion();
            }
        }

        private void OnImportProgress(GLTFEditorImporter.IMPORT_STEP step, int current, int total)
        {
            var progress = total > 0 ? (float)current / total : 0f;
            EditorUtility.DisplayProgressBar(WindowTitle, "Import " + step + " (" + current + " / " + total + ")", progress);
            Repaint();
        }

        private void OnImportFinished()
        {
            ClearProgress();
            AssetDatabase.Refresh();

            try
            {
                ExportImportedPrefab();
                CleanupImportedPrefab();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                AddMessage("Erreur : " + activeRequest.SourceAssetPath + " -> " + exception.Message);
            }
            finally
            {
                activeImporter = null;
                activeRequest = null;
                activeImportedPrefabPath = null;
                StartNextConversion();
            }
        }

        private void ExportImportedPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(activeImportedPrefabPath);
            if (prefab == null)
            {
                throw new FileNotFoundException("Prefab importe introuvable", activeImportedPrefabPath);
            }

            EnsureFolderForAssetPath(activeRequest.OutputAssetPath);
            var outputAbsolutePath = ProjectRelativeToAbsolutePath(activeRequest.OutputAssetPath);
            var loadedPrefabRoot = PrefabUtility.LoadPrefabContents(activeImportedPrefabPath);

            try
            {
                var exportedPath = ModelExporter.ExportObject(outputAbsolutePath, loadedPrefabRoot);
                if (string.IsNullOrEmpty(exportedPath))
                {
                    throw new InvalidOperationException("Le FBX Exporter n'a pas retourne de fichier.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loadedPrefabRoot);
            }

            AssetDatabase.ImportAsset(activeRequest.OutputAssetPath, ImportAssetOptions.ForceUpdate);
            AddMessage("FBX cree : " + activeRequest.OutputAssetPath);
        }

        private void CleanupImportedPrefab()
        {
            if (keepImportedPrefab || string.IsNullOrEmpty(activeImportedPrefabPath))
            {
                return;
            }

            AssetDatabase.DeleteAsset(activeImportedPrefabPath);
        }

        private void FinishAllConversions()
        {
            isConverting = false;
            ClearProgress();
            AssetDatabase.Refresh();
            AddMessage("Conversion terminee.");
            Repaint();
        }

        private void ClearProgress()
        {
            EditorUtility.ClearProgressBar();
        }

        private void AddMessage(string message)
        {
            messages.Add(message);
            Debug.Log("[GLB to FBX] " + message);
            Repaint();
        }

        private IEnumerable<string> FindSupportedFilesInFolder(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return new string[0];
            }

            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var absoluteFolderPath = ProjectRelativeToAbsolutePath(folderPath);
            var results = new List<string>();

            foreach (var filePath in Directory.EnumerateFiles(absoluteFolderPath, "*.*", searchOption))
            {
                var assetPath = AbsolutePathToProjectRelative(filePath);
                if (IsSupportedSourcePath(assetPath))
                {
                    results.Add(assetPath);
                }
            }

            return results;
        }

        private string GetOutputAssetPath(string sourcePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(sourcePath) + ".fbx";
            var outputFolderPath = outputNextToSource ? Path.GetDirectoryName(sourcePath) : AssetDatabase.GetAssetPath(outputFolder);

            if (string.IsNullOrEmpty(outputFolderPath)
                || !outputFolderPath.StartsWith("Assets", StringComparison.Ordinal)
                || !AssetDatabase.IsValidFolder(outputFolderPath))
            {
                return null;
            }

            return NormalizeAssetPath(Path.Combine(outputFolderPath, fileName));
        }

        private string GetImportedPrefabPath(string sourcePath)
        {
            var prefabName = Path.GetFileNameWithoutExtension(sourcePath) + ".prefab";
            return NormalizeAssetPath(Path.Combine(GetImportFolderAssetPath(sourcePath), prefabName));
        }

        private string GetImportFolderAssetPath(string sourcePath)
        {
            var sourceFolderPath = NormalizeAssetPath(Path.GetDirectoryName(sourcePath));
            var relativeSourceFolderPath = sourceFolderPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? sourceFolderPath.Substring("Assets/".Length)
                : "External";

            return NormalizeAssetPath(Path.Combine(GetWorkFolderAssetPath(), relativeSourceFolderPath));
        }

        private string GetWorkFolderAssetPath()
        {
            var folderPath = AssetDatabase.GetAssetPath(workFolder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                folderPath = DefaultWorkFolder;
            }

            return NormalizeAssetPath(folderPath);
        }

        private static bool IsSupportedSourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase);
        }

        private static DefaultAsset EnsureFolderAsset(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (!AssetDatabase.IsValidFolder(assetPath))
            {
                EnsureFolderForAssetPath(NormalizeAssetPath(Path.Combine(assetPath, "placeholder.asset")));
            }

            return AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetPath);
        }

        private static void EnsureFolderForAssetPath(string assetPath)
        {
            var folderPath = NormalizeAssetPath(Path.GetDirectoryName(assetPath));
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parts = folderPath.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string ProjectRelativeToAbsolutePath(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (!assetPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                return assetPath;
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string AbsolutePathToProjectRelative(string absolutePath)
        {
            var projectRoot = NormalizeAssetPath(Path.GetDirectoryName(Application.dataPath));
            var normalizedAbsolutePath = NormalizeAssetPath(Path.GetFullPath(absolutePath));

            if (!normalizedAbsolutePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedAbsolutePath;
            }

            return normalizedAbsolutePath.Substring(projectRoot.Length + 1);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private sealed class ConversionRequest
        {
            public ConversionRequest(
                string sourceAssetPath,
                string outputAssetPath,
                string importFolderAssetPath,
                string importedPrefabAssetPath)
            {
                SourceAssetPath = sourceAssetPath;
                OutputAssetPath = outputAssetPath;
                ImportFolderAssetPath = importFolderAssetPath;
                ImportedPrefabAssetPath = importedPrefabAssetPath;
            }

            public string SourceAssetPath { get; }
            public string OutputAssetPath { get; }
            public string ImportFolderAssetPath { get; }
            public string ImportedPrefabAssetPath { get; }
        }
    }
}
#endif
