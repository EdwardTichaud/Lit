using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Presentation locale des items utilises pour des contres de combat.
public static class CombatCounterItemPresentation
{
    private static readonly string[] RightHandNames =
    {
        "RightHand", "right_hand", "hand_r", "Hand_R", "mixamorig:RightHand"
    };

    private static readonly string[] SpineNames =
    {
        "spine_03", "Spine_03", "mixamorig:Spine2", "spine_04", "Spine"
    };

    private sealed class AnimationEventItemContext
    {
        public MonoBehaviour Runner;
        public Transform PlayerRoot;
        public Transform EnemyRoot;
        public Item Item;
        public Item.CombatReactionProfile Profile;
        public Action OnCounterHit;
        public Action<Transform> OnRelease;
        public GameObject Visual;
        public Coroutine CleanupRoutine;
    }

    private static readonly Dictionary<int, AnimationEventItemContext> AnimationEventContexts = new Dictionary<int, AnimationEventItemContext>();

    public static Coroutine PlayMeleeCounter(
        MonoBehaviour runner,
        Transform playerRoot,
        Transform enemyRoot,
        Item item,
        Item.CombatReactionProfile profile,
        float handSeconds,
        float totalSeconds,
        Action<Transform> onImpact)
    {
        if (runner == null)
        {
            return null;
        }

        return runner.StartCoroutine(PlayMeleeCounterRoutine(
            playerRoot,
            enemyRoot,
            item,
            profile,
            handSeconds,
            totalSeconds,
            onImpact));
    }

    public static Coroutine PlayHeldItem(
        MonoBehaviour runner,
        Transform playerRoot,
        Item item,
        Item.CombatReactionProfile profile,
        float totalSeconds)
    {
        if (runner == null)
        {
            return null;
        }

        return runner.StartCoroutine(PlayHeldItemRoutine(playerRoot, item, profile, totalSeconds));
    }

    public static void BeginAnimationEventItemPresentation(
        MonoBehaviour runner,
        Transform playerRoot,
        Transform enemyRoot,
        Item item,
        Item.CombatReactionProfile profile,
        float totalSeconds,
        Action onCounterHit,
        Action<Transform> onRelease)
    {
        if (playerRoot == null || item == null)
        {
            return;
        }

        int key = playerRoot.GetInstanceID();
        ClearAnimationEventItemPresentation(playerRoot);

        AnimationEventItemContext context = new AnimationEventItemContext
        {
            Runner = runner,
            PlayerRoot = playerRoot,
            EnemyRoot = enemyRoot,
            Item = item,
            Profile = profile,
            OnCounterHit = onCounterHit,
            OnRelease = onRelease
        };

        AnimationEventContexts[key] = context;
        if (runner != null && totalSeconds > 0f)
        {
            context.CleanupRoutine = runner.StartCoroutine(CleanupAnimationEventItemPresentationRoutine(key, totalSeconds));
        }
    }

    public static bool TakeAnimationEventItem(Transform actorRoot, Transform enemyRoot = null)
    {
        if (!TryGetAnimationEventContext(actorRoot, out AnimationEventItemContext context))
        {
            return false;
        }

        if (enemyRoot != null)
        {
            context.EnemyRoot = enemyRoot;
        }

        GameObject visual = EnsureAnimationEventVisual(context);
        Transform rightHand = ResolveRightHand(context.PlayerRoot, context.Profile);
        if (visual == null || rightHand == null)
        {
            return false;
        }

        AttachToPoint(
            visual.transform,
            rightHand,
            context.Profile != null ? context.Profile.playerAttachLocalPosition : Vector3.zero,
            context.Profile != null ? context.Profile.playerAttachLocalEulerAngles : Vector3.zero);
        return true;
    }

    public static bool CounterHitAnimationEventItem(Transform actorRoot, Transform enemyRoot = null)
    {
        if (!TryGetAnimationEventContext(actorRoot, out AnimationEventItemContext context))
        {
            return false;
        }

        if (enemyRoot != null)
        {
            context.EnemyRoot = enemyRoot;
        }

        context.OnCounterHit?.Invoke();
        context.OnCounterHit = null;
        return true;
    }

