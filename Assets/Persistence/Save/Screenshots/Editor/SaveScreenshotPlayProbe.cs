using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lit.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Opt-in functional smoke. No focus/keyboard takeover, no authored scene save, saves isolated under Library.
[InitializeOnLoad]
internal static class SaveScreenshotPlayProbe
{
    private const string Key = "Lit.SaveScreenshot.PlayProbe";
    private const string Folder = "Library/SaveScreenshots";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] private sealed class State
    {
        public string status, reason, oldRoot, oldStartScene, sessionId, saveId, manualPath;
        public bool oldDirect, oldMenu, passed;
        public int stage;
        public double started, phaseStarted;
        public List<string> errors = new List<string>();
    }
    private static GameObject marker, overlay;
    private static Material markerMaterial;
    private static byte[] firstPhoto;
    private static State state;
    static SaveScreenshotPlayProbe()
    {
        string saved = SessionState.GetString(Key, "");
        if (!string.IsNullOrEmpty(saved)) state = JsonUtility.FromJson<State>(saved);
        EditorApplication.update += Poll;
        Application.logMessageReceived += Observe;
    }
    private static void Observe(string text, string trace, LogType type)
    {
        if (state != null && state.status != "Done" && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            state.errors.Add(text);
    }
    private static void Write()
    {
        var json = JsonUtility.ToJson(state, true);
        SessionState.SetString(Key, json);
        File.WriteAllText(Folder + "/PlayProbe.json", json);
    }
    private static T EditorComponent<T>() where T : Component => Enumerable.Range(0, SceneManager.sceneCount)
        .Select(SceneManager.GetSceneAt).Where(scene => scene.name == "Bootstrap" && !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene)).SelectMany(scene => scene.GetRootGameObjects())
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
    private static void Begin()
    {
        var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
        if (!setup.Any(scene => Path.GetFileNameWithoutExtension(scene.path) == "Bootstrap") ||
            setup.Any(scene => Path.GetFileNameWithoutExtension(scene.path) != "Bootstrap" && !scene.path.StartsWith("Assets/Scenes/Maison/", StringComparison.Ordinal)))
        {
            File.WriteAllText(Folder + "/PlayProbe.result", "NOT RUN: requires Bootstrap with optional Maison scenes, preserving the current setup: " +
                string.Join("; ", setup.Select(scene => scene.path)));
            return;
        }
        var saves = EditorComponent<SaveSessionManager>(); var flow = EditorComponent<GameFlowService>();
        var saved = new SerializedObject(saves); var config = new SerializedObject(flow);
        state = new State { status = "Launching", started = EditorApplication.timeSinceStartup, phaseStarted = EditorApplication.timeSinceStartup,
            oldRoot = saved.FindProperty("savesRootFolder").stringValue, oldDirect = config.FindProperty("editorStartGameplayDirectly").boolValue,
            oldMenu = config.FindProperty("loadMenuAfterBootstrap").boolValue,
            oldStartScene = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene != null ? AssetDatabase.GetAssetPath(UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene) : "" };
        Directory.CreateDirectory(Folder + "/PlaySaves");
        saved.FindProperty("savesRootFolder").stringValue = Path.GetFullPath(Folder + "/PlaySaves"); saved.ApplyModifiedPropertiesWithoutUndo();
        config.FindProperty("editorStartGameplayDirectly").boolValue = false;
        config.FindProperty("loadMenuAfterBootstrap").boolValue = true; config.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
        Write(); EditorApplication.isPlaying = true;
    }
    private static void Advance(int stage) { state.stage = stage; state.phaseStarted = EditorApplication.timeSinceStartup; Write(); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Finish(bool passed, string reason)
    {
        if (marker != null) UnityEngine.Object.Destroy(marker);
        if (overlay != null) UnityEngine.Object.Destroy(overlay);
        if (markerMaterial != null) UnityEngine.Object.Destroy(markerMaterial);
        state.passed = passed; state.reason = reason; state.status = "Stopping"; Write(); EditorApplication.isPlaying = false;
    }
    private static void Restore()
    {
        var saves = new SerializedObject(EditorComponent<SaveSessionManager>());
        saves.FindProperty("savesRootFolder").stringValue = state.oldRoot; saves.ApplyModifiedPropertiesWithoutUndo();
        var flow = new SerializedObject(EditorComponent<GameFlowService>());
        flow.FindProperty("editorStartGameplayDirectly").boolValue = state.oldDirect;
        flow.FindProperty("loadMenuAfterBootstrap").boolValue = state.oldMenu; flow.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(state.oldStartScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.oldStartScene);
        state.status = "Done"; Write();
        File.WriteAllText(Folder + "/PlayProbe.result", (state.passed ? "PASS: " : "FAIL: ") + state.reason + "\nRuntime errors observed: " + state.errors.Count + "; see PlayProbe.json. Editor configuration restored; no scene saved.");
        SessionState.EraseString(Key); state = null;
    }
    private static void Poll()
    {
        if (state == null)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder + "/Play.request")) return;
            File.Delete(Folder + "/Play.request"); Begin(); return;
        }
        if (state.status == "Stopping") { if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore(); return; }
        if (!EditorApplication.isPlaying) return;
        try
        {
            Application.runInBackground = true;
            if (EditorApplication.timeSinceStartup - state.started > 360 || EditorApplication.timeSinceStartup - state.phaseStarted > 100)
                throw new TimeoutException("Screenshot probe stage " + state.stage + ": " + InputModeCoordinator.CurrentMode);
            var flow = GameFlowService.Instance; var saves = SaveSessionManager.Instance;
            if (flow == null || saves == null) return;
            Require(Path.GetFullPath(saves.GetSavesRoot()) == Path.GetFullPath(Folder + "/PlaySaves"), "Isolated save root was not applied.");
            bool house = SceneManager.GetActiveScene().name == "Maison_Core" && !flow.IsTransitioning && !LoadingScreenService.IsLoading;
            var playerRoot = LocalPlayerContext.LocalCharacterRoot;
            var player = playerRoot != null ? playerRoot.GetComponent<SquadCharacterController>() : null;
            switch (state.stage)
            {
                case 0:
                    if (flow.IsTransitioning || !SceneManager.GetSceneByName("MainMenu").isLoaded) return;
                    Require(saves.TryCreateNewGame("Screenshot validation", SaveSessionType.Solo, "Photo", out var slot, out string issue), issue);
                    state.sessionId = slot.sessionId; state.saveId = slot.saveId;
                    saves.SetActiveSave(slot.sessionId, slot.saveId); Require(GameFlowService.StartOrLoadGame(), "New game refused."); Advance(1); break;
                case 1:
                    if (!house || player == null || !player.IsGrounded) return;
                    foreach (var runner in UnityEngine.Object.FindObjectsByType<StorySequenceRunner>()) if (runner.IsPlaying) runner.Abort();
                    var vigil = ObjectFind<MaisonHearthVigil>();
                    if (vigil != null) StorySequenceCompletionStore.MarkCompleted(vigil.prerequisite);
                    var camera = Camera.main; Require(camera != null, "Gameplay camera absent.");
                    marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    marker.name = "Screenshot color fixture"; marker.transform.SetParent(camera.transform, false);
                    marker.transform.localPosition = new Vector3(0, 0, 1); marker.transform.localRotation = Quaternion.identity;
                    marker.transform.localScale = new Vector3(8, 8, 1);
                    UnityEngine.Object.Destroy(marker.GetComponent<Collider>());
                    markerMaterial = new Material(Shader.Find("HDRP/Unlit")); markerMaterial.SetColor("_UnlitColor", Color.green);
                    marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
                    overlay = new GameObject("Excluded screenshot overlay", typeof(RectTransform), typeof(Canvas));
                    overlay.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    overlay.GetComponent<Canvas>().sortingOrder = 32767;
                    var panel = new GameObject("Opaque red UI", typeof(RectTransform), typeof(UnityEngine.UI.Image)); panel.transform.SetParent(overlay.transform, false);
                    var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero; panel.GetComponent<UnityEngine.UI.Image>().color = Color.red;
                    var pause = ObjectFind<PausePanelController>(); Require(pause != null, "Pause save controller absent.");
                    pause.UI_Save();
                    var store = CharacterStateStore.Instance;
                    Require(store != null && store.LastSaveSucceeded && store.LastScreenshotSucceeded && !store.IsSaving, "Manual save/photo failed or stayed pending.");
                    Require(saves.CurrentSaveId != state.saveId, "Manual action did not create its own slot.");
                    state.manualPath = saves.GetActiveSaveFilePath("screenshot.png");
                    firstPhoto = File.ReadAllBytes(state.manualPath); CheckPhoto(firstPhoto, true);
                    Require(overlay.GetComponent<Canvas>().enabled, "UI canvas was not restored after capture.");
                    Advance(2); break;
                case 2:
                    markerMaterial.SetColor("_UnlitColor", Color.blue);
                    CharacterStateStore.Instance.Save();
                    Require(CharacterStateStore.Instance.LastSaveSucceeded && CharacterStateStore.Instance.LastScreenshotSucceeded, "Automatic save/photo failed.");
                    var updated = File.ReadAllBytes(state.manualPath);
                    Require(!firstPhoto.SequenceEqual(updated), "Overwritten slot kept its old screenshot.");
                    CheckPhoto(updated, false);
                    Require(overlay.GetComponent<Canvas>().enabled, "Overlay state changed after the automatic save.");
                    Require(GameFlowService.QuitToMainMenu(), "Automatic departure save refused."); Advance(3); break;
                case 3:
                    if (flow.IsTransitioning || !SceneManager.GetSceneByName("MainMenu").isLoaded) return;
                    var latest = Directory.GetFiles(Path.GetFullPath(Folder + "/PlaySaves"), "screenshot.png", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).First();
                    Require(Path.GetFullPath(latest) != Path.GetFullPath(state.manualPath), "Departure did not capture its own automatic slot.");
                    CheckPhoto(File.ReadAllBytes(latest), false);
                    var menu = ObjectFind<MainMenuController>(); Require(menu != null, "Load menu absent.");
                    var selected = saves.Sessions.SelectMany(session => session.saves).First(save => Path.GetFullPath(save.directoryPath) == Path.GetDirectoryName(latest));
                    menu.UI_ShowLoadMenu();
                    typeof(MainMenuController).GetMethod("ShowSaveDetails", Private).Invoke(menu, new object[] { selected });
                    var preview = (UnityEngine.UI.RawImage)typeof(MainMenuController).GetField("previewImage", Private).GetValue(menu);
                    Require(preview != null && preview.enabled && preview.texture != null && preview.texture.width == 1280, "Load menu did not bind the selected screenshot.");
                    Advance(4); break;
                case 4:
                    if (EditorApplication.timeSinceStartup - state.phaseStarted < .5) return;
                    var activeMenu = ObjectFind<MainMenuController>();
                    var image = (UnityEngine.UI.RawImage)typeof(MainMenuController).GetField("previewImage", Private).GetValue(activeMenu);
                    Require(image.gameObject.activeInHierarchy, "Selected preview is hidden in the actual load menu.");
                    Finish(true, "Manual save, automatic overwrite and automatic departure saved fresh 1280px gameplay PNGs; red overlay excluded and restored; actual load menu displays the selected photo. Saves isolated; editor config restored; no UI focus/input takeover."); break;
            }
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    private static void CheckPhoto(byte[] png, bool green)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            Require(texture.LoadImage(png), "Photo is not a complete PNG.");
            Require(texture.width == 1280 && texture.height > 0 && texture.height <= 1280 && png.Length < 8 * 1024 * 1024, "Preview exceeds menu limits.");
            Color pixel = texture.GetPixel(texture.width / 2, texture.height / 2);
            Require(green ? pixel.g > pixel.r + .05f && pixel.g > pixel.b + .05f : pixel.b > pixel.r + .05f && pixel.b > pixel.g + .05f,
                "Photo is not the current gameplay camera color, or contains overlay UI: " + pixel);
        }
        finally { UnityEngine.Object.Destroy(texture); }
    }
    private static T ObjectFind<T>() where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>();
}
