using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>Pre-spawn enemy authoring. Never installs a second movement or animation authority.</summary>
public static class CombatEnemyNetworkAuthoring
{
    public static string Validate(GameObject root, Animator animator)
    {
        if (root == null || animator == null || animator.runtimeAnimatorController == null)
            return "Enemy requires its authored Animator/controller before network presentation can be installed.";
        var animations = root.GetComponentsInChildren<NetworkAnimator>(true);
        if (animations.Length > 1) return "Enemy has multiple NetworkAnimators; choose one authority explicitly.";
        if (animations.Length == 1)
        {
            if (!animations[0].IsServerAuthoritative()) return "Enemy NetworkAnimator must be server authoritative.";
            if (animations[0].Animator != null && animations[0].Animator != animator)
                return "Enemy NetworkAnimator is bound to a different authored Animator.";
            var identity = animations[0].GetComponentInParent<NetworkObject>(true);
            if (identity != null && identity.gameObject != root)
                return "Enemy NetworkAnimator belongs to a nested NetworkObject.";
        }
        var transforms = root.GetComponents<NetworkTransform>();
        if (transforms.Length > 1) return "Enemy has multiple root NetworkTransforms.";
        if (transforms.Length == 1 && !transforms[0].IsServerAuthoritative())
            return "Enemy NetworkTransform must be server authoritative.";
        return null;
    }

    public static bool NeedsChanges(GameObject root, Animator animator)
    {
        var animation = root.GetComponentInChildren<NetworkAnimator>(true);
        var movement = root.GetComponent<NetworkTransform>();
        return animation == null || animation.Animator != animator || !animation.enabled ||
            movement == null || !movement.enabled || !movement.SwitchTransformSpaceWhenParented;
    }

    public static bool Ensure(GameObject root, Animator animator)
    {
        string error = Validate(root, animator);
        if (error != null) throw new InvalidOperationException(error);
        var identity = root.GetComponent<NetworkObject>();
        if (identity == null || identity.IsSpawned)
            throw new InvalidOperationException("Author enemy network presentation on an unspawned root NetworkObject.");
        bool changed = NeedsChanges(root, animator);
        var movement = root.GetComponent<NetworkTransform>();
        if (movement == null) movement = root.AddComponent<NetworkTransform>();
        movement.enabled = true;
        movement.SwitchTransformSpaceWhenParented = true;
        var animation = root.GetComponentInChildren<NetworkAnimator>(true);
        if (animation == null) animation = root.AddComponent<NetworkAnimator>();
        animation.Animator = animator;
        animation.enabled = true;
        return changed;
    }
}

/// <summary>Authors the same player components required by the existing spawn factory, before spawn.</summary>
public static class CombatPlayerNetworkAuthoring
{
    private static readonly Type[] Required = {
        typeof(ServerUccNetworkTransform), typeof(NetcodeCharacterIdentity), typeof(NetcodeLocalPlayer),
        typeof(NetworkCharacterInput), typeof(NetworkInventory), typeof(PersistentNetworkObject)
    };

    public static string Validate(GameObject root)
    {
        if (root == null || root.GetComponent<SquadCharacterController>() == null)
            return "Network player authoring requires the existing squad actor.";
        foreach (var type in Required)
            if (root.GetComponents(type).Length > 1) return "Multiple root network authorities: " + type.Name;
        var movement = root.GetComponent<NetworkTransform>();
        if (movement != null && !movement.IsServerAuthoritative()) return "Player movement must retain server authority.";
        if (movement != null && !(movement is ServerUccNetworkTransform))
            return "Player NetworkTransform must synchronize the UCC motor cache (ServerUccNetworkTransform).";
        return null;
    }

    public static bool NeedsChanges(GameObject root)
    {
        foreach (var type in Required) if (root.GetComponent(type) == null) return true;
        return false;
    }

    public static bool Ensure(GameObject root)
    {
        string error = Validate(root);
        if (error != null) throw new InvalidOperationException(error);
        var identity = root.GetComponent<NetworkObject>();
        if (identity == null || identity.IsSpawned) throw new InvalidOperationException("Author an unspawned player NetworkObject.");
        bool changed = false;
        foreach (var type in Required)
            if (root.GetComponent(type) == null) { root.AddComponent(type); changed = true; }
        return changed;
    }
}
