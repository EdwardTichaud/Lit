using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public static partial class CombatBootstrapPlayValidation
{
    private static bool EncounterProbe => Environment.GetCommandLineArgs().Contains("-combatMigrationEncounter");
    private static LitBrainsEnemy encounterEnemy;
    private static Flame encounterFlameFixture;
    private static readonly HashSet<int> encounterImpacts = new HashSet<int>();
    private static readonly HashSet<LitBrainsAttackPhase> encounterPhases = new HashSet<LitBrainsAttackPhase>();
    private static Vector3 encounterOrigin, encounterEnemyOrigin;
    private static Quaternion encounterRotation;
    private static float encounterStarted, encounterPeakSpeed, encounterTravel;
    private static float encounterDiagnosticAt;
    private static int encounterHealth, encounterFrames;
    private static bool encounterStartedProbe, encounterDone, encounterAggro, encounterFacade, encounterBaseEvidenceReported;
    private static string encounterImpactError;

    private static void ResetEncounterProbe()
    {
        CleanupEncounterProbe();
        encounterImpacts.Clear();
        encounterPhases.Clear();
        encounterStartedProbe = encounterDone = encounterAggro = encounterFacade = false;
        encounterBaseEvidenceReported = false;
        encounterPeakSpeed = encounterTravel = 0;
        encounterFrames = 0;
        encounterImpactError = null;
        ResetEncounterLifecycle();
    }

    private static bool ValidateProductionEncounter(RealTimeCombatManager manager, Transform root)
    {
        if (encounterDone) return true;
        var player = root.GetComponent<SquadCharacterController>();
        if (!encounterStartedProbe)
        {
            var marker = UnityEngine.Object.FindObjectsByType<SceneMarker>()
                .Where(candidate => candidate.CharacterData != null &&
                    candidate.CharacterData.UniqueId == "c54e94b14c7826c4482529b6d4d739ec" && candidate.RuntimeInstance != null)
                .OrderBy(candidate => candidate.MarkerId, StringComparer.Ordinal).FirstOrDefault();
            Require(marker != null, "Encounter uses the actual production Juggernaut marker");
            encounterEnemy = marker.RuntimeInstance.GetComponent<LitBrainsEnemy>();
            Require(encounterEnemy != null && encounterEnemy.ValidateRuntimeContract(out _),
                "Production enemy has one configured Brains runtime");
            Require(encounterEnemy.detectionTargetOverride == null && !encounterEnemy.IsDead,
                "Vision probe uses a living authored enemy without a test target override");
            Require(!manager.HasLockedCombatTarget && !manager.IsCombatActive,
                "Encounter starts without manual lock or an existing engagement");
            Debug.Log("[CombatBootstrapEncounter] Initial " + DescribeEncounter());
            if (encounterEnemy.IsFlameDormant && Environment.GetCommandLineArgs().Contains("-combatMigrationEncounterLitFixture"))
                PrepareLitFlameFixture();
            Require(!encounterEnemy.IsFlameDormant,
                "Authored Flame activation prerequisite satisfied; " + DescribeEncounter());
            Require(TryFindEncounterPosition(encounterEnemy, out var position),
                "Player fixture has a local same-level NavMesh path in front of the enemy");
            encounterOrigin = root.position;
            encounterRotation = root.rotation;
            encounterEnemyOrigin = encounterEnemy.Root.position;
            encounterHealth = player.CurrentHp;
            Require(player.TrySetUccExternalPositionAndRotation(position,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(encounterEnemyOrigin - position, Vector3.up)), true),
                "Only the player fixture is placed through the existing UCC adapter");
            encounterEnemy.AttackImpact += RecordEncounterImpact;
            encounterStarted = Time.realtimeSinceStartup;
            encounterDiagnosticAt = encounterStarted + 10;
            encounterStartedProbe = true;
            return false;
        }

        if (encounterEnemy == null || encounterEnemy.IsDead || player == null || player.CurrentHp <= 0)
            throw new InvalidOperationException("Encounter participant disappeared or died. " + DescribeEncounter());
        if (encounterImpactError != null) throw new InvalidOperationException(encounterImpactError);
        if (encounterLifecycleStage > 0) return ValidateEncounterLifecycle(manager, player);
        encounterFrames++;
        encounterAggro |= encounterEnemy.IsEngagedWith(root);
        encounterFacade |= manager.IsCombatActive;
        encounterPeakSpeed = Mathf.Max(encounterPeakSpeed, encounterEnemy.ActualSpeed);
        encounterTravel = Mathf.Max(encounterTravel, Vector3.ProjectOnPlane(encounterEnemy.Root.position - encounterEnemyOrigin, Vector3.up).magnitude);
        encounterPhases.Add(encounterEnemy.AttackPhase);
        if (Time.realtimeSinceStartup >= encounterDiagnosticAt)
        {
            encounterDiagnosticAt = Time.realtimeSinceStartup + 10;
            Debug.Log("[CombatBootstrapEncounter] Observation " + DescribeEncounter());
        }
        if (manager.HasLockedCombatTarget)
            throw new InvalidOperationException("Vision engagement unexpectedly created a manual camera lock. " + DescribeEncounter());

        if (encounterAggro && encounterFacade && encounterPeakSpeed > .1f && encounterTravel > .5f &&
            encounterImpacts.Count >= 2 && encounterPhases.Contains(LitBrainsAttackPhase.Recovery) && player.CurrentHp < encounterHealth)
        {
            if (!encounterBaseEvidenceReported)
            {
                Require(true, "Vision-only aggro reached the common facade without camera lock");
                Require(true, "Production navigation moved the enemy toward Lucian: peak=" + encounterPeakSpeed + " travel=" + encounterTravel);
                Require(true, "At least two distinct attacks reached real impact events and recovery; player health decreased: " + DescribeEncounter());
                encounterBaseEvidenceReported = true;
            }
            if (DefenseProbe && !ValidateDefenseProbe(manager, player)) return false;
            if (Environment.GetCommandLineArgs().Contains("-combatMigrationEncounterLifecycle"))
            {
                BeginEncounterEscape(player, 1);
                return false;
            }
            Require(player.TrySetUccExternalPositionAndRotation(encounterOrigin, encounterRotation, true),
                "Player fixture restored through UCC after the encounter observation");
            CleanupEncounterProbe();
            encounterDone = true;
            return true;
        }
        if (Time.realtimeSinceStartup - encounterStarted > 65)
            throw new TimeoutException("Bounded production encounter did not satisfy its evidence requirements. " + DescribeEncounter());
        return false;
    }

    private static void RecordEncounterImpact(AnimationEvent source)
    {
        if (encounterEnemy == null) return;
        if (!encounterImpacts.Add(encounterEnemy.ActionSequenceId))
            encounterImpactError = "Duplicate production impact for generation " + encounterEnemy.ActionSequenceId;
        Debug.Log("[CombatBootstrapEncounter] impact=" + encounterEnemy.ActionSequenceId + " " + DescribeEncounter());
    }

    private static bool TryFindEncounterPosition(LitBrainsEnemy enemy, out Vector3 position, float[] distances = null, float[] angles = null)
    {
        position = default;
        var world = NavMeshWorldService.Instance;
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (world == null || agent == null) return false;
        Vector3 forward = Vector3.ProjectOnPlane(enemy.Root.forward, Vector3.up).normalized;
        var refusals = new List<string>();
        foreach (float angle in angles ?? new[] { 0f, 20f, -20f, 40f, -40f })
        foreach (float distance in distances ?? new[] { 12f, 10f, 8f, 7f })
        {
            Vector3 expected = enemy.Root.position + Quaternion.AngleAxis(angle, Vector3.up) * forward * distance;
            string label = "angle=" + angle + " distance=" + distance;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            // Select the player's fixture on this walkable surface, not at the
            // enemy root's Y across the room. Enemy spawn tolerances stay intact.
            if (!NavMesh.SamplePosition(expected, out var hit, .35f, filter) ||
                Vector3.ProjectOnPlane(hit.position - expected, Vector3.up).magnitude > .15f ||
                !world.TryValidatePosition(hit.position, agent.areaMask, out _))
            {
                bool found = NavMesh.SamplePosition(expected, out var nearest, 3, agent.areaMask);
                refusals.Add(label + " local projection refused; nearest=" + (found ? nearest.position.ToString() : "absent") + " expected=" + expected);
                continue;
            }
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(enemy.Root.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
            { refusals.Add(label + " path=" + path.status); continue; }
            var vision = enemy.RuntimeCharacterData.vision;
            Vector3 start = enemy.Root.position + Vector3.up * vision.eyeHeight;
            Vector3 end = hit.position + Vector3.up * vision.targetHeight;
            var blockers = Physics.RaycastAll(start, (end - start).normalized, Vector3.Distance(start, end),
                vision.obstructionMask, QueryTriggerInteraction.Ignore).Where(contact =>
                    !contact.transform.IsChildOf(enemy.Root) && contact.transform.GetComponentInParent<SquadCharacterController>() == null);
            if (blockers.Any())
            { refusals.Add(label + " blocked=" + string.Join(",", blockers.Select(contact => contact.collider.name))); continue; }
            Debug.Log("[CombatBootstrapEncounter] Fixture position=" + hit.position + " " + label);
            position = hit.position;
            return true;
        }
        Debug.LogWarning("[CombatBootstrapEncounter] No valid fixture position: " + string.Join(" | ", refusals));
        return false;
    }

    private static string DescribeEncounter()
    {
        var playerRoot = LocalPlayerContext.LocalCharacterRoot;
        var player = playerRoot != null ? playerRoot.GetComponent<SquadCharacterController>() : null;
        var agent = encounterEnemy != null ? encounterEnemy.GetComponent<NavMeshAgent>() : null;
        var clock = encounterEnemy != null ? encounterEnemy.GetComponent<CombatTimeDomain>() : null;
        string vision = "absent";
        if (encounterEnemy != null && encounterEnemy.characterData != null && playerRoot != null)
        {
            bool visible = encounterEnemy.characterData.TryEvaluateVision(encounterEnemy.Root, playerRoot,
                out float distance, out float angle, out string reason);
            vision = encounterEnemy.characterData.name + " path=" + UnityEditor.AssetDatabase.GetAssetPath(encounterEnemy.characterData) +
                " id=" + encounterEnemy.characterData.UniqueId + " range=" + encounterEnemy.characterData.vision.maximumDistance +
                " distance=" + distance + " angle=" + angle + " visible=" + visible + " reason=" + reason;
        }
        string navigation = agent != null && agent.enabled && agent.isOnNavMesh
            ? " stopped=" + agent.isStopped + " path=" + agent.pathStatus + " remaining=" + agent.remainingDistance + " destination=" + agent.destination : "";
        return "frames=" + encounterFrames + " aggro=" + encounterAggro + " facade=" + encounterFacade +
            " impacts=" + encounterImpacts.Count + " health=" + (player != null ? player.CurrentHp : -1) + "/" + encounterHealth +
            " playerPosition=" + (playerRoot != null ? playerRoot.position.ToString() : "absent") +
            " grounded=" + (player != null && player.IsGrounded) +
            " globalScale=" + Time.timeScale + " localScale=" + (clock != null ? clock.Scale : 1) +
            " inputMode=" + InputModeCoordinator.CurrentMode + " currentVision=" + vision +
            " speed=" + encounterPeakSpeed + " travel=" + encounterTravel + " phases=" + string.Join(",", encounterPhases) +
            " enemy=" + (encounterEnemy != null ? encounterEnemy.DetectionReason + "/" + encounterEnemy.AttackPhase +
                " nav=" + encounterEnemy.NavigationReady + " dormant=" + encounterEnemy.IsFlameDormant +
                " suspended=" + encounterEnemy.IsCinematicSuspended + " position=" + encounterEnemy.Root.position : "absent") +
            " agent=" + (agent != null ? agent.enabled + "/" + (agent.enabled && agent.isOnNavMesh) : "absent") + navigation +
            " brainEnabled=" + (encounterEnemy != null && encounterEnemy.isActiveAndEnabled) +
            " stops=" + (encounterEnemy != null ? string.Join(",", encounterEnemy.StopControlConditions.Where(pair => pair.Value).Select(pair => pair.Key)) : "absent");
    }

    private static void CleanupEncounterProbe()
    {
        ResetDefenseProbe();
        if (encounterEnemy != null) encounterEnemy.AttackImpact -= RecordEncounterImpact;
        if (encounterFlameFixture != null) encounterFlameFixture.SetLit(false);
        encounterFlameFixture = null;
        encounterEnemy = null;
    }

    private static void PrepareLitFlameFixture()
    {
        // Explicit fixture only: illuminate an existing source covering the actor,
        // never change its radius, placement or the enemy's Flame policy.
        var sourceField = typeof(Flame).GetField("litInfluence",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var contacts = encounterEnemy.GetComponentsInChildren<Collider>();
        foreach (var flame in UnityEngine.Object.FindObjectsByType<Flame>().OrderBy(candidate => candidate.FlameId, StringComparer.Ordinal))
        {
            var source = sourceField?.GetValue(flame) as LitInfluenceSource;
            if (flame.IsLit || flame.IsExternallySuppressed || source == null ||
                !contacts.Any(contact => source.AcceptsCollider(contact) &&
                    source.TouchesCollider(flame.transform, contact, contact.transform.position))) continue;
            encounterFlameFixture = flame;
            flame.SetLit(true);
            encounterEnemy.GetComponent<ProductionBrainsFlamePolicy>()?.Refresh();
            Require(!encounterEnemy.IsFlameDormant, "Explicit isolated lit fixture: existing Flame " + flame.FlameId +
                " in " + flame.gameObject.scene.name + " at " + flame.transform.position +
                "; activation interaction/progression is NOT validated by this fixture");
            return;
        }
        throw new InvalidOperationException("No authored Flame covers the production Juggernaut; no source is invented or relocated. " + DescribeEncounter());
    }
}
