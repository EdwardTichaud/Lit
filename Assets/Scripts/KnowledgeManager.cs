using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Role: gere les connaissances debloquees pendant la partie.
// Usage: singleton runtime appele par les voice lines, la sauvegarde et certains objets interactifs.
// Responsibilities: stocker les connaissances, declencher les effets d'unlock, notifier les autres systemes.
// Dependencies: KnowledgeSO, AudioManager, SquadManager, PersistentWorldSceneInstaller.
// Precautions: ne pas changer la semantique de la liste unlockedKnowledge sans verifier la sauvegarde.
/// <summary>
/// Gestionnaire runtime des connaissances debloquees par le joueur.
/// </summary>
public class KnowledgeManager : MonoBehaviour
{
    /// <summary>
    /// Instance singleton active dans la scene ou conservee entre scenes.
    /// </summary>
    public static KnowledgeManager Instance { get; private set; }

    [Header("Runtime")]
    /// <summary>
    /// Liste serialisee des connaissances deja debloquees.
    /// </summary>
    [SerializeField, Tooltip("Connaissances debloquees.")] private List<KnowledgeSO> unlockedKnowledge = new List<KnowledgeSO>();
    /// <summary>
    /// Si vrai, ce manager survit aux chargements de scene.
    /// </summary>
    [SerializeField, Tooltip("Ne pas detruire au changement de scene.")] private bool dontDestroyOnLoad = true;

    [Header("Unlock FX")]
    [SerializeField, Tooltip("Joue l'effet de debloquage a chaque nouvelle connaissance.")]
    private bool playUnlockEffects = true;
    [SerializeField, Tooltip("Prefab instancie lors d'un debloquage.")]
    private GameObject unlockVfxPrefab;
    [SerializeField, Tooltip("Offset applique au prefab de debloquage.")]
    private Vector3 unlockVfxOffset = Vector3.zero;
    [SerializeField, Tooltip("Duree de vie du prefab de debloquage (0 = ne pas detruire).")]
    private float unlockVfxLifetime = 2f;
    [SerializeField, Tooltip("Audio joue lors du debloquage.")]
    private AudioClipSO unlockSfx;
    [SerializeField, Tooltip("Nom de la state d'animation a jouer.")]
    private string unlockAnimationState = "Knowledge_Unlock";
    [SerializeField, Tooltip("Duree du crossfade d'animation.")]
    private float unlockAnimationCrossfade = 0.05f;

    [Header("Unlock Text")]
    [SerializeField, Tooltip("Affiche un texte au-dessus du personnage.")]
    private bool showUnlockText = true;
    [SerializeField, Tooltip("Prefab de texte (TMP_Text). Laisse vide pour auto-creer.")]
    private GameObject unlockTextPrefab;
    [SerializeField, Tooltip("Root optionnel pour le texte world space.")]
    private Transform unlockTextRoot;
    [SerializeField, Tooltip("Offset du texte par rapport au personnage.")]
    private Vector3 unlockTextOffset = new Vector3(0f, 2f, 0f);
    [SerializeField, Tooltip("Duree d'affichage du texte (secondes).")]
    private float unlockTextDuration = 2f;
    [SerializeField, Tooltip("Oriente le texte vers la camera.")]
    private bool unlockTextFaceCamera = true;
    [SerializeField, Tooltip("Prefixe affiche avant le titre de connaissance.")]
    private string unlockTextPrefix = "Connaissance debloquee: ";
    [SerializeField, Tooltip("Utilise le titre de la connaissance (sinon le nom de l'asset).")]
    private bool unlockUseKnowledgeTitle = true;

    private readonly HashSet<KnowledgeSO> lookup = new HashSet<KnowledgeSO>();
    private bool lookupReady;

    /// <summary>
    /// Vue lecture seule des connaissances debloquees.
    /// </summary>
    public IReadOnlyList<KnowledgeSO> UnlockedKnowledge => unlockedKnowledge;

    /// <summary>
    /// Signature commune des evenements de connaissance.
    /// </summary>
    public delegate void KnowledgeEvent(KnowledgeSO knowledge);
    /// <summary>
    /// Declenche quand une nouvelle connaissance est ajoutee.
    /// </summary>
    public event KnowledgeEvent KnowledgeUnlocked;
    /// <summary>
    /// Declenche quand une connaissance est retiree.
    /// </summary>
    public event KnowledgeEvent KnowledgeRemoved;

    private void Awake()
    {
        // Unity appelle Awake au chargement de l'objet; ici on impose une seule instance active.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        PersistentWorldSceneInstaller.EnsureRuntimeKnowledgeManager(this);
        RebuildLookup();
    }

