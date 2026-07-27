using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class KnowledgeCombatPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text knowledgeListText;
    [SerializeField] private string emptyText = "Aucun savoir de combat actif.";

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (knowledgeListText == null)
        {
            return;
        }

        KnowledgeManager manager = KnowledgeManager.Instance;
        if (manager == null)
        {
            knowledgeListText.text = emptyText;
            return;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < manager.UnlockedKnowledge.Count; i++)
        {
            KnowledgeSO knowledge = manager.UnlockedKnowledge[i];
            if (knowledge == null || !knowledge.CombatBonusEnabled)
            {
                continue;
            }

            if (builder.Length > 0) builder.AppendLine();
            CombatKnowledgeModifier modifier = knowledge.CombatModifier;
            builder.Append(string.IsNullOrWhiteSpace(knowledge.title) ? knowledge.name : knowledge.title);
            builder.Append("  Lumiere x").Append(modifier.lightDamageMultiplier.ToString("0.##"));
            if (modifier.clarityBonus != 0f) builder.Append("  Clarite +").Append(modifier.clarityBonus.ToString("0.##"));
        }

        knowledgeListText.text = builder.Length > 0 ? builder.ToString() : emptyText;
    }
}
