using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lit.Timeline
{
    public enum TimelinePlaybackState
    {
        Pending,
        Playing,
        Completed,
        Failed,
        Stopped
    }

    [Serializable]
    public struct TimelinePlaybackOptions
    {
        public bool waitForRequiredBindings;
        [Min(0f)] public float requiredBindingsTimeout;

        public static TimelinePlaybackOptions Default => new TimelinePlaybackOptions
        {
            waitForRequiredBindings = false,
            requiredBindingsTimeout = 0f
        };
    }

    public sealed class TimelineBindingContext
    {
        private readonly Dictionary<string, UnityEngine.Object> overrides =
            new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);

        public TimelineBindingContext Bind(string bindingId, UnityEngine.Object target)
        {
            if (!string.IsNullOrWhiteSpace(bindingId))
            {
                overrides[bindingId.Trim()] = target;
            }

            return this;
        }

        internal bool TryResolve(string bindingId, out UnityEngine.Object target)
        {
            return overrides.TryGetValue(bindingId, out target);
        }
    }

    public sealed class TimelinePlaybackHandle
    {
        private readonly TimelineManager manager;

        internal TimelinePlaybackHandle(TimelineManager manager)
        {
            this.manager = manager;
            State = TimelinePlaybackState.Pending;
        }

        public TimelinePlaybackState State { get; internal set; }
        public string FailureReason { get; internal set; }
        public bool IsDone => State == TimelinePlaybackState.Completed ||
                              State == TimelinePlaybackState.Failed ||
                              State == TimelinePlaybackState.Stopped;
        public event Action<TimelinePlaybackHandle> Finished;

        public void Stop() => manager?.Stop(this, false);
        public void Skip() => manager?.Stop(this, true);

        internal void Finish(TimelinePlaybackState state, string failureReason = null)
        {
            if (IsDone)
            {
                return;
            }

            State = state;
            FailureReason = failureReason;
            Finished?.Invoke(this);
        }
    }
}
