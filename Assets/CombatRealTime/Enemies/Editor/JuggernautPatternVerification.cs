using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class JuggernautPatternVerification
{
    private const string Folder = "Assets/Characters/3_Enemy/Juggernaut/";
    private static GameObject fixture;
    private static RealTimeCombatEnemy enemy;
    private static int starts, landings;
    private static float began, peak, nextSnapshot;
    private static bool wasAirborne;
    private static NavMeshDataInstance nav;
    static JuggernautPatternVerification()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlay;
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying && !EditorApplication.isCompiling && File.Exists("Library/Juggernaut.verify"))
        {
            File.Delete("Library/Juggernaut.verify");
            try { ValidateAssets(); File.WriteAllText("Library/Juggernaut.verify.result", "PASS assets"); }
            catch (Exception e) { File.WriteAllText("Library/Juggernaut.verify.result", e.ToString()); }
        }
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists("Library/Juggernaut.playtest"))
        {
            File.Move("Library/Juggernaut.playtest", "Library/Juggernaut.playtest.running");
            EditorApplication.isPlaying = true;
        }
        if (enemy == null || !EditorApplication.isPlaying) return;
        var motor = enemy.GetComponent<CombatEnemyPhysicsMotor>();
        if (Time.realtimeSinceStartup >= nextSnapshot)
        {
            nextSnapshot = Time.realtimeSinceStartup + 5f;
            var agent = enemy.GetComponent<NavMeshAgent>();
            var brain = enemy.GetComponent<EnemyCombatBrain>();
            var target = brain.Target;
            File.WriteAllText("Library/Juggernaut.playtest.snapshot", "phase=" + brain.Phase +
                " physics=" + motor.State + " pos=" + enemy.transform.position + " time=" + Time.timeScale +
                " target=" + (target == null ? "null" : target.transform.position.ToString()) +
                " nav=" + agent.enabled + " update=" + agent.updatePosition + " speed=" + agent.speed +
                (agent.isOnNavMesh ? " stopped=" + agent.isStopped + " velocity=" + agent.velocity + " desired=" + agent.desiredVelocity + " path=" + agent.pathStatus + " pending=" + agent.pathPending + " destination=" + agent.destination : " offMesh"));
        }
        peak = Mathf.Max(peak, enemy.transform.position.y);
        if (wasAirborne && !motor.IsAirborne) landings++;
        wasAirborne = motor.IsAirborne;
        if (landings >= 10) Finish("PASS runtime: " + starts + " attaques, " + landings + " atterrissages; sommet=" + peak);
        else if (Time.realtimeSinceStartup - began > 120f) Finish("FAIL timeout: starts=" + starts + " landings=" + landings + " phase=" + enemy.GetComponent<EnemyCombatBrain>().Phase + " physics=" + motor.State + " position=" + enemy.transform.position);
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists("Library/Juggernaut.playtest.running"))
        {
            try { CreateFixture(); }
            catch (Exception e) { Finish(e.ToString()); }
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (nav.valid) nav.Remove();
            if (File.Exists("Library/Juggernaut.playtest.running")) File.Delete("Library/Juggernaut.playtest.running");
        }
    }
    private static void CreateFixture()
    {
        fixture = new GameObject("Juggernaut verification (temporary)");
        starts = landings = 0;
        peak = nextSnapshot = 0f;
        wasAirborne = false;
        UnityEngine.Object.DontDestroyOnLoad(fixture);
        fixture.SetActive(false);
        Vector3 origin = new Vector3(10000f, 0f, 10000f);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.SetParent(fixture.transform);
        floor.transform.position = origin + Vector3.down * .5f;
        floor.transform.localScale = new Vector3(50f, 1f, 50f);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Juggernaut_Combat.prefab");
        var settings = NavMesh.GetSettingsByID(prefab.GetComponent<NavMeshAgent>().agentTypeID);
        var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(50f, 1f, 50f), transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one), area = 0 };
        var mesh = NavMeshBuilder.BuildNavMeshData(settings, new System.Collections.Generic.List<NavMeshBuildSource> { source }, new Bounds(origin, new Vector3(55f, 12f, 55f)), Vector3.zero, Quaternion.identity);
        nav = NavMesh.AddNavMeshData(mesh);
        var actor = UnityEngine.Object.Instantiate(prefab, origin, Quaternion.identity, fixture.transform);
        foreach (var network in actor.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true)) network.enabled = false;
        var data = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterData>(Folder + "Juggernaut.asset"));
        data.enableCombatHealthThresholds = false;
        data.hp = 10000;
        var profile = UnityEngine.Object.Instantiate(data.enemyCombatProfile);
        profile.patterns = new System.Collections.Generic.List<EnemyCombatPattern> { new EnemyCombatPattern { name = "Assomoir test", skills = new System.Collections.Generic.List<SkillSO> { data.combatSkills.Last() }, maximumConsecutiveUses = 100, cooldownSeconds = .1f, recoverySeconds = .1f, maximumStartAngle = 180f } };
        profile.guardChance = 0f;
        data.enemyCombatProfile = profile;
        actor.GetComponent<CharacterInfo>().SetCharacterData(data);
        GameObject player = new GameObject("Verification target");
        player.transform.SetParent(fixture.transform);
        player.transform.position = origin + Vector3.forward * 4f;
        player.AddComponent<Rigidbody>().isKinematic = true;
        player.AddComponent<CapsuleCollider>();
        var character = player.AddComponent<SquadCharacterController>();
        foreach (var component in player.GetComponents<MonoBehaviour>()) component.enabled = false;
        fixture.SetActive(true);
        enemy = actor.GetComponent<RealTimeCombatEnemy>();
        enemy.RetaliationStarted += (_, __) => starts++;
        began = Time.realtimeSinceStartup;
        enemy.ReceiveLightDamage(1, character);
    }
    private static void Finish(string result)
    {
        File.WriteAllText("Library/Juggernaut.playtest.result", result);
        if (File.Exists("Library/Juggernaut.playtest.running")) File.Delete("Library/Juggernaut.playtest.running");
        enemy = null;
        EditorApplication.isPlaying = false;
    }
    [MenuItem("Lit/Combat/Validate Juggernaut Patterns")]
    public static void ValidateAssets()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(Folder + "Juggernaut.asset");
        if (data == null || data.combatSkills.Count != 4 || data.enemyCombatProfile.patterns.Count != 4) throw new Exception("4 skills/patterns requis");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Juggernaut_Combat.prefab");
        var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
        if (controller == null || controller.name != "Juggernaut_Model") throw new Exception("controller invalide");
        var machine = controller.layers[0].stateMachine;
        foreach (var skill in data.combatSkills)
        {
            if (!skill.AnimationClip.humanMotion || !machine.states.Any(s => s.state.name == skill.AnimatorState && s.state.motion == skill.AnimationClip)) throw new Exception("binding incorrect: " + skill.name);
            var events = AnimationUtility.GetAnimationEvents(skill.AnimationClip);
            if (events.Count(e => e.functionName == "EndEnemyAttack") != 1) throw new Exception("fin d'attaque incorrecte");
            if (!events.Any(e => e.functionName == "OpenEnemyAttackHitbox" || e.functionName == "ResolveEnemyAttackImpact")) throw new Exception("impact absent");
        }
        RealTimeCombatEnemyBehaviour legacyBehaviour = prefab.GetComponent<RealTimeCombatEnemyBehaviour>();
        if (prefab.GetComponent<EnemyTacticalResponseController>() != null ||
            legacyBehaviour != null) throw new Exception("Composant IA legacy present, meme desactive");
        if (!prefab.GetComponent<CombatEnemyPhysicsMotor>().ScriptedOnly || prefab.GetComponent<Animator>().applyRootMotion)
            throw new Exception("Juggernaut doit utiliser ScriptedOnly sans root motion Animator");
        if (!CombatEnemyRuntimeContract.HasRequiredComponents(prefab)) throw new Exception("contrat physique invalide");
        Debug.Log("[JuggernautPatterns] Validation assets reussie.");
    }
}
