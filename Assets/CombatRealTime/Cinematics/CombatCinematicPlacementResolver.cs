using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chooses a safe yaw for a baked LightSkill trajectory while keeping its midpoint fixed.
/// </summary>
public static class CombatCinematicPlacementResolver
{
    private const int ColliderBufferSize = 64;

    private static readonly Collider[] ColliderBuffer = new Collider[ColliderBufferSize];
    private static readonly RaycastHit[] CastHitBuffer = new RaycastHit[ColliderBufferSize];

    public static bool TryResolve(
        CombatCinematicRig rig,
        CombatCinematicContext context,
        CombatCinematicClearanceProfile profile,
        out CombatCinematicPlacement placement,
        out string error)
    {
        placement = default;
        error = null;

        if (rig == null || context == null)
        {
            error = "Rig cinematographique ou contexte manquant.";
            return false;
        }

        if (profile == null || !profile.enabled)
        {
            return rig.TryGetMidpointPlacement(
                context,
                out placement,
                out error);
        }

        if (!rig.HasAuthoringMotionEnvelope)
        {
            error =
                "L'enveloppe de mouvement est absente ou obsolete. " +
                "Rebakez cette LightSkill depuis AnimationLab.";

            return false;
        }

        CombatCinematicClearanceProxy playerProxy =
            FindOrFallbackProxy(context.PlayerRoot);

        CombatCinematicClearanceProxy enemyProxy =
            FindOrFallbackProxy(
                context.TargetEnemy != null
                    ? context.TargetEnemy.transform
                    : null);

        if (playerProxy == null || enemyProxy == null)
        {
            error =
                "Proxy de degagement cinematographique manquant sur Lucian ou l'ennemi.";

            return false;
        }

        float rotationStep =
            profile != null
                ? profile.rotationStepDegrees
                : 15f;

        foreach (float yaw in GetYawCandidates(rotationStep))
        {
            if (!rig.TryGetMidpointPlacement(
                    context,
                    yaw,
                    out CombatCinematicPlacement candidate,
                    out error))
            {
                return false;
            }

            if (IsTrajectoryClear(
                    rig,
                    context,
                    profile,
                    candidate,
                    playerProxy,
                    enemyProxy,
                    out string blocker))
            {
                placement = candidate;

                Debug.Log(
                    "[LightSkill Clearance] Orientation retenue=" +
                    yaw.ToString("0.##") +
                    " deg | rig=" +
                    candidate.RigPosition +
                    ".",
                    rig);

                return true;
            }

            Debug.Log(
                "[LightSkill Clearance] Orientation refusee=" +
                yaw.ToString("0.##") +
                " deg | " +
                blocker,
                rig);
        }

        error =
            "Aucune orientation sure n'est disponible pour cette LightSkill.";

        Debug.LogWarning(
            "[LightSkill Clearance] Refus sans cout : " + error,
            rig);

        return false;
    }

    public static void ApplyFinalDepenetration(
        CombatCinematicRig rig,
        CombatCinematicContext context,
        CombatCinematicClearanceProfile profile)
    {
        if (context == null)
            return;

        if (profile == null)
            return;

        if (!profile.enabled)
            return;

        if (profile.maximumFinalDepenetration <= 0f)
            return;

        ResolveActorDepenetration(
            context.PlayerRoot,
            FindOrFallbackProxy(context.PlayerRoot),
            context,
            profile,
            rig);

        Transform enemyRoot =
            context.TargetEnemy != null
                ? context.TargetEnemy.transform
                : null;

        ResolveActorDepenetration(
            enemyRoot,
            FindOrFallbackProxy(enemyRoot),
            context,
            profile,
            rig);
    }