    /// <summary>
    /// Retourne le manager existant ou en cree un minimal pour les scenes qui n'en possedent pas encore.
    /// </summary>
    public static KnowledgeManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        KnowledgeManager existing = FindObjectOfType<KnowledgeManager>();
        if (existing != null)
        {
            Instance = existing;
            PersistentWorldSceneInstaller.EnsureRuntimeKnowledgeManager(existing);
            existing.RebuildLookup();
            return existing;
        }

        GameObject go = new GameObject("KnowledgeManager");
        KnowledgeManager manager = go.AddComponent<KnowledgeManager>();
        return manager;
    }

    /// <summary>
    /// Prepare ce manager avant une remise a zero runtime controlee.
    /// </summary>
    public void PrepareForRuntimeReset(string reason)
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Indique si une connaissance est actuellement debloquee.
    /// </summary>
    public bool HasKnowledge(KnowledgeSO knowledge)
    {
        if (knowledge == null)
        {
            return false;
        }

        EnsureLookup();
        return lookup.Contains(knowledge);
    }

    /// <summary>
    /// Debloque une connaissance si elle ne l'est pas deja.
    /// </summary>
    public bool UnlockKnowledge(KnowledgeSO knowledge)
    {
        if (knowledge == null)
        {
            return false;
        }

        EnsureLookup();
        if (lookup.Add(knowledge))
        {
            if (unlockedKnowledge == null)
            {
                unlockedKnowledge = new List<KnowledgeSO>();
            }

            unlockedKnowledge.Add(knowledge);
            if (playUnlockEffects)
            {
                PlayUnlockEffects(knowledge);
            }
            KnowledgeUnlocked?.Invoke(knowledge);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Retire une connaissance deja debloquee.
    /// </summary>
    public bool RemoveKnowledge(KnowledgeSO knowledge)
    {
        if (knowledge == null)
        {
            return false;
        }

        EnsureLookup();
        if (lookup.Remove(knowledge))
        {
            if (unlockedKnowledge != null)
            {
                unlockedKnowledge.Remove(knowledge);
            }

            KnowledgeRemoved?.Invoke(knowledge);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Efface toutes les connaissances runtime.
    /// </summary>
    public void ClearKnowledge()
    {
        EnsureLookup();
        lookup.Clear();
        if (unlockedKnowledge != null)
        {
            unlockedKnowledge.Clear();
        }
    }

    /// <summary>
    /// Remplace la liste runtime par les connaissances restaurees depuis la sauvegarde.
    /// </summary>
    public void RestoreUnlockedKnowledge(IReadOnlyList<KnowledgeSO> restoredKnowledge)
    {
        if (unlockedKnowledge == null)
        {
            unlockedKnowledge = new List<KnowledgeSO>();
        }
        else
        {
            unlockedKnowledge.Clear();
        }

        lookup.Clear();
        if (restoredKnowledge != null)
        {
            for (int i = 0; i < restoredKnowledge.Count; i++)
            {
                KnowledgeSO knowledge = restoredKnowledge[i];
                if (knowledge == null || !lookup.Add(knowledge))
                {
                    continue;
                }

                unlockedKnowledge.Add(knowledge);
            }
        }

        lookupReady = true;
        PersistentWorldSceneInstaller.EnsureRuntimeKnowledgeManager(this);
    }

    private void EnsureLookup()
    {
        if (!lookupReady)
        {
            RebuildLookup();
        }
    }

    private void RebuildLookup()
    {
        lookup.Clear();
        if (unlockedKnowledge != null)
        {
            // Parcours inverse pour pouvoir supprimer les references nulles sans decalage d'index.
            for (int i = unlockedKnowledge.Count - 1; i >= 0; i--)
            {
                KnowledgeSO knowledge = unlockedKnowledge[i];
                if (knowledge == null)
                {
                    unlockedKnowledge.RemoveAt(i);
                    continue;
                }

                lookup.Add(knowledge);
            }
        }

        lookupReady = true;
    }

    private void PlayUnlockEffects(KnowledgeSO knowledge)
    {
        Transform anchor = ResolveAnchor();
        Vector3 anchorPosition = anchor != null ? anchor.position : transform.position;

        // Les effets sont facultatifs pour permettre a une scene de test d'utiliser uniquement les donnees.
        if (unlockVfxPrefab != null)
        {
            GameObject instance = Instantiate(unlockVfxPrefab, anchorPosition + unlockVfxOffset, Quaternion.identity);
            if (unlockVfxLifetime > 0f)
            {
                Destroy(instance, unlockVfxLifetime);
            }
        }

        if (unlockSfx != null && unlockSfx.audioClip != null)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayClip(unlockSfx, anchorPosition);
            }
            else
            {
                AudioSource.PlayClipAtPoint(unlockSfx.audioClip, anchorPosition, Mathf.Clamp01(unlockSfx.volume));
            }
        }

        if (showUnlockText)
        {
            ShowUnlockText(anchor, knowledge);
        }

        PlayUnlockAnimationOnSquad();
    }

    private Transform ResolveAnchor()
    {
        SquadManager manager = SquadManager.Instance;
        if (manager != null)
        {
            if (manager.currentCharacter != null)
            {
                return manager.currentCharacter.transform;
            }

            if (manager.squadCharacters != null)
            {
                for (int i = 0; i < manager.squadCharacters.Count; i++)
                {
                    GameObject candidate = manager.squadCharacters[i];
                    if (candidate != null)
                    {
                        return candidate.transform;
                    }
                }
            }
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
        {
            return tagged.transform;
        }

        return transform;
    }

    private void ShowUnlockText(Transform anchor, KnowledgeSO knowledge)
    {
        if (unlockTextDuration <= 0f)
        {
            return;
        }

        string text = BuildUnlockText(knowledge);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        GameObject instance = null;
        TMP_Text textTarget = null;
        if (unlockTextPrefab != null)
        {
            instance = Instantiate(unlockTextPrefab);
            if (instance != null)
            {
                textTarget = instance.GetComponentInChildren<TMP_Text>();
            }
        }
        else
        {
            instance = new GameObject("KnowledgeUnlockText");
            TextMeshPro tmp = instance.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            tmp.fontSize = 2f;
            textTarget = tmp;
        }

        if (instance == null || textTarget == null)
        {
            if (instance != null)
            {
                Destroy(instance);
            }

            return;
        }

        if (unlockTextRoot != null)
        {
            instance.transform.SetParent(unlockTextRoot, false);
        }

        textTarget.text = text;
        StartCoroutine(UnlockTextRoutine(instance.transform, anchor, unlockTextDuration));
    }

    private IEnumerator UnlockTextRoutine(Transform textTransform, Transform anchor, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            UpdateUnlockTextTransform(textTransform, anchor);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (textTransform != null)
        {
            Destroy(textTransform.gameObject);
        }
    }

    private void UpdateUnlockTextTransform(Transform textTransform, Transform anchor)
    {
        if (textTransform == null)
        {
            return;
        }

        Transform target = anchor != null ? anchor : transform;
        Vector3 worldPos = target.TransformPoint(unlockTextOffset);
        textTransform.position = worldPos;

        if (unlockTextFaceCamera)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCamera = textTransform.position - cam.transform.position;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    textTransform.rotation = Quaternion.LookRotation(toCamera);
                }
            }
        }
    }

    private string BuildUnlockText(KnowledgeSO knowledge)
    {
        string label = null;
        if (knowledge != null)
        {
            if (unlockUseKnowledgeTitle && !string.IsNullOrWhiteSpace(knowledge.title))
            {
                label = knowledge.title;
            }
            else
            {
                label = knowledge.name;
            }
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return unlockTextPrefix;
        }

        if (string.IsNullOrWhiteSpace(unlockTextPrefix))
        {
            return label;
        }

        return unlockTextPrefix + label;
    }

    private void PlayUnlockAnimationOnSquad()
    {
        if (string.IsNullOrWhiteSpace(unlockAnimationState))
        {
            return;
        }

        SquadManager manager = SquadManager.Instance;
        if (manager == null || manager.squadCharacters == null)
        {
            return;
        }

        int stateHash = Animator.StringToHash(unlockAnimationState);
        float fade = Mathf.Max(0f, unlockAnimationCrossfade);

        for (int i = 0; i < manager.squadCharacters.Count; i++)
        {
            GameObject character = manager.squadCharacters[i];
            if (character == null)
            {
                continue;
            }

            Animator[] animators = character.GetComponentsInChildren<Animator>(true);
            for (int j = 0; j < animators.Length; j++)
            {
                Animator animator = animators[j];
                if (animator == null || !animator.HasState(0, stateHash))
                {
                    continue;
                }

                animator.CrossFade(stateHash, fade, 0);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Unity appelle OnValidate dans l'editeur apres modification de l'inspecteur.
        lookupReady = false;
    }
#endif
}
