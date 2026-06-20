using System;
using UnityEngine;

namespace Lit.Story
{
    public static class StorySequenceCompletionStore
    {
        public static Func<StorySequenceAsset, bool> IsCompletedOverride;
        public static Action<StorySequenceAsset> MarkCompletedOverride;

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

            return PlayerPrefs.GetInt(sequence.ProgressKey, 0) != 0;
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

            PlayerPrefs.SetInt(sequence.ProgressKey, 1);
            PlayerPrefs.Save();
        }

        public static void ResetCompletion(StorySequenceAsset sequence)
        {
            if (sequence == null)
            {
                return;
            }

            PlayerPrefs.DeleteKey(sequence.ProgressKey);
        }
    }
}
