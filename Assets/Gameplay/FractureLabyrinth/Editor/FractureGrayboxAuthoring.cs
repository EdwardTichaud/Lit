using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Lit.Fractures.Editor
{
    /// <summary>Creates only the dedicated fracture fixture; never rewrites campaign scenes or existing fixture authoring.</summary>
    [InitializeOnLoad]
    public static class FractureGrayboxAuthoring
    {
        public const string Folder = "Assets/Gameplay/FractureLabyrinth/Prototype";
        public const string ScenePath = Folder + "/Fracture_01_Graybox.unity";
        public const string KnowledgePath = Folder + "/LireLeReseau.asset";
        private const string Request = "Library/FractureLabyrinthValidation/Create.request";
        static FractureGrayboxAuthoring() => EditorApplication.update += Poll;
        private static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string refresh = "Library/FractureLabyrinthValidation/Refresh.request";
            if (File.Exists(refresh))
            {
                File.Delete(refresh);
                AssetDatabase.Refresh();
                return;
            }
            if (!File.Exists(Request)) return;
            string command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            try
            {
                if (command == "repair-work-flames") RepairWorkFlames();
                else if (command == "repair-preview") { RepairExposureProfile(); RepairWorkFlames(); }
                else if (command == "install-camera") InstallGameplayCamera();
                else if (command == "install-puzzles") InstallArchiveGalleryPuzzles();
                else if (command == "inspect-art") FractureDungeonArt.Inspect();
                else if (command == "install-art") FractureDungeonArt.Install();
                else if (command == "district-audit") FractureDistrictAuthoring.Audit();
                else if (command == "district-install") FractureDistrictAuthoring.Install();
                else Create();
                File.WriteAllText("Library/FractureLabyrinthValidation/Create.result", "SUCCESS " + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception exception)
            {
                File.WriteAllText("Library/FractureLabyrinthValidation/Create.result", exception.ToString());
                Debug.LogException(exception);
            }
        }

        [MenuItem("Lit/Fractures/Create Solo Graybox")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring.");
            EnsureKnowledgeIdentity();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("[Fracture] Graybox deja creee : aucune modification auteur ecrasee.");
                return;
            }
            EnsureFolder(Folder);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab");
            if (prefab == null) throw new InvalidOperationException("Prefab Lucian manquant.");
            var knowledge = AssetDatabase.LoadAssetAtPath<KnowledgeSO>(KnowledgePath);
            if (knowledge == null)
            {
                knowledge = ScriptableObject.CreateInstance<KnowledgeSO>();
                knowledge.title = "Lire le reseau";
                knowledge.description = "O : une arrivee alimente le reseau. Y : une derivation relie une autre branche. — : une coupure interrompt un circuit. Suivez les conduites pour comprendre ce que vous modifiez.";
                knowledge.tags.Add("fracture.prototype.network");
                AssetDatabase.CreateAsset(knowledge, KnowledgePath);
                EnsureKnowledgeIdentity();
            }
            Material floor = Material("Floor", new Color(.23f, .28f, .33f));
            Material wall = Material("Wall", new Color(.45f, .48f, .52f));
            Material station = Material("Station", new Color(.1f, .55f, .75f));
            Material locked = Material("Locked", new Color(.75f, .23f, .12f));
            Material archive = Material("Archive", new Color(.55f, .35f, .65f));
            var previousActive = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("Fracture_01_Graybox");
                var lab = root.AddComponent<FractureGraybox>();
                lab.lucianPrefab = prefab;
                lab.networkKnowledge = knowledge;
                var geometry = new GameObject("Geometry").transform; geometry.SetParent(root.transform);
                var cells = new HashSet<Vector2Int>();
                Rect(cells, -2, 2, -2, 2); // Cour.
                Rect(cells, -1, 1, -7, -4); Rect(cells, 0, 0, -3, -3); // Entree.
                Rect(cells, 5, 8, -2, 2); Rect(cells, 3, 4, -2, -2); Rect(cells, 3, 4, 0, 0); // Galerie et retour.
                Rect(cells, -8, -5, -2, 2); Rect(cells, -4, -3, 0, 0); // Archives.
                Rect(cells, -8, -5, 6, 9); Rect(cells, -6, -6, 3, 5); // Machines et liaison.
                Rect(cells, -2, 2, 6, 8); Rect(cells, 0, 0, 3, 5); // Seuil depuis cour.
                Rect(cells, -4, -3, 7, 7); Rect(cells, 6, 6, 3, 7); Rect(cells, 3, 5, 7, 7); // Boucle galerie–machines.
                Rect(cells, 0, 0, 9, 12); Rect(cells, -3, 3, 13, 19); // Arene.
                foreach (var cell in cells)
                {
                    Vector3 center = new Vector3(cell.x * 4, 0, cell.y * 4);
                    Cube("Floor_" + cell.x + "_" + cell.y, geometry, center - Vector3.up * .25f, new Vector3(4, .5f, 4), floor);
                    // Cutaway roof: collider prevents bypassing gates by jumping; renderer is deliberately absent.
                    var roof = new GameObject("Ceiling_" + cell.x + "_" + cell.y, typeof(BoxCollider)); roof.transform.SetParent(geometry);
                    roof.transform.position = center + Vector3.up * 4.25f; roof.GetComponent<BoxCollider>().size = new Vector3(4, .5f, 4);
                    foreach (var direction in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                    {
                        if (cells.Contains(cell + direction)) continue;
                        bool horizontal = direction.y != 0;
                        Vector3 at = center + new Vector3(direction.x * 2, 2, direction.y * 2);
                        Cube("Wall", geometry, at, horizontal ? new Vector3(4.2f, 4, .2f) : new Vector3(.2f, 4, 4.2f), wall);
                    }
                }
                lab.thresholdGate = Cube("Seuil verrouille", geometry, new Vector3(0, 2, 42), new Vector3(3.9f, 4, .3f), locked);
                lab.galleryGate = Cube("Retour Galerie–Cour", geometry, new Vector3(14, 2, 0), new Vector3(.3f, 4, 3.9f), locked);
                lab.networkGate = Cube("Liaison Archives–Machines", geometry, new Vector3(-24, 2, 16), new Vector3(3.9f, 4, .3f), locked);
                lab.spawn = new GameObject("Spawn_Lucian").transform; lab.spawn.SetParent(root.transform); lab.spawn.position = new Vector3(0, .04f, -26);
                lab.previewCamera = new GameObject("GrayboxCamera", typeof(Camera), typeof(AudioListener), typeof(HDAdditionalCameraData)).GetComponent<Camera>();
                lab.previewCamera.transform.SetParent(root.transform); lab.previewCamera.tag = "MainCamera";
                lab.previewCamera.transform.SetPositionAndRotation(new Vector3(0, 20, -41), Quaternion.Euler(52, 0, 0));
                lab.previewCamera.fieldOfView = 52; lab.previewCamera.nearClipPlane = .1f; lab.previewCamera.farClipPlane = 180;
                var sun = new GameObject("GrayboxSun", typeof(Light), typeof(HDAdditionalLightData)).GetComponent<Light>();
                sun.transform.SetParent(root.transform); sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(48, -25, 0); sun.intensity = 35000;
                var volume = new GameObject("GrayboxExposure", typeof(Volume)).GetComponent<Volume>(); volume.transform.SetParent(root.transform);
                volume.isGlobal = true; volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder + "/Exposure.asset");
                if (volume.sharedProfile == null)
                {
                    volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(volume.sharedProfile, Folder + "/Exposure.asset");
                }
                RepairExposureProfile();

                Station(lab, "Commencer / reformer", FractureStation.Kind.Entry, new Vector3(0, .5f, -23), station);
                Station(lab, "Piece collective — garde au jalon 2", FractureStation.Kind.Part, new Vector3(27, .5f, -3), station);
                Station(lab, "Reparer la commande", FractureStation.Kind.Repair, new Vector3(-3, .5f, 28), station);
                var thresholdDial = Station(lab, "Choisir le circuit du seuil", FractureStation.Kind.RoutingDial, new Vector3(3, .5f, 31), station);
                Station(lab, "Alimenter le seuil (O)", FractureStation.Kind.Threshold, new Vector3(0, .5f, 35), station).linkedDial = thresholdDial;
                for (int i = 0; i < 3; i++) Station(lab, "Poste " + (i + 1) + " : regler le symbole", FractureStation.Kind.ArchiveDial,
                    new Vector3(-30 + i * 4, .5f, -2), archive).dialIndex = i;
                Station(lab, "Comparer les trois postes au schema", FractureStation.Kind.ArchiveValidate, new Vector3(-26, .5f, 5), archive);
                Sign(root.transform, "Traces :\n1. O alimente\n2. Y derive\n3. — interrompt", new Vector3(-29, 2, 3));
                var shortcutDial = Station(lab, "Choisir le circuit de liaison", FractureStation.Kind.RoutingDial, new Vector3(-25, .5f, 9), station);
                Station(lab, "Deriver vers les Machines (Y)", FractureStation.Kind.NetworkShortcut, new Vector3(-23, .5f, 12), station).linkedDial = shortcutDial;
                var protectionDial = Station(lab, "Choisir le circuit de protection", FractureStation.Kind.RoutingDial, new Vector3(-29, .5f, 29), station);
                Station(lab, "Couper la protection (—) — effet au jalon 2", FractureStation.Kind.Protection, new Vector3(-25, .5f, 33), station).linkedDial = protectionDial;
                Station(lab, "Relais de renforts — effet au jalon 2", FractureStation.Kind.Reinforcements, new Vector3(31, .5f, 6), station);
                Station(lab, "Sortir du parcours (pas une victoire de boss)", FractureStation.Kind.Exit, new Vector3(0, .5f, 67), station);
                Sign(root.transform, "COUR\nArchives <  |  > Galerie\nSeuil ^", new Vector3(0, 1.7f, -3));
                Sign(root.transform, "ARENE\nGardien non raccorde\nJalon 2", new Vector3(0, 2, 59));
                Sign(root.transform, "MACHINES", new Vector3(-26, 2, 35));
                foreach (Vector3 at in new[] { new Vector3(0, 1, -23), new Vector3(0, 1, 0), new Vector3(-26, 1, 0), new Vector3(26, 1, 0), new Vector3(-26, 1, 30), new Vector3(0, 1, 30), new Vector3(0, 1, 64), new Vector3(-24, 1, 10) })
                {
                    var flame = new GameObject("Flame_de_travail_graybox", typeof(Flame)).GetComponent<Flame>();
                    flame.transform.SetParent(root.transform); flame.transform.position = at; flame.useInteractInput = false;
                    foreach (var collider in flame.GetComponents<SphereCollider>()) collider.isTrigger = true;
                    flame.ConfigureFromSceneMarker("fracture.graybox." + at.x + "." + at.z, false, true, .1f, 11f, 0);
                }
                CopyGameplayCamera(lab);
                ConfigureArchiveGalleryPuzzles(lab);
                FractureDungeonArt.Dress(lab);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Echec sauvegarde scene prototype.");
                AssetDatabase.SaveAssets();
                Debug.Log("[Fracture] Graybox solo creee : " + ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("Lit/Fractures/Install Archive Gallery Puzzles")]
        public static void InstallArchiveGalleryPuzzles()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (loaded && scene.isDirty) throw new InvalidOperationException("La graybox contient des modifications non sauvegardées.");
            if (!loaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var lab = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FractureGraybox>()).Single();
                ConfigureArchiveGalleryPuzzles(lab);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (!loaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void ConfigureArchiveGalleryPuzzles(FractureGraybox lab)
        {
            if (lab.startGate == null)
            {
                var existing = lab.transform.Find("Porte de départ");
                lab.startGate = existing != null ? existing.gameObject : Cube("Porte de départ", lab.transform,
                    new Vector3(0, 2, -12), new Vector3(3.9f, 4, .3f), Material("Locked", new Color(.75f, .23f, .12f)));
                EditorUtility.SetDirty(lab);
            }
            if (lab.transform.Find("Puzzles_ArchivesGaleries") != null) return;
            foreach (var old in lab.GetComponentsInChildren<FractureStation>(true))
                if (old.kind != FractureStation.Kind.Entry && old.kind != FractureStation.Kind.Exit)
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach (var sign in lab.GetComponentsInChildren<TextMesh>())
                UnityEngine.Object.DestroyImmediate(sign.gameObject);
            var group = new GameObject("Puzzles_ArchivesGaleries").transform; group.SetParent(lab.transform);
            var archive = Material("Archive", new Color(.55f, .35f, .65f));
            var material = Material("Station", new Color(.1f, .55f, .75f));
            var wall = Material("Wall", new Color(.45f, .48f, .52f));
            int[] clues = { 2, -1, 0, -1, 1, -1 };
            for (int i = 0; i < 6; i++)
            {
                var shelf = Station(lab, "Bibliothèque " + (char)('A' + i) + " — Fouiller", FractureStation.Kind.Bookshelf,
                    new Vector3(-30 + (i % 3) * 4, 1.2f, i < 3 ? -2 : 5), archive);
                shelf.transform.SetParent(group); shelf.transform.localScale = new Vector3(1.6f, 2.4f, .7f);
                shelf.clueIndex = clues[i];
                // Open shelves and rows of books, with one selection collider on the cabinet.
                shelf.GetComponent<Renderer>().enabled = false;
                for (int row = 0; row < 4; row++)
                {
                    var board = GameObject.CreatePrimitive(PrimitiveType.Cube); board.name = "Tablette";
                    board.transform.SetParent(shelf.transform, false); board.transform.localPosition = new Vector3(0, -.48f + row * .32f, 0);
                    board.transform.localScale = new Vector3(1, .035f, 1); board.GetComponent<Renderer>().sharedMaterial = wall;
                    UnityEngine.Object.DestroyImmediate(board.GetComponent<Collider>());
                    if (row == 3) continue;
                    for (int book = 0; book < 5; book++)
                    {
                        var volume = GameObject.CreatePrimitive(PrimitiveType.Cube); volume.name = "Livre";
                        volume.transform.SetParent(shelf.transform, false); volume.transform.localPosition = new Vector3(-.36f + book * .18f, -.34f + row * .32f, 0);
                        volume.transform.localScale = new Vector3(.12f, .23f, .7f); volume.GetComponent<Renderer>().sharedMaterial = book % 2 == 0 ? archive : material;
                        UnityEngine.Object.DestroyImmediate(volume.GetComponent<Collider>());
                    }
                }
            }
            for (int i = 0; i < 3; i++)
            {
                var dial = Station(lab, "Porte — Case " + (i + 1), FractureStation.Kind.GalleryDial, new Vector3(21 + i * 3, .5f, -2), material);
                dial.dialIndex = i; dial.transform.SetParent(group);
            }
            Station(lab, "Porte — Essayer la combinaison", FractureStation.Kind.GalleryValidate, new Vector3(30, .5f, -2), material).transform.SetParent(group);
            lab.galleryGate.transform.position = new Vector3(24, 2, 2);
            lab.galleryGate.transform.localScale = new Vector3(3.9f, 4, .3f);
            lab.galleryGate.name = "Porte des Galeries — combinaison";
            Cube("Mur gauche de la porte", group, new Vector3(20, 2, 2), new Vector3(4, 4, .3f), wall);
            Cube("Mur droit de la porte", group, new Vector3(30, 2, 2), new Vector3(8, 4, .3f), wall);
            Cube("Machines hors parcours", group, new Vector3(24, 2, 12), new Vector3(3.9f, 4, .3f), wall);
            Cube("Ancien seuil hors parcours", group, new Vector3(0, 2, 10), new Vector3(3.9f, 4, .3f), wall);
            var exit = lab.GetComponentsInChildren<FractureStation>().Single(station => station.kind == FractureStation.Kind.Exit);
            exit.transform.position = new Vector3(28, .5f, 7); exit.label = "Terminer les deux énigmes"; exit.name = exit.label;
            exit.GetComponent<WorldInteractionPrompt>().label = exit.label;
            lab.networkKnowledge.title = "Les symboles numérotés des Archives";
            lab.networkKnowledge.description = "Les bibliothèques des Archives cachent trois indices : 1 = Y, 2 = —, 3 = O. Les numéros donnent l'ordre des symboles sur la porte des Galeries.";
            EditorUtility.SetDirty(lab.networkKnowledge);
            Sign(group, "ARCHIVES\nFouillez les bibliotheques", new Vector3(-26, 2.8f, 7));
            Sign(group, "GALERIES\nPorte a combinaison", new Vector3(26, 2.8f, 8));
            Sign(group, "Archives <  |  > Galeries", new Vector3(0, 1.7f, -3));
            foreach (var sign in group.GetComponentsInChildren<TextMesh>()) sign.characterSize = .07f;
            EditorUtility.SetDirty(lab);
        }
        [MenuItem("Lit/Fractures/Install Gameplay Camera")]
        public static void InstallGameplayCamera()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (alreadyLoaded && scene.isDirty) throw new InvalidOperationException("La graybox contient des modifications non sauvegardees.");
            if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var lab = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FractureGraybox>(true)).Single();
                if (CopyGameplayCamera(lab))
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static bool CopyGameplayCamera(FractureGraybox lab)
        {
            if (lab.previewCamera != null && lab.previewCamera.GetComponent<LitGameplayCameraModeController>() != null) return false;
            var session = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/System/GameplaySessionRoot.prefab");
            var source = session != null ? session.GetComponentInChildren<LitGameplayCameraModeController>(true) : null;
            if (source == null || source.GetComponent<Camera>() == null || source.GetComponent<LitUccCameraCharacterBinder>() == null)
                throw new InvalidOperationException("La camera de gameplay et son binder sont requis dans GameplaySessionRoot.");
            var clone = UnityEngine.Object.Instantiate(source.gameObject);
            clone.name = "Fracture_GameplayCamera";
            clone.transform.SetParent(lab.transform, false);
            var camera = clone.GetComponent<Camera>();
            var controller = clone.GetComponent<Opsive.UltimateCharacterController.Camera.CameraController>();
            controller.InitCharacterOnAwake = false;
            controller.Character = null;
            camera.tag = "MainCamera";
            if (clone.GetComponent<AudioListener>() == null) clone.AddComponent<AudioListener>();
            clone.SetActive(true);
            var old = lab.previewCamera;
            lab.previewCamera = camera;
            EditorUtility.SetDirty(lab);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            return true;
        }
        [MenuItem("Lit/Fractures/Repair Graybox Work Flames")]
        public static void RepairWorkFlames()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (alreadyLoaded && scene.isDirty) throw new InvalidOperationException("La graybox contient des modifications non sauvegardees.");
            if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var lab = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FractureGraybox>(true)).Single();
                foreach (var flame in lab.GetComponentsInChildren<Flame>(true))
                {
                    if (flame.name != "Flame_de_travail_graybox") continue;
                    foreach (var collider in flame.GetComponents<SphereCollider>())
                    {
                        if (collider.isTrigger) continue;
                        collider.isTrigger = true;
                        EditorUtility.SetDirty(collider);
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        private static void EnsureKnowledgeIdentity()
        {
            var knowledge = AssetDatabase.LoadAssetAtPath<KnowledgeSO>(KnowledgePath);
            if (knowledge == null) return;
            string guid = AssetDatabase.AssetPathToGUID(KnowledgePath);
            var serialized = new SerializedObject(knowledge);
            var id = serialized.FindProperty("uniqueId");
            var publicId = serialized.FindProperty("knowledgeId");
            if (string.IsNullOrWhiteSpace(id.stringValue)) id.stringValue = guid;
            if (string.IsNullOrWhiteSpace(publicId.stringValue)) publicId.stringValue = guid;
            if (serialized.ApplyModifiedPropertiesWithoutUndo()) { EditorUtility.SetDirty(knowledge); AssetDatabase.SaveAssets(); }
        }
        private static void RepairExposureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder + "/Exposure.asset");
            if (profile == null) return;
            if (profile.TryGet<Exposure>(out var existing) && existing != null)
            {
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(existing)))
                {
                    existing.mode.Override(ExposureMode.Fixed);
                    existing.fixedExposure.Override(13);
                    AssetDatabase.AddObjectToAsset(existing, profile);
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssets();
                }
                return;
            }
            profile.components.RemoveAll(component => component == null);
            var exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(13);
            AssetDatabase.AddObjectToAsset(exposure, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
        private static Material Material(string name, Color color)
        {
            string path = Folder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            var shader = Shader.Find("HDRP/Lit"); if (shader == null) throw new InvalidOperationException("HDRP/Lit indisponible.");
            var value = new Material(shader) { name = "Fracture_" + name }; value.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(value, path); return value;
        }
        private static void Rect(HashSet<Vector2Int> cells, int x0, int x1, int z0, int z1)
        { for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++) cells.Add(new Vector2Int(x, z)); }
        private static GameObject Cube(string name, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name; cube.transform.SetParent(parent);
            cube.transform.position = at; cube.transform.localScale = size; cube.GetComponent<Renderer>().sharedMaterial = material; return cube;
        }
        private static FractureStation Station(FractureGraybox lab, string label, FractureStation.Kind kind, Vector3 at, Material material)
        {
            var cube = Cube(label, lab.transform, at, new Vector3(.7f, 1, .7f), material);
            var station = cube.AddComponent<FractureStation>(); station.labyrinth = lab; station.kind = kind; station.label = label;
            var prompt = cube.AddComponent<WorldInteractionPrompt>(); prompt.target = station; prompt.label = label;
            return station;
        }
        private static void Sign(Transform root, string text, Vector3 at)
        {
            var sign = new GameObject("Repere_" + text.Split('\n')[0], typeof(TextMesh)); sign.transform.SetParent(root);
            sign.transform.SetPositionAndRotation(at, Quaternion.Euler(45, 0, 0));
            var label = sign.GetComponent<TextMesh>(); label.text = text; label.characterSize = .25f; label.fontSize = 48;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
        }
    }
}
