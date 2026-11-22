using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lit.Story
{
    /// <summary>A house-only conversation whose sequence creates its departure portal.</summary>
    public sealed class MaisonHearthVigil : MonoBehaviour
    {
        [Serializable] public sealed class Seat { public string actorId; public Transform point; }
        public const string PortalOpenKey = "narrative.maison.hearth.portal_open";
        public const string PortalSpawnId = "hearth.departure_portal";
        public const string PortalSpawnPointId = "hearth.portal_position";
        public StorySequenceRunner runner;
        public StorySequenceAsset prerequisite;
        public StorySequenceSceneBindings bindings;
        public Seat[] companionSeats = Array.Empty<Seat>();
        public RuntimeAnimatorController seatedController;
        public WorldInteractionPrompt prompt;
        private float nextReferenceCheck;
        public string foyerScene = "Maison_Interactives";
        public string departurePortalPath = "Maison_Interactive_Root/Portals/Portal_1";
        private WorldRulesStateManager rules;
        private WorldRulesStateManager Rules => rules != null ? rules : rules = FindAnyObjectByType<WorldRulesStateManager>();
        private bool SharedCompletion => Rules != null && Rules.TryGetBool(PortalOpenKey, out bool open) && open;
        private MaisonCompanionReunion group;
        private GameObject portal;
        private Foyer foyer;
        private Coroutine preparation;
        private SquadCharacterController seatedPlayer;
        private bool ownsPortalVisibility, portalWasActive, portalRevealed, staged;
        private readonly Dictionary<StorySequenceActor, ActorPose> poses = new Dictionary<StorySequenceActor, ActorPose>();
        private sealed class ActorPose { public Vector3 position; public Quaternion rotation; public RuntimeAnimatorController controller; }
        public bool IsCompleted => SharedCompletion || runner != null && StorySequenceCompletionStore.IsCompleted(runner.Sequence);
        public bool IsUnlocked => prerequisite != null && StorySequenceCompletionStore.IsCompleted(prerequisite) && !IsCompleted;
        private bool IsAuthority => Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening ||
            Unity.Netcode.NetworkManager.Singleton.IsServer;

        private bool IsHouseReady => SceneManager.GetActiveScene().name == "Maison_Core" && !LoadingScreenService.IsLoading &&
            !GameFlowService.IsPreparingGameplayScene && !(GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning);

        private void ResolveReferences()
        {
            if (group == null)
                foreach (var item in FindObjectsByType<MaisonCompanionReunion>(FindObjectsInactive.Exclude))
                    if (item.gameObject.scene.name == "Maison_Sequences" || item.gameObject.scene.name == "Maison_Core") { group = item; break; }
            var scene = SceneManager.GetSceneByName(foyerScene);
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (foyer == null) foyer = root.GetComponentInChildren<Foyer>(true);
                if (portal == null)
                {
                    string[] path = departurePortalPath.Split('/');
                    if (path.Length > 1 && root.name == path[0])
                    {
                        var found = root.transform.Find(string.Join("/", path, 1, path.Length - 1));
                        if (found != null) portal = found.gameObject;
                    }
                }
            }
        }

        private bool BindGroup() => BindGroup(out _);

        private bool BindGroup(out string configurationIssue)
        {
            configurationIssue = null;
            ResolveReferences();
            if (group == null || bindings == null || companionSeats.Length != 3 || seatedController == null || portal == null)
            {
                configurationIssue = "groupe, bindings, trois sieges, controleur assis ou portail non configure";
                return false;
            }
            if (bindings.ResolveSpawnPoint(PortalSpawnPointId)?.point == null)
            {
                configurationIssue = "Transform '" + PortalSpawnPointId + "' manquant dans Scene Bindings > Spawn Points";
                return false;
            }
            group.EnsurePresentations();
            foreach (var seat in companionSeats)
            {
                if (seat == null || seat.point == null)
                {
                    configurationIssue = "Transform de siege manquant";
                    return false;
                }
                var actor = FindCompanion(seat.actorId);
                if (actor == null || !actor.gameObject.activeInHierarchy || actor.Animator == null) return false;
                bindings.BindRuntimeActor(seat.actorId, actor);
            }
            return true;
        }
        private StorySequenceActor FindCompanion(string id)
        {
            if (group == null) return null;
            foreach (var companion in group.Companions)
                if (companion != null && companion.actorId == id) return companion.instance;
            return null;
        }

        /// <returns>True when the story owns this interaction; result says whether playback was accepted.</returns>
        public bool TryHandleFoyer(Foyer source, SquadCharacterController player, out bool result)
        {
            result = false;
            if (!isActiveAndEnabled) return false;
            ResolveReferences();
            if (source != foyer || !IsUnlocked || !IsAuthority || player == null || player.CharacterData == null ||
                !MaisonCompanionReunion.IsLucian(player.CharacterData.characterId, player.CharacterData.characterName)) return false;
            if (preparation != null || runner == null || runner.IsPlaying || StorySequenceRunner.IsAnySequencePlaying ||
                !IsHouseReady || InputModeCoordinator.CurrentMode != InputMode.Exploration || InputFocusStack.HasAnyFocus() ||
                player == null || player.transform != LocalPlayerContext.LocalCharacterRoot || player.CurrentHp <= 0 ||
                player.CharacterData == null || !MaisonCompanionReunion.IsLucian(player.CharacterData.characterId, player.CharacterData.characterName) ||
                !CharacterInteractionDetection.IsCharacterWithinRange(player.transform, source.GetInteractionDetectionCollider(),
                    source.GetInteractionAnchor(), source.GetInteractionMaxDistance(player))) return true;
            if (!BindGroup(out string configurationIssue))
            {
                // Companions absent through progression still allow the foyer's normal sitting interaction.
                if (configurationIssue == null) return false;
                Debug.LogWarning("[Maison Hearth] Veillee indisponible : " + configurationIssue + ".", this);
                return true;
            }
            var actor = player.GetComponent<StorySequenceActor>();
            if (actor == null) return true;
            bindings.BindRuntimeActor("Lucian", actor);
            preparation = StartCoroutine(Prepare(player));
            result = true;
            return true;
        }

        private IEnumerator Prepare(SquadCharacterController player)
        {
            // Let Foyer's interaction dispatch finish before the runner claims its input map.
            yield return null;
            if (player == null || !IsHouseReady || !IsUnlocked) { preparation = null; yield break; }
            if (player.IsInSittingCycle)
            {
                player.TrySetSitting(false);
                float deadline = Time.realtimeSinceStartup + 4f;
                while (player != null && player.IsInSittingCycle && IsHouseReady && Time.realtimeSinceStartup < deadline) yield return null;
            }
            preparation = null;
            if (player == null || player.IsInSittingCycle || !IsHouseReady || !IsUnlocked || !BindGroup() ||
                InputModeCoordinator.CurrentMode != InputMode.Exploration || InputFocusStack.HasAnyFocus()) yield break;
            runner.Play();
        }

        public void SeatLucian()
        {
            var root = LocalPlayerContext.LocalCharacterRoot;
            seatedPlayer = root != null ? root.GetComponent<SquadCharacterController>() : null;
            if (seatedPlayer == null || !seatedPlayer.TryBeginStorySitting())
            {
                Debug.LogWarning("[Maison Hearth] Seating failed; conversation remains available.", this);
                runner.Abort();
            }
        }

        // Authored immediately after the fade to black: no visible NPC teleport or controller switch.
        public void GatherAtHearth()
        {
            if (!BindGroup()) { runner.Abort(); return; }
            foreach (var seat in companionSeats)
            {
                var actor = FindCompanion(seat.actorId);
                if (!poses.ContainsKey(actor)) poses.Add(actor, new ActorPose { position = actor.transform.position,
                    rotation = actor.transform.rotation, controller = actor.Animator.runtimeAnimatorController });
                actor.transform.SetPositionAndRotation(seat.point.position, seat.point.rotation);
                actor.Animator.runtimeAnimatorController = seatedController;
                actor.Animator.Play("Sitting_Idle", 0, 0);
                actor.Animator.Update(0);
            }
            staged = true;
            if (!ownsPortalVisibility)
            {
                portalWasActive = portal.activeSelf;
                ownsPortalVisibility = true;
            }
            portal.SetActive(false);
        }

        public void OnPortalSpawned(GameObject instance)
        {
            ResolveReferences();
            var original = portal != null ? portal.GetComponent<PortalController>() : null;
            var created = instance != null ? instance.GetComponent<PortalController>() : null;
            if (original == null || created == null) throw new InvalidOperationException("Hearth portal requires the authored destination portal and a PortalController prefab.");
            created.CopyDestinationFrom(original);
            portalRevealed = true;
            portal.SetActive(false);
        }

        public void CompleteVigil()
        {
            if (!portalRevealed) { RestoreStaging(); return; }
            ownsPortalVisibility = false;
            if (IsAuthority && Rules != null) Rules.SetBool(PortalOpenKey, true);
            StandLucian();
            // The three narrative companions remain around the fire for the rest of this visit.
        }
        public void AbortVigil() { StandLucian(); RestoreStaging(); }
        private void StandLucian()
        {
            if (seatedPlayer != null) seatedPlayer.TrySetSitting(false);
            seatedPlayer = null;
        }
        private void RestoreStaging()
        {
            foreach (var pair in poses)
                if (pair.Key != null)
                {
                    pair.Key.transform.SetPositionAndRotation(pair.Value.position, pair.Value.rotation);
                    if (pair.Key.Animator != null) pair.Key.Animator.runtimeAnimatorController = pair.Value.controller;
                }
            poses.Clear();
            if (ownsPortalVisibility && portal != null)
            {
                portal.SetActive(portalWasActive);
            }
            ownsPortalVisibility = portalRevealed = staged = false;
        }
        private void Update()
        {
            if (Time.unscaledTime >= nextReferenceCheck)
            {
                nextReferenceCheck = Time.unscaledTime + .25f;
                ResolveReferences();
                if (IsCompleted && portal != null && IsHouseReady)
                {
                    runner.RestoreCompletedSpawns();
                    if (portal.activeSelf) portal.SetActive(false);
                    // Also repairs an older local completion whose shared gate was not saved yet.
                    if (IsAuthority && Rules != null && !SharedCompletion) Rules.SetBool(PortalOpenKey, true);
                }
                if (prompt != null)
                {
                    prompt.target = foyer;
                    bool groupPresent = group != null && companionSeats.Length == 3 && Array.TrueForAll(companionSeats,
                        seat => seat != null && FindCompanion(seat.actorId) != null && FindCompanion(seat.actorId).gameObject.activeInHierarchy);
                    prompt.label = IsUnlocked && IsAuthority && groupPresent ? "Partager la veill\u00e9e" : "S'asseoir";
                }
            }
            if (runner != null && runner.IsPlaying && (!IsHouseReady || LocalPlayerContext.LocalCharacterRoot == null)) runner.Abort();
        }
        private void OnDisable()
        {
            if (preparation != null) StopCoroutine(preparation);
            preparation = null;
            if (runner != null && runner.IsPlaying) runner.Abort();
            StandLucian();
            RestoreStaging();
        }
    }
}
