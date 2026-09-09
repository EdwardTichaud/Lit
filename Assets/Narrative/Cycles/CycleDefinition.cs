using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CycleCondition
{
    [Min(0)] public int allFlags;
    [Min(0)] public int anyFlags;
    public KnowledgeSO[] knowledge = Array.Empty<KnowledgeSO>();
    public bool Matches(int state, Func<KnowledgeSO, bool> knows)
    {
        if ((state & allFlags) != allFlags || (anyFlags != 0 && (state & anyFlags) == 0)) return false;
        if (knowledge != null) foreach (var item in knowledge)
            if (item == null || knows == null || !knows(item)) return false;
        return true;
    }
}

[Serializable]
public sealed class CycleDialogue
{
    public string id;
    public CycleCondition condition = new CycleCondition();
    [TextArea] public string line;
    [TextArea, Tooltip("Optional dialogue before prerequisites are met; grants nothing.")] public string unavailableLine;
    [TextArea] public string repeatLine;
    [Min(0)] public int openedFlags;
    [Min(0)] public int completedFlags;
    public SkillSO rewardSkill;
    [Min(0), Tooltip("Unique single bit, set only after the dialogue closes naturally.")] public int rewardFlag;
    public bool HasReward(int state) => rewardFlag != 0 && (state & rewardFlag) == rewardFlag;
}

/// <summary>Flags are local to a cycle. Never renumber released flags or rename its ID.</summary>
[CreateAssetMenu(menuName = "Lit/Narrative/Cycle", fileName = "Cycle")]
public sealed class CycleDefinition : ScriptableObject
{
    public string cycleId;
    [Min(1)] public float dialogueSeconds = 4f;
    public CycleDialogue[] dialogues = Array.Empty<CycleDialogue>();
    [Header("Optional encounter")]
    [Min(0)] public int enemyDefeatedFlags = 1;
    public KnowledgeSO[] knowledgeOnEnemyDefeat = Array.Empty<KnowledgeSO>();
    public bool playCinematicAfterDefeat;
    [Min(0)] public float deathDelay = 3f;
    [Min(1)] public int cinematicCompletedFlags = 2;
    public string StateKey => "narrative." + cycleId;

    public CycleDialogue FindDialogue(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || dialogues == null) return null;
        foreach (var dialogue in dialogues)
            if (dialogue != null && dialogue.id == id) return dialogue;
        return null;
    }

    public List<string> ValidateConfiguration()
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(cycleId)) issues.Add("Assign a stable, unique cycle ID.");
        var ids = new HashSet<string>();
        int rewardBits = 0;
        if (dialogues != null) foreach (var dialogue in dialogues)
        {
            if (dialogue == null) { issues.Add("Empty dialogue entry."); continue; }
            if (string.IsNullOrWhiteSpace(dialogue.id) || !ids.Add(dialogue.id)) issues.Add("Empty or duplicate dialogue ID: " + dialogue.id);
            if (string.IsNullOrWhiteSpace(dialogue.line)) issues.Add("Missing dialogue text: " + dialogue.id);
            if (dialogue.rewardFlag != 0 || dialogue.rewardSkill != null)
            {
                int flag = dialogue.rewardFlag;
                if (flag <= 0 || (flag & (flag - 1)) != 0 || (rewardBits & flag) != 0 || dialogue.rewardSkill == null)
                    issues.Add("Reward needs a skill and a unique positive single bit: " + dialogue.id);
                rewardBits |= flag;
            }
        }
        int transitionBits = enemyDefeatedFlags | (playCinematicAfterDefeat ? cinematicCompletedFlags : 0);
        if (dialogues != null) foreach (var dialogue in dialogues)
            if (dialogue != null) transitionBits |= dialogue.openedFlags | dialogue.completedFlags;
        if ((rewardBits & transitionBits) != 0) issues.Add("Reward bits must not be granted by other transitions.");
        if (playCinematicAfterDefeat && (enemyDefeatedFlags <= 0 || cinematicCompletedFlags <= 0 ||
            (enemyDefeatedFlags & cinematicCompletedFlags) != 0)) issues.Add("Encounter and cinematic flags must be distinct and positive.");
        return issues;
    }
}
