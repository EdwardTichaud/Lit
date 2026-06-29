using UnityEngine;

public interface IInteractable
{
    GameObject GameObject { get; }
    void IncrementDialogueStage();
    void Interact();
}