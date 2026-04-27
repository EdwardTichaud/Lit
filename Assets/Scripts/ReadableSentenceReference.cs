using System;
using UnityEngine;

[Serializable]
public struct ReadableSentenceReference
{
    [SerializeField] private Item readableItem;
    [Min(1)]
    [SerializeField] private int sentenceNumber;
    [SerializeField] private string displayNameOverride;

    public Item ReadableItem => readableItem;

    public int SentenceNumber => Mathf.Max(1, sentenceNumber);

    public int SentenceIndex => SentenceNumber - 1;

    public bool IsConfigured => readableItem != null;

    public string ResolveDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayNameOverride))
        {
            return displayNameOverride.Trim();
        }

        if (readableItem == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(readableItem.itemName))
        {
            return readableItem.itemName.Trim();
        }

        return readableItem.name ?? string.Empty;
    }

    public bool TryGetGeneratedSentence(out string sentence)
    {
        sentence = string.Empty;
        return readableItem != null &&
            ReadableContentRuntime.TryGetGeneratedSentence(readableItem, SentenceIndex, out sentence);
    }
}
