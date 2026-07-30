using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Lit.Timeline
{
    [Serializable]
    public sealed class TimelineBindingDefinition
    {
        [Tooltip("Piste Timeline a lier. Elle doit appartenir a la Timeline de ce profil.")]
        public TrackAsset track;
        [Tooltip("Cle stable resolue par TimelineManager.")]
        public string bindingId;
        [Tooltip("Une piste requise empeche la lecture si sa cible est absente ou incompatible.")]
        public bool required = true;
    }

    [CreateAssetMenu(fileName = "TimelineBindingProfile", menuName = "Lit/Timeline/Binding Profile", order = 20)]
    public sealed class TimelineBindingProfile : ScriptableObject
    {
        [SerializeField] private PlayableAsset timeline;
        [SerializeField] private List<TimelineBindingDefinition> bindings = new List<TimelineBindingDefinition>();

        public PlayableAsset Timeline => timeline;
        public IReadOnlyList<TimelineBindingDefinition> Bindings => bindings;

        public bool Matches(PlayableAsset asset)
        {
            return timeline != null && timeline == asset;
        }

        public bool TryGetBinding(UnityEngine.Object sourceObject, out TimelineBindingDefinition definition)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                TimelineBindingDefinition candidate = bindings[i];
                if (candidate != null && candidate.track == sourceObject)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        private void OnValidate()
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                TimelineBindingDefinition binding = bindings[i];
                if (binding != null && !string.IsNullOrWhiteSpace(binding.bindingId))
                {
                    binding.bindingId = binding.bindingId.Trim();
                }
            }
        }
    }
}
