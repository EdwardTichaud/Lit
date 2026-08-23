using System;
using UnityEngine;

// Role: composant de points de vie reutilisable pour les ennemis de combat en scene.
// Usage: attache aux ennemis ou objets qui doivent garder des PV entre deux combats.
// Responsibilities: initialiser les PV depuis CharacterData, stocker PV max/courants, appliquer degats, notifier les changements.
// Dependencies: Action indirecte via l'evenement HealthChanged.
// Precautions: ne pas confondre avec la sante du SquadCharacterController, qui gere les personnages joueurs.
/// <summary>
/// Points de vie d'un objet de combat place en scene.
/// </summary>
public class CombatHealth : MonoBehaviour
{
    /// <summary>
    /// PV maximum utilises par le combat.
    /// </summary>
    [SerializeField, Min(1), Tooltip("PV max utilises par le combat.")]
    private int maxHp = 8;
    /// <summary>
    /// PV courants. Une valeur vide peut etre initialisee aux PV max dans Awake.
    /// </summary>
    [SerializeField, Min(0), Tooltip("PV courants. 0 au demarrage est remplace par les PV max.")]
    private int currentHp;
    /// <summary>
    /// Si vrai, un objet sans PV courants demarre plein.
    /// </summary>
    [SerializeField, Tooltip("Reinitialise les PV au Start si currentHp vaut 0.")]
    private bool initializeEmptyHealthToMax = true;

    /// <summary>
    /// Evenement declenche apres chaque changement de PV.
    /// </summary>
    public event Action<CombatHealth> HealthChanged;

    /// <summary>PV maximum actuels.</summary>
    public int MaxHp => maxHp;
    /// <summary>PV courants.</summary>
    public int CurrentHp => currentHp;
    /// <summary>Indique si les PV sont a zero.</summary>
    public bool IsDead => currentHp <= 0;

    private void Awake()
    {
        InitializeMaxHealthFromCharacterData();

        // Unity appelle Awake au chargement; on nettoie les valeurs avant tout combat.
        if (initializeEmptyHealthToMax && currentHp <= 0)
        {
            currentHp = maxHp;
        }

        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
    }

    /// <summary>
    /// Initialise le maximum depuis le CharacterData porte par ce root ou un de
    /// ses enfants. Les PV courants ne sont remplis que s'ils etaient vides afin
    /// de ne pas ecraser une restauration de sauvegarde ulterieure.
    /// </summary>
    public bool InitializeMaxHealthFromCharacterData()
    {
        CharacterData data = ResolveCharacterData();
        if (data == null)
        {
            return false;
        }

        int previousMaxHp = Mathf.Max(1, maxHp);
        int resolvedMaxHp = data.ResolveMaxHp();
        bool wasEmpty = currentHp <= 0;
        bool wasFull = currentHp >= previousMaxHp;
        maxHp = Mathf.Max(1, resolvedMaxHp);
        if (initializeEmptyHealthToMax && (wasEmpty || wasFull))
        {
            currentHp = maxHp;
        }
        else
        {
            currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        }

        return true;
    }

    private CharacterData ResolveCharacterData()
    {
        CharacterInfo characterInfo = GetComponent<CharacterInfo>() ?? GetComponentInChildren<CharacterInfo>(true);
        if (characterInfo != null && characterInfo.CharacterData != null)
        {
            return characterInfo.CharacterData;
        }

        return null;
    }

    /// <summary>
    /// Remplace les PV courants et maximum, puis notifie les ecouteurs.
    /// </summary>
    public void SetHealth(int current, int max)
    {
        maxHp = Mathf.Max(1, max);
        currentHp = Mathf.Clamp(current, 0, maxHp);
        HealthChanged?.Invoke(this);
    }

    /// <summary>
    /// Applique des degats et retourne le montant reellement retire.
    /// </summary>
    public int ApplyDamage(int amount)
    {
        int sanitized = Mathf.Max(0, amount);
        if (sanitized <= 0 || currentHp <= 0)
        {
            return 0;
        }

        int before = currentHp;
        currentHp = Mathf.Max(0, currentHp - sanitized);
        HealthChanged?.Invoke(this);
        return before - currentHp;
    }

    /// <summary>
    /// Restaure les PV courants au maximum.
    /// </summary>
    public void RestoreToMax()
    {
        currentHp = maxHp;
        HealthChanged?.Invoke(this);
    }
}
