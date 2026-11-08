using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lit.Fractures.Editor
{
    public sealed class FractureGrayboxTests
    {
        [Test]
        public void DistrictContentUsesSessionAndManifestAndPackedStateSurvivesRestore()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(FractureDistrictAuthoring.ScenePath, OpenSceneMode.Additive);
                var lab = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FractureGraybox>()).Single();
                Assert.That(lab.districtIntegration, Is.True);
                Assert.That(lab.previewCamera, Is.Null); Assert.That(lab.lucianPrefab, Is.Null);
                Assert.That(lab.GetComponentsInChildren<Camera>(true), Is.Empty);
                Assert.That(lab.GetComponentsInChildren<Flame>(true), Is.Empty, "No fixture Flame may reveal the entrance from inside.");
                var gate = lab.GetComponentInChildren<FractureLightEntrance>();
                Assert.That(gate.lightProbe.isTrigger, Is.True);
                Assert.That(gate.concealedWall.GetComponent<BoxCollider>().isTrigger, Is.False);
                Assert.That(Vector3.Distance(gate.transform.position - Vector3.up * 1.5f, FractureDistrictAuthoring.EntrancePosition), Is.LessThan(.001f));
                var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>(FractureDistrictAuthoring.ManifestPath);
                Assert.That(manifest.loadingScenes.Contains(AssetDatabase.LoadAssetAtPath<SceneAsset>(FractureDistrictAuthoring.ScenePath)), Is.True);
                Assert.That(manifest.LoadingSceneNames, Does.Contain(scene.name));
                Assert.That(EditorBuildSettings.scenes.Any(s => s.path == FractureDistrictAuthoring.ScenePath && s.enabled), Is.True);
                Assert.That(manifest.BakedNavMeshData, Is.Not.Null);
                var original = lab.CaptureDistrictState();
                int state = 1 | 7 << 1 | (1 + 2 * 3) << 4 | 512 | 1024 | 1 << 11;
                lab.ApplyDistrictState(state, null, null);
                Assert.That(lab.CaptureDistrictState(), Is.EqualTo(state));
                Assert.That(lab.ArchivePuzzle.DoorOpen, Is.True);
                Assert.That(lab.startGate.activeSelf, Is.False);
                Assert.That(lab.ExplorationCompleted, Is.True);
                lab.ApplyDistrictState(original, null, null);
                Assert.That(lab.ArchivePuzzle.FoundCount, Is.Zero);
                Assert.That(lab.startGate.activeSelf, Is.True);
            }
            finally
            {
                if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else
                {
                    var loaded = SceneManager.GetSceneByPath(FractureDistrictAuthoring.ScenePath);
                    if (loaded.IsValid() && loaded.isLoaded) EditorSceneManager.CloseScene(loaded, true);
                }
            }
        }

        [Test]
        public void NumberedCluesRequireSearchAndCorrectOrderAndRetryKeepsKnowledge()
        {
            var puzzle = new FractureArchivePuzzle();
            Assert.That(puzzle.TryOpen(), Is.False);
            Assert.That(puzzle.Reveal(-1), Is.False);
            Assert.That(puzzle.Reveal(3), Is.False);
            Assert.That(puzzle.Cycle(-1), Is.False);
            puzzle.Cycle(0); puzzle.Cycle(1); puzzle.Cycle(1);
            Assert.That(puzzle.TryOpen(), Is.False, "Knowing the code externally does not replace finding the three clues.");
            Assert.That(puzzle.Reveal(2), Is.True);
            Assert.That(puzzle.Reveal(2), Is.False);
            puzzle.Reveal(0); puzzle.Reveal(1);
            Assert.That(puzzle.FoundCount, Is.EqualTo(3));
            Assert.That(puzzle.TryOpen(), Is.True);
            Assert.That(puzzle.TryOpen(), Is.False);
            Assert.That(puzzle.Cycle(0), Is.False);
            puzzle.ResetAttempt();
            Assert.That(puzzle.DoorOpen, Is.False);
            Assert.That(puzzle.FoundCount, Is.EqualTo(3));
            Assert.That(puzzle.GetInput(0), Is.EqualTo(0));
            Assert.That(puzzle.TryOpen(), Is.False, "Wrong order must be rejected even with all clues.");
            puzzle.Cycle(0); puzzle.Cycle(1); puzzle.Cycle(1);
            Assert.That(puzzle.TryOpen(), Is.True);
            Assert.That(new FractureArchivePuzzle().FoundCount, Is.Zero, "No static clue leak to a new session.");
        }

        [Test]
        public void AuthoredSceneHasIsolatedBindingsAndRealInteractionStations()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(FractureGrayboxAuthoring.ScenePath), Is.Not.Null);
            var scene = EditorSceneManager.OpenScene(FractureGrayboxAuthoring.ScenePath, OpenSceneMode.Additive);
            try
            {
                var lab = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FractureGraybox>()).Single();
                Assert.That(lab.lucianPrefab.GetComponent<LitOpsiveLocomotionBridge>(), Is.Not.Null);
                Assert.That(lab.lucianPrefab.GetComponent<PlayerScriptedJumpController>(), Is.Not.Null);
                Assert.That(lab.networkKnowledge, Is.Not.Null);
                Assert.That(lab.networkKnowledge.UniqueId, Is.Not.Null.And.Not.Empty);
                Assert.That(lab.networkKnowledge.knowledgeId, Is.Not.Null.And.Not.Empty);
                Assert.That(lab.previewCamera, Is.Not.Null);
                Assert.That(lab.previewCamera.GetComponent<LitGameplayCameraModeController>(), Is.Not.Null);
                Assert.That(lab.previewCamera.GetComponent<LitUccCameraCharacterBinder>(), Is.Not.Null);
                var cameraController = lab.previewCamera.GetComponent<Opsive.UltimateCharacterController.Camera.CameraController>();
                Assert.That(cameraController, Is.Not.Null);
                Assert.That(cameraController.InitCharacterOnAwake, Is.False);
                Assert.That(cameraController.Character, Is.Null);
                var sourceCamera = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/System/GameplaySessionRoot.prefab").GetComponentInChildren<LitGameplayCameraModeController>(true);
                Assert.That(new SerializedObject(lab.previewCamera.GetComponent<LitGameplayCameraModeController>()).FindProperty("profile").objectReferenceValue,
                    Is.SameAs(new SerializedObject(sourceCamera).FindProperty("profile").objectReferenceValue));
                var volume = lab.GetComponentInChildren<Volume>();
                Assert.That(volume.sharedProfile.TryGet<Exposure>(out var exposure), Is.True);
                Assert.That(exposure.mode.value, Is.EqualTo(ExposureMode.Fixed));
                Assert.That(AssetDatabase.GetAssetPath(exposure), Is.EqualTo(AssetDatabase.GetAssetPath(volume.sharedProfile)), "Exposure must be a persisted subasset, not an unsaved component.");
                Assert.That(lab.thresholdGate && lab.galleryGate && lab.networkGate, Is.True);
                Assert.That(lab.startGate, Is.Not.Null);
                Assert.That(lab.startGate.activeSelf, Is.True);
                Assert.That(lab.startGate.GetComponent<Collider>().isTrigger, Is.False);
                Assert.That(lab.startGate.transform.position, Is.EqualTo(new Vector3(0, 2, -12)));
                var visualRoots = lab.GetComponentsInChildren<Transform>(true).Where(value => value.name == FractureDungeonArt.Marker).ToArray();
                Assert.That(visualRoots.Length, Is.GreaterThan(184), "Floors, walls and gameplay props must be dressed.");
                foreach (var root in visualRoots)
                {
                    Assert.That(root.GetComponentsInChildren<Collider>(true).All(value => !value.enabled), Is.True, root.parent.name);
                    foreach (var mesh in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        string path = AssetDatabase.GetAssetPath(mesh.sharedMesh);
                        Assert.That(path.StartsWith(FractureDungeonArt.Pack) || path.StartsWith(FractureGrayboxAuthoring.Folder + "/SolidWall_"), Is.True);
                        if (mesh.name != "Mur massif") continue;
                        var wallMesh = mesh.sharedMesh;
                        Assert.That(wallMesh.vertexCount, Is.EqualTo(24));
                        Assert.That(wallMesh.triangles.Length, Is.EqualTo(36));
                        Assert.That(wallMesh.vertices.Distinct().Count(), Is.EqualTo(8), "Closed box corners, not two planes.");
                        Assert.That(Mathf.Min(wallMesh.bounds.size.x, wallMesh.bounds.size.y, wallMesh.bounds.size.z), Is.GreaterThan(.19f));
                        Assert.That(root.childCount, Is.EqualTo(1), "Old paired wall planes must be removed.");
                        var support = root.parent.GetComponent<BoxCollider>();
                        Assert.That(Vector3.Distance(mesh.GetComponent<Renderer>().bounds.size, support.bounds.size), Is.LessThan(.001f));
                        for (int face = 0; face < 6; face++)
                        {
                            int i = face * 4;
                            Assert.That(Vector3.Dot(wallMesh.normals[i], wallMesh.vertices[i]), Is.GreaterThan(0), "Face must point outward.");
                            Assert.That(Vector2.Distance(wallMesh.uv[i], wallMesh.uv[i + 1]) * 4,
                                Is.EqualTo(Vector3.Distance(wallMesh.vertices[i], wallMesh.vertices[i + 1])).Within(.0001f), "Horizontal texture density.");
                            Assert.That(Vector2.Distance(wallMesh.uv[i], wallMesh.uv[i + 3]) * 4,
                                Is.EqualTo(Vector3.Distance(wallMesh.vertices[i], wallMesh.vertices[i + 3])).Within(.0001f), "Vertical texture density.");
                        }
                    }
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                        Assert.That(renderer.sharedMaterials.All(value => value != null && value.shader != null && value.shader.isSupported), Is.True);
                }
                Assert.That(lab.GetComponentsInChildren<FractureStation>().All(station => station.transform.Find(FractureDungeonArt.Marker) != null), Is.True);
                foreach (var shelf in lab.GetComponentsInChildren<FractureStation>().Where(station => station.kind == FractureStation.Kind.Bookshelf))
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(shelf.transform.Find(FractureDungeonArt.Marker).GetChild(0).eulerAngles.y, 180)), Is.LessThan(1));
                Assert.That(lab.GetComponentsInChildren<FractureStation>().Count(station => station.kind == FractureStation.Kind.ArchiveDial), Is.Zero);
                Assert.That(lab.GetComponentsInChildren<Flame>().All(flame => !flame.IsAncientFlame && flame.IsLit), Is.True);
                Assert.That(lab.GetComponentsInChildren<Flame>().SelectMany(flame => flame.GetComponents<SphereCollider>()).All(collider => collider.isTrigger), Is.True,
                    "Work Flames have no solid physical body: their colliders must not block selection rays.");
                Assert.That(lab.GetComponentsInChildren<SquadCharacterController>().Length, Is.Zero, "Player is instantiated only on standalone Play.");
                Assert.That(lab.GetComponentsInChildren<FractureStation>().All(station => station.labyrinth == lab && station.GetComponent<WorldInteractionPrompt>() != null), Is.True);
                foreach (var station in lab.GetComponentsInChildren<FractureStation>())
                    Assert.That(station.GetPrototypeHelp(), Does.Contain("À FAIRE MAINTENANT").And.Contain("À QUOI ÇA SERT"), station.label);
                var shelves = lab.GetComponentsInChildren<FractureStation>().Where(station => station.kind == FractureStation.Kind.Bookshelf).ToArray();
                Assert.That(shelves.Length, Is.EqualTo(6));
                Assert.That(shelves.Where(station => station.clueIndex >= 0).Select(station => station.clueIndex).OrderBy(index => index), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(lab.GetComponentsInChildren<FractureStation>().Count(station => station.kind == FractureStation.Kind.GalleryDial), Is.EqualTo(3));
                Assert.That(lab.GetComponentsInChildren<FractureStation>().All(station => station.kind == FractureStation.Kind.Entry || station.kind == FractureStation.Kind.Exit ||
                    station.kind == FractureStation.Kind.Bookshelf || station.kind == FractureStation.Kind.GalleryDial || station.kind == FractureStation.Kind.GalleryValidate), Is.True);
                Assert.That(lab.galleryGate.transform.position, Is.EqualTo(new Vector3(24, 2, 2)));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }

    public sealed class FractureGrayboxPlayTests
    {
        private Gamepad pad;
        private Keyboard keyboard;
        private FractureGraybox lab;
        private Action recenterObserver;
        private static string Folder => "Library/FractureLabyrinthValidation";
        private static FractureStation Find(FractureGraybox value, FractureStation.Kind kind, int index = 0) =>
            value.GetComponentsInChildren<FractureStation>().First(station => station.kind == kind && (kind != FractureStation.Kind.GalleryDial || station.dialIndex == index));

        [UnityTest]
        public IEnumerator GameplayCameraBindsAndUsesSharedOrbitZoomInspectionAndRecenter()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False);
            EditorSceneManager.OpenScene(FractureGrayboxAuthoring.ScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            pad = InputSystem.AddDevice<Gamepad>();
            float deadline = Time.realtimeSinceStartup + 20;
            Opsive.UltimateCharacterController.Camera.CameraController camera = null;
            LitGameplayCameraModeController mode = null;
            while (true)
            {
                lab = UnityEngine.Object.FindAnyObjectByType<FractureGraybox>();
                if (lab != null && lab.Player != null)
                {
                    camera = lab.previewCamera.GetComponent<Opsive.UltimateCharacterController.Camera.CameraController>();
                    mode = lab.previewCamera.GetComponent<LitGameplayCameraModeController>();
                    if (camera.Character == lab.Player.gameObject && camera.ActiveViewType is LitTacticalUccViewType) break;
                }
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Gameplay camera did not bind the graybox Lucian.");
                yield return null;
            }
            for (int i = 0; i < 15; i++) yield return null;
            Assert.That(LocalPlayerInput.FindSharedActionMap("Camera").enabled, Is.True);
            var view = (LitTacticalUccViewType)camera.ActiveViewType;
            var readInput = typeof(LitGameplayCameraModeController).GetMethod("ReadInput", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Quaternion startRotation = camera.transform.rotation;
            Vector3 playerPosition = lab.Player.transform.position;
            float end = Time.realtimeSinceStartup + .5f;
            for (int frame = 0; frame < 20 || Time.realtimeSinceStartup < end; frame++)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(.8f, .2f) }); InputSystem.Update();
                if (!Application.isFocused)
                {
                    // Same focused-input calculation seam as the existing camera
                    // validations. The production focus guard is never weakened.
                    readInput.Invoke(mode, null); view.Rotate(0, 0, false);
                }
                yield return null;
            }
            Assert.That(LocalInputRouter.CameraOrbitValue.x, Is.GreaterThan(.1f));
            InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(Quaternion.Angle(startRotation, camera.transform.rotation), Is.GreaterThan(2), "Fixed graybox code must not overwrite the UCC orbit.");
            Assert.That(Vector3.Distance(playerPosition, lab.Player.transform.position), Is.LessThan(.15f), "Camera stick moved Lucian.");
            float distance = view.ManualDistance;
            float physicalDistance = Vector3.Distance(camera.transform.position, lab.Player.transform.position);
            File.WriteAllText(Folder + "/Camera.trace", "zoom start=" + distance + " focused=" + Application.isFocused + " gamepad=" + MainMenuInputSettings.AllowsGamepad() + "\n");
            end = Time.realtimeSinceStartup + .5f;
            for (int frame = 0; frame < 20 || Time.realtimeSinceStartup < end; frame++)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder)); InputSystem.Update();
                if (!Application.isFocused) { readInput.Invoke(mode, null); view.Rotate(0, 0, false); }
                File.AppendAllText(Folder + "/Camera.trace", "zoom=" + view.ManualDistance + " rb=" + pad.rightShoulder.ReadValue() + " current=" + (Gamepad.current == pad) + " frameZoom=" + mode.FrameInput.zoom + " dt=" + Time.deltaTime + " focus=" + Application.isFocused + " allowed=" + InputModeCoordinator.IsCameraAllowed + "\n");
                yield return null;
            }
            InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
            for (int i = 0; i < 15; i++) yield return null;
            Assert.That(Mathf.Abs(view.ManualDistance - distance), Is.GreaterThan(.4f));
            Assert.That(Mathf.Abs(Vector3.Distance(camera.transform.position, lab.Player.transform.position) - physicalDistance), Is.GreaterThan(.2f), "UCC camera pose did not follow zoom.");
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftStick)); InputSystem.Update();
            Assert.That(mode.InspectionActive, Is.True, "L3 inspection was not routed to the shared camera controller.");
            InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftStick)); InputSystem.Update();
            Assert.That(mode.InspectionActive, Is.False, "Pressing L3 again must return to follow.");
            Assert.That(view.Following, Is.True);
            InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
            int recentered = 0; recenterObserver = () => recentered++; LocalInputRouter.CameraRecenter += recenterObserver;
            end = Time.realtimeSinceStartup + .8f;
            while (Time.realtimeSinceStartup < end)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightStick)); InputSystem.Update();
                yield return null;
            }
            Assert.That(recentered, Is.Zero, "Exploration reserves R3 for the torch, not the camera.");
            InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
            if (MainMenuInputSettings.AllowsKeyboardMouse())
            {
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C)); InputSystem.Update();
                Assert.That(recentered, Is.GreaterThan(0), "Keyboard C must route the existing recenter action.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            }
            for (int i = 0; i < 10; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Folder + "/Camera-gameplay.png");
            yield return null;
            File.WriteAllText(Folder + "/Camera.result", "PASS: source gameplay stack, local binding, routed right-stick orbit, shoulder zoom and native pose, L3 inspection/follow, R3 torch reservation, keyboard C recenter when allowed by user settings. Unfocused calculation seam used where needed; physical controller feel not qualified.");
        }

        [UnityTest]
        public IEnumerator PreviewSpawnWithPersistedExposure()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False);
            EditorSceneManager.OpenScene(FractureGrayboxAuthoring.ScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            for (int i = 0; i < 30; i++) yield return null;
            lab = UnityEngine.Object.FindAnyObjectByType<FractureGraybox>();
            Assert.That(lab.Player, Is.Not.Null);
            Assert.That(lab.startGate.activeSelf, Is.True, "Entry door must be closed before starting.");
            var entry = Find(lab, FractureStation.Kind.Entry);
            Assert.That(lab.GetNearbyHelpStation(entry.transform.position), Is.SameAs(entry));
            Assert.That(lab.GetNearbyHelpStation(new Vector3(1000, 0, 1000)), Is.Null);
            lab.showPuzzleHelp = false;
            Assert.That(lab.GetNearbyHelpStation(entry.transform.position), Is.Null);
            lab.showPuzzleHelp = true;
            Assert.That(InputFocusStack.HasAnyFocus(), Is.False, "Prototype help must never acquire input focus.");
            Assert.That(lab.GetComponentInChildren<Volume>().sharedProfile.TryGet<Exposure>(out var exposure), Is.True);
            Assert.That(exposure.mode.value, Is.EqualTo(ExposureMode.Fixed));
            string screenshot = Folder + "/Preview-spawn.png";
            ScreenCapture.CaptureScreenshot(screenshot);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(File.Exists(screenshot), Is.True);
        }

        [UnityTest]
        public IEnumerator WalkSolveArchiveSearchAndGalleryDoor()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False, "Unsaved user scene.");
            EditorSceneManager.OpenScene(FractureGrayboxAuthoring.ScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            pad = InputSystem.AddDevice<Gamepad>();
            float until = Time.realtimeSinceStartup + 20;
            while ((lab = UnityEngine.Object.FindAnyObjectByType<FractureGraybox>()) == null || lab.Player == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(until)); yield return null;
            }
            while (!lab.Player.GetComponent<LitOpsiveLocomotionBridge>().Grounded)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(until)); yield return null;
            }
            Assert.That(UnityEngine.Object.FindObjectsByType<SquadCharacterController>().Length, Is.EqualTo(1));
            Assert.That(lab.startGate.activeSelf, Is.True);
            Assert.That(lab.startGate.GetComponent<Collider>().Raycast(new Ray(new Vector3(0, 1, -14), Vector3.forward), out _, 4), Is.True);
            yield return Walk(0, -24.8f); yield return Use(Find(lab, FractureStation.Kind.Entry));
            Assert.That(lab.startGate.activeSelf, Is.False, "Entry desk must open the door.");
            Assert.That(lab.ArchivePuzzle.TryOpen(), Is.False);
            yield return Path(new Vector2(2,-25), new Vector2(2,-19), new Vector2(0,-14), new Vector2(0,0), new Vector2(-8,0), new Vector2(-16,0), new Vector2(-24,0), new Vector2(-24,-5), new Vector2(-30,-5), new Vector2(-30,-4.2f));
            var shelves = lab.GetComponentsInChildren<FractureStation>().Where(value => value.kind == FractureStation.Kind.Bookshelf).ToArray();
            var first = shelves.Single(value => value.clueIndex == 2);
            yield return Use(first); yield return Use(first);
            Assert.That(lab.ArchivePuzzle.FoundCount, Is.EqualTo(1), "Repeat search cannot duplicate a clue.");
            Assert.That(lab.ClueNotebook, Does.Contain("1 = ?").And.Contain("3 = O"));
            yield return Path(new Vector2(-30,-5), new Vector2(-26,-5), new Vector2(-26,-4.2f));
            yield return Use(shelves.Single(value => value.name.StartsWith("Bibliothèque B")));
            Assert.That(lab.ArchivePuzzle.FoundCount, Is.EqualTo(1), "Empty shelf cannot invent a clue.");
            yield return Path(new Vector2(-26,-5), new Vector2(-22,-5), new Vector2(-22,-4.2f));
            yield return Use(shelves.Single(value => value.clueIndex == 0));
            yield return Path(new Vector2(-22,-5), new Vector2(-24,-5), new Vector2(-24,0), new Vector2(-24,3.1f), new Vector2(-26,3.1f));
            yield return Use(shelves.Single(value => value.clueIndex == 1));
            Assert.That(lab.ArchivePuzzle.FoundCount, Is.EqualTo(3));
            Assert.That(lab.KnowsNetwork, Is.True);
            Assert.That(lab.ClueNotebook, Is.EqualTo("1 = Y   |   2 = —   |   3 = O"));
            ScreenCapture.CaptureScreenshot(Folder + "/PuzzleHelp-archives.png"); yield return null;
            yield return Path(new Vector2(-24,3.1f), new Vector2(-24,0), new Vector2(-16,0), new Vector2(-8,0), new Vector2(0,0), new Vector2(8,0), new Vector2(16,0), new Vector2(30,0));
            var validate = Find(lab, FractureStation.Kind.GalleryValidate);
            yield return Use(validate);
            Assert.That(lab.ArchivePuzzle.DoorOpen, Is.False, "Wrong combination must not open the door.");
            Assert.That(lab.galleryGate.activeSelf, Is.True);
            yield return Walk(21, 0); yield return Use(Find(lab, FractureStation.Kind.GalleryDial,0));
            yield return Walk(24, 0); yield return Use(Find(lab, FractureStation.Kind.GalleryDial,1)); yield return Use(Find(lab, FractureStation.Kind.GalleryDial,1));
            yield return Walk(30, 0); yield return Use(validate);
            Assert.That(lab.ArchivePuzzle.DoorOpen, Is.True);
            Assert.That(lab.galleryGate.activeSelf, Is.False);
            ScreenCapture.CaptureScreenshot(Folder + "/PuzzleHelp-gallery.png"); yield return null;
            yield return Path(new Vector2(24,0), new Vector2(24,4), new Vector2(28,4), new Vector2(28,5.2f));
            yield return Use(Find(lab, FractureStation.Kind.Exit));
            Assert.That(lab.ExplorationCompleted, Is.True);
            Assert.That(lab.startGate.activeSelf, Is.False, "Completion must not trap Lucian away from the entry desk.");
            Assert.That(lab.Attempt.Phase, Is.EqualTo(FracturePhase.Abandoned));
            Assert.That(lab.Attempt.RewardRecorded, Is.False);
            File.WriteAllText(Folder + "/Walk.result", "PASS: physical UCC + virtual gamepad. Empty/repeated searches, 3 numbered clues discovered out of order, wrong code refused, correct gallery code and physical door passage, completion. No multiplayer qualification.");
        }


        private IEnumerator Path(params Vector2[] points) { foreach (var point in points) yield return Walk(point.x, point.y); }
        private IEnumerator Walk(float x, float z)
        {
            Vector3 target = new Vector3(x, lab.Player.transform.position.y, z);
            float until = Time.realtimeSinceStartup + 18;
            Vector3 initial = lab.Player.transform.position;
            while (true)
            {
                Vector3 delta = Vector3.ProjectOnPlane(target - lab.Player.transform.position, Vector3.up);
                if (delta.magnitude < .35f) break;
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(until), "Physical path stuck: " + initial + " -> " + target + " actual=" + lab.Player.transform.position + " input=" + LocalInputRouter.MoveValue);
                Vector2 move = ToScreenInput(new Vector2(delta.x, delta.z).normalized) * Mathf.Clamp(delta.magnitude, .3f, 1);
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = move, rightTrigger = delta.magnitude > 3 ? 1 : 0 });
                InputSystem.Update();
                yield return null;
            }
            InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
            for (int i = 0; i < 5; i++) yield return null;
            File.AppendAllText(Folder + "/Walk.trace", "WALK " + lab.Player.transform.position + " -> " + target + "\n");
        }
        private IEnumerator Use(FractureStation station)
        {
            // Walking can end with a tiny correction pointing away from the
            // console. Face it with ordinary stick input, never SetRotation.
            float faceUntil = Time.realtimeSinceStartup + 1.5f;
            while (true)
            {
                Vector3 facing = Vector3.ProjectOnPlane(station.transform.position - lab.Player.transform.position, Vector3.up).normalized;
                if (Vector3.Dot(lab.Player.transform.forward, facing) > .6f) break;
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(faceUntil), "Could not face " + station.label + " with normal input.");
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = ToScreenInput(new Vector2(facing.x, facing.z)) * .4f }); InputSystem.Update();
                yield return null;
            }
            InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
            for (int i = 0; i < 3; i++) yield return null;
            lab.Player.RefreshLocalInteractionDetectionForExternalLocomotion();
            Assert.That(station.IsInReach(lab.Player), Is.True, station.label + " out of range");
            var args = new object[] { station, lab.Player.GetInteractionOriginWorldPosition(), lab.Player.transform.forward, false, 0f };
            var evaluate = typeof(SquadCharacterController).GetMethod("TryEvaluateInteractionCandidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            string diagnostic = station.label + " position=" + lab.Player.transform.position + " forward=" + lab.Player.transform.forward +
                " eligible=" + station.CanBeDetectedBy(lab.Player) + " lit=" + CharacterInteractionDetection.IsInActiveFlameInfluence(station) +
                " gate=" + lab.Player.GetComponent<LitUccInteractionBridge>()?.GetInteractionBlockReason() +
                " candidate=" + evaluate?.Invoke(lab.Player, args) + " current=" + RuntimeOutlineSelectionManager.ActiveInteractable +
                " mode=" + InputModeCoordinator.CurrentMode + " suspended=" + RuntimeOutlineSelectionManager.IsSuspended;
            Vector3 origin = lab.Player.GetInteractionOriginWorldPosition();
            diagnostic += " origin=" + origin + " distance=" + Mathf.Sqrt((float)args[4]) +
                " visible=" + CharacterInteractionDetection.IsInteractionTargetVisibleFromCharacter(station, station.GetInteractionDetectionCollider(), station.transform, origin, lab.Player.transform) +
                " rays=" + string.Join(",", Physics.RaycastAll(origin, (station.transform.position - origin).normalized, Vector3.Distance(origin, station.transform.position), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Select(hit => hit.collider.name));
            File.AppendAllText(Folder + "/Walk.trace", diagnostic + "\n");
            Assert.That(RuntimeOutlineSelectionManager.IsActiveInteractable(station), Is.True, diagnostic);
            // Separate physical presses in real time: three fast frames can fall
            // inside the production router's debounce when the Editor runs fast.
            float neutralUntil = Time.unscaledTime + LocalInputRouter.InputDebounceSeconds + .03f;
            while (Time.unscaledTime < neutralUntil) yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South)); InputSystem.Update();
            for (int i = 0; i < 3; i++) yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
            for (int i = 0; i < 3; i++) yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (recenterObserver != null) { LocalInputRouter.CameraRecenter -= recenterObserver; recenterObserver = null; }
            if (keyboard != null) { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); InputSystem.RemoveDevice(keyboard); keyboard = null; }
            if (pad != null) { InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update(); InputSystem.RemoveDevice(pad); pad = null; }
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }

        private Vector2 ToScreenInput(Vector2 world)
        {
            if (!LitGameplayCameraModeController.TryGetTacticalMovement(lab.Player.transform, out var mode)) return world;
            Vector2 right = mode.ResolveScreenMoveInput(Vector2.right);
            Vector2 forward = mode.ResolveScreenMoveInput(Vector2.up);
            return new Vector2(Vector2.Dot(world, right), Vector2.Dot(world, forward));
        }
    }

    [InitializeOnLoad]
    public sealed class FractureGrayboxTestRunner : ICallbacks
    {
        private static TestRunnerApi api;
        private const string Running = "Lit.Fracture.TestsRunning";
        static FractureGrayboxTestRunner()
        {
            EditorApplication.update += Poll;
            if (SessionState.GetBool(Running, false)) Bind();
        }
        private static void Bind() { api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new FractureGrayboxTestRunner()); }
        private static void Poll()
        {
            const string request = "Library/FractureLabyrinthValidation/Tests.request";
            if (api != null || SessionState.GetBool(Running, false) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
            string command = File.ReadAllText(request).Trim(); File.Delete(request);
            SessionState.SetString("Lit.Fracture.TestsOutput", command == "walk" ? "WalkTests.xml" : command == "camera" ? "CameraTests.xml" : command == "preview" ? "PreviewTests.xml" : "EditTests.xml");
            SessionState.SetBool(Running, true); Bind();
            api.Execute(new ExecutionSettings(new Filter { testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
                groupNames = command == "walk" ? new[] { "^Lit.Fractures.Editor.FractureGrayboxPlayTests.WalkSolve" } : command == "camera" ?
                    new[] { "^Lit.Fractures.Editor.FractureGrayboxPlayTests.GameplayCamera" } : command == "preview" ?
                    new[] { "^Lit.Fractures.Editor.FractureGrayboxPlayTests.PreviewSpawn" } : new[] { "^Lit.Fractures.Editor.FractureGrayboxTests", "^FractureAttemptTests" }
            }) { runSynchronously = command == "edit" });
        }
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Library/FractureLabyrinthValidation/" + SessionState.GetString("Lit.Fracture.TestsOutput", "Tests.xml"));
            SessionState.SetBool(Running, false); api.UnregisterCallbacks(this); UnityEngine.Object.DestroyImmediate(api); api = null;
        }
    }
}