    public static bool ReleaseAnimationEventItem(Transform actorRoot, Transform enemyRoot = null)
    {
        if (!TryGetAnimationEventContext(actorRoot, out AnimationEventItemContext context))
        {
            return false;
        }

        if (enemyRoot != null)
        {
            context.EnemyRoot = enemyRoot;
        }

        GameObject visual = EnsureAnimationEventVisual(context);
        Transform enemyAttach = ResolveNamedChild(context.EnemyRoot, context.Profile?.enemyAttachBoneName, SpineNames);
        if (visual != null && enemyAttach != null)
        {
            AttachToEnemyPoint(
                visual.transform,
                enemyAttach,
                context.EnemyRoot,
                context.Profile != null ? context.Profile.enemyAttachLocalPosition : Vector3.zero,
                context.Profile != null ? context.Profile.enemyAttachLocalEulerAngles : Vector3.zero);
        }

        context.OnRelease?.Invoke(enemyAttach);
        context.OnRelease = null;
        return visual != null && enemyAttach != null;
    }

    public static void ClearAnimationEventItemPresentation(Transform actorRoot)
    {
        if (actorRoot == null)
        {
            return;
        }

        int key = actorRoot.GetInstanceID();
        if (!AnimationEventContexts.TryGetValue(key, out AnimationEventItemContext context))
        {
            return;
        }

        if (context.CleanupRoutine != null && context.Runner != null)
        {
            context.Runner.StopCoroutine(context.CleanupRoutine);
        }

        if (context.Visual != null)
        {
            UnityEngine.Object.Destroy(context.Visual);
        }

        AnimationEventContexts.Remove(key);
    }

    private static IEnumerator PlayHeldItemRoutine(
        Transform playerRoot,
        Item item,
        Item.CombatReactionProfile profile,
        float totalSeconds)
    {
        GameObject visual = CreateItemVisual(item, profile, playerRoot, null);
        Transform rightHand = ResolveRightHand(playerRoot, profile);
        if (visual != null && rightHand != null)
        {
            AttachToPoint(
                visual.transform,
                rightHand,
                profile != null ? profile.playerAttachLocalPosition : Vector3.zero,
                profile != null ? profile.playerAttachLocalEulerAngles : Vector3.zero);
        }

        yield return WaitPresentationSeconds(totalSeconds);

        if (visual != null)
        {
            UnityEngine.Object.Destroy(visual);
        }
    }

    private static IEnumerator CleanupAnimationEventItemPresentationRoutine(int key, float totalSeconds)
    {
        yield return WaitPresentationSeconds(totalSeconds);

        if (!AnimationEventContexts.TryGetValue(key, out AnimationEventItemContext context))
        {
            yield break;
        }

        if (context.Visual != null)
        {
            UnityEngine.Object.Destroy(context.Visual);
        }

        AnimationEventContexts.Remove(key);
    }

    private static GameObject EnsureAnimationEventVisual(AnimationEventItemContext context)
    {
        if (context == null)
        {
            return null;
        }

        if (context.Visual != null)
        {
            return context.Visual;
        }

        context.Visual = CreateItemVisual(context.Item, context.Profile, context.PlayerRoot, context.EnemyRoot);
        return context.Visual;
    }

    private static bool TryGetAnimationEventContext(Transform actorRoot, out AnimationEventItemContext context)
    {
        context = null;
        if (actorRoot == null)
        {
            return false;
        }

        int key = actorRoot.GetInstanceID();
        if (AnimationEventContexts.TryGetValue(key, out context))
        {
            return true;
        }

        foreach (AnimationEventItemContext candidate in AnimationEventContexts.Values)
        {
            if (candidate == null)
            {
                continue;
            }

            if (BelongsTo(actorRoot, candidate.PlayerRoot) || BelongsTo(actorRoot, candidate.EnemyRoot))
            {
                context = candidate;
                return true;
            }
        }

        return false;
    }

    private static IEnumerator PlayMeleeCounterRoutine(
        Transform playerRoot,
        Transform enemyRoot,
        Item item,
        Item.CombatReactionProfile profile,
        float handSeconds,
        float totalSeconds,
        Action<Transform> onImpact)
    {
        GameObject visual = CreateItemVisual(item, profile, playerRoot, enemyRoot);
        Transform rightHand = ResolveRightHand(playerRoot, profile);
        Transform enemyAttach = ResolveNamedChild(enemyRoot, profile?.enemyAttachBoneName, SpineNames);

        if (visual != null && rightHand != null)
        {
            AttachToPoint(
                visual.transform,
                rightHand,
                profile != null ? profile.playerAttachLocalPosition : Vector3.zero,
                profile != null ? profile.playerAttachLocalEulerAngles : Vector3.zero);
        }

        yield return WaitPresentationSeconds(handSeconds);

        if (visual != null && enemyAttach != null)
        {
            AttachToEnemyPoint(
                visual.transform,
                enemyAttach,
                enemyRoot,
                profile != null ? profile.enemyAttachLocalPosition : Vector3.zero,
                profile != null ? profile.enemyAttachLocalEulerAngles : Vector3.zero);
        }

        onImpact?.Invoke(enemyAttach);

        float remaining = Mathf.Max(0f, totalSeconds - Mathf.Max(0f, handSeconds));
        yield return WaitPresentationSeconds(remaining);

        if (visual != null)
        {
            UnityEngine.Object.Destroy(visual);
        }
    }

