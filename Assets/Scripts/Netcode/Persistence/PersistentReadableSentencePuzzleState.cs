using System;
using UnityEngine;

// Role: sauvegarde/restaure l'etat resolu d'un ReadableSentencePuzzle.
// Usage: attache au meme GameObject qu'un puzzle de phrase lisible persistant.
// Responsibilities: capturer l'etat, appliquer l'etat sauvegarde a la phase gameplay.
// Dependencies: ReadableSentencePuzzle, IPersistentStateProvider, PersistentStateJson.
// Precautions: garder ProviderId stable, car il identifie les donnees dans les sauvegardes.
/// <summary>
/// Provider de persistance pour les puzzles de phrase lisible.
/// </summary>
[DisallowMultipleComponent]
public class PersistentReadableSentencePuzzleState : MonoBehaviour, IPersistentStateProvider
{
    /// <summary>
    /// Donnee minimale sauvegardee pour un puzzle lisible.
    /// </summary>
    [Serializable]
    private sealed class ReadableSentencePuzzleStateData
    {
        public bool Solved;
    }

    /// <summary>
    /// Puzzle dont l'etat resolu doit etre persiste.
    /// </summary>
    [SerializeField] private ReadableSentencePuzzle puzzle;

    /// <summary>
    /// Identifiant stable du provider dans le systeme de persistance.
    /// </summary>
    public string ProviderId => "readable_sentence_puzzle";

    private void Awake()
    {
        // Unity appelle Awake au chargement; le puzzle est souvent sur le meme GameObject.
        if (puzzle == null)
        {
            puzzle = GetComponent<ReadableSentencePuzzle>();
        }
    }

    /// <summary>
    /// Capture l'etat resolu courant sous forme de JSON binaire.
    /// </summary>
    public byte[] CaptureState(PersistentStateContext context)
    {
        if (puzzle == null)
        {
            PersistentStateValidation.LogValidation(
                "readable_sentence_puzzle",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' puzzleMissing=true capture=true",
                this,
                context);
            return Array.Empty<byte>();
        }

        return PersistentStateJson.ToBytes(new ReadableSentencePuzzleStateData
        {
            Solved = puzzle.IsSolved
        });
    }

    /// <summary>
    /// Applique l'etat sauvegarde pendant la phase gameplay de restauration.
    /// </summary>
    public void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (phase != PersistentApplyPhase.ApplyGameplayState)
        {
            return;
        }

        if (puzzle == null)
        {
            PersistentStateValidation.LogValidation(
                "readable_sentence_puzzle",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' puzzleMissing=true apply=true",
                this,
                context);
            return;
        }

        if (!PersistentStateJson.TryFromBytes(state, ProviderId, puzzle, context, out ReadableSentencePuzzleStateData data))
        {
            return;
        }

        puzzle.RestoreSolvedState(data.Solved);
        PersistentStateValidation.LogValidation(
            "readable_sentence_puzzle",
            puzzle.IsSolved == data.Solved,
            $"persistentId='{PersistentStateValidation.ResolvePersistentId(puzzle)}' solved={puzzle.IsSolved}",
            puzzle,
            context);
    }
}
