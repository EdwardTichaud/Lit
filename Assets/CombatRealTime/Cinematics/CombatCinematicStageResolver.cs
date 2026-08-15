using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CinematicStageProfile
{
    [SerializeField, Range(0.5f, 10f)] private float clearanceDiameter = 10f;
    [SerializeField, Min(0f)] private float searchRadius = 12f;
    [SerializeField, Range(1, 24)] private int samplesPerRing = 12;
    [SerializeField, Range(0f, 60f)] private float maximumSlope = 45f;
    [SerializeField, Min(0f)] private float maximumGroundHeightDifference = 0.15f;
    [SerializeField, Tooltip("Masque utilise uniquement pour trouver le sol sous le plateau et les deux acteurs.")]
    private LayerMask groundMask = ~0;
    [SerializeField, Tooltip("Seuls ces colliders empechent la LightSkill. Par defaut : layer Obstacle (murs).")]
    private LayerMask wallMask = 1 << 12;
    [SerializeField, Min(0.01f)] private float transitionFlashDuration = 0.12f;

    public float ClearanceRadius => Mathf.Clamp(clearanceDiameter, 0.5f, 10f) * 0.5f;
    public float SearchRadius => Mathf.Max(0f, searchRadius);
    public int SamplesPerRing => Mathf.Clamp(samplesPerRing, 1, 24);
    public float MaximumSlope => Mathf.Clamp(maximumSlope, 0f, 60f);
    public float MaximumGroundHeightDifference => Mathf.Max(0f, maximumGroundHeightDifference);
    public LayerMask GroundMask => groundMask;
    public LayerMask WallMask => wallMask;
    public float TransitionFlashDuration => Mathf.Max(0.01f, transitionFlashDuration);
}

public readonly struct CombatCinematicStagePlacement
{
    public readonly Vector3 RigPosition;
    public readonly Quaternion RigRotation;
    public readonly Vector3 PlayerPosition;
    public readonly Quaternion PlayerRotation;
    public readonly Vector3 EnemyPosition;
    public readonly Quaternion EnemyRotation;
    public readonly Vector3 Center;

    public CombatCinematicStagePlacement(
        Vector3 rigPosition,
        Quaternion rigRotation,
        Vector3 playerPosition,
        Quaternion playerRotation,
        Vector3 enemyPosition,
        Quaternion enemyRotation,
        Vector3 center)
    {
        RigPosition = rigPosition;
        RigRotation = rigRotation;
        PlayerPosition = playerPosition;
        PlayerRotation = playerRotation;
        EnemyPosition = enemyPosition;
        EnemyRotation = enemyRotation;
        Center = center;
    }
}

[DisallowMultipleComponent]
public sealed class CombatCinematicStageResolver : MonoBehaviour
{
    private const float GroundProbeHeight = 12f;
    private const float GroundProbeDistance = 24f;
    private const float ClearanceSkin = 0.1f;
    private const float DefaultPlayerRadius = 0.45f;
    private const float DefaultPlayerHeight = 1.8f;
    private const float DefaultEnemyRadius = 0.7f;
    private const float DefaultEnemyHeight = 2.2f;

    private readonly Collider[] overlapBuffer = new Collider[128];
    private readonly RaycastHit[] groundHits = new RaycastHit[32];
    [SerializeField, Tooltip("Ecrit un bilan de recherche de plateau dans la Console pour diagnostiquer un refus de LightSkill.")]
    private bool logStageSearchDiagnostics = true;

