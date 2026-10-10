using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

/// <summary>Isolated-editor validation against real actors; no mock combat contracts.</summary>
[InitializeOnLoad]
public static class LucianFluidCombatValidation
{
    private const string Running = "Lit.FluidCombatValidation";
    private static LucianBrainsCombatArena arena;
    private static Gamepad pad;
    private static int stage;
    private static double started;
    private static float cooldown;
    private static Vector3 enemyPosition;
    private static Vector3 movementStart;
    private static int directionIndex;
    private static bool movementPrimed;
    private static readonly Vector2[] Directions = { Vector2.right, new Vector2(1,1).normalized, Vector2.up, new Vector2(-1,1).normalized,
        Vector2.left, new Vector2(-1,-1).normalized, Vector2.down, new Vector2(1,-1).normalized };
    static LucianFluidCombatValidation() { EditorApplication.update += Tick; }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in an isolated batch project.");
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying) return;
        if (arena == null) { arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>(); started = EditorApplication.timeSinceStartup; }
        if (arena == null || arena.Player == null || arena.Enemy == null) return;
        double elapsed = EditorApplication.timeSinceStartup - started;
        try
        {
            if (elapsed > 20) throw new Exception("Validation stage timed out: " + stage);
            switch (stage)
            {
                case 0:
                    if (elapsed < .5) return;
                    Require(arena.Enemy.combatProfile != null, "Combat profile missing.");
                    Require(arena.Enemy.NavigationReady, "Navigation not ready.");
                    pad = InputSystem.AddDevice<Gamepad>();
                    arena.Enemy.canAttack = false;
                    arena.Enemy.ReceiveImpact(new CombatImpact(5, 20, CombatImpactOrigin.Basic));
                    Require(arena.Enemy.ResistanceDamage == 20 && GetFloat("hurtRemaining") == 0, "Small hit incorrectly interrupts.");
                    arena.Enemy.ReceiveImpact(new CombatImpact(10, 30, CombatImpactOrigin.Basic));
                    arena.Enemy.ReceiveImpact(new CombatImpact(15, 50, CombatImpactOrigin.Basic));
                    Require(arena.Enemy.ResistanceDamage == 0 && GetFloat("hurtRemaining") > 0, "Combo did not break resistance.");
                    arena.Enemy.ReceiveImpact(new CombatImpact(1, 100, CombatImpactOrigin.Basic));
                    Require(arena.Enemy.ResistanceDamage == 0, "Protected enemy accumulated a second interruption.");
                    typeof(LitBrainsEnemy).GetField("fluidCooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena.Enemy, 2.4f);
                    cooldown = arena.Enemy.AttackCooldownRemaining;
                    Next(1); break;
                case 1:
                    if (elapsed < .5) return;
                    Require(GetFloat("hurtRemaining") == 0, "Short reaction failed to release.");
                    Require(arena.Enemy.AttackCooldownRemaining < cooldown, "Cooldown froze during reaction/pursuit.");
                    arena.Enemy.ReceiveImpact(new CombatImpact(1, 100, CombatImpactOrigin.Counter));
                    Require(GetFloat("hurtRemaining") > 0, "Counter did not bypass protection.");
                    arena.SetPaused(true); cooldown = arena.Enemy.AttackCooldownRemaining;
                    Next(2); break;
                case 2:
                    if (elapsed < .25) return;
                    Require(Mathf.Abs(arena.Enemy.AttackCooldownRemaining - cooldown) < .01f, "Pause advanced attack timer.");
                    arena.SetPaused(false);
                    arena.Player.GetComponent<LitOpsiveLocomotionBridge>().SetExternalPositionAndRotation(arena.Enemy.transform.position + Vector3.forward * 3.5f, Quaternion.Euler(0, 180, 0), true);
                    arena.Enemy.canAttack = true;
                    typeof(LitBrainsEnemy).GetField("fluidCooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena.Enemy, 0f);
                    arena.Enemy.AttackImpact += VerifyDuplicateImpact;
                    Next(3); break;
                case 3:
                    if (arena.Enemy.AttackPhase != LitBrainsAttackPhase.Preparation) return;
                    Require(Mathf.Abs(Time.timeScale - 1) < .01f, "Ordinary attack slowed global time.");
                    enemyPosition = arena.Enemy.transform.position;
                    Next(4); break;
                case 4:
                    if (arena.Enemy.AttackPhase != LitBrainsAttackPhase.Recovery) return;
                    Require(Time.timeScale > .99f, "Ordinary contact slowed global time.");
                    float advance = Vector3.Distance(enemyPosition, arena.Enemy.transform.position);
                    var attack = arena.Enemy.ActiveAttack;
                    Require(advance <= attack.preparationAdvanceDistance + attack.advanceDistance + .05f, "Attack exceeded authored advance.");
                    Require(advance > .6f, "Enemy failed to close from engagement range during its attack: advance=" + advance + " gap=" + Vector3.Distance(arena.Player.transform.position, arena.Enemy.transform.position));
                    arena.Enemy.ReceiveImpact(new CombatImpact(1, 100, CombatImpactOrigin.Counter));
                    Require(!arena.Enemy.IsAttackCommitted, "Cancelled attack remained eligible during its animation fade.");
                    arena.Enemy.canAttack = false;
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    Next(5); break;
                case 5:
                    if (!movementPrimed)
                    {
                        if (arena.Player.GetComponent<PlayerActionPresentationController>().IsActionActive) return;
                        movementPrimed = true;
                        movementStart = arena.Player.transform.position;
                        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Directions[0] });
                        Next(5); return;
                    }
                    if (elapsed < .35) return;
                    Require(Vector3.Distance(movementStart, arena.Player.transform.position) > .03f, "Direction did not move Lucian: " + directionIndex);
                    directionIndex++;
                    if (directionIndex >= Directions.Length)
                    {
                        InputSystem.QueueStateEvent(pad, new GamepadState()); Next(6);
                    }
                    else
                    {
                        movementStart = arena.Player.transform.position;
                        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Directions[directionIndex] }); Next(5);
                    }
                    break;
                case 6:
                    if (elapsed < .2) return;
                    Require(RealTimeCombatManager.Instance.TryUseSkill(arena.basicSkills[0]), "Basic attack unavailable for buffer validation.");
                    arena.GetComponent<CombatMobilityController>().RequestDodge();
                    Require(arena.GetComponent<CombatMobilityController>().HasPendingCommand, "Early dodge was lost instead of buffered.");
                    Next(7); break;
                case 7:
                    if (elapsed < .2) return;
                    Require(!arena.GetComponent<CombatMobilityController>().HasPendingCommand, "Expired dodge survived its 150 ms lifetime.");
                    Require(!arena.Player.GetComponent<PlayerScriptedDodgeController>().IsActive, "Expired dodge executed later.");
                    var presentation = arena.Player.GetComponent<PlayerActionPresentationController>();
                    var state = arena.Player.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0);
                    if (state.normalizedTime < arena.basicSkills[0].presentation.mobilityCancelNormalizedTime - .07f) return;
                    Require(RealTimeCombatManager.Instance.TryUseSkill(arena.basicSkills[1]), "Next basic attack could not be buffered.");
                    arena.GetComponent<CombatMobilityController>().RequestDodge();
                    Require(presentation.CanAcceptBasicSkillInput, "Dodge did not discard the pending combo.");
                    Next(8); break;
                case 8:
                    if (!arena.Player.GetComponent<PlayerScriptedDodgeController>().IsActive) return;
                    Require(!arena.GetComponent<CombatMobilityController>().HasPendingCommand, "Executed dodge remained pending.");
                    arena.RestartEncounter();
                    Require(!arena.Enemy.IsDead && arena.Enemy.CurrentHealth == 300, "Restart retained previous encounter state.");
                    Finish("PASS resistance, protection, counter priority, cooldown continuity, pause, normal attack time, authored advance, duplicate event rejection, eight gamepad directions, dodge buffer expiry/execution/priority, restart", 0);
                    break;
            }
        }
        catch (Exception e) { Debug.LogException(e); Finish("FAIL " + e, 1); }
    }

    private static float GetFloat(string name) => (float)typeof(LitBrainsEnemy).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena.Enemy);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Next(int next) { stage = next; started = EditorApplication.timeSinceStartup; }
    private static void Finish(string message, int exitCode)
    {
        SessionState.SetBool(Running, false);
        File.WriteAllText(Path.Combine(Application.dataPath, "../Library/FluidCombatValidation.result"), message);
        if (pad != null) InputSystem.RemoveDevice(pad);
        Debug.Log(message);
        EditorApplication.Exit(exitCode);
    }

    private static void VerifyDuplicateImpact(AnimationEvent source)
    {
        int health = arena.Player.CurrentHp;
        arena.Enemy.GetComponent<AnimationEvents>().ResolveBrainsAttackImpact(source);
        Require(arena.Player.CurrentHp == health, "Duplicate event applied extra damage.");
    }

    public static void CaptureAttackFrames()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated rendering project.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(JuggernautV2Setup.PrefabPath));
        var animator = actor.GetComponent<Animator>();
        animator.Rebind(); animator.Update(0);
        Debug.Log("Preview avatar human=" + animator.isHuman + " valid=" + animator.avatar.isValid);
        foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
        var pipeline = GraphicsSettings.defaultRenderPipeline;
        var quality = QualitySettings.renderPipeline;
        GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        var cameraRoot = new GameObject("Preview", typeof(Camera));
        var camera = cameraRoot.GetComponent<Camera>();
        camera.backgroundColor = new Color(.12f, .14f, .18f); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.transform.position = new Vector3(4, 2.5f, 4);
        camera.transform.LookAt(new Vector3(0, 1.1f, 0));
        camera.fieldOfView = 35;
        var render = new RenderTexture(640, 640, 24); camera.targetTexture = render;
        RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
        var keyLight = new GameObject("PreviewKey", typeof(Light));
        keyLight.GetComponent<Light>().type = LightType.Directional;
        keyLight.GetComponent<Light>().intensity = 1.5f;
        keyLight.transform.rotation = Quaternion.Euler(40, -35, 0);
        foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var material = new Material(Shader.Find("Standard"));
                material.color = renderer.name.ToLowerInvariant().Contains("sword") ? Color.cyan : new Color(.6f, .65f, .7f);
                material.SetFloat("_Glossiness", .15f);
                materials[i] = material;
            }
            renderer.sharedMaterials = materials;
        }
        string output = "Library/FluidCombatFrames"; Directory.CreateDirectory(output);
        var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
        var bakedMeshes = new Mesh[skins.Length];
        for (int i = 0; i < skins.Length; i++)
        {
            // Multiple previews are rendered in one editor frame. Bake explicitly
            // rather than reusing the GPU skinning cache of that first frame.
            bakedMeshes[i] = new Mesh();
            var preview = new GameObject("BakedPreview", typeof(MeshFilter), typeof(MeshRenderer));
            preview.transform.SetParent(skins[i].transform, false);
            Vector3 scale = skins[i].transform.lossyScale;
            preview.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            preview.GetComponent<MeshFilter>().sharedMesh = bakedMeshes[i];
            preview.GetComponent<MeshRenderer>().sharedMaterials = skins[i].sharedMaterials;
            skins[i].enabled = false;
        }
        try
        {
            var profile = AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(LucianBrainsCombatSetup.FluidProfilePath);
            foreach (var attack in profile.attacks)
                for (float time = 0; time < attack.clip.length; time += .1f)
                {
                    animator.Play(attack.stateName, 0, time / attack.clip.length); animator.Update(0);
                    for (int i = 0; i < skins.Length; i++) skins[i].BakeMesh(bakedMeshes[i]);
                    Debug.Log($"Pose {attack.stateName} {time:F2} arm={animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localEulerAngles} hand={animator.GetBoneTransform(HumanBodyBones.RightHand).localEulerAngles}");
                    camera.Render(); RenderTexture.active = render;
                    var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
                    File.WriteAllBytes(output + "/" + attack.stateName + "_" + time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ".png", image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                }
        }
        finally
        {
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(cameraRoot); UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(render); UnityEngine.Object.DestroyImmediate(keyLight);
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = quality;
            foreach (var mesh in bakedMeshes) UnityEngine.Object.DestroyImmediate(mesh);
            EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log("Attack frame captures written to " + output);
    }
}
