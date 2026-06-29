using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regroupe toutes les données permettant de décrire un ensemble
/// préconfiguré pour un personnage jouable.
/// </summary>
[System.Serializable]
public class CharacterMusicalMoveSet
{
    [Tooltip("Nom affiché dans l'éditeur pour identifier rapidement ce set.")]
    public string setName = "Répertoire principal";

    [Tooltip("Liste des attaques musicales mises en avant pour ce set.")]
    public List<MusicalMoveSO> prioritizedMoves = new();
}

/// <summary>
/// Décrit un regroupement d'items favoris pour accélérer la navigation dans les menus.
/// </summary>
[System.Serializable]
public class CharacterItemSet
{
    [Tooltip("Nom affiché dans l'éditeur pour identifier rapidement ce set d'objets.")]
    public string setName = "Trousses favorites";

    [Tooltip("Items à mettre en tête de liste lorsque ce set est actif.")]
    public List<ItemData> prioritizedItems = new();
}