    public bool TryResolve(
        CombatCinematicRig rigPrefab,
        CombatCinematicContext context,
        CinematicStageProfile profile,
        out CombatCinematicStagePlacement placement,
        out string error)
    {
        placement = default;
        error = null;
        if (rigPrefab == null || context == null || context.PlayerRoot == null || context.TargetEnemy == null)
        {
            error = "Plateau cinematographique ou acteurs manquants.";
            Trace(error);
            return false;
        }
        if (!rigPrefab.HasAuthoringStageLayout)
        {
            error = "Le rig cinematographique doit etre rebake avec les poses Player et Enemy.";
            Trace(error);
            return false;
        }

        profile ??= new CinematicStageProfile();
        Vector3 livePlayer = context.PlayerRoot.position;
        Vector3 liveEnemy = context.TargetEnemy.transform.position;
        Vector3 midpoint = (livePlayer + liveEnemy) * 0.5f;
        Vector3 facing = liveEnemy - livePlayer;
        facing.y = 0f;
        if (facing.sqrMagnitude <= 0.0001f)
        {
            facing = context.PlayerRoot.forward;
            facing.y = 0f;
        }
        Quaternion stageRotation = facing.sqrMagnitude > 0.0001f
            ? rigPrefab.GetStageRotationForFacing(facing.normalized)
            : rigPrefab.GetStageRotationForFacing(context.PlayerRoot.forward);

        int ringCount = Mathf.CeilToInt(profile.SearchRadius / Mathf.Max(0.5f, profile.ClearanceRadius));
        int testedCandidates = 0;
        Dictionary<string, int> rejectionCounts = new Dictionary<string, int>();
        for (int ring = 0; ring <= ringCount; ring++)
        {
            int samples = ring == 0 ? 1 : profile.SamplesPerRing;
            float radius = ring == 0 ? 0f : profile.SearchRadius * ring / ringCount;
            for (int sample = 0; sample < samples; sample++)
            {
                float angle = ring == 0 ? 0f : sample * 360f / samples;
                Vector3 candidate = midpoint + Quaternion.AngleAxis(angle, Vector3.up) * facing.normalized * radius;
                testedCandidates++;
                if (!TryBuildPlacement(candidate, stageRotation, rigPrefab, context, profile, out placement, out string rejectionReason))
                {
                    AddRejection(rejectionCounts, rejectionReason);
                    continue;
                }

                Trace("Plateau accepte apres " + testedCandidates + " test(s). Midpoint=" + midpoint +
                      ", centre=" + placement.Center + ", rig=" + placement.RigPosition + ".");
                return true;
            }
        }

        error = "Espace insuffisant pour cette LightSkill.";
        Trace("Plateau refuse. Midpoint=" + midpoint + ", " + testedCandidates +
              " position(s) testee(s). " + FormatRejections(rejectionCounts));
        return false;
    }

    public void PlayTransitionFlash(CombatCinematicStagePlacement placement, CinematicStageProfile profile)
    {
        ScreenWaveController wave = ScreenWaveController.EnsureInstance();
        if (wave == null) return;

        ScreenWaveController.ScreenWaveSettings settings = ScreenWaveController.ScreenWaveSettings.Default;
        settings.duration = profile != null ? profile.TransitionFlashDuration : 0.12f;
        settings.fadeOutDuration = settings.duration;
        settings.amplitude = 0.025f;
        settings.frequency = 7f;
        settings.propagationSpeed = 5f;
        settings.falloff = 14f;
        settings.highlightIntensity = 2f;
        settings.edgeContrast = 3.5f;
        settings.highlightColor = new Color(0.8f, 0.96f, 1f, 1f);
        wave.TryPlayScreenWavePhase(placement.Center, settings);
    }

    private bool TryBuildPlacement(
        Vector3 candidate,
        Quaternion stageRotation,
        CombatCinematicRig rig,
        CombatCinematicContext context,
        CinematicStageProfile profile,
        out CombatCinematicStagePlacement placement,
        out string rejectionReason)
    {
        placement = default;
        rejectionReason = null;
        if (!TryFindGround(candidate, context, profile, out RaycastHit centerGround) ||
            Vector3.Angle(centerGround.normal, Vector3.up) > profile.MaximumSlope)
        {
            rejectionReason = "sol central absent ou pente trop forte";
            return false;
        }
        if (!HasStageClearance(centerGround.point, context, profile))
        {
            rejectionReason = "mur dans le dome";
            return false;
        }

        rig.GetStageActorPoses(centerGround.point, stageRotation,
            out Vector3 rigPosition, out Vector3 playerPosition, out Quaternion playerRotation,
            out Vector3 enemyPosition, out Quaternion enemyRotation);
        if (!TryFindGround(playerPosition, context, profile, out RaycastHit playerGround) ||
            !TryFindGround(enemyPosition, context, profile, out RaycastHit enemyGround) ||
            Vector3.Angle(playerGround.normal, Vector3.up) > profile.MaximumSlope ||
            Vector3.Angle(enemyGround.normal, Vector3.up) > profile.MaximumSlope ||
            Mathf.Abs(playerGround.point.y - enemyGround.point.y) > profile.MaximumGroundHeightDifference)
        {
            rejectionReason = "sol des acteurs invalide, pente ou ecart de hauteur";
            return false;
        }

        playerPosition.y = playerGround.point.y;
        enemyPosition.y = enemyGround.point.y;
        if (!HasActorClearance(playerPosition, playerRotation, context.PlayerRoot, context, profile, DefaultPlayerRadius, DefaultPlayerHeight) ||
            !HasActorClearance(enemyPosition, enemyRotation, context.TargetEnemy.transform, context, profile, DefaultEnemyRadius, DefaultEnemyHeight))
        {
            rejectionReason = "mur dans la capsule Player ou Enemy";
            return false;
        }

        RealTimeCombatEnemyBehaviour enemyBehaviour = context.TargetEnemy.GetComponent<RealTimeCombatEnemyBehaviour>();
        if (enemyBehaviour != null && !enemyBehaviour.CanPlaceForCinematic(enemyPosition))
        {
            rejectionReason = "pose Enemy hors NavMesh";
            return false;
        }

        placement = new CombatCinematicStagePlacement(
            rigPosition, stageRotation, playerPosition, playerRotation, enemyPosition, enemyRotation, centerGround.point);
        return true;
    }