    private static GameObject CreateItemVisual(
        Item item,
        Item.CombatReactionProfile profile,
        Transform playerRoot,
        Transform enemyRoot)
    {
        Vector3 position = playerRoot != null ? playerRoot.position : enemyRoot != null ? enemyRoot.position : Vector3.zero;
        Quaternion rotation = ResolveItemRotation(playerRoot, enemyRoot);
        GameObject prefab = profile != null ? profile.ResolveVisualPrefab(item) : item != null ? item.ResolveWorldPrefab() : null;
        GameObject instance = prefab != null
            ? UnityEngine.Object.Instantiate(prefab, position, rotation)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);

        if (instance == null)
        {
            return null;
        }

        if (prefab == null)
        {
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = new Vector3(0.08f, 0.08f, 0.8f);
        }

        PrepareVisualOnlyInstance(instance);
        return instance;
    }

    private static void PrepareVisualOnlyInstance(GameObject instance)
    {
        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] bodies = instance.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].linearVelocity = Vector3.zero;
            bodies[i].angularVelocity = Vector3.zero;
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }
    }

    private static void AttachToPoint(Transform visual, Transform point, Vector3 localPosition, Vector3 localEulerAngles)
    {
        visual.SetParent(point, false);
        visual.localPosition = localPosition;
        visual.localRotation = Quaternion.Euler(localEulerAngles);
    }

    private static void AttachToEnemyPoint(
        Transform visual,
        Transform point,
        Transform enemyRoot,
        Vector3 localPosition,
        Vector3 localEulerAngles)
    {
        visual.SetParent(point, false);
        visual.localPosition = localPosition;
        visual.rotation = ResolveImpaledItemRotation(enemyRoot, point, localEulerAngles.z);
    }

    private static Quaternion ResolveImpaledItemRotation(Transform enemyRoot, Transform fallbackPoint, float rollDegrees)
    {
        Transform reference = enemyRoot != null ? enemyRoot : fallbackPoint;
        Vector3 forward = reference != null ? -reference.forward : Vector3.forward;
        Vector3 up = reference != null ? reference.up : Vector3.up;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        if (up.sqrMagnitude <= 0.0001f || Mathf.Abs(Vector3.Dot(forward.normalized, up.normalized)) > 0.98f)
        {
            up = Vector3.up;
        }

        return Quaternion.LookRotation(forward.normalized, up.normalized) *
               Quaternion.Euler(0f, 0f, rollDegrees);
    }

    private static Quaternion ResolveItemRotation(Transform playerRoot, Transform enemyRoot)
    {
        Vector3 direction = enemyRoot != null && playerRoot != null
            ? enemyRoot.position - playerRoot.position
            : playerRoot != null ? playerRoot.forward : Vector3.forward;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.forward;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static Transform ResolveRightHand(Transform root, Item.CombatReactionProfile profile)
    {
        if (root == null)
        {
            return null;
        }

        Transform configured = ResolveNamedChild(root, profile?.playerAttachBoneName, null);
        if (configured != null)
        {
            return configured;
        }

        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand != null)
            {
                return hand;
            }
        }

        return ResolveNamedChild(root, null, RightHandNames);
    }

    private static Transform ResolveNamedChild(Transform root, string preferredName, string[] fallbackNames)
    {
        if (root == null)
        {
            return null;
        }

        Transform preferred = FindChildByName(root, preferredName);
        if (preferred != null)
        {
            return preferred;
        }

        if (fallbackNames == null)
        {
            return null;
        }

        for (int i = 0; i < fallbackNames.Length; i++)
        {
            Transform match = FindChildByName(root, fallbackNames[i]);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildByName(root.GetChild(i), childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool BelongsTo(Transform actor, Transform candidate)
    {
        return actor != null &&
               candidate != null &&
               (actor == candidate || actor.IsChildOf(candidate) || candidate.IsChildOf(actor));
    }

    private static IEnumerator WaitPresentationSeconds(float seconds)
    {
        float duration = Mathf.Max(0f, seconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
