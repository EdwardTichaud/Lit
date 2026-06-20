using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace Lit.Story
{
    [Serializable]
    public sealed class StorySequenceActorBinding
    {
        public string actorId;
        public StorySequenceActor actor;
        [Tooltip("Resout cet identifiant vers le personnage controle localement.")]
        public bool useLocalPlayer;
    }

    [Serializable]
    public sealed class StorySequenceDirectorBinding
    {
        public string directorId;
        public PlayableDirector director;
    }

    [Serializable]
    public sealed class StorySequenceEventBinding
    {
        public string eventId;
        public UnityEvent callback;
    }

    [DisallowMultipleComponent]
    public sealed class StorySequenceSceneBindings : MonoBehaviour
    {
        [SerializeField] private List<StorySequenceActorBinding> actors = new List<StorySequenceActorBinding>();
        [SerializeField] private List<StorySequenceCameraPoint> cameraPoints = new List<StorySequenceCameraPoint>();
        [SerializeField] private List<StorySequenceDirectorBinding> directors = new List<StorySequenceDirectorBinding>();
        [SerializeField] private List<StorySequenceEventBinding> events = new List<StorySequenceEventBinding>();
        [SerializeField] private bool searchSceneWhenBindingMissing = true;

        private StorySequenceActor runtimeLocalPlayerActor;

        public StorySequenceActor ResolveActor(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return null;
            }

            string id = actorId.Trim();
            for (int i = 0; i < actors.Count; i++)
            {
                StorySequenceActorBinding binding = actors[i];
                if (binding == null ||
                    !string.Equals(binding.actorId, id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (binding.useLocalPlayer)
                {
                    return ResolveLocalPlayerActor(id);
                }

                if (binding.actor != null)
                {
                    return binding.actor;
                }
            }

            if (IsLocalPlayerAlias(id))
            {
                return ResolveLocalPlayerActor(id);
            }

            if (!searchSceneWhenBindingMissing)
            {
                return null;
            }

            StorySequenceActor[] sceneActors = FindObjectsByType<StorySequenceActor>(
                FindObjectsInactive.Include);
            for (int i = 0; i < sceneActors.Length; i++)
            {
                if (sceneActors[i] != null && sceneActors[i].Matches(id))
                {
                    return sceneActors[i];
                }
            }

            return null;
        }

        public StorySequenceCameraPoint ResolveCameraPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
            {
                return null;
            }

            string id = pointId.Trim();
            for (int i = 0; i < cameraPoints.Count; i++)
            {
                StorySequenceCameraPoint point = cameraPoints[i];
                if (point != null && point.Matches(id))
                {
                    return point;
                }
            }

            if (!searchSceneWhenBindingMissing)
            {
                return null;
            }

            StorySequenceCameraPoint[] scenePoints = FindObjectsByType<StorySequenceCameraPoint>(
                FindObjectsInactive.Include);
            for (int i = 0; i < scenePoints.Length; i++)
            {
                if (scenePoints[i] != null && scenePoints[i].Matches(id))
                {
                    return scenePoints[i];
                }
            }

            return null;
        }

        public PlayableDirector ResolveDirector(string directorId)
        {
            if (!string.IsNullOrWhiteSpace(directorId))
            {
                for (int i = 0; i < directors.Count; i++)
                {
                    StorySequenceDirectorBinding binding = directors[i];
                    if (binding != null &&
                        binding.director != null &&
                        string.Equals(binding.directorId, directorId.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return binding.director;
                    }
                }
            }

            return GetComponent<PlayableDirector>();
        }

        public bool InvokeEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return false;
            }

            for (int i = 0; i < events.Count; i++)
            {
                StorySequenceEventBinding binding = events[i];
                if (binding == null ||
                    !string.Equals(binding.eventId, eventId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                binding.callback?.Invoke();
                return true;
            }

            return false;
        }

        private StorySequenceActor ResolveLocalPlayerActor(string requestedId)
        {
            Transform root = LocalPlayerContext.LocalCharacterRoot;
            if (root == null)
            {
                return null;
            }

            if (runtimeLocalPlayerActor != null && runtimeLocalPlayerActor.transform == root)
            {
                return runtimeLocalPlayerActor;
            }

            runtimeLocalPlayerActor = root.GetComponent<StorySequenceActor>();
            if (runtimeLocalPlayerActor == null)
            {
                runtimeLocalPlayerActor = root.gameObject.AddComponent<StorySequenceActor>();
            }

            runtimeLocalPlayerActor.ConfigureRuntime(requestedId, ResolveLocalPlayerName(root));
            return runtimeLocalPlayerActor;
        }

        private static string ResolveLocalPlayerName(Transform root)
        {
            SquadCharacterController controller = root != null
                ? root.GetComponent<SquadCharacterController>()
                : null;
            CharacterData data = controller != null ? controller.CharacterData : null;
            return data != null && !string.IsNullOrWhiteSpace(data.characterName)
                ? data.characterName
                : root != null ? root.name : "Player";
        }

        private static bool IsLocalPlayerAlias(string id)
        {
            return string.Equals(id, "player", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "lucian", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "localplayer", StringComparison.OrdinalIgnoreCase);
        }
    }
}
