using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lit.Story
{
    /// <summary>House-only narrative actors, deliberately separate from playable squad members.</summary>
    [DisallowMultipleComponent]
    public sealed class MaisonCompanionReunion : MonoBehaviour
    {
        [Serializable]
        public sealed class Companion
        {
            public string actorId;
            public StorySequenceActor presentationPrefab;
            public Transform waitingPoint;
            [NonSerialized] public StorySequenceActor instance;
        }

        [SerializeField] private StorySequenceRunner runner;
        [SerializeField] private StorySequenceSceneBindings bindings;
        [SerializeField] private Companion[] companions = Array.Empty<Companion>();
        [SerializeField, Min(0.1f)] private float approachDistance = 3f;
        [SerializeField, Min(0.1f)] private float maximumHeightDifference = 1.5f;
        private bool ready;
        private float nextCheck;

        public Companion[] Companions => companions;

        private IEnumerator Start()
        {
            EnsurePresentations();
            yield return LoadingScreenService.WaitUntilHidden();
            ready = true;
        }

        public void EnsurePresentations()
        {
            if (bindings == null) return;
            foreach (var companion in companions)
            {
                if (companion == null || companion.presentationPrefab == null || companion.waitingPoint == null)
                    continue;
                if (companion.instance == null)
                {
                    companion.instance = Instantiate(companion.presentationPrefab,
                        companion.waitingPoint.position, companion.waitingPoint.rotation, transform);
                    companion.instance.name = companion.actorId + "_Maison";
                }
                bindings.BindRuntimeActor(companion.actorId, companion.instance);
            }
        }

        private void Update()
        {
            if (!ready || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.2f;
            if (runner == null || runner.Sequence == null || runner.IsPlaying ||
                StorySequenceRunner.IsAnySequencePlaying || LoadingScreenService.IsLoading ||
                InputModeCoordinator.IsGameplayBlocked ||
                (GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning) ||
                SceneManager.GetActiveScene() != gameObject.scene ||
                StorySequenceCompletionStore.IsCompleted(runner.Sequence)) return;

            Transform player = LocalPlayerContext.LocalCharacterRoot;
            if (player == null) return;
            var character = player.GetComponent<SquadCharacterController>();
            var data = character != null ? character.CharacterData : null;
            if (data == null || !IsLucian(data.characterId, data.characterName)) return;

            // Never begin with a missing speaker, nor through the floor of another storey.
            bool near = false;
            foreach (var companion in companions)
            {
                if (companion == null || companion.instance == null || !companion.instance.gameObject.activeInHierarchy)
                    return;
                near |= IsWithinApproach(player.position, companion.instance.transform.position,
                    approachDistance, maximumHeightDifference);
            }
            if (!near || companions.Length == 0) return;
            var playerActor = player.GetComponent<StorySequenceActor>();
            if (playerActor == null) return;
            bindings.BindRuntimeActor("Lucian", playerActor);
            foreach (var companion in companions)
            {
                // Presentation actors only: never rotate/teleport the player's UCC motor.
                Vector3 direction = Vector3.ProjectOnPlane(player.position - companion.instance.transform.position, Vector3.up);
                if (direction.sqrMagnitude > 0.001f)
                    companion.instance.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
            runner.Play();
        }

        public static bool IsLucian(string id, string displayName) =>
            string.Equals(id, "Lucian", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(displayName, "Lucian", StringComparison.OrdinalIgnoreCase);

        public static bool IsWithinApproach(Vector3 player, Vector3 companion, float distance, float height)
        {
            Vector3 offset = player - companion;
            return Mathf.Abs(offset.y) <= height && offset.x * offset.x + offset.z * offset.z <= distance * distance;
        }
    }
}
