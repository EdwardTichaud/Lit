// Role:
// Lightweight bridge that unlocks KnowledgeSO entries from scene events.
// Usage:
// Add to rooms, clue props, anomalies, trigger volumes, or UnityEvent targets.
// Configure Knowledge To Unlock, then call UnlockKnowledge or enable trigger modes.
// Responsibilities:
// Convert exploration, observation, and simple interactions into persistent knowledge.
// Dependencies:
// KnowledgeManager and KnowledgeRequirement.
// Precautions:
// This component does not save anything by itself. KnowledgeManager persistence
// handles unlocked knowledge through the existing PersistentKnowledgeState provider.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Lit/Narrative/Knowledge Unlock Trigger")]
public class KnowledgeUnlockTrigger : MonoBehaviour
{
    [Header("Knowledge")]
    [SerializeField, Tooltip("Connaissances debloquees par ce trigger.")]
    private List<KnowledgeSO> knowledgeToUnlock = new List<KnowledgeSO>();
    [SerializeField, Tooltip("Conditions optionnelles avant debloquage.")]
    private KnowledgeRequirement requirement = new KnowledgeRequirement();

    [Header("Trigger Modes")]
    [SerializeField, Tooltip("Debloque a l'activation du GameObject.")]
    private bool unlockOnEnable;
    [SerializeField, Tooltip("Debloque quand le joueur entre dans ce trigger.")]
    private bool unlockOnTriggerEnter;
    [SerializeField, Tooltip("Si true, seul le personnage controle localement peut declencher.")]
    private bool requireControlledCharacter = true;
    [SerializeField, Tooltip("Si true, ce trigger ne debloque qu'une fois par session.")]
    private bool unlockOnce = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs;

    private bool unlockedThisSession;

    public IReadOnlyList<KnowledgeSO> KnowledgeToUnlock => knowledgeToUnlock;
    public bool HasUnlockedThisSession => unlockedThisSession;

    private void OnEnable()
    {
        if (unlockOnEnable)
        {
            UnlockKnowledge();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!unlockOnTriggerEnter || other == null)
        {
            return;
        }

        GameObject character = ResolveCharacterRoot(other);
        if (character == null)
        {
            return;
        }

        if (requireControlledCharacter && !IsControlledCharacter(character))
        {
            return;
        }

        UnlockKnowledge();
    }

    public int UnlockKnowledge()
    {
        if (unlockOnce && unlockedThisSession)
        {
            return 0;
        }

        KnowledgeManager manager = KnowledgeManager.GetOrCreate();
        if (manager == null || requirement != null && !requirement.IsSatisfied(manager))
        {
            return 0;
        }

        int count = manager.UnlockKnowledgeList(knowledgeToUnlock);
        if (count > 0)
        {
            unlockedThisSession = true;
            if (verboseLogs)
            {
                Debug.Log($"KnowledgeUnlockTrigger '{name}' unlocked {count} knowledge entries.", this);
            }
        }

        return count;
    }

    private static GameObject ResolveCharacterRoot(Collider collider)
    {
        SquadCharacterController controller = collider.GetComponentInParent<SquadCharacterController>();
        if (controller != null)
        {
            return controller.gameObject;
        }

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }

    private static bool IsControlledCharacter(GameObject character)
    {
        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        return controlled != null && character != null && controlled.transform.root == character.transform.root;
    }
}
