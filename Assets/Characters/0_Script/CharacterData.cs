using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum CombatHealthThresholdSuccessResult
{
    KillEnemy,
    ResumeCombat
}

/// <summary>Buttons that an authored health-threshold sequence may request.</summary>
public enum CombatThresholdQteInput
{
    Y,
    B,
    A,
    X
}

/// <summary>
/// Authored health breakpoint for a real-time enemy.
/// </summary>
[System.Serializable]
public sealed class CombatHealthThresholdStage
{
    [Range(1, 99), Tooltip("Pourcentage de PV auquel la scenette s'arrete.")]
    public int healthPercent = 50;
    [Tooltip("Sequence autonome qui pilote les animations de Lucian, le QTE et ses resultats.")]
    public ThresholdSequence sequence;

    // Serialized only to migrate existing CharacterData assets. Runtime never
    // reads these values; the editor migration transfers them to a sequence.
    [FormerlySerializedAs("cinematicRig"), HideInInspector]
    public CombatCinematicRig legacyCinematicRig;
    [FormerlySerializedAs("successResult"), HideInInspector]
    public CombatHealthThresholdSuccessResult legacySuccessResult = CombatHealthThresholdSuccessResult.ResumeCombat;
    [FormerlySerializedAs("failureRetaliationSkill"), HideInInspector]
    public SkillSO legacyFailureRetaliationSkill;

    public bool IsComplete => sequence != null && sequence.IsComplete;
}

// Role: ScriptableObject de donnees pour personnages joueurs et ennemis.
// Usage: reference par les prefabs de personnages, la squad, le combat, les voice lines et l'inventaire de depart.
// Responsibilities: stocker identite, apparence, stats, competences et inventaire de depart.
// Dependencies: Item, StatsSO, CharacterStats, VoiceLineData.
// Precautions: plusieurs champs publics sont serialises dans des assets; ne pas les renommer sans migration Unity.
/// <summary>
/// Donnees centrales d'un personnage jouable ou ennemi.
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "Scriptable Objects/CharacterData")]
public class CharacterData : ScriptableObject
{
    /// <summary>
    /// Stack d'item donne au personnage au demarrage.
    /// </summary>
    [System.Serializable]
    public class StarterItemStack
    {
        /// <summary>
        /// Item donne au personnage.
        /// </summary>
        [Tooltip("Item de depart.")]
        public Item item;
        /// <summary>
        /// Quantite initiale. Pour la flamme, cette valeur represente des secondes.
        /// </summary>
        [Min(1)]
        [Tooltip("Quantite initiale (pour la flamme: secondes).")]
        public int quantity = 1;
    }

    [SerializeField, HideInInspector] private string uniqueId;
    /// <summary>Identifiant gameplay optionnel du personnage.</summary>
    public string characterId;
    /// <summary>Nom affiche dans l'UI.</summary>
    public string characterName;
    /// <summary>Portrait utilise par les interfaces.</summary>
    public Sprite portrait;
    /// <summary>Prefab unique utilise pour le monde, la squad et le reseau.</summary>
    public GameObject worldPrefab;
    /// <summary>Competences possedees par le personnage.</summary>
    public List<StatsSO> skills;

    /// <summary>Competences de combat temps reel connues par le personnage.</summary>
    public List<SkillSO> combatSkills = new List<SkillSO>();

    [Header("Basic Skills")]
    [Tooltip("Combo d'attaques basiques disponible au sol. L'ordre definit l'enchainement.")]
    public List<BasicSkillsSO> groundBasicSkills = new List<BasicSkillsSO>();
    [Tooltip("Combo d'attaques basiques disponible en l'air. L'ordre definit l'enchainement.")]
    public List<BasicSkillsSO> airBasicSkills = new List<BasicSkillsSO>();

    /// <summary>Statistiques de type JDR utilisees par les checks.</summary>
    public CharacterStats stats = new CharacterStats();
    /// <summary>Items donnes au debut ou lors de l'initialisation d'inventaire.</summary>
    public List<StarterItemStack> starterItemsWithQuantity = new List<StarterItemStack>();
    /// <summary>Points de vie maximum par defaut.</summary>
    public int hp = 10;

    [Header("Combat")]
    /// <summary>Indique si cette donnee represente un ennemi.</summary>
    [Tooltip("Indique que ce CharacterData represente un ennemi.")]
    public bool isEnemy;

    [Tooltip("Active les paliers de PV cinematiques pour cet ennemi. Laisse false pour un combat normal.")]
    public bool enableCombatHealthThresholds;
    [Tooltip("Paliers declenches du plus haut pourcentage au plus bas.")]
    public List<CombatHealthThresholdStage> combatHealthThresholdStages = new List<CombatHealthThresholdStage>();

    [Header("Voice Lines")]
    /// <summary>Voice lines disponibles pour ce personnage.</summary>
    public List<VoiceLineData> voiceLines = new List<VoiceLineData>();

    [Header("Maison")]
    /// <summary>Index de point d'attente dans la maison.</summary>
    public int maisonWaitingPoint = 0;


    /// <summary>
    /// Identifiant stable derive du GUID d'asset quand disponible.
    /// </summary>
    public string UniqueId => uniqueId;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Dans l'editeur, on garde un id stable base sur le GUID Unity de l'asset.
        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (!string.IsNullOrEmpty(path))
        {
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid) && uniqueId != guid)
            {
                uniqueId = guid;
                UnityEditor.EditorUtility.SetDirty(this);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            uniqueId = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    /// <summary>Retourne le prefab unique du personnage.</summary>
    public GameObject ResolveWorldPrefab()
    {
        return worldPrefab;
    }

    /// <summary>
    /// Retourne le nom affiche, avec fallback sur le nom d'asset.
    /// </summary>
    public string ResolveDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(characterName))
        {
            return characterName;
        }

        return !string.IsNullOrWhiteSpace(name) ? name : "Ennemi";
    }

    /// <summary>
    /// Retourne les PV max avec fallback pour les ennemis incomplets.
    /// </summary>
    public int ResolveMaxHp()
    {
        return hp > 0 ? hp : 8;
    }


    /// <summary>
    /// Indique si ce personnage possede une competence donnee.
    /// </summary>
    public bool HasSkill(StatsSO skill)
    {
        if (skill == null || skills == null)
        {
            return false;
        }

        return skills.Contains(skill);
    }

    /// <summary>
    /// Ajoute une competence si elle n'est pas deja presente.
    /// </summary>
    public void AddSkill(StatsSO skill)
    {
        if (skill == null)
        {
            return;
        }

        if (skills == null)
        {
            skills = new List<StatsSO>();
        }

        if (!skills.Contains(skill))
        {
            skills.Add(skill);
        }
    }

    /// <summary>
    /// Retire une competence de la liste.
    /// </summary>
    public void RemoveSkill(StatsSO skill)
    {
        if (skill == null || skills == null)
        {
            return;
        }

        skills.Remove(skill);
    }

    /// <summary>
    /// Remplace toute la liste de competences en supprimant les doublons et nulls.
    /// </summary>
    public void SetSkills(List<StatsSO> newSkills)
    {
        if (skills == null)
        {
            skills = new List<StatsSO>();
        }
        else
        {
            skills.Clear();
        }

        if (newSkills == null)
        {
            return;
        }

        for (int i = 0; i < newSkills.Count; i++)
        {
            StatsSO skill = newSkills[i];
            if (skill != null && !skills.Contains(skill))
            {
                skills.Add(skill);
            }
        }
    }

    /// <summary>
    /// Effectue un test de competence et retourne le detail du jet.
    /// </summary>
    public bool TryCheckSkill(StatsSO skill, out int roll, out int modifier, out int total)
    {
        roll = 0;
        modifier = 0;
        total = 0;

        if (skill == null)
        {
            return false;
        }

        if (!HasSkill(skill))
        {
            return false;
        }

        if (!skill.requiresRoll)
        {
            roll = 0;
            modifier = 0;
            total = skill.difficultyClass;
            return true;
        }

        int statValue = GetStatValue(skill.linkedStat);
        modifier = GetStatModifier(statValue);
        // Random.Range(int,int) exclut la borne max, donc 21 produit un d20.
        roll = Random.Range(1, 21);
        total = roll + modifier;
        return total >= skill.difficultyClass;
    }

    /// <summary>
    /// Retourne la valeur brute d'une statistique.
    /// </summary>
    public int GetStatValue(StatType stat)
    {
        if (stats == null)
        {
            return 10;
        }

        switch (stat)
        {
            case StatType.Strength:
                return stats.strength;
            case StatType.Dexterity:
                return stats.dexterity;
            case StatType.Constitution:
                return stats.constitution;
            case StatType.Intelligence:
                return stats.intelligence;
            case StatType.Wisdom:
                return stats.wisdom;
            case StatType.Charisma:
                return stats.charisma;
            default:
                return 10;
        }
    }

    /// <summary>
    /// Calcule le modificateur de stat au format JDR: (valeur - 10) / 2 arrondi vers le bas.
    /// </summary>
    public static int GetStatModifier(int statValue)
    {
        return Mathf.FloorToInt((statValue - 10) / 2f);
    }
}

/// <summary>
/// Bloc de statistiques de base associe a un personnage.
/// </summary>
[System.Serializable]
public class CharacterStats
{
    /// <summary>Force physique.</summary>
    public int strength = 10;
    /// <summary>Adresse et agilite.</summary>
    public int dexterity = 10;
    /// <summary>Endurance et resistance.</summary>
    public int constitution = 10;
    /// <summary>Raisonnement et savoir.</summary>
    public int intelligence = 10;
    /// <summary>Perception et intuition.</summary>
    public int wisdom = 10;
    /// <summary>Presence sociale.</summary>
    public int charisma = 10;
}
