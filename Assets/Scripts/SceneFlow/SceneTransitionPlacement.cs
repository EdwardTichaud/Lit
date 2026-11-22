using System.Collections.Generic;
using UnityEngine;
using Opsive.UltimateCharacterController.Character;

/// <summary>Read-only physical checks. Never invents a replacement spawn or moves the player.</summary>
public static class SceneTransitionPlacement
{
    public static bool Validate(IReadOnlyList<GameObject> players, IReadOnlyList<Pose> poses,
        ICollection<string> destinationScenes, out string issue)
    {
        issue = null;
        if (players == null || players.Count == 0 || poses == null || poses.Count < players.Count)
        { issue = "Il manque un emplacement d'arrivee par joueur."; return false; }
        Physics.SyncTransforms();
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null) { issue = "Personnage absent."; return false; }
            Vector3 position = poses[i].position;
            if (!IsFinite(position)) { issue = "Position d'arrivee non finie."; return false; }
            for (int j = 0; j < i; j++)
                if (Vector3.Distance(position, poses[j].position) < .8f)
                { issue = "Emplacements d'arrivee superposes."; return false; }
            float bottom = 0f, radius = .3f, height = 1.8f;
            // Ragdoll capsules are not the UCC motor capsule and must not determine arrival clearance.
            var locomotion = players[i].GetComponent<CharacterLocomotion>();
            Collider[] colliders = locomotion != null && locomotion.Colliders != null && locomotion.ColliderCount > 0
                ? locomotion.Colliders : players[i].GetComponentsInChildren<Collider>();
            float selectedHeight = 0;
            foreach (var candidate in colliders)
            {
                var capsule = candidate as CapsuleCollider;
                if (capsule == null || !capsule.enabled || capsule.isTrigger || capsule.bounds.size.y < .5f ||
                    capsule.bounds.size.y <= selectedHeight) continue;
                selectedHeight = capsule.bounds.size.y;
                bottom = capsule.bounds.min.y - players[i].transform.position.y;
                radius = Mathf.Clamp(Mathf.Min(capsule.bounds.extents.x, capsule.bounds.extents.z), .1f, .6f);
                height = Mathf.Max(radius * 2, capsule.bounds.size.y);
            }
            float feet = position.y + bottom;
            bool floor = false;
            foreach (var hit in Physics.RaycastAll(new Vector3(position.x, feet + .5f, position.z),
                Vector3.down, 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!BelongsToDestination(hit.collider, destinationScenes) || IsPlayer(hit.collider, players) || hit.normal.y < .5f) continue;
                floor = true;
                break;
            }
            if (!floor) { issue = "Aucun sol de la zone cible sous l'arrivee " + position; return false; }
            Vector3 low = new Vector3(position.x, feet + radius + .05f, position.z);
            Vector3 high = low + Vector3.up * Mathf.Max(0, height - radius * 2 - .1f);
            foreach (var collider in Physics.OverlapCapsule(low, high, radius * .9f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!BelongsToDestination(collider, destinationScenes) || IsPlayer(collider, players) || collider.bounds.max.y <= feet + .06f) continue;
                issue = "Capsule d'arrivee bloquee par " + collider.name + " a " + position;
                return false;
            }
        }
        return true;
    }

    private static bool BelongsToDestination(Collider collider, ICollection<string> scenes) =>
        collider != null && collider.enabled && collider.gameObject.activeInHierarchy &&
        (scenes == null || scenes.Contains(collider.gameObject.scene.name));

    private static bool IsPlayer(Collider collider, IReadOnlyList<GameObject> players)
    {
        foreach (var player in players)
            if (player != null && collider.transform.IsChildOf(player.transform)) return true;
        return false;
    }

    public static bool IsFinite(Vector3 position) =>
        !float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsNaN(position.z) &&
        !float.IsInfinity(position.x) && !float.IsInfinity(position.y) && !float.IsInfinity(position.z);
}
