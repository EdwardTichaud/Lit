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
internal static class PlayerSittingPlayProbe
{
    private const string Key = "Lit.PlayerSitting.PlayProbe";
    private const string Folder = "Library/PlayerSitting";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] private sealed class State
    {
        public string status, reason, oldRoot, oldStartScene, sessionId, saveId, manualPath;
        public bool oldDirect, oldMenu, passed;
        public int stage;
        public double started, phaseStarted;
        public List<string> errors = new List<string>();
    }
    private static double readyAt;
    private static State state;
    static PlayerSittingPlayProbe()
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
        SetMove(Vector2.zero);
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
                throw new TimeoutException("Sitting probe stage " + state.stage + ": " + InputModeCoordinator.CurrentMode);
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
                    foreach (string path in new[] { "Assets/Characters/4_Animations/Player_Model.controller", "Assets/Characters/1_Squad/Vesta/Vesta_Model.controller", "Assets/Characters/1_Squad/Soren/Soren_Model.controller", "Assets/Characters/3_Enemy/Iluvilirae/Lucian_ImpactLab.controller" })
                    {
                        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
                        foreach (string name in new[] { "Sit_Down", "Stand_Up" })
                            Require(Mathf.Abs(controller.layers[0].stateMachine.states.First(x => x.state.name == name).state.speed) == 2, path + ": wrong " + name + " speed");
                        Require(controller.layers[0].stateMachine.states.First(x => x.state.name == "Sitting_Idle").state.speed == 1, "Idle was accelerated.");
                    }
                    Require(saves.TryCreateNewGame("Sitting validation", SaveSessionType.Solo, "Sitting", out var slot, out string issue), issue);
                    state.sessionId = slot.sessionId; state.saveId = slot.saveId;
                    saves.SetActiveSave(slot.sessionId, slot.saveId); Require(GameFlowService.StartOrLoadGame(), "New game refused."); Advance(1); break;
                case 1:
                    if (!house || player == null || !player.IsGrounded) return;
                    if (readyAt == 0) readyAt = EditorApplication.timeSinceStartup;
                    foreach (var runner in UnityEngine.Object.FindObjectsByType<StorySequenceRunner>()) if (runner.IsPlaying) { runner.Abort(); readyAt = EditorApplication.timeSinceStartup; }
                    if (player.IsInSittingCycle) { player.TrySetSitting(false); return; }
                    if (InputFocusStack.HasAnyFocus() || EditorApplication.timeSinceStartup - readyAt < 1) return;
                    SetMove(Vector2.zero);
                    Require(player.TrySetSitting(true), "Cannot begin normal sitting.");
                    Require(player.IsSittingTransitionActive && !player.TryToggleSitting(), "An immediate repeated interaction queued a stand-up.");
                    AssertTorchBlocked();
                    SetMove(Vector2.right); Advance(2); break;
                case 2:
                    var animator = (Animator)typeof(SquadCharacterController).GetField("animator", Private).GetValue(player);
                    var info = animator.GetCurrentAnimatorStateInfo(0);
                    if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Sit_Down")) info = animator.GetNextAnimatorStateInfo(0);
                    if (info.shortNameHash == Animator.StringToHash("Sit_Down"))
                    { Require(Mathf.Abs(info.speed) == 2, "Runtime sit speed is not doubled."); }
                    SetMove(Vector2.right);
                    Require(player.IsSittingRequested, "Movement during sitting down queued a stand-up.");
                    if (player.IsSittingTransitionActive) { AssertTorchBlocked(); return; }
                    Advance(3); break;
                case 3:
                    SetMove(Vector2.right);
                    Require(player.IsSittingRequested && !player.IsSittingTransitionActive, "Held stick stood up after the animation.");
                    if (EditorApplication.timeSinceStartup - state.phaseStarted < .35) return;
                    SetMove(Vector2.zero); Advance(4); break;
                case 4:
                    if (EditorApplication.timeSinceStartup - state.phaseStarted < .2) return;
                    Require(player.IsSittingRequested, "Release should not request standing.");
                    SetMove(Vector2.right); Advance(5); break;
                case 5:
                    if (player.IsSittingRequested) return;
                    Require(player.IsSittingTransitionActive && !player.TryToggleSitting(), "Interaction during stand-up started another sit.");
                    AssertTorchBlocked(); SetMove(Vector2.zero); Advance(6); break;
                case 6:
                    var standingAnimator = (Animator)typeof(SquadCharacterController).GetField("animator", Private).GetValue(player);
                    var standing = standingAnimator.GetCurrentAnimatorStateInfo(0);
                    if (standingAnimator.IsInTransition(0) && standingAnimator.GetNextAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Stand_Up")) standing = standingAnimator.GetNextAnimatorStateInfo(0);
                    if (standing.shortNameHash == Animator.StringToHash("Stand_Up"))
                    { Require(Mathf.Abs(standing.speed) == 2, "Runtime stand speed is not doubled."); }
                    if (player.IsInSittingCycle) { Require(!player.TryToggleSitting(), "Stand-up interaction accepted."); AssertTorchBlocked(); return; }
                    Require(!player.IsMovementInputSuppressed, "Movement lock leaked after standing.");
                    Require(player.CanToggleSitting && player.TrySetSitting(true), "Sitting cannot be entered again."); Advance(7); break;
                case 7:
                    if (player.IsSittingTransitionActive) return;
                    Require(player.TryToggleSitting(), "Fresh seated interaction cannot stand up."); Advance(8); break;
                case 8:
                    if (player.IsInSittingCycle) return;
                    Finish(true, "Four player controllers verified: sit/stand speed x2, idle unchanged. Real Lucian/UCC: transition movement and torch discarded, held stick stays seated, release/new gesture stands, repeated interaction ignored, later seated interaction works, movement lock released. Saves isolated; no scene saved or input takeover."); break;
            }
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    private static void SetMove(Vector2 value) => typeof(LocalInputRouter).GetMethod("SetMoveValue", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { value });
    private static void AssertTorchBlocked()
    {
        bool received = false;
        System.Action<UnityEngine.InputSystem.InputAction.CallbackContext> handler = _ => received = true;
        LocalInputRouter.ToggleTorch += handler;
        try { typeof(LocalInputRouter).GetMethod("RaiseToggleTorch", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { default(UnityEngine.InputSystem.InputAction.CallbackContext) }); }
        finally { LocalInputRouter.ToggleTorch -= handler; }
        Require(!received, "Gameplay press during transition was dispatched.");
    }
    private static T ObjectFind<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
}