    private static bool IsTrajectoryClear(
        CombatCinematicRig rig,
        CombatCinematicContext context,
        CombatCinematicClearanceProfile profile,
        CombatCinematicPlacement placement,
        CombatCinematicClearanceProxy playerProxy,
        CombatCinematicClearanceProxy enemyProxy,
        out string blocker)
    {
        blocker = null;

        int mask = GetEffectiveMask(profile);

        if (mask == 0)
            return true;

        int count = rig.MotionEnvelopeCount;

        for (int i = 0; i < count; i++)
        {
            rig.GetMotionEnvelopePose(
                i,
                placement,
                out Vector3 playerPosition,
                out Quaternion playerRotation,
                out Vector3 enemyPosition,
                out Quaternion enemyRotation,
                out float time);

            if (!IsCapsuleClear(
                    playerProxy,
                    playerPosition,
                    playerRotation,
                    context,
                    profile,
                    mask,
                    "Lucian",
                    time,
                    out blocker))
            {
                return false;
            }

            if (!IsCapsuleClear(
                    enemyProxy,
                    enemyPosition,
                    enemyRotation,
                    context,
                    profile,
                    mask,
                    "Ennemi",
                    time,
                    out blocker))
            {
                return false;
            }

            if (i + 1 >= count)
                continue;

            rig.GetMotionEnvelopePose(
                i + 1,
                placement,
                out Vector3 nextPlayerPosition,
                out Quaternion nextPlayerRotation,
                out Vector3 nextEnemyPosition,
                out Quaternion nextEnemyRotation,
                out float nextTime);

            if (!IsCapsuleSegmentClear(
                    playerProxy,
                    playerPosition,
                    playerRotation,
                    nextPlayerPosition,
                    context,
                    profile,
                    mask,
                    "Lucian",
                    time,
                    nextTime,
                    out blocker))
            {
                return false;
            }

            if (!IsCapsuleSegmentClear(
                    enemyProxy,
                    enemyPosition,
                    enemyRotation,
                    nextEnemyPosition,
                    context,
                    profile,
                    mask,
                    "Ennemi",
                    time,
                    nextTime,
                    out blocker))
            {
                return false;
            }
        }

        blocker = null;
        return true;
    }

    private static bool IsCapsuleClear(
        CombatCinematicClearanceProxy proxy,
        Vector3 position,
        Quaternion rotation,
        CombatCinematicContext context,
        CombatCinematicClearanceProfile profile,
        int mask,
        string actor,
        float time,
        out string blocker)
    {
        // Obligatoire pour garantir que le parametre out
        // est assigne sur tous les chemins de sortie.
        blocker = null;

        if (proxy == null)
        {
            blocker = actor + " : proxy de clearance manquant.";
            return false;
        }

        proxy.GetWorldCapsule(
            position,
            rotation,
            profile.safetyMargin,
            out Vector3 first,
            out Vector3 second,
            out float radius);

        int hits = Physics.OverlapCapsuleNonAlloc(
            first,
            second,
            radius,
            ColliderBuffer,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits; i++)
        {
            Collider collider = ColliderBuffer[i];

            ColliderBuffer[i] = null;

            if (ShouldIgnore(collider, context))
                continue;

            blocker =
                actor +
                " bloque par '" +
                collider.name +
                "' a t=" +
                time.ToString("0.000") +
                ".";

            return false;
        }

        return true;
    }

    private static bool IsCapsuleSegmentClear(
        CombatCinematicClearanceProxy proxy,
        Vector3 from,
        Quaternion rotation,
        Vector3 to,
        CombatCinematicContext context,
        CombatCinematicClearanceProfile profile,
        int mask,
        string actor,
        float time,
        float nextTime,
        out string blocker)
    {
        blocker = null;

        if (proxy == null)
        {
            blocker = actor + " : proxy de clearance manquant.";
            return false;
        }

        Vector3 delta = to - from;
        float distance = delta.magnitude;

        if (distance <= 0.0001f)
            return true;

        proxy.GetWorldCapsule(
            from,
            rotation,
            profile.safetyMargin,
            out Vector3 first,
            out Vector3 second,
            out float radius);

        Vector3 direction = delta / distance;

        int hits = Physics.CapsuleCastNonAlloc(
            first,
            second,
            radius,
            direction,
            CastHitBuffer,
            distance,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits; i++)
        {
            Collider collider = CastHitBuffer[i].collider;

            CastHitBuffer[i] = default;

            if (ShouldIgnore(collider, context))
                continue;

            blocker =
                actor +
                " traverse '" +
                collider.name +
                "' entre t=" +
                time.ToString("0.000") +
                " et " +
                nextTime.ToString("0.000") +
                ".";

            return false;
        }

        return true;
    }

