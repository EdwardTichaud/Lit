using System;
using System.Collections.Generic;
using UnityEngine;

// Role: garde en memoire le contenu genere des documents lisibles pendant une session.
// Usage: appele par les items lisibles, les puzzles de phrase et la sauvegarde.
// Responsibilities: generer des phrases stables, restaurer/capturer l'etat sauvegarde, fournir pages et parchemins.
// Dependencies: Item, ItemRegistry, ReadableGeneratedContentData, NetcodeLauncher, SaveSessionManager.
// Precautions: ne pas changer les cles de contenu ou la logique de seed sans migrer les sauvegardes existantes.
/// <summary>
/// Cache runtime pour les contenus lisibles generes a partir des donnees d'un <see cref="Item"/>.
/// </summary>
public static class ReadableContentRuntime
{
    /// <summary>
    /// Etat interne sauvegardable pour un item lisible deja genere.
    /// </summary>
    private sealed class GeneratedReadableContentState
    {
        public string contentKey;
        public int seed;
        public List<string> generatedSentences = new List<string>();
        public List<string> bookPages = new List<string>();
        public string parchmentText = string.Empty;
    }

    private static readonly Dictionary<string, GeneratedReadableContentState> generatedByContentKey = new Dictionary<string, GeneratedReadableContentState>();
    private static int runtimeSeedSalt = GenerateRuntimeSeedSalt();

    /// <summary>
    /// Vide le cache de generation et force un nouveau sel runtime pour les lancements hors sauvegarde.
    /// </summary>
    public static void ResetRuntimeState(string reason)
    {
        generatedByContentKey.Clear();
        runtimeSeedSalt = GenerateRuntimeSeedSalt();
    }

    /// <summary>
    /// Recharge les contenus lisibles deja generes depuis les donnees de sauvegarde.
    /// </summary>
    public static void RestoreSaveData(List<ReadableGeneratedContentData> savedStates)
    {
        generatedByContentKey.Clear();
        runtimeSeedSalt = GenerateRuntimeSeedSalt();
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

            // Les anciennes sauvegardes peuvent stocker un itemId alors que le systeme actuel prefere la contentKey.
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

    /// <summary>
    /// Capture les contenus generes afin que la sauvegarde restaure exactement les memes textes.
    /// </summary>
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

    /// <summary>
    /// Force la generation du contenu lisible si l'item est configure pour cela.
    /// </summary>
    public static bool EnsureGenerated(Item item)
    {
        return TryGetOrGenerateState(item, out _);
    }

    /// <summary>
    /// Retourne le nombre de pages generees pour un livre lisible.
    /// </summary>
    public static int GetBookPageCount(Item item)
    {
        return TryGetOrGenerateState(item, out GeneratedReadableContentState state)
            ? state.bookPages.Count
            : 0;
    }

    /// <summary>
    /// Retourne le texte d'une page generee, ou une chaine vide si l'index est invalide.
    /// </summary>
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

    /// <summary>
    /// Retourne le texte genere pour un parchemin lisible.
    /// </summary>
    public static string GetParchmentText(Item item)
    {
        return TryGetOrGenerateState(item, out GeneratedReadableContentState state)
            ? state.parchmentText ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// Retourne le nombre de phrases generees pour un item lisible.
    /// </summary>
    public static int GetGeneratedSentenceCount(Item item)
    {
        return TryGetOrGenerateState(item, out GeneratedReadableContentState state)
            ? state.generatedSentences.Count
            : 0;
    }

    /// <summary>
    /// Retourne une phrase generee par index, ou une chaine vide si elle n'existe pas.
    /// </summary>
    public static string GetGeneratedSentence(Item item, int index)
    {
        return TryGetGeneratedSentence(item, index, out string sentence)
            ? sentence
            : string.Empty;
    }

    /// <summary>
    /// Tente de recuperer une phrase generee depuis un item.
    /// </summary>
    public static bool TryGetGeneratedSentence(Item item, int index, out string sentence)
    {
        sentence = string.Empty;
        if (!TryGetOrGenerateState(item, out GeneratedReadableContentState state))
        {
            return false;
        }

        return TryGetGeneratedSentence(state, index, out sentence);
    }

    /// <summary>
    /// Tente de recuperer une phrase generee depuis une cle de contenu.
    /// </summary>
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
        // Le shuffle determine quelles phrases deviennent les pages ou le texte du parchemin.
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
            else
            {
                // Direct scene launches may not have a save/session yet; keep those runs varied.
                seed = (seed * 397) ^ GetRuntimeSeedSalt();
            }

            return seed == 0 ? 1 : seed;
        }
    }

    private static int GetRuntimeSeedSalt()
    {
        if (runtimeSeedSalt == 0)
        {
            runtimeSeedSalt = GenerateRuntimeSeedSalt();
        }

        return runtimeSeedSalt;
    }

    private static int GenerateRuntimeSeedSalt()
    {
        unchecked
        {
            long ticks = DateTime.UtcNow.Ticks;
            int seed = Guid.NewGuid().GetHashCode();
            seed = (seed * 397) ^ Environment.TickCount;
            seed = (seed * 397) ^ (int)ticks;
            seed = (seed * 397) ^ (int)(ticks >> 32);
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
            // Hash FNV-1a stable: contrairement a string.GetHashCode(), il ne change pas entre executions.
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
