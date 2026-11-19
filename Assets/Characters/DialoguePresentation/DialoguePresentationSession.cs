using System;
using UnityEngine;

/// <summary>A line's two physical participants, independent from portraits and UI lifetime.</summary>
public sealed class DialoguePresentationSession : IDisposable
{
    private CharacterDialoguePresentationController speaker, listener;
    private int speakerToken, listenerToken;

    public DialoguePresentationSession(UnityEngine.Object owner, Transform speakerRoot, Transform listenerRoot,
        bool stationary, DialogueDirection direction)
    {
        speaker = Resolve(speakerRoot);
        listener = Resolve(listenerRoot);
        if (listener == speaker) listener = null;
        if (speaker != null) speakerToken = speaker.Begin(owner, listenerRoot, stationary, direction, DialogueRole.Speaking);
        if (listener != null) listenerToken = listener.Begin(owner, speakerRoot, stationary, null, DialogueRole.Listening);
    }

    public void FinishSpeaking()
    {
        if (speaker != null) speaker.FinishSpeaking(speakerToken);
    }

    public void Dispose()
    {
        if (speaker != null) speaker.End(speakerToken);
        if (listener != null) listener.End(listenerToken);
        speaker = listener = null;
    }

    private static CharacterDialoguePresentationController Resolve(Transform root)
    {
        return root == null ? null : root.GetComponentInParent<CharacterDialoguePresentationController>(true);
    }

    public static Transform ResolveSpeaker(CharacterData data)
    {
        if (data == null) return null;
        Transform result = null;
        foreach (var actor in UnityEngine.Object.FindObjectsByType<CharacterDialoguePresentationController>(FindObjectsInactive.Exclude))
        {
            if (actor.CharacterData != data) continue;
            if (result != null) return null;
            result = actor.transform;
        }
        return result;
    }
}