    private static void ResolveActorDepenetration(
        Transform root,
        CombatCinematicClearanceProxy proxy,
        CombatCinematicContext context,
        CombatCinematicClearanceProfile profile,
        CombatCinematicRig rig)
    {
        if (root == null || proxy == null)
            return;

        int mask = GetEffectiveMask(profile);

        if (mask == 0)
            return;

        proxy.GetWorldCapsule(
            root.position,
            root.rotation,
            profile.safetyMargin,
            out Vector3 first,
            out Vector3 second,
            out float radius);

        int hits = Physics.OverlapCapsuleNonAlloc(
            first,
            second,
            radius,
            ColliderBuffer,
            mask,
            QueryTriggerInteraction.Ignore);

        Collider actorCollider = proxy.SourceCollider;

        if (actorCollider == null)
            return;

        for (int i = 0; i < hits; i++)
        {
            Collider blocker = ColliderBuffer[i];

            ColliderBuffer[i] = null;

            if (ShouldIgnore(blocker, context))
                continue;

            bool penetrating = Physics.ComputePenetration(
                actorCollider,
                actorCollider.transform.position,
                actorCollider.transform.rotation,
                blocker,
                blocker.transform.position,
                blocker.transform.rotation,
                out Vector3 direction,
                out float distance);

            if (!penetrating)
                continue;

            float correction = Mathf.Min(
                profile.maximumFinalDepenetration,
                distance + profile.safetyMargin);

            if (correction <= 0f)
                continue;

            CombatActorAnimationRoot contract =
                root.GetComponent<CombatActorAnimationRoot>();

            Vector3 correctedPosition =
                root.position + direction * correction;

            if (contract != null)
            {
                contract.SetActorPose(
                    correctedPosition,
                    root.rotation);
            }
            else
            {
                root.position = correctedPosition;
            }

            Debug.Log(
                "[LightSkill Clearance] Depenetration finale " +
                root.name +
                " de " +
                correction.ToString("0.000") +
                " m contre '" +
                blocker.name +
                "'.",
                rig);

            break;
        }
    }

    private static CombatCinematicClearanceProxy FindOrFallbackProxy(
        Transform root)
    {
        if (root == null)
            return null;

        CombatCinematicClearanceProxy proxy =
            root.GetComponent<CombatCinematicClearanceProxy>();

        if (proxy != null)
            return proxy;

        return root.GetComponentInChildren<CombatCinematicClearanceProxy>(true);
    }

    private static bool ShouldIgnore(
        Collider collider,
        CombatCinematicContext context)
    {
        if (collider == null)
            return true;

        if (collider.isTrigger)
            return true;

        if (context != null)
        {
            if (context.PlayerRoot != null &&
                collider.transform.IsChildOf(context.PlayerRoot))
            {
                return true;
            }

            if (context.TargetEnemy != null &&
                collider.transform.IsChildOf(context.TargetEnemy.transform))
            {
                return true;
            }
        }

        CinematicPassThrough passThrough =
            collider.GetComponentInParent<CinematicPassThrough>();

        return passThrough != null &&
               passThrough.Allows(collider);
    }

    private static int GetEffectiveMask(
        CombatCinematicClearanceProfile profile)
    {
        int mask =
            profile != null
                ? profile.blockingLayers.value
                : ~0;

        if (profile == null ||
            !profile.ignoreCommonNonBlockingLayers)
        {
            return mask;
        }

        string[] ignoredLayers =
        {
            "Ground",
            "Water",
            "UI",
            "Character",
            "Player",
            "Enemy",
            "VFX",
            "VisualEffect",
            "Ignore Raycast",
            "Stairs"
        };

        for (int i = 0; i < ignoredLayers.Length; i++)
        {
            int layer =
                LayerMask.NameToLayer(ignoredLayers[i]);

            if (layer >= 0)
            {
                mask &= ~(1 << layer);
            }
        }

        return mask;
    }

    private static IEnumerable<float> GetYawCandidates(float step)
    {
        step = Mathf.Clamp(step, 1f, 180f);

        yield return 0f;

        for (float offset = step;
             offset < 180f;
             offset += step)
        {
            yield return offset;
            yield return -offset;
        }

        yield return 180f;
    }
}