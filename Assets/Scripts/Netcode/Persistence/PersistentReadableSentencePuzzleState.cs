using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PersistentReadableSentencePuzzleState : MonoBehaviour, IPersistentStateProvider
{
    [Serializable]
    private sealed class ReadableSentencePuzzleStateData
    {
        public bool Solved;
    }

    [SerializeField] private ReadableSentencePuzzle puzzle;

    public string ProviderId => "readable_sentence_puzzle";

    private void Awake()
    {
        if (puzzle == null)
        {
            puzzle = GetComponent<ReadableSentencePuzzle>();
        }
    }

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
