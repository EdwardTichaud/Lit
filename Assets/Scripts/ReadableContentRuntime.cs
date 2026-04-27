using System;
using System.Collections.Generic;
using UnityEngine;

public static class ReadableContentRuntime
{
    private sealed class GeneratedReadableContentState
    {
        public string contentKey;
        public int seed;
        public List<string> generatedSentences = new List<string>();
        public List<string> bookPages = new List<string>();
        public string parchmentText = string.Empty;
    }

    private static readonly Dictionary<string, GeneratedReadableContentState> generatedByContentKey = new Dictionary<string, GeneratedReadableContentState>();

    public static void ResetRuntimeState(string reason)
    {
        generatedByContentKey.Clear();
    }

    public static void RestoreSaveData(List<ReadableGeneratedContentData> savedStates)
    {
        generatedByContentKey.Clear();
        if (savedStates == null || savedStates.Count == 0)
        {
            return;
        }

        for (int i = 0; i < savedStates.Count; i++)
        {
            ReadableGeneratedContentData entry = savedStates[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId))
            {
                continue;
            }

            string contentKey = entry.itemId;
            Item resolvedItem = ResolveItem(entry.itemId);
            if (resolvedItem != null)
            {
                string resolvedContentKey = resolvedItem.GetReadableContentKey();
                if (!string.IsNullOrWhiteSpace(resolvedContentKey))
                {
                    contentKey = resolvedContentKey;
                }
            }

            generatedByContentKey[contentKey] = new GeneratedReadableContentState
            {
                contentKey = contentKey,
                seed = entry.seed,
                generatedSentences = CloneList(entry.generatedSentences),
                bookPages = CloneList(entry.bookPages),
                parchmentText = entry.parchmentText ?? string.Empty
            };
        }
    }

    public static List<ReadableGeneratedContentData> CaptureSaveData()
    {
        List<ReadableGeneratedContentData> result = new List<ReadableGeneratedContentData>(generatedByContentKey.Count);
        foreach (KeyValuePair<string, GeneratedReadableContentState> pair in generatedByContentKey)
        {
            GeneratedReadableContentState state = pair.Value;
            if (state == null || string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            result.Add(new ReadableGeneratedContentData
            {
                itemId = pair.Key,
                seed = state.seed,
                generatedSentences = CloneList(state.generatedSentences),
                bookPages = CloneList(state.bookPages),
                parchmentText = state.parchmentText ?? string.Empty
            });
        }

        return result;
    }

    public static bool EnsureGenerated(Item item)
    {
        return TryGetOrGenerateState(item, out _);
    }

    public static int GetBookPageCount(Item item)
    {
        return TryGetOrGenerateState(item, out GeneratedReadableContentState state)
            ? state.bookPages.Count
            : 0;
    }

    public static string GetBookPageText(Item item, int pageIndex)
    {
        if (!TryGetOrGenerateState(item, out GeneratedReadableContentState state))
        {
            return string.Empty;
        }

        if (pageIndex < 0 || pageIndex >= state.bookPages.Count)
        {
            return string.Empty;
        }

        return state.bookPages[pageIndex] ?? string.Empty;
    }

    public static string GetParchmentText(Item item)
    {
        return TryGetOrGenerateState(item, out GeneratedReadableContentState state)
            ? state.parchmentText ?? string.Empty
            : string.Empty;
    }

    public static int GetGeneratedSentenceCount(Item item)
    {
        return TryGetOrGenerateState(item, out GeneratedReadableContentState state)
            ? state.generatedSentences.Count
            : 0;
    }

    public static string GetGeneratedSentence(Item item, int index)
    {
        return TryGetGeneratedSentence(item, index, out string sentence)
            ? sentence
            : string.Empty;
    }

    public static bool TryGetGeneratedSentence(Item item, int index, out string sentence)
    {
        sentence = string.Empty;
        if (!TryGetOrGenerateState(item, out GeneratedReadableContentState state))
        {
            return false;
        }

        return TryGetGeneratedSentence(state, index, out sentence);
    }

    public static bool TryGetGeneratedSentence(string contentKey, int index, out string sentence)
    {
        sentence = string.Empty;
        if (string.IsNullOrWhiteSpace(contentKey))
        {
            return false;
        }

        if (generatedByContentKey.TryGetValue(contentKey, out GeneratedReadableContentState state) &&
            TryGetGeneratedSentence(state, index, out sentence))
        {
            return true;
        }

        Item item = ResolveItem(contentKey);
        return item != null && TryGetGeneratedSentence(item, index, out sentence);
    }

    private static bool TryGetGeneratedSentence(GeneratedReadableContentState state, int index, out string sentence)
    {
        sentence = string.Empty;
        if (state == null || index < 0 || index >= state.generatedSentences.Count)
        {
            return false;
        }

        sentence = state.generatedSentences[index] ?? string.Empty;
        return !string.IsNullOrWhiteSpace(sentence);
    }

    private static bool TryGetOrGenerateState(Item item, out GeneratedReadableContentState state)
    {
        state = null;
        if (item == null || !item.UsesRandomReadableSentences())
        {
            return false;
        }

        string contentKey = item.GetReadableContentKey();
        if (string.IsNullOrWhiteSpace(contentKey))
        {
            return false;
        }

        if (generatedByContentKey.TryGetValue(contentKey, out state) && state != null)
        {
            return true;
        }

        state = GenerateState(item, contentKey);
        if (state == null)
        {
            return false;
        }

        generatedByContentKey[contentKey] = state;
        return true;
    }

    private static GeneratedReadableContentState GenerateState(Item item, string contentKey)
    {
        List<string> candidates = item.CollectReadableSentenceCandidates();
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        int sentenceCount = item.GetValidatedGeneratedSentenceCount(candidates.Count);
        if (sentenceCount <= 0)
        {
            return null;
        }

        int seed = ResolveSeed(item, contentKey);
        Shuffle(candidates, new System.Random(seed));

        GeneratedReadableContentState state = new GeneratedReadableContentState
        {
            contentKey = contentKey,
            seed = seed
        };

        for (int i = 0; i < sentenceCount && i < candidates.Count; i++)
        {
            state.generatedSentences.Add(candidates[i]);
        }

        if (item.IsReadableBook())
        {
            state.bookPages.AddRange(state.generatedSentences);
        }
        else if (item.IsReadableParchment())
        {
            state.parchmentText = string.Join(" ", state.generatedSentences).Trim();
        }

        return state;
    }

    private static int ResolveSeed(Item item, string contentKey)
    {
        unchecked
        {
            int seed = GetStableHash(contentKey);
            seed = (seed * 397) ^ item.readableGenerationSeedOffset;

            // Prefer the shared multiplayer session code when available so host and clients
            // derive the same readable content before any save has been written.
            string sessionKey = ResolveSessionSeedKey();
            if (!string.IsNullOrWhiteSpace(sessionKey))
            {
                seed = (seed * 397) ^ GetStableHash(sessionKey);
            }

            return seed == 0 ? 1 : seed;
        }
    }

    private static string ResolveSessionSeedKey()
    {
        NetcodeLauncher launcher = FindComponent<NetcodeLauncher>();
        if (launcher != null &&
            launcher.TryGetLastConnectionAttempt(out NetcodeConnectionAttemptInfo attempt) &&
            attempt.SessionDerived &&
            !string.IsNullOrWhiteSpace(attempt.Code))
        {
            return $"netcode:{attempt.Code}";
        }

        SaveSessionManager session = SaveSessionManager.Instance;
        if (session != null)
        {
            if (!string.IsNullOrWhiteSpace(session.CurrentSessionId))
            {
                return $"session:{session.CurrentSessionId}";
            }

            if (!string.IsNullOrWhiteSpace(session.CurrentSaveId))
            {
                return $"save:{session.CurrentSaveId}";
            }
        }

        return string.Empty;
    }

    private static Item ResolveItem(string contentKey)
    {
        Item item = ItemRegistry.Resolve(contentKey);
        if (item != null)
        {
            return item;
        }

        Item[] items = Resources.FindObjectsOfTypeAll<Item>();
        for (int i = 0; i < items.Length; i++)
        {
            Item candidate = items[i];
            if (candidate == null)
            {
                continue;
            }

            if (string.Equals(candidate.GetReadableContentKey(), contentKey, StringComparison.Ordinal) ||
                string.Equals(ItemIdUtils.GetItemId(candidate), contentKey, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void Shuffle(List<string> values, System.Random random)
    {
        if (values == null || random == null)
        {
            return;
        }

        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            string temp = values[i];
            values[i] = values[swapIndex];
            values[swapIndex] = temp;
        }
    }

    private static List<string> CloneList(List<string> source)
    {
        if (source == null || source.Count == 0)
        {
            return new List<string>();
        }

        return new List<string>(source);
    }

    private static int GetStableHash(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return (int)hash;
        }
    }

    private static T FindComponent<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return UnityEngine.Object.FindObjectOfType<T>(true);
#endif
    }
}
