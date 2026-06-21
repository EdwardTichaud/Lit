using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lit.Story
{
    public static class StorySequenceCompletionStore
    {
        public static Func<StorySequenceAsset, bool> IsCompletedOverride;
        public static Action<StorySequenceAsset> MarkCompletedOverride;
        private static readonly HashSet<string> transientCompletedIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static bool HasActiveSave =>
            SaveSessionManager.Instance != null &&
            SaveSessionManager.Instance.HasActiveSave;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTransientState()
        {
            transientCompletedIds.Clear();
            IsCompletedOverride = null;
            MarkCompletedOverride = null;
        }

        public static bool IsCompleted(StorySequenceAsset sequence)
        {
            if (sequence == null)
            {
                return false;
            }

            if (IsCompletedOverride != null)
            {
                return IsCompletedOverride(sequence);
            }

            string id = ResolveSequenceId(sequence);
            SaveSessionManager session = SaveSessionManager.Instance;
            return session != null && session.HasActiveSave
                ? session.IsStorySequenceCompleted(id)
                : transientCompletedIds.Contains(id);
        }

        public static void MarkCompleted(StorySequenceAsset sequence)
        {
            if (sequence == null)
            {
                return;
            }

            if (MarkCompletedOverride != null)
            {
                MarkCompletedOverride(sequence);
                return;
            }

            string id = ResolveSequenceId(sequence);
            SaveSessionManager session = SaveSessionManager.Instance;
            if (session != null && session.HasActiveSave)
            {
                session.MarkStorySequenceCompleted(id);
            }
            else
            {
                transientCompletedIds.Add(id);
            }

            DeleteLegacyPlayerPrefsKey(sequence);
        }

        public static void ResetCompletion(StorySequenceAsset sequence)
        {
            if (sequence == null)
            {
                return;
            }

            string id = ResolveSequenceId(sequence);
            SaveSessionManager session = SaveSessionManager.Instance;
            if (session != null && session.HasActiveSave)
            {
                session.ResetStorySequenceCompletion(id);
            }

            transientCompletedIds.Remove(id);
            DeleteLegacyPlayerPrefsKey(sequence);
        }

        public static int ResetAllForActiveSave()
        {
            transientCompletedIds.Clear();
            SaveSessionManager session = SaveSessionManager.Instance;
            return session != null && session.HasActiveSave
                ? session.ResetAllStorySequenceCompletions()
                : 0;
        }

        public static string ResolveSequenceId(StorySequenceAsset sequence)
        {
            if (sequence == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(sequence.sequenceId)
                ? sequence.name
                : sequence.sequenceId.Trim();
        }

        private static void DeleteLegacyPlayerPrefsKey(StorySequenceAsset sequence)
        {
            if (sequence == null || !PlayerPrefs.HasKey(sequence.ProgressKey))
            {
                return;
            }

            PlayerPrefs.DeleteKey(sequence.ProgressKey);
            PlayerPrefs.Save();
        }
    }
}
