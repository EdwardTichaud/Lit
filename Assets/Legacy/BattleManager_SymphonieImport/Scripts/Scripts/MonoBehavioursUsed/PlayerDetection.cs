using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerDetection : MonoBehaviour
{
    [Header("Détection de l'environnement")]
    public LayerMask groundLayer;
    public Transform groundCheck;
    public float groundCheckRadius = 1.1f;
    public bool isGrounded;
    private bool wasGrounded;
    public float coyoteTime = 0.2f;

    [Header("Détection des ennemis (par tag)")]
    // On garde baseDetectionRadius et detectionExpansion, mais on ne se base plus sur un LayerMask
    public float currentDetectionRadius = 1.5f;
    public float baseDetectionRadius = 1.5f;
    public float detectionExpansion = 5.5f;
    // Le tag à rechercher
    [Tooltip("Tag utilisé pour identifier les ennemis (par exemple : \"Enemy\").")]
    public string enemyTag = "Enemy";

    private readonly Collider[] baseDetectionBuffer = new Collider[32];
    private readonly Collider[] expandedDetectionBuffer = new Collider[64];
    private readonly List<Enemy> detectionCandidates = new();

    // ------------------------------------------------------------------
    // Liste temporaire des ennemis actuellement engagés dans un combat
    // Permet d'éviter de redétecter en boucle les mêmes ennemis tant
    // qu'ils ne sont pas définitivement retirés du monde.
    // ------------------------------------------------------------------
    public List<Enemy> enemiesInFight = new List<Enemy>();

    public List<CharacterData> detectedEnemies = new List<CharacterData>();
    public bool detectionOn = true;

    [Header("Effets sonores")]
    [Tooltip("Clips vocaux joués lors de la première détection d'un ennemi")]
    public List<AudioClipSO> firstDetectionVoices = new List<AudioClipSO>();

    [Tooltip("Volume appliqué au clip de première détection")]
    [Range(0f, 1f)]
    public float firstDetectionVoiceVolume = 1f;

    private bool firstEnemyDetected;

    [Header("State")]
    public bool battleEngaged;

    // ------------------------------------------------------------------
    // Gestion interne de la réinitialisation différée : nous avons besoin
    // d'une coroutine active pour attendre en temps réel avant de
    // réactiver la détection. Or, si l'objet est désactivé (ce qui arrive
    // lors de certaines transitions), Unity interdit l'utilisation de
    // StartCoroutine et déclenche l'erreur observée par l'équipe.
    // Ces champs nous permettent de mémoriser l'attente à effectuer et
    // de la relancer automatiquement quand le GameObject redevient actif.
    // ------------------------------------------------------------------
    private Coroutine resetDetectionCoroutine;
    private bool resetReactivationScheduled;
    private float resetReactivateAtRealtime;

    private void Awake()
    {
        currentDetectionRadius = baseDetectionRadius;
        firstEnemyDetected = false;
    }

    private void Update()
    {
        HandleEnemyDetection();
        CleanupInvalidFields();
    }

    private void CleanupInvalidFields()
    {
        // Nettoie les références invalides pour éviter les fuites d'objets
        detectedEnemies.RemoveAll(c => c == null);
        enemiesInFight.RemoveAll(e => e == null);
    }

    private void HandleEnemyDetection()
    {
        if (!detectionOn)
            return;

        Vector3 origin = transform.position;
        int baseHitCount = Physics.OverlapSphereNonAlloc(origin, currentDetectionRadius, baseDetectionBuffer);
        bool enemyDetected = false;
        for (int i = 0; i < baseHitCount; i++)
        {
            Collider collider = baseDetectionBuffer[i];
            if (collider == null || !collider.CompareTag(enemyTag))
                continue;
            enemyDetected = true;
            break;
        }

        if (!enemyDetected)
            return; // aucun ennemi trouvé dans le rayon de base

        // Lecture du clip vocal uniquement lors de la première détection effective d'un ennemi
        if (!firstEnemyDetected && firstDetectionVoices.Count > 0)
        {
            // Sélection aléatoire d'un clip parmi la liste disponible
            AudioClipSO clip = firstDetectionVoices[UnityEngine.Random.Range(0, firstDetectionVoices.Count)];

            // Lecture du clip avec le volume choisi dans l'inspecteur
            CharacterUnit speaker = GetComponentInParent<CharacterUnit>();
            string speakerName = speaker != null && speaker.Data != null ? speaker.Data.characterName : null;
            AudioManager.Instance?.PlayVoice(clip, speakerName, firstDetectionVoiceVolume);

            // On mémorise que la première détection a eu lieu afin de ne pas répéter la voix
            firstEnemyDetected = true;
        }

        // À partir d'ici, au moins un ennemi a été trouvé : on peut élargir le rayon de détection
        // et poursuivre la logique de mise en combat.
        detectionOn = false;
        currentDetectionRadius = currentDetectionRadius + detectionExpansion;

        detectionCandidates.Clear();
        int expandedHits = Physics.OverlapSphereNonAlloc(origin, currentDetectionRadius, expandedDetectionBuffer);
        for (int i = 0; i < expandedHits; i++)
        {
            Collider collider = expandedDetectionBuffer[i];
            if (collider == null || !collider.CompareTag(enemyTag))
                continue;

            Enemy enemy = collider.GetComponentInParent<Enemy>();
            if (enemy == null || enemiesInFight.Contains(enemy))
                continue;

            if (detectionCandidates.Contains(enemy))
                continue;

            detectionCandidates.Add(enemy);
        }

        detectionCandidates.Sort((a, b) =>
        {
            float aDist = Vector3.SqrMagnitude(a.transform.position - origin);
            float bDist = Vector3.SqrMagnitude(b.transform.position - origin);
            return aDist.CompareTo(bDist);
        });

        // On remplit detectedEnemies
        detectedEnemies.Clear();
        for (int i = 0; i < detectionCandidates.Count && i < 3; i++)
        {
            var enemy = detectionCandidates[i];
            detectedEnemies.Add(enemy.enemyData);

            // Ajout à la liste temporaire pour empêcher une redétection
            // et indiquer que cet ennemi est actuellement engagé dans ce combat
            enemiesInFight.Add(enemy);
        }

        // 9) On déclenche ou termine le combat
        if (detectedEnemies.Count > 0 && !battleEngaged)
        {
            var mgr = NewBattleManager.Instance;
            mgr.enemyTemplates.Clear();
            mgr.enemyTemplates.AddRange(detectedEnemies);

            battleEngaged = true;
            BattleTransitionManager.Instance.StartCombatTransition();
        }
        else if (detectedEnemies.Count == 0 && battleEngaged)
        {
            currentDetectionRadius = baseDetectionRadius;
            battleEngaged = false;
        }
    }

    /// <summary>
    /// Réinitialise tous les paramètres de détection aux valeurs de base,
    /// pour repartir proprement après un combat.
    /// </summary>
    public void ResetDetection(float delay = 1f)
    {
        // On annule toute coroutine précédente pour éviter les chevauchements.
        if (resetDetectionCoroutine != null)
        {
            StopCoroutine(resetDetectionCoroutine);
            resetDetectionCoroutine = null;
        }

        // Stocke le moment auquel la détection doit redevenir active.
        resetReactivateAtRealtime = Time.realtimeSinceStartup + Mathf.Max(0f, delay);
        resetReactivationScheduled = true;

        ApplyImmediateResetState();

        // Si le composant est actif, on peut démarrer la coroutine tout de suite.
        if (isActiveAndEnabled)
        {
            resetDetectionCoroutine = StartCoroutine(ResetDetectionRoutine());
        }
        else
        {
            // Lorsque l'objet est désactivé, on se contente de tracer un message.
            // La coroutine sera relancée automatiquement lors du OnEnable.
            Debug.LogWarning(
                $"[PlayerDetection] ResetDetection demandé alors que \"{name}\" est inactif. La réactivation sera différée jusqu'à la prochaine activation.",
                this
            );
        }
    }

    private IEnumerator ResetDetectionRoutine()
    {
        // Utilise le temps réel pour ne pas dépendre du timeScale (gelé pendant les transitions).
        float remainingDelay = resetReactivateAtRealtime - Time.realtimeSinceStartup;
        if (remainingDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(remainingDelay);
        }

        detectionOn = true;
        resetReactivationScheduled = false;
        resetDetectionCoroutine = null;

        Debug.Log("[PlayerDetection] Détection réinitialisée.");
    }

    /// <summary>
    /// Applique immédiatement l'état de base de la détection
    /// (rayon, listes, flags) sans attendre la réactivation différée.
    /// </summary>
    private void ApplyImmediateResetState()
    {
        currentDetectionRadius = baseDetectionRadius;
        battleEngaged = false;
        detectedEnemies.Clear();
        enemiesInFight.Clear(); // On s'assure qu'aucun ennemi du combat prcédent ne reste enregistré
        detectionOn = false;
        firstEnemyDetected = false;

        Debug.Log(
            $"[PlayerDetection] Réinitialisation en cours, détection réactivée dans {Mathf.Max(0f, resetReactivateAtRealtime - Time.realtimeSinceStartup):0.00} s."
        );
    }

    private void OnEnable()
    {
        // Si une réactivation différée est en attente, on relance la coroutine.
        if (resetReactivationScheduled && resetDetectionCoroutine == null)
        {
            resetDetectionCoroutine = StartCoroutine(ResetDetectionRoutine());
        }
    }

    private void OnDisable()
    {
        // On stoppe la coroutine en cours pour éviter de conserver une référence invalide.
        if (resetDetectionCoroutine != null)
        {
            StopCoroutine(resetDetectionCoroutine);
            resetDetectionCoroutine = null;
        }
    }


    private void OnDrawGizmos()
    {
        // Rayon de détection des ennemis (base)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f); // orange semi-transparent
        Gizmos.DrawWireSphere(transform.position, currentDetectionRadius);

        // Rayon agrandi si détection déclenchée
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // rouge
        Gizmos.DrawWireSphere(transform.position, currentDetectionRadius + detectionExpansion);

        // Détection du sol
        if (groundCheck != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f); // vert
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

}