    private bool TryFindGround(Vector3 position, CombatCinematicContext context, CinematicStageProfile profile, out RaycastHit ground)
    {
        int count = Physics.RaycastNonAlloc(
            position + Vector3.up * GroundProbeHeight,
            Vector3.down,
            groundHits,
            GroundProbeDistance,
            profile.GroundMask,
            QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        ground = default;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || IsActorCollider(hit.collider, context)) continue;
            if (hit.distance >= nearest) continue;
            nearest = hit.distance;
            ground = hit;
        }
        return nearest < float.PositiveInfinity;
    }

    private bool HasStageClearance(Vector3 groundPoint, CombatCinematicContext context, CinematicStageProfile profile)
    {
        float radius = Mathf.Max(0.1f, profile.ClearanceRadius - ClearanceSkin);
        int count = Physics.OverlapSphereNonAlloc(groundPoint, radius, overlapBuffer, profile.WallMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            // Terrain is validated by the ground probes for the center and both actors.
            // Its global bounds would otherwise always intersect a dome rooted on it.
            if (collider == null || collider is TerrainCollider || IsActorCollider(collider, context)) continue;
            if (IntersectsGroundedDome(collider.bounds, groundPoint, radius)) return false;
        }
        return true;
    }

    // The Timeline is staged on the ground, so only the upper hemisphere is relevant.
    // Bounds make this conservative: a close decorative collider may reject a stage,
    // but an unsafe ceiling, wall, or uneven terrain can never be accepted.
    private static bool IntersectsGroundedDome(Bounds bounds, Vector3 groundPoint, float radius)
    {
        float floor = groundPoint.y + ClearanceSkin;
        if (bounds.max.y <= floor) return false;

        float closestX = Mathf.Clamp(groundPoint.x, bounds.min.x, bounds.max.x);
        float closestZ = Mathf.Clamp(groundPoint.z, bounds.min.z, bounds.max.z);
        float horizontalSqr = (new Vector2(closestX - groundPoint.x, closestZ - groundPoint.z)).sqrMagnitude;
        float radiusSqr = radius * radius;
        if (horizontalSqr >= radiusSqr) return false;

        float domeCeiling = groundPoint.y + Mathf.Sqrt(radiusSqr - horizontalSqr);
        return bounds.min.y < domeCeiling;
    }

    private bool HasActorClearance(
        Vector3 position,
        Quaternion rotation,
        Transform actor,
        CombatCinematicContext context,
        CinematicStageProfile profile,
        float fallbackRadius,
        float fallbackHeight)
    {
        CapsuleCollider capsule = actor != null ? actor.GetComponentInChildren<CapsuleCollider>() : null;
        float radius = capsule != null ? Mathf.Max(0.1f, capsule.radius * Mathf.Max(actor.lossyScale.x, actor.lossyScale.z)) : fallbackRadius;
        float height = capsule != null ? Mathf.Max(radius * 2f, capsule.height * actor.lossyScale.y) : fallbackHeight;
        Vector3 bottom = position + Vector3.up * (radius + ClearanceSkin);
        Vector3 top = position + Vector3.up * Mathf.Max(radius + ClearanceSkin, height - radius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlapBuffer, profile.WallMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (collider != null && !IsActorCollider(collider, context)) return false;
        }
        return true;
    }

    private static bool IsActorCollider(Collider collider, CombatCinematicContext context)
    {
        Transform transform = collider.transform;
        return (context.PlayerRoot != null && transform.IsChildOf(context.PlayerRoot)) ||
               (context.TargetEnemy != null && transform.IsChildOf(context.TargetEnemy.transform));
    }

    private void Trace(string message)
    {
        if (logStageSearchDiagnostics)
        {
            Debug.Log("[LightSkill Stage] " + message, this);
        }
    }

    private static void AddRejection(Dictionary<string, int> counts, string reason)
    {
        string key = string.IsNullOrWhiteSpace(reason) ? "raison inconnue" : reason;
        counts.TryGetValue(key, out int count);
        counts[key] = count + 1;
    }

    private static string FormatRejections(Dictionary<string, int> counts)
    {
        if (counts.Count == 0) return "Aucune raison de refus remontee.";

        string report = "Refus : ";
        bool first = true;
        foreach (KeyValuePair<string, int> rejection in counts)
        {
            if (!first) report += ", ";
            report += rejection.Key + " x" + rejection.Value;
            first = false;
        }
        return report + ".";
    }
}
