using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lit.Story
{
    public sealed partial class StorySequenceRunner
    {
        private sealed class SpawnedObject { public GameObject instance; public bool keep; }
        private readonly Dictionary<string, SpawnedObject> sequenceSpawns = new Dictionary<string, SpawnedObject>(StringComparer.OrdinalIgnoreCase);

        public GameObject GetSpawnedObject(string id) => !string.IsNullOrWhiteSpace(id) && sequenceSpawns.TryGetValue(id.Trim(), out var value) ? value.instance : null;

        public bool TryInstantiateStep(StorySequenceStep step)
        {
            if (step == null || step.type != StorySequenceStepType.InstantiateGameObject || step.spawnPrefab == null || string.IsNullOrWhiteSpace(step.spawnId))
                return false;
            ResolveReferences();
            string id = step.spawnId.Trim();
            if (GetSpawnedObject(id) != null) return true;
            var binding = bindings != null ? bindings.ResolveSpawnPoint(step.spawnPointId) : null;
            if (binding?.point == null)
            {
                Debug.LogError($"StorySequenceRunner: spawn '{id}' requires scene Transform '{step.spawnPointId}'.", this);
                return false;
            }
            GameObject instance = null;
            try
            {
                instance = Instantiate(step.spawnPrefab, binding.point.position, binding.point.rotation, binding.point);
                if (!step.parentToSpawnPoint)
                {
                    instance.transform.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(instance, binding.point.gameObject.scene);
                }
                binding.onSpawned?.Invoke(instance);
                sequenceSpawns[id] = new SpawnedObject { instance = instance, keep = step.keepSpawnAfterCompletion };
                return true;
            }
            catch (Exception exception)
            {
                RemoveSpawn(instance);
                Debug.LogException(exception, this);
                return false;
            }
        }

        // The caller confirms completion, allowing existing authoritative shared-world gates to restore too.
        public void RestoreCompletedSpawns()
        {
            if (sequence?.steps == null) return;
            foreach (var step in sequence.steps)
                if (step != null && step.type == StorySequenceStepType.InstantiateGameObject && step.keepSpawnAfterCompletion)
                    TryInstantiateStep(step);
        }

        private void ReleaseSequenceSpawns(bool completed)
        {
            var removed = new List<string>();
            foreach (var pair in sequenceSpawns)
            {
                if (completed && pair.Value.keep) continue;
                RemoveSpawn(pair.Value.instance);
                removed.Add(pair.Key);
            }
            foreach (var id in removed) sequenceSpawns.Remove(id);
        }

        private static void RemoveSpawn(GameObject instance)
        {
            if (instance == null) return;
            instance.SetActive(false);
            if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
        }

        private void OnDestroy() => ReleaseSequenceSpawns(false);
    }
}
