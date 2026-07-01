using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Role: detecte un joueur proche et lance une session de combat contre cet ennemi.
// Usage: attache aux ennemis de scene capables d'aggro.
// Responsibilities: creer les definitions d'ennemis, demarrer le combat, appliquer le resultat monde.
// Dependencies: CombatSessionManager, CombatHealth, CharacterData, Netcode.
// Precautions: ce script peut desactiver l'objet apres victoire; verifier les prefabs avant de changer ce comportement.
/// <summary>
/// Ennemi de monde qui peut declencher un combat quand un personnage entre dans son trigger.
/// </summary>
[DisallowMultipleComponent]
public class CombatAggroEnemy : MonoBehaviour
{
    [Header("Aggro")]
    [SerializeField, Tooltip("Declenche automatiquement un combat quand un joueur entre dans le trigger.")]
    private bool aggroEnabled = true;
    [SerializeField, Min(0.1f), Tooltip("Rayon du trigger cree si aucun trigger n'est assigne.")]
    private float aggroRadius = 2.5f;
    [SerializeField, Min(0f), Tooltip("Delai minimal entre deux tentatives d'aggro.")]
    private float aggroCooldown = 5f;
    [SerializeField, Tooltip("Ajoute un SphereCollider trigger si aucun trigger valide n'existe.")]
    private bool autoCreateTrigger = true;
    [SerializeField, Tooltip("Trigger utilise pour detecter le joueur.")]
    private Collider aggroTrigger;

    [Header("Local Combat Music")]
    [SerializeField, Tooltip("Declenche localement la musique de combat quand le joueur local entre dans la portee d'aggro.")]
    private bool playLocalCombatMusicInAggroRange = true;
    [SerializeField, Min(0f), Tooltip("Distance supplementaire hors du trigger avant de restaurer la musique precedente.")]
    private float localCombatMusicExitPadding = 1.25f;

    [Header("Combat")]
    [SerializeField, Tooltip("CharacterData ennemi utilise pour les donnees de placement et de combat.")]
    private CharacterData enemy;
    [SerializeField, Tooltip("Nom affiche pour l'ennemi principal.")]
    private string enemyDisplayName = "Ennemi";
    [SerializeField, Min(1), Tooltip("PV max de l'ennemi principal si aucun CharacterData/CombatHealth n'est disponible.")]
    private int maxHp = 8;
    [SerializeField, Min(0), Tooltip("Degats bruts de l'ennemi principal.")]
    private int attackDamage = 4;
    [SerializeField, Tooltip("CharacterData optionnel utilise pour le nom et les PV.")]
    private CharacterData characterData;
    [SerializeField, Tooltip("PV optionnels portes par l'objet monde.")]
    private CombatHealth combatHealth;
    [SerializeField, Tooltip("Ennemis additionnels dans la meme session solo.")]
    private List<CombatEnemyDefinition> additionalEnemies = new List<CombatEnemyDefinition>();

    [Header("Outcome")]
    [SerializeField, Tooltip("Desactive cet objet apres une victoire du joueur.")]
    private bool disableAfterVictory = true;
    [SerializeField, Tooltip("Passe les PV monde a 0 apres une victoire.")]
    private bool markHealthDeadAfterVictory = true;

    private float nextAggroTime;
    private bool defeated;
    private bool combatInProgress;
    private bool localCombatMusicActive;

    private void Reset()
    {
        // Unity appelle Reset dans l'editeur quand le composant est ajoute ou reinitialise.
        combatHealth = GetComponent<CombatHealth>();
        ResolveCharacterDataReferences();
        EnsureAggroTrigger();
    }

