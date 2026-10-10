using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ultrabolt.BrainsAI;

/// <summary>Batch-only behavioral smoke test. Never takes over the developer's open Editor.</summary>
[InitializeOnLoad]
public static class IluviliraePlayModeValidation
{
    private const string RunningKey = "Iluvilirae.BatchSmoke";
    private static IluviliraeTestArena arena;
    private static int stage;
    private static double stageStarted;
    private static Vector3 origin, frozenPosition;
    private static Quaternion frozenRotation;
    private static bool sawAttack;
    private static int healthAfterHurt;
    static IluviliraePlayModeValidation()
    {
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("This smoke test requires a separate batch Editor.");
        IluviliraeSetup.Validate();
        RunScene(IluviliraeSetup.Folder + "/Iluvilirae_Perception_Test.unity", "IluviliraeSmoke.result");
    }

    public static void RunScene(string scenePath, string resultPath)
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("This smoke test requires a separate batch Editor.");
        EditorSceneManager.OpenScene(scenePath);
        SessionState.SetString("Iluvilirae.SmokeResult", resultPath);
        SessionState.SetBool(RunningKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(RunningKey, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (arena == null)
            {
                arena = UnityEngine.Object.FindAnyObjectByType<IluviliraeTestArena>();
                Require(arena != null && arena.enemy != null, "Arena/brain missing.");
                origin = arena.enemy.transform.position;
                Next(1);
            }
            var enemy = arena.enemy;
            double elapsed = EditorApplication.timeSinceStartup - stageStarted;
            switch (stage)
            {
                case 1:
                    if (elapsed < 2) return;
                    Require(enemy.NavigationReady, "NavMesh attachment failed.");
                    Require(enemy.fov.CurrentTarget == arena.target, "Lit vision did not feed Brains AI.");
                    Require(Vector3.Distance(origin, enemy.transform.position) > 1, "Brains AI did not chase.");
                    healthAfterHurt = enemy.CurrentHealth - 10;
                    enemy.TakeDamage(10);
                    frozenPosition = enemy.transform.position;
                    Next(2);
                    break;
                case 2:
                    if (elapsed < 0.3) return;
                    Require(enemy.CurrentHealth == healthAfterHurt, "Hurt health wrong.");
                    Require(enemy.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Hurt"), "Hurt animation missing.");
                    Require(Vector3.Distance(frozenPosition, enemy.transform.position) < 0.2f,
                        "Enemy moved during hurt: " + frozenPosition + " -> " + enemy.transform.position + "; velocity=" + enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().velocity);
                    Next(3);
                    break;
                case 3:
                    if (elapsed < 1.5) return;
                    arena.target.gameObject.SetActive(false);
                    Next(4);
                    break;
                case 4:
                    if (elapsed < 0.3) return;
                    Require(enemy.fov.CurrentTarget == null && enemy.fov.status == FOV.Status.Investigate, "Lost target did not enter investigation.");
                    arena.target.position = enemy.transform.position + enemy.transform.forward * 1.5f;
                    arena.target.gameObject.SetActive(true);
                    Next(5);
                    break;
                case 5:
                    var attackState = enemy.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0);
                    sawAttack |= attackState.IsName("Attack") || attackState.IsTag("Attack");
                    if (elapsed < 5) return;
                    Require(sawAttack, "Brains AI never played attack.");
                    enemy.TakeDamage(enemy.CurrentHealth);
                    frozenPosition = enemy.transform.position;
                    frozenRotation = enemy.transform.rotation;
                    Next(6);
                    break;
                case 6:
                    if (elapsed < 0.5) return;
                    Require(enemy.IsDead && enemy.CurrentHealth == 0, "Death health wrong.");
                    Require(enemy.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Death"), "Death animation missing.");
                    Require(!enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled, "Dead agent still active.");
                    Require(Vector3.Distance(frozenPosition, enemy.transform.position) < 0.01f && Quaternion.Angle(frozenRotation, enemy.transform.rotation) < 0.1f,
                        "Dead enemy moved or turned.");
                    Next(7);
                    break;
                case 7:
                    if (elapsed < 4.2) return;
                    Require(!enemy.gameObject.activeSelf, "Dead enemy did not deactivate.");
                    Finish(true, "PASS: attachment, shared perception, pursuit, hurt pause, target loss, attack, death, frozen motion and deactivation.");
                    break;
            }
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Next(int next) { stage = next; stageStarted = EditorApplication.timeSinceStartup; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Finish(bool success, string result)
    {
        SessionState.SetBool(RunningKey, false);
        File.WriteAllText(SessionState.GetString("Iluvilirae.SmokeResult", "IluviliraeSmoke.result"), result);
        if (success) Debug.Log(result); else Debug.LogError(result);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
