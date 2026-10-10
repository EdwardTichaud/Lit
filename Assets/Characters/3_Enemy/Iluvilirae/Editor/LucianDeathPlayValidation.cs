using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Death presentation regression using the real laboratory actors in an isolated Editor.</summary>
[InitializeOnLoad]
public static class LucianDeathPlayValidation
{
    private const string Key = "Lit.DeathPlayValidation";
    private static LucianBrainsCombatArena arena;
    private static int stage;
    private static double started;
    private static float normalized;
    private static Vector3 position;
    private static Quaternion pose;
    private static Camera capture;
    static LucianDeathPlayValidation() { EditorApplication.update += Tick; }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        Directory.CreateDirectory("Library/DeathValidation");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void Next(int value) { stage = value; started = EditorApplication.timeSinceStartup; }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static Animator PlayerAnimator => arena.Player.GetComponent<Animator>();
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (arena == null) { arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>(); started = EditorApplication.timeSinceStartup; }
            if (arena == null || arena.Player == null || arena.Enemy == null) return;
            double elapsed = EditorApplication.timeSinceStartup - started;
            if (elapsed > 15) throw new InvalidOperationException("Timeout at death stage " + stage);
            switch (stage)
            {
                case 0:
                    if (elapsed < .5 || !arena.Enemy.NavigationReady) return;
                    SetupCapture(); arena.Enemy.canAttack = false;
                    position = arena.Enemy.transform.position;
                    arena.Enemy.Animator.Play("Attack_Sweep", 0, .4f);
                    arena.Enemy.TakeDamage(arena.Enemy.CurrentHealth); Next(1); break;
                case 1:
                    if (elapsed < .4) return;
                    var enemyState = arena.Enemy.Animator.GetCurrentAnimatorStateInfo(0);
                    Require(enemyState.IsName("Death") && enemyState.normalizedTime > .02f, "Enemy death did not interrupt attack.");
                    normalized = enemyState.normalizedTime; pose = arena.Enemy.Animator.GetBoneTransform(HumanBodyBones.Hips).rotation;
                    Save("enemy-early"); Next(2); break;
                case 2:
                    if (elapsed < 1) return;
                    Require(arena.Enemy.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime > normalized + .15f, "Enemy death keeps restarting.");
                    Require(Quaternion.Angle(pose, arena.Enemy.Animator.GetBoneTransform(HumanBodyBones.Hips).rotation) > 1, "Enemy death pose did not animate.");
                    Require(Vector3.Distance(position, arena.Enemy.transform.position) < .03f, "Dead enemy moved.");
                    Save("enemy-falling"); Next(3); break;
                case 3:
                    if (elapsed < 3.2) return;
                    Require(!arena.Enemy.gameObject.activeSelf, "Enemy did not deactivate after death clip.");
                    arena.RestartEncounter(); Next(4); break;
                case 4:
                    if (elapsed < .5 || !arena.Enemy.NavigationReady) return;
                    arena.Enemy.canAttack = false;
                    PlayerAnimator.Play(arena.hurtState, 0, .2f);
                    arena.Player.ApplyDamage(arena.Player.CurrentHp, "Death validation"); Next(5); break;
                case 5:
                    if (elapsed < .4) return;
                    var playerState = PlayerAnimator.GetCurrentAnimatorStateInfo(0);
                    Require(playerState.IsName("Death") && playerState.normalizedTime > .02f, "Lucian death did not interrupt hurt.");
                    normalized = playerState.normalizedTime; pose = PlayerAnimator.GetBoneTransform(HumanBodyBones.Hips).rotation;
                    Save("player-early"); Next(6); break;
                case 6:
                    if (elapsed < 1.1) return;
                    Require(PlayerAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > normalized + .15f, "Lucian death keeps restarting.");
                    Require(Quaternion.Angle(pose, PlayerAnimator.GetBoneTransform(HumanBodyBones.Hips).rotation) > 1, "Lucian death pose did not animate.");
                    Require(arena.Player.CurrentHp == 0 && !arena.Enemy.enabled, "Defeat did not prevent further combat.");
                    Save("player-falling"); arena.RestartEncounter(); Next(7); break;
                case 7:
                    if (elapsed < .5 || !arena.Enemy.NavigationReady) return;
                    arena.Enemy.canAttack = false;
                    arena.Enemy.deathDisplaySeconds = .1f;
                    arena.Enemy.SetCinematicSuspended(true);
                    arena.Enemy.TakeDamage(arena.Enemy.CurrentHealth); Next(8); break;
                case 8:
                    if (elapsed < 1) return;
                    Require(arena.Enemy.gameObject.activeSelf, "Cinematic death disappeared before graph release.");
                    arena.Enemy.SetCinematicSuspended(false); Next(9); break;
                case 9:
                    if (elapsed < 1) return;
                    Require(arena.Enemy.gameObject.activeSelf && arena.Enemy.Animator.GetCurrentAnimatorStateInfo(0).IsName("Death"),
                        "Cinematic release did not preserve a full death animation despite a short display delay.");
                    Next(10); break;
                case 10:
                    if (elapsed < 3) return;
                    Require(!arena.Enemy.gameObject.activeSelf, "Death did not finish after cinematic release.");
                    arena.RestartEncounter(); Next(11); break;
                case 11:
                    if (elapsed < .5) return;
                    Require(arena.Player.CurrentHp == arena.playerMaxHealth && !arena.Enemy.IsDead &&
                        !arena.Player.GetComponent<PlayerActionPresentationController>().IsDeathAnimationLocked, "Restart retained death state.");
                    Finish("PASS Lucian/Juggernaut death progression and poses, stationary enemy, deactivation after full clip, cinematic handoff, restart", 0); break;
            }
        }
        catch (Exception error) { Finish(error.ToString(), 1); }
    }

    private static void SetupCapture()
    {
        GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (renderer is ParticleSystemRenderer) { renderer.enabled = false; continue; }
            var mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = new Material(Shader.Find("Unlit/Color"));
                mats[i].color = renderer.transform.IsChildOf(arena.Player.transform) ? Color.cyan :
                    renderer.transform.IsChildOf(arena.Enemy.transform) ? new Color(1, .4f, .1f) : Color.gray;
            }
            renderer.sharedMaterials = mats;
        }
        capture = new GameObject("DeathValidationCamera", typeof(Camera)).GetComponent<Camera>();
        capture.orthographic = true; capture.orthographicSize = 2.5f; capture.cullingMask = ~(1 << 5);
        capture.clearFlags = CameraClearFlags.SolidColor; capture.backgroundColor = Color.gray;
    }
    private static void Save(string name)
    {
        Vector3 focus = name.StartsWith("player") ? arena.Player.transform.position : arena.Enemy.transform.position;
        capture.transform.position = focus + new Vector3(4, 2.5f, -4); capture.transform.LookAt(focus + Vector3.up);
        var rt = new RenderTexture(512, 512, 24); capture.targetTexture = rt; capture.Render(); RenderTexture.active = rt;
        var texture = new Texture2D(512, 512, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); texture.Apply();
        File.WriteAllBytes("Library/DeathValidation/" + name + ".png", texture.EncodeToPNG());
        RenderTexture.active = null; capture.targetTexture = null; UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(rt);
    }
    private static void Finish(string result, int code)
    {
        SessionState.SetBool(Key, false); File.WriteAllText("Library/DeathValidation/result.txt", result); Debug.Log(result); EditorApplication.Exit(code);
    }
}
