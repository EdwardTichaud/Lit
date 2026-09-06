using UnityEngine;

[CreateAssetMenu(menuName = "Lit/Narrative/Nina Cycle", fileName = "NinaCycle")]
public sealed class NinaCycleDefinition : ScriptableObject
{
    public string cycleId = "district1.nina";
    public KnowledgeSO existence;
    public KnowledgeSO dilemma;
    public SkillSO cicatrice;
    [Min(0)] public float deathDelay = 3f;
    [Min(1)] public float dialogueSeconds = 4f;
    [TextArea] public string idleLine = "É... douard ? Tu reviendras ? Je ne veux pas rester seule...";
    [TextArea] public string deadLine = "Le souvenir se précise. Nina ne bouge plus. Même la science ne pouvait justifier ce qu'on lui avait fait.";
    [TextArea] public string scarLine = "Certaines blessures ne se referment jamais. Prends cette force, et souviens-toi d'elle.";
    public string StateKey => "narrative." + cycleId;
}
