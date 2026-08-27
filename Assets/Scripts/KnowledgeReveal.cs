using System.Collections.Generic;
using UnityEngine;

/// <summary>Point d'entree unique des sources de Savoirs.</summary>
public static class KnowledgeReveal
{
    public static int Reveal(IReadOnlyList<KnowledgeSO> knowledge, GameObject revealer, string origin)
    {
        if (knowledge == null) return 0;
        int requested = 0;
        string name = ResolveRevealerName(revealer);
        for (int i = 0; i < knowledge.Count; i++)
        {
            if (Reveal(knowledge[i], name, origin)) requested++;
        }
        return requested;
    }

    public static bool Reveal(KnowledgeSO knowledge, GameObject revealer, string origin) => Reveal(knowledge, ResolveRevealerName(revealer), origin);

    public static bool Reveal(KnowledgeSO knowledge, string revealerName, string origin)
    {
        if (knowledge == null) return false;
        KnowledgeSynchronizationService service = KnowledgeSynchronizationService.Instance;
        if (service != null && service.IsSpawned) return service.RequestReveal(knowledge, revealerName, origin);
        return RequestLocalReveal(knowledge, revealerName, origin);
    }

    internal static bool RequestLocalReveal(KnowledgeSO knowledge, string revealerName, string origin)
    {
        return KnowledgeManager.GetOrCreate().ApplyValidatedKnowledge(knowledge);
    }

    public static string ResolveRevealerName(GameObject revealer)
    {
        if (revealer == null) revealer = LocalPlayerUtils.GetControlledCharacter();
        SquadCharacterController controller = revealer != null ? revealer.GetComponentInParent<SquadCharacterController>() : null;
        if (controller != null && controller.CharacterData != null && !string.IsNullOrWhiteSpace(controller.CharacterData.characterName)) return controller.CharacterData.characterName;
        return revealer != null ? revealer.name : "L'equipe";
    }
}
