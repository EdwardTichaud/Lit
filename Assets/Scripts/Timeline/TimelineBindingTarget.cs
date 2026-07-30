using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lit.Timeline
{
    [Serializable]
    public sealed class TimelineBindingTargetEntry
    {
        [Tooltip("Cle stable exposee aux TimelineBindingProfile.")]
        public string bindingId;
        [Tooltip("Objet exact a assigner a la piste (Animator, GameObject, SignalReceiver, etc.).")]
        public UnityEngine.Object target;
    }

    [DisallowMultipleComponent]
    public sealed class TimelineBindingTarget : MonoBehaviour
    {
        [SerializeField] private List<TimelineBindingTargetEntry> targets = new List<TimelineBindingTargetEntry>();

        public IReadOnlyList<TimelineBindingTargetEntry> Targets => targets;

        private void OnEnable()
        {
            if (TimelineManager.Instance == null)
            {
                Debug.LogWarning(
                    $"TimelineBindingTarget '{name}' ne peut pas s'enregistrer : TimelineManager Bootstrap absent.",
                    this);
                return;
            }

            TimelineManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            TimelineManager.Instance?.Unregister(this);
        }

        private void OnValidate()
        {
            for (int i = 0; i < targets.Count; i++)
            {
                TimelineBindingTargetEntry entry = targets[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.bindingId))
                {
                    entry.bindingId = entry.bindingId.Trim();
                }
            }
        }
    }
}
