using System;
using UnityEngine;

// Role: reference une phrase precise dans un item lisible genere.
// Usage: stocke par les puzzles qui demandent au joueur de recopier une phrase d'un document.
// Responsibilities: exposer item, numero de phrase et nom lisible pour l'UI.
// Dependencies: Item, ReadableContentRuntime.
// Precautions: sentenceNumber est affiche en 1-based, mais l'acces aux listes reste en 0-based.
/// <summary>
/// Reference serialisable vers une phrase generee d'un item lisible.
/// </summary>
[Serializable]
public struct ReadableSentenceReference
{
    /// <summary>
    /// Item lisible qui porte la phrase cible.
    /// </summary>
    [SerializeField] private Item readableItem;
    /// <summary>
    /// Numero affiche a l'utilisateur. Il commence a 1 pour rester naturel dans l'interface.
    /// </summary>
    [Min(1)]
    [SerializeField] private int sentenceNumber;
    /// <summary>
    /// Nom optionnel utilise dans le prompt au lieu du nom de l'item.
    /// </summary>
    [SerializeField] private string displayNameOverride;

    /// <summary>
    /// Item lisible configure dans l'inspecteur.
    /// </summary>
    public Item ReadableItem => readableItem;

    /// <summary>
    /// Numero de phrase corrige pour rester au minimum a 1.
    /// </summary>
    public int SentenceNumber => Mathf.Max(1, sentenceNumber);

    /// <summary>
    /// Index zero-based utilise pour lire la liste generee.
    /// </summary>
    public int SentenceIndex => SentenceNumber - 1;

    /// <summary>
    /// Indique si une reference d'item existe.
    /// </summary>
    public bool IsConfigured => readableItem != null;

    /// <summary>
    /// Resout le nom a afficher dans l'UI du puzzle.
    /// </summary>
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

    /// <summary>
    /// Tente de recuperer la phrase generee referencee.
    /// </summary>
    public bool TryGetGeneratedSentence(out string sentence)
    {
        sentence = string.Empty;
        return readableItem != null &&
            ReadableContentRuntime.TryGetGeneratedSentence(readableItem, SentenceIndex, out sentence);
    }
}