    private void Awake()
    {
        // Awake prepare les references avant que les triggers puissent lancer un combat.
        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        ResolveCharacterDataReferences();
        EnsureAggroTrigger();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Unity appelle ceci quand un collider entre dans un trigger de l'ennemi.
        TryBeginLocalCombatMusic(other);
        TryAggro(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // OnTriggerStay permet de lancer l'aggro si le cooldown expire pendant que le joueur est deja dans la zone.
        TryBeginLocalCombatMusic(other);
        TryAggro(other);
    }

    private void Update()
    {
        UpdateLocalCombatMusicExit();
    }

    private void OnDisable()
    {
        EndLocalCombatMusic();
    }

    private void OnDestroy()
    {
        EndLocalCombatMusic();
    }

    /// <summary>
    /// Cree les definitions d'ennemis qui seront copiees dans une session de combat.
    /// </summary>
    public List<CombatEnemyDefinition> CreateEnemyDefinitions()
    {
        List<CombatEnemyDefinition> result;
        if (enemy != null)
        {
            result = enemy.CreateCombatDefinitions(combatHealth);
        }
        else
        {
            int resolvedMaxHp = ResolveMaxHp();
            int resolvedCurrentHp = ResolveCurrentHp(resolvedMaxHp);
            result = new List<CombatEnemyDefinition>
            {
                new CombatEnemyDefinition(ResolveDisplayName(), resolvedMaxHp, resolvedCurrentHp, attackDamage)
            };

            if (additionalEnemies != null)
            {
                int total = result.Count + additionalEnemies.Count;
                for (int i = 0; i < additionalEnemies.Count; i++)
                {
                    CombatEnemyDefinition definition = additionalEnemies[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    result.Add(definition.CreateRuntimeCopy(result.Count, total));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Remplace le CharacterData ennemi et synchronise les champs principaux.
    /// </summary>
    public void SetEnemy(CharacterData data)
    {
        enemy = data;
        if (data == null)
        {
            return;
        }

        characterData = data;
        enemyDisplayName = data.ResolveDisplayName();
        maxHp = data.ResolveMaxHp();
        attackDamage = Mathf.Max(0, data.attackDamage);
    }

    /// <summary>
    /// Notifie l'ennemi qu'une session s'est terminee sans forcement recevoir les PV restants.
    /// </summary>
    public void HandleCombatEnded(bool playerVictory)
    {
        combatInProgress = false;
        nextAggroTime = Time.time + aggroCooldown;
        if (!playerVictory)
        {
            return;
        }

        defeated = true;
        EndLocalCombatMusic();
        if (markHealthDeadAfterVictory && combatHealth != null)
        {
            combatHealth.SetHealth(0, combatHealth.MaxHp);
        }

        if (disableAfterVictory)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Applique le resultat final et les PV restants au composant monde.
    /// </summary>
    public void FinalizeCombatResult(bool playerVictory, int remainingHp)
    {
        combatInProgress = false;
        nextAggroTime = Time.time + aggroCooldown;

        if (combatHealth != null)
        {
            int max = Mathf.Max(1, combatHealth.MaxHp);
            combatHealth.SetHealth(playerVictory ? 0 : Mathf.Clamp(remainingHp, 0, max), max);
        }

        if (!playerVictory)
        {
            return;
        }

        defeated = true;
        EndLocalCombatMusic();
        if (disableAfterVictory)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Retourne l'Animator le plus proche pour les transitions de combat.
    /// </summary>
    public Animator ResolveAnimator()
    {
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            return animator;
        }

        return GetComponentInChildren<Animator>(true);
    }

    private void TryAggro(Collider other)
    {
        if (!CanTryAggro())
        {
            return;
        }

        SquadCharacterController controller = other != null ? other.GetComponentInParent<SquadCharacterController>() : null;
        if (!IsValidPlayerTarget(controller))
        {
            return;
        }

        // Le manager refuse si le personnage est deja engage ou si le reseau n'est pas pret.
        CombatSessionManager manager = CombatSessionManager.EnsureInstance();
        if (manager == null || !manager.TryStartCombat(controller, this))
        {
            return;
        }

        combatInProgress = true;
        nextAggroTime = Time.time + aggroCooldown;
    }

    private void TryBeginLocalCombatMusic(Collider other)
    {
        if (!CanUseLocalCombatMusic())
        {
            return;
        }

        SquadCharacterController controller = other != null ? other.GetComponentInParent<SquadCharacterController>() : null;
        if (!IsLocalPlayerTarget(controller))
        {
            return;
        }

        if (!localCombatMusicActive)
        {
            CombatTransitionController.EnsureInstance().BeginProximityCombatMusic(this);
            localCombatMusicActive = true;
        }
    }

    private void UpdateLocalCombatMusicExit()
    {
        if (!localCombatMusicActive)
        {
            return;
        }

        Transform localPlayer = LocalPlayerContext.LocalCharacterRoot;
        if (!CanUseLocalCombatMusic() || localPlayer == null || !IsWithinLocalCombatMusicExitRange(localPlayer.position))
        {
            EndLocalCombatMusic();
        }
    }

    private void EndLocalCombatMusic()
    {
        if (!localCombatMusicActive)
        {
            return;
        }

        localCombatMusicActive = false;
        CombatTransitionController controller = CombatTransitionController.Instance;
        if (controller != null)
        {
            controller.EndProximityCombatMusic(this);
        }
    }

    private bool CanUseLocalCombatMusic()
    {
        return playLocalCombatMusicInAggroRange && aggroEnabled && !defeated && isActiveAndEnabled;
    }

    private bool IsWithinLocalCombatMusicExitRange(Vector3 playerPosition)
    {
        float padding = Mathf.Max(0f, localCombatMusicExitPadding);
        if (aggroTrigger != null && aggroTrigger.enabled)
        {
            Vector3 closestPoint = aggroTrigger.ClosestPoint(playerPosition);
            return (playerPosition - closestPoint).sqrMagnitude <= padding * padding;
        }

        float fallbackRadius = Mathf.Max(0.1f, aggroRadius) + padding;
        return (playerPosition - transform.position).sqrMagnitude <= fallbackRadius * fallbackRadius;
    }

    private bool CanTryAggro()
    {
        if (!aggroEnabled || defeated || !isActiveAndEnabled)
        {
            return false;
        }

        if (combatInProgress)
        {
            return false;
        }

        if (Time.time < nextAggroTime)
        {
            return false;
        }

        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }

    private static bool IsValidPlayerTarget(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return false;
        }

        if (CombatSessionManager.IsCharacterInCombat(controller))
        {
            return false;
        }

        if (NetcodePlayerUtils.ShouldUsePlayerControl(controller.gameObject, out _))
        {
            return true;
        }

        return controller.CompareTag("Player") || controller.GetComponentInParent<NetcodeLocalPlayer>() != null;
    }

    private static bool IsLocalPlayerTarget(SquadCharacterController controller)
    {
        if (controller == null)
        {
            return false;
        }

        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        if (localRoot == null || !localRoot.gameObject.activeInHierarchy)
        {
            return false;
        }

        Transform controllerTransform = controller.transform;
        return controllerTransform == localRoot ||
               controllerTransform.IsChildOf(localRoot) ||
               localRoot.IsChildOf(controllerTransform);
    }

    private void EnsureAggroTrigger()
    {
        if (aggroTrigger != null)
        {
            aggroTrigger.isTrigger = true;
            return;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].isTrigger)
            {
                aggroTrigger = colliders[i];
                return;
            }
        }

        if (!autoCreateTrigger)
        {
            return;
        }

        // Fallback utile pour les prototypes: l'ennemi devient detectable sans prefab de trigger dedie.
        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.1f, aggroRadius);
        aggroTrigger = trigger;
    }

    private string ResolveDisplayName()
    {
        if (enemy != null)
        {
            return enemy.ResolveDisplayName();
        }

        if (!string.IsNullOrWhiteSpace(enemyDisplayName))
        {
            return enemyDisplayName;
        }

        if (characterData != null && !string.IsNullOrWhiteSpace(characterData.characterName))
        {
            return characterData.characterName;
        }

        return name;
    }

    private int ResolveMaxHp()
    {
        if (combatHealth != null)
        {
            return Mathf.Max(1, combatHealth.MaxHp);
        }

        if (enemy != null)
        {
            return enemy.ResolveMaxHp();
        }

        if (characterData != null)
        {
            return Mathf.Max(1, characterData.hp);
        }

        return Mathf.Max(1, maxHp);
    }

    private int ResolveCurrentHp(int resolvedMaxHp)
    {
        if (combatHealth != null && combatHealth.CurrentHp > 0)
        {
            return Mathf.Clamp(combatHealth.CurrentHp, 0, resolvedMaxHp);
        }

        return resolvedMaxHp;
    }

    private void ResolveCharacterDataReferences()
    {
        if (enemy == null)
        {
            EnemyInfo enemyInfo = GetComponentInChildren<EnemyInfo>(true);
            enemy = enemyInfo != null ? enemyInfo.Enemy : null;
        }

        if (characterData == null)
        {
            CharacterInfo info = GetComponentInChildren<CharacterInfo>(true);
            characterData = info != null ? info.CharacterData : null;
        }

        if (enemy == null && characterData != null && characterData.isEnemy)
        {
            enemy = characterData;
        }

        if (enemy != null)
        {
            characterData = enemy;
        }
    }
}
