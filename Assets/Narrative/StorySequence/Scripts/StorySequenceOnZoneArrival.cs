using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lit.Story
{
    /// <summary>Scene-owned, save-aware first arrival. No scene loading or player creation.</summary>
    [DisallowMultipleComponent]
    public sealed class StorySequenceOnZoneArrival : MonoBehaviour
    {
        [SerializeField] private StorySequenceRunner runner;
        [SerializeField] private StorySequenceSceneBindings bindings;
        [SerializeField] private string[] requiredSceneNames = System.Array.Empty<string>();
        [SerializeField] private ZoneManifest zoneManifest;
        private Coroutine routine;
        private bool ownsPlayback;

        private void OnEnable() => routine = StartCoroutine(AwaitArrival());

        private IEnumerator AwaitArrival()
        {
            // Let scene activation, player placement and other opening presentations settle first.
            yield return null;
            while (runner != null && runner.Sequence != null)
            {
                if (StorySequenceCompletionStore.IsCompleted(runner.Sequence)) break;
                if (!CanStart()) { yield return null; continue; }
                yield return LoadingScreenService.WaitUntilHidden();
                if (!CanStart()) continue;
                var player = LocalPlayerContext.LocalCharacterRoot;
                var controller = player != null ? player.GetComponent<SquadCharacterController>() : null;
                var data = controller != null ? controller.CharacterData : null;
                var actor = player != null ? player.GetComponent<StorySequenceActor>() : null;
                if (data == null || actor == null ||
                    !(string.Equals(data.characterId, "Lucian", System.StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(data.characterName, "Lucian", System.StringComparison.OrdinalIgnoreCase)))
                { yield return null; continue; }
                bindings.BindRuntimeActor("Lucian", actor);
                ownsPlayback = runner.Play();
                while (ownsPlayback && runner.IsPlaying)
                {
                    if (!IsDestinationActive())
                    {
                        runner.Abort();
                        break;
                    }
                    yield return null;
                }
                ownsPlayback = false;
                // An aborted presentation is not completed and remains available on a later arrival.
                yield return null;
            }
            routine = null;
        }

        private bool CanStart()
        {
            if (bindings == null || !IsDestinationActive() ||
                LoadingScreenService.IsLoading || GameFlowService.IsPreparingGameplayScene ||
                (GameFlowService.Instance != null && GameFlowService.Instance.IsTransitioning) ||
                StorySequenceRunner.IsAnySequencePlaying || InputModeCoordinator.IsGameplayBlocked)
                return false;
            var required = zoneManifest != null ? zoneManifest.LoadingSceneNames : requiredSceneNames;
            foreach (string name in required)
            {
                if (zoneManifest != null && CycleProgressionService.ShouldSkipScene(name)) continue;
                if (!SceneManager.GetSceneByName(name).isLoaded) return false;
            }
            return true;
        }

        private bool IsDestinationActive() => zoneManifest != null
            ? SceneManager.GetActiveScene().name == zoneManifest.PrimarySceneName
            : SceneManager.GetActiveScene() == gameObject.scene;

        private void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (ownsPlayback && runner != null && runner.IsPlaying) runner.Abort();
            ownsPlayback = false;
        }
    }
}
