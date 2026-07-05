using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Role: joue des lignes de voix locales et affiche leur texte en world space.
// Usage: attache a un personnage, PNJ ou objet qui doit parler lors d'une interaction.
// Responsibilities: choisir une ligne selon les connaissances, jouer l'audio, afficher le texte, debloquer des connaissances.
// Dependencies: VoiceLineData, KnowledgeSO, KnowledgeManager, AudioManager, LocalInputRouter, CharacterData.
// Precautions: les champs publics peuvent etre references par des prefabs; ne pas les renommer sans migration Unity.
/// <summary>
/// Controleur de voice lines locales avec conditions de connaissances et affichage texte.
/// </summary>
public class LocalVoiceLineController : MonoBehaviour
{
    /// <summary>
    /// Ligne de voix conditionnelle configuree directement sur ce controleur.
    /// </summary>
    [System.Serializable]
    public class LocalVoiceLineEntry
    {
        /// <summary>
        /// Donnee de voice line jouee quand cette entree est selectionnee.
        /// </summary>
        [Tooltip("VoiceLineData declenchee par cette entree.")]
        public VoiceLineData voiceLine;
        /// <summary>
        /// Connaissances necessaires pour autoriser cette entree.
        /// </summary>
        [Tooltip("Connaissances requises pour declencher la ligne.")]
        public List<KnowledgeSO> requiredKnowledge = new List<KnowledgeSO>();
        /// <summary>
        /// Connaissances debloquees apres lecture reussie de cette entree.
        /// </summary>
        [Tooltip("Connaissances debloquees lorsque la ligne est declenchee.")]
        public List<KnowledgeSO> unlockKnowledge = new List<KnowledgeSO>();
    }

    /// <summary>
    /// Strategie utilisee quand plusieurs entrees de voice line sont valides.
    /// </summary>
    public enum KnowledgeSelectionMode
    {
        /// <summary>
        /// Prend la premiere entree valide dans la liste.
        /// </summary>
        FirstMatch,
        /// <summary>
        /// Prend l'entree valide avec le plus de connaissances requises.
        /// </summary>
        MostSpecific
    }

    [Header("Voice Lines")]
    [Tooltip("CharacterData qui contient les voice lines.")]
    private CharacterData characterData;
    /// <summary>
    /// Reconstruit automatiquement le dictionnaire des voice lines.
    /// </summary>
    [Tooltip("Reconstruit le lookup au Awake/OnEnable.")]
    public bool autoBuildLookup = true;
    /// <summary>
    /// Tente de trouver le CharacterData via les composants voisins.
    /// </summary>
    [Tooltip("Auto-resout le CharacterData si manquant.")]
    public bool autoResolveCharacterData = true;

    [Header("Voice Lines")]
    /// <summary>
    /// Entrees locales avec conditions de connaissances.
    /// </summary>
    [Tooltip("Liste locale de voice lines (avec conditions de connaissances).")]
    [FormerlySerializedAs("entries")]
    public List<LocalVoiceLineEntry> voiceLines = new List<LocalVoiceLineEntry>();
    /// <summary>
    /// Mode de selection applique par <see cref="PlayBestEntry"/>.
    /// </summary>
    [Tooltip("Mode de selection quand plusieurs lignes sont valides.")]
    public KnowledgeSelectionMode knowledgeSelectionMode = KnowledgeSelectionMode.MostSpecific;

    [Header("Interaction")]
    /// <summary>
    /// Si vrai, l'input Interact declenche une ligne quand un personnage est a portee.
    /// </summary>
    [Tooltip("Ecoute l'input Interact pour declencher une ligne.")]
    public bool useInteractInput = false;
    /// <summary>
    /// Autorise le fallback par tag Player quand aucun membre de squad n'est trouve.
    /// </summary>
    [Tooltip("Exige un tag Player si aucun personnage de squad n'est trouve.")]
    public bool requirePlayerTag = true;
    /// <summary>
    /// Utilise les bounds du collider pour calculer la zone d'interaction.
    /// </summary>
    [Tooltip("Utilise le bounds du collider pour estimer le rayon.")]
    public bool useColliderBounds = true;
    /// <summary>
    /// Rayon manuel utilise si aucun collider ne sert de reference.
    /// </summary>
    [Tooltip("Rayon manuel d'interaction.")]
    public float interactionRadius = 1.25f;
    /// <summary>
    /// Marge ajoutee au rayon derive du collider.
    /// </summary>
    [Tooltip("Padding ajoute au rayon du collider.")]
    public float colliderRadiusPadding = 0.1f;
    /// <summary>
    /// Temps minimal entre deux interactions.
    /// </summary>
    [Tooltip("Cooldown entre deux interactions (secondes).")]
    public float interactCooldown = 0f;

    [Header("Display")]
    /// <summary>
    /// Transform qui sert de point d'ancrage au texte.
    /// </summary>
    [Tooltip("Point d'ancrage du texte (tete). Laisse vide pour utiliser ce transform.")]
    public Transform textAnchor;
    /// <summary>
    /// Offset du texte par rapport a l'ancrage.
    /// </summary>
    [Tooltip("Offset local du texte.")]
    public Vector3 textOffset = new Vector3(0f, 2f, 0f);
    /// <summary>
    /// Prefab optionnel contenant un TMP_Text pour l'affichage.
    /// </summary>
    [Tooltip("Prefab optionnel contenant un TMP_Text.")]
    public GameObject textPrefab;
    [Tooltip("Reference directe vers le TMP_Text instancie.")]
    [SerializeField] private TMP_Text textTarget;
    [Tooltip("Root des voice lines dans la scene (tag LocalVoiceLines).")]
    [SerializeField] private Transform textRoot;
    /// <summary>
    /// Oriente le texte vers la camera principale.
    /// </summary>
    [Tooltip("Oriente le texte vers la camera.")]
    public bool faceCamera = true;
    /// <summary>
    /// Duree d'affichage quand aucune duree audio exploitable n'existe.
    /// </summary>
    [Tooltip("Duree par defaut d'affichage si pas d'audio valide.")]
    public float fallbackTextDuration = 1.5f;
    /// <summary>
    /// Temps ajoute apres la fin du clip audio.
    /// </summary>
    [Tooltip("Temps ajoute a la duree audio (lecture).")]
    public float extraTextDuration = 0.1f;
    /// <summary>
    /// Masque le texte lorsque la lecture est terminee.
    /// </summary>
    [Tooltip("Masque le texte a la fin.")]
    public bool hideWhenDone = true;
    /// <summary>
    /// Garde le texte visible jusqu'a l'input Interact, meme si l'audio est termine.
    /// </summary>
    [Tooltip("Si actif, le texte reste visible jusqu'a Interact.")]
    public bool requireInteractToDismissText = true;
    /// <summary>
    /// Utilise le temps non scale pour continuer pendant certaines pauses UI.
    /// </summary>
    [Tooltip("Utilise le temps non-scale (UI).")]
    public bool useUnscaledTime = false;

    [Header("Audio")]
    /// <summary>
    /// Utilise AudioManager s'il est present dans la scene.
    /// </summary>
    [Tooltip("Utilise AudioManager si present.")]
    public bool useAudioManager = true;
    /// <summary>
    /// Spatial blend applique par la source locale de fallback.
    /// </summary>
    [Range(0f, 1f), Tooltip("Spatial blend si AudioManager absent.")]
    public float fallbackSpatialBlend = 1f;
    /// <summary>
    /// Distance minimale de la source audio de fallback.
    /// </summary>
    [Tooltip("Distance min si AudioManager absent.")]
    public float fallbackMinDistance = 1f;
    /// <summary>
    /// Distance maximale de la source audio de fallback.
    /// </summary>
    [Tooltip("Distance max si AudioManager absent.")]
    public float fallbackMaxDistance = 20f;

    [Header("Music Ducking")]
    /// <summary>
    /// Reduit temporairement la musique pendant la lecture d'une voice line.
    /// </summary>
    [Tooltip("Reduit la musique pendant une voiceline.")]
    public bool duckMusicDuringVoiceLine = true;
    /// <summary>
    /// Multiplicateur de volume musique pendant le ducking.
    /// </summary>
    [Range(0f, 1f), Tooltip("Multiplicateur applique a la musique pendant une voiceline.")]
    public float musicDuckMultiplier = 0.5f;

    private readonly Dictionary<int, VoiceLineData> lookup = new Dictionary<int, VoiceLineData>();
    private Coroutine displayRoutine;
    private AudioSource activeSource;
    private AudioClipSO activeAudioClip;
    private AudioSource localSource;
    private Transform textInstanceRoot;
    private bool warnedMissingTextRoot;
    private Collider interactionCollider;
    private float nextInteractTime;
    private bool isMusicDucked;
    private bool textVisible;
    private int textShownFrame = -1;

    private const string TextRootTag = "LocalVoiceLines";
    private const string TextRootObjectName = "LocalVoiceLines";

    private void Awake()
    {
        // Unity appelle Awake une fois au chargement du composant; on prepare les references cachees.
        EnsureTextTarget();
        ResolveCharacterData();
        if (autoBuildLookup)
        {
            RebuildLookup();
        }

        if (useInteractInput)
        {
            interactionCollider = GetComponent<Collider>();
        }
    }

    private void OnEnable()
    {
        // Unity appelle OnEnable a chaque activation; l'abonnement input doit etre refait ici.
        ResolveCharacterData();
        if (autoBuildLookup)
        {
            RebuildLookup();
        }

        if (useInteractInput || requireInteractToDismissText)
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }

            LocalInputRouter.EnsureInitialized();
            LocalInputRouter.Interact += OnInteractPerformed;
        }
    }

    private void OnDisable()
    {
        // Unity appelle OnDisable a la desactivation; il faut liberer input/audio pour eviter les callbacks orphelins.
        EndMusicDucking();
        LocalInputRouter.Interact -= OnInteractPerformed;
        ReleaseTextInputFocus();
    }

    private void LateUpdate()
    {
        // LateUpdate garde le texte et le son alignes apres le deplacement des personnages.
        UpdateTextTransform();
        UpdateAudioTransform();
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (requireInteractToDismissText && textVisible && InputFocusStack.HasFocus(this))
        {
            if (Time.frameCount == textShownFrame)
            {
                return;
            }

            if (LocalInputRouter.TryConsumeInteract())
            {
                DismissCurrentText(stopAudio: true);
            }

            return;
        }

        if (!useInteractInput)
        {
            return;
        }

        if (InputFocusStack.HasAnyFocus())
        {
            return;
        }

        if (!IsCharacterInRange())
        {
            return;
        }

        LocalInputRouter.ConsumeInteract();

        if (interactCooldown > 0f && Time.time < nextInteractTime)
        {
            return;
        }

        TriggerInteraction();
    }

    /// <summary>
    /// Declenche la meilleure voice line disponible pour l'interaction courante.
    /// </summary>
    public void TriggerInteraction()
    {
        if (interactCooldown > 0f)
        {
            nextInteractTime = Time.time + interactCooldown;
        }

        bool played = PlayBestEntry();
        if (!played)
        {
            PlayRandomVoiceLine();
        }
    }

    /// <summary>
    /// Reconstruit le dictionnaire index -> VoiceLineData a partir du CharacterData courant.
    /// </summary>
    public void RebuildLookup()
    {
        ResolveCharacterData();
        lookup.Clear();
        List<VoiceLineData> source = GetVoiceLineSource();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            VoiceLineData data = source[i];
            if (data == null)
            {
                continue;
            }

            lookup[data.voiceLineIndex] = data;
        }
    }

    /// <summary>
    /// Joue une voice line par son index dans le CharacterData.
    /// </summary>
    public bool PlayVoiceLine(int index)
    {
        if (!lookup.TryGetValue(index, out VoiceLineData data) || data == null)
        {
            data = FindVoiceLine(index);
            if (data == null)
            {
                return false;
            }

            lookup[index] = data;
        }

        return PlayVoiceLine(data);
    }

    /// <summary>
    /// Joue une voice line precise, avec support des cues texte si elle en contient.
    /// </summary>
    public bool PlayVoiceLine(VoiceLineData data)
    {
        if (data == null)
        {
            return false;
        }

        if (HasCues(data))
        {
            return PlayVoiceLineSequence(data);
        }

        return PlayLine(data.voiceLineText, data.voiceLineAudioClip);
    }

    /// <summary>
    /// Joue une entree conditionnelle et applique ses connaissances a debloquer.
    /// </summary>
    public bool PlayEntry(LocalVoiceLineEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        if (entry.voiceLine == null)
        {
            return false;
        }

        bool played = PlayVoiceLine(entry.voiceLine);
        if (played)
        {
            UnlockEntryKnowledge(entry);
        }

        return played;
    }

    /// <summary>
    /// Joue une entree locale par index dans la liste serialisee.
    /// </summary>
    public bool PlayEntry(int index)
    {
        if (voiceLines == null || index < 0 || index >= voiceLines.Count)
        {
            return false;
        }

        return PlayEntry(voiceLines[index]);
    }

    /// <summary>
    /// Selectionne puis joue l'entree la plus pertinente selon les connaissances du joueur.
    /// </summary>
    public bool PlayBestEntry()
    {
        LocalVoiceLineEntry entry = SelectEntryForKnowledge();
        if (entry == null)
        {
            return false;
        }

        return PlayEntry(entry);
    }

    /// <summary>
    /// Affiche un texte et joue le clip associe sans passer par une VoiceLineData.
    /// </summary>
    public bool PlayLine(string text, AudioClipSO clip)
    {
        EnsureTextTarget();
        if (textTarget == null)
        {
            return false;
        }

        StopCurrentPlayback();

        textTarget.text = text ?? string.Empty;
        SetTextVisible(true);

        float duration = ResolveTextDuration(clip);
        displayRoutine = StartCoroutine(CompleteLineAfterDelay(duration));

        BeginMusicDucking();
        PlayAudio(clip);
        return true;
    }

    private bool PlayVoiceLineSequence(VoiceLineData data)
    {
        EnsureTextTarget();
        if (textTarget == null)
        {
            return false;
        }

        StopCurrentPlayback();
        SetTextVisible(false);

        List<VoiceLineData.VoiceLineTextCue> cues = BuildSortedCues(data);
        if (cues == null || cues.Count == 0)
        {
            return PlayLine(data.voiceLineText, data.voiceLineAudioClip);
        }

        BeginMusicDucking();
        PlayAudio(data.voiceLineAudioClip);
        displayRoutine = StartCoroutine(PlayCuesRoutine(cues, data.voiceLineAudioClip));
        return true;
    }

    private bool HasCues(VoiceLineData data)
    {
        return data != null && data.voiceLineCues != null && data.voiceLineCues.Count > 0;
    }

    private List<VoiceLineData.VoiceLineTextCue> BuildSortedCues(VoiceLineData data)
    {
        if (data == null || data.voiceLineCues == null || data.voiceLineCues.Count == 0)
        {
            return null;
        }

        List<VoiceLineData.VoiceLineTextCue> cues = new List<VoiceLineData.VoiceLineTextCue>(data.voiceLineCues.Count);
        for (int i = 0; i < data.voiceLineCues.Count; i++)
        {
            VoiceLineData.VoiceLineTextCue cue = data.voiceLineCues[i];
            if (cue == null)
            {
                continue;
            }

            cues.Add(cue);
        }

        if (cues.Count == 0)
        {
            return null;
        }

        cues.Sort((a, b) => a.time.CompareTo(b.time));
        return cues;
    }

    private IEnumerator PlayCuesRoutine(List<VoiceLineData.VoiceLineTextCue> cues, AudioClipSO clip)
    {
        float startTime = useUnscaledTime ? Time.unscaledTime : Time.time;
        bool shownAny = false;

        for (int i = 0; i < cues.Count; i++)
        {
            VoiceLineData.VoiceLineTextCue cue = cues[i];
            if (cue == null)
            {
                continue;
            }

            float targetTime = Mathf.Max(0f, cue.time);
            while (GetPlaybackTime(startTime, clip) < targetTime)
            {
                yield return null;
            }

            if (textTarget != null)
            {
                textTarget.text = cue.text ?? string.Empty;
            }

            SetTextVisible(true);
            shownAny = true;
        }

        if (!shownAny)
        {
            EndMusicDucking();
            displayRoutine = null;
            yield break;
        }

        float duration = ResolveSequenceDuration(clip, cues);
        while (GetPlaybackTime(startTime, clip) < duration)
        {
            yield return null;
        }

        if (hideWhenDone && !requireInteractToDismissText)
        {
            SetTextVisible(false);
        }

        EndMusicDucking();
        displayRoutine = null;
    }

    private float GetPlaybackTime(float startTime, AudioClipSO clip)
    {
        if (activeSource != null && clip != null && clip.audioClip != null && !clip.loop && activeSource.clip == clip.audioClip)
        {
            return activeSource.time;
        }

        float now = useUnscaledTime ? Time.unscaledTime : Time.time;
        return Mathf.Max(0f, now - startTime);
    }

    private float ResolveSequenceDuration(AudioClipSO clip, List<VoiceLineData.VoiceLineTextCue> cues)
    {
        float duration = Mathf.Max(0.1f, fallbackTextDuration);

        if (clip != null && clip.audioClip != null && !clip.loop)
        {
            duration = Mathf.Max(duration, clip.audioClip.length + Mathf.Max(0f, extraTextDuration));
        }

        if (cues != null && cues.Count > 0)
        {
            float lastTime = cues[cues.Count - 1].time;
            duration = Mathf.Max(duration, lastTime + Mathf.Max(0.05f, fallbackTextDuration));
        }

        return duration;
    }

    private void SetTextVisible(bool visible)
    {
        if (visible)
        {
            textVisible = true;
            textShownFrame = Time.frameCount;
            if (requireInteractToDismissText)
            {
                InputFocusStack.Push(this);
            }
        }
        else
        {
            textVisible = false;
            ReleaseTextInputFocus();
        }

        Transform root = GetTextInstanceRoot();
        if (root != null && root.gameObject != null)
        {
            root.gameObject.SetActive(visible);
            return;
        }

        if (textTarget != null && textTarget.gameObject != null)
        {
            textTarget.gameObject.SetActive(visible);
        }
    }

    private bool IsCharacterInRange()
    {
        return FindClosestCharacter() != null;
    }

    private GameObject FindClosestCharacter()
    {
        Vector3 center = transform.position;
        float radius = interactionRadius;

        if (interactionCollider != null && useColliderBounds)
        {
            Bounds bounds = interactionCollider.bounds;
            center = bounds.center;
            Vector3 extents = bounds.extents;
            radius = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) + colliderRadiusPadding;
        }

        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        float bestDistance = float.MaxValue;
        GameObject bestCharacter = null;
        for (int i = 0; i < hits.Length; i++)
        {
            GameObject character = GetSquadCharacter(hits[i]);
            if (character == null)
            {
                continue;
            }

            float distance = (character.transform.position - center).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCharacter = character;
            }
        }

        return bestCharacter;
    }

    private GameObject GetSquadCharacter(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        SquadManager manager = SquadManager.Instance;
        Transform current = other.transform;
        bool hasPlayerTag = false;
        GameObject taggedRoot = null;
        GameObject squadRoot = null;

        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                hasPlayerTag = true;
                taggedRoot = current.gameObject;
            }

            if (manager != null && manager.squadCharacters != null && manager.squadCharacters.Contains(current.gameObject))
            {
                squadRoot = current.gameObject;
            }

            current = current.parent;
        }

        if (squadRoot == null && manager != null && manager.squadCharacters != null)
        {
            Transform root = other.transform.root;
            if (root != null)
            {
                if (root.CompareTag("Player"))
                {
                    hasPlayerTag = true;
                    taggedRoot = root.gameObject;
                }

                for (int i = 0; i < manager.squadCharacters.Count; i++)
                {
                    GameObject candidate = manager.squadCharacters[i];
                    if (candidate != null && candidate.transform.IsChildOf(root))
                    {
                        squadRoot = candidate;
                        break;
                    }
                }
            }
        }

        if (squadRoot != null)
        {
            return squadRoot;
        }

        if (requirePlayerTag && hasPlayerTag)
        {
            return taggedRoot;
        }

        return null;
    }

    private void StopCurrentPlayback()
    {
        if (displayRoutine != null)
        {
            StopCoroutine(displayRoutine);
            displayRoutine = null;
        }

        if (activeSource != null)
        {
            if (activeSource.isPlaying)
            {
                activeSource.Stop();
            }
        }

        activeAudioClip = null;
        EndMusicDucking();
    }

    private float ResolveTextDuration(AudioClipSO clip)
    {
        float duration = Mathf.Max(0.1f, fallbackTextDuration);
        if (clip != null && clip.audioClip != null && !clip.loop)
        {
            duration = Mathf.Max(duration, clip.audioClip.length + Mathf.Max(0f, extraTextDuration));
        }

        return duration;
    }

    private IEnumerator CompleteLineAfterDelay(float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        if (hideWhenDone && !requireInteractToDismissText)
        {
            SetTextVisible(false);
        }

        EndMusicDucking();
        displayRoutine = null;
    }

    private void DismissCurrentText(bool stopAudio)
    {
        if (displayRoutine != null)
        {
            StopCoroutine(displayRoutine);
            displayRoutine = null;
        }

        if (stopAudio && activeSource != null && activeSource.isPlaying)
        {
            activeSource.Stop();
        }

        if (stopAudio)
        {
            activeAudioClip = null;
        }

        SetTextVisible(false);
        EndMusicDucking();
    }

    private void ReleaseTextInputFocus()
    {
        InputFocusStack.Pop(this);
    }

    private void PlayAudio(AudioClipSO clip)
    {
        if (clip == null || clip.audioClip == null)
        {
            activeSource = null;
            activeAudioClip = null;
            return;
        }

        activeAudioClip = clip;
        Vector3 position = GetAnchorPosition();
        if (useAudioManager && AudioManager.Instance != null)
        {
            activeSource = AudioManager.Instance.PlayClip(clip, position);
            return;
        }

        AudioSource source = EnsureLocalSource();
        source.transform.position = position;
        source.clip = clip.audioClip;
        source.loop = clip.loop;
        AudioManager.ApplyClipPitch(source, clip);
        source.volume = Mathf.Clamp01(clip.volume);
        source.Play();
        activeSource = source;
    }

    private void BeginMusicDucking()
    {
        if (!duckMusicDuringVoiceLine || isMusicDucked)
        {
            return;
        }

        AudioManager manager = AudioManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.BeginMusicDucking(musicDuckMultiplier);
        isMusicDucked = true;
    }

    private void EndMusicDucking()
    {
        if (!isMusicDucked)
        {
            return;
        }

        AudioManager manager = AudioManager.Instance;
        if (manager != null)
        {
            manager.EndMusicDucking();
        }

        isMusicDucked = false;
    }

    private AudioSource EnsureLocalSource()
    {
        if (localSource == null)
        {
            localSource = GetComponent<AudioSource>();
            if (localSource == null)
            {
                localSource = gameObject.AddComponent<AudioSource>();
            }
        }

        localSource.playOnAwake = false;
        localSource.spatialBlend = Mathf.Clamp01(fallbackSpatialBlend);
        localSource.minDistance = Mathf.Max(0f, fallbackMinDistance);
        localSource.maxDistance = Mathf.Max(localSource.minDistance + 0.01f, fallbackMaxDistance);
        return localSource;
    }

    private void EnsureTextTarget()
    {
        ResolveTextRoot();
        if (textTarget != null)
        {
            if (textInstanceRoot == null)
            {
                textInstanceRoot = textTarget.transform;
            }

            return;
        }

        Transform anchor = textAnchor != null ? textAnchor : transform;
        Transform parent = textRoot != null ? textRoot : anchor;
        GameObject instance = null;
        if (textPrefab != null)
        {
            instance = Instantiate(textPrefab, parent);
        }
        else
        {
            instance = new GameObject("VoiceLineText");
            instance.transform.SetParent(parent, false);
            TextMeshPro tmp = instance.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            tmp.fontSize = 2f;
            tmp.text = string.Empty;
            textTarget = tmp;
        }

        if (instance == null)
        {
            return;
        }

        textInstanceRoot = instance.transform;
        if (textTarget == null)
        {
            textTarget = instance.GetComponentInChildren<TMP_Text>();
        }

        if (textRoot != null)
        {
            textInstanceRoot.position = anchor.TransformPoint(textOffset);
        }
        else
        {
            textInstanceRoot.localPosition = textOffset;
        }

        if (textInstanceRoot != null && textInstanceRoot.gameObject != null)
        {
            textInstanceRoot.gameObject.SetActive(false);
        }
        else if (textTarget != null && textTarget.gameObject != null)
        {
            textTarget.gameObject.SetActive(false);
        }
    }

    private VoiceLineData FindVoiceLine(int index)
    {
        List<VoiceLineData> source = GetVoiceLineSource();
        if (source == null)
        {
            return null;
        }

        for (int i = 0; i < source.Count; i++)
        {
            VoiceLineData data = source[i];
            if (data != null && data.voiceLineIndex == index)
            {
                return data;
            }
        }

        return null;
    }

    /// <summary>
    /// Joue une voice line aleatoire issue du CharacterData courant.
    /// </summary>
    public bool PlayRandomVoiceLine()
    {
        List<VoiceLineData> source = GetVoiceLineSource();
        if (source == null || source.Count == 0)
        {
            return false;
        }

        List<VoiceLineData> valid = null;
        for (int i = 0; i < source.Count; i++)
        {
            VoiceLineData data = source[i];
            if (data == null)
            {
                continue;
            }

            if (valid == null)
            {
                valid = new List<VoiceLineData>();
            }

            valid.Add(data);
        }

        if (valid == null || valid.Count == 0)
        {
            return false;
        }

        int index = Random.Range(0, valid.Count);
        return PlayVoiceLine(valid[index]);
    }

    private LocalVoiceLineEntry SelectEntryForKnowledge()
    {
        if (voiceLines == null || voiceLines.Count == 0)
        {
            return null;
        }

        LocalVoiceLineEntry best = null;
        int bestScore = -1;

        for (int i = 0; i < voiceLines.Count; i++)
        {
            LocalVoiceLineEntry entry = voiceLines[i];
            if (entry == null || entry.voiceLine == null)
            {
                continue;
            }

            if (!IsEntryUnlocked(entry))
            {
                continue;
            }

            if (knowledgeSelectionMode == KnowledgeSelectionMode.FirstMatch)
            {
                return entry;
            }

            int score = entry != null && entry.requiredKnowledge != null ? entry.requiredKnowledge.Count : 0;
            if (score > bestScore)
            {
                bestScore = score;
                best = entry;
            }
        }

        return best;
    }

    private bool IsEntryUnlocked(LocalVoiceLineEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        if (entry.requiredKnowledge == null || entry.requiredKnowledge.Count == 0)
        {
            return true;
        }

        if (KnowledgeManager.Instance == null)
        {
            return false;
        }

        for (int i = 0; i < entry.requiredKnowledge.Count; i++)
        {
            KnowledgeSO knowledge = entry.requiredKnowledge[i];
            if (knowledge == null)
            {
                continue;
            }

            if (!KnowledgeManager.Instance.HasKnowledge(knowledge))
            {
                return false;
            }
        }

        return true;
    }

    private void UnlockEntryKnowledge(LocalVoiceLineEntry entry)
    {
        if (entry == null || entry.unlockKnowledge == null || entry.unlockKnowledge.Count == 0)
        {
            return;
        }

        KnowledgeManager manager = KnowledgeManager.Instance;
        if (manager == null)
        {
            return;
        }

        for (int i = 0; i < entry.unlockKnowledge.Count; i++)
        {
            KnowledgeSO knowledge = entry.unlockKnowledge[i];
            if (knowledge != null)
            {
                manager.UnlockKnowledge(knowledge);
            }
        }
    }

    private void ResolveCharacterData()
    {
        CharacterInfo info = GetComponent<CharacterInfo>();
        if (info == null)
        {
            info = GetComponentInParent<CharacterInfo>();
        }

        if (info != null && info.CharacterData != null)
        {
            characterData = info.CharacterData;
            return;
        }

        SquadCharacterController controller = GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            controller = GetComponentInParent<SquadCharacterController>();
        }

        if (controller != null && controller.CharacterData != null)
        {
            characterData = controller.CharacterData;
        }
    }

    private List<VoiceLineData> GetVoiceLineSource()
    {
        ResolveCharacterData();
        return characterData != null ? characterData.voiceLines : null;
    }

    private void UpdateTextTransform()
    {
        if (textTarget == null)
        {
            return;
        }

        Transform t = GetTextInstanceRoot();
        if (t == null)
        {
            return;
        }

        Transform anchor = textAnchor != null ? textAnchor : transform;
        if (textRoot != null)
        {
            if (t.parent != textRoot)
            {
                t.SetParent(textRoot, true);
            }

            t.position = anchor.TransformPoint(textOffset);
        }
        else
        {
            if (t.parent != anchor)
            {
                t.SetParent(anchor, false);
            }

            t.localPosition = textOffset;
        }

        if (faceCamera)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCamera = t.position - cam.transform.position;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    t.rotation = Quaternion.LookRotation(toCamera);
                }
            }
        }
    }

    private void UpdateAudioTransform()
    {
        if (activeSource == null)
        {
            return;
        }

        activeSource.transform.position = GetAnchorPosition();
        AudioManager.ApplyClipPitch(activeSource, activeAudioClip);
    }

    private Vector3 GetAnchorPosition()
    {
        Transform anchor = textAnchor != null ? textAnchor : transform;
        return anchor.position;
    }

    private Transform GetTextInstanceRoot()
    {
        if (textInstanceRoot != null)
        {
            return textInstanceRoot;
        }

        return textTarget != null ? textTarget.transform : null;
    }

    private void ResolveTextRoot()
    {
        if (textRoot != null)
        {
            return;
        }

        GameObject root = null;
        try
        {
            root = GameObject.FindGameObjectWithTag(TextRootTag);
        }
        catch (UnityException)
        {
        }

        if (root != null)
        {
            textRoot = root.transform;
            return;
        }

        Transform namedRoot = FindSceneTransformByName(TextRootObjectName);
        if (namedRoot != null)
        {
            textRoot = namedRoot;
            warnedMissingTextRoot = false;
            return;
        }

        GameObject createdRoot = new GameObject(TextRootObjectName);
        try
        {
            createdRoot.tag = TextRootTag;
        }
        catch (UnityException)
        {
        }

        textRoot = createdRoot.transform;
        warnedMissingTextRoot = false;
    }

    private void WarnMissingTextRoot(string message)
    {
        if (warnedMissingTextRoot)
        {
            return;
        }

        Debug.LogWarning($"{nameof(LocalVoiceLineController)}: {message}", this);
        warnedMissingTextRoot = true;
    }

    private static Transform FindSceneTransformByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] candidates = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = candidates[i];
            if (candidate == null)
            {
                continue;
            }

            GameObject candidateObject = candidate.gameObject;
            if (candidateObject == null ||
                !candidateObject.scene.IsValid() ||
                !string.Equals(candidateObject.name, objectName, System.StringComparison.Ordinal))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }
}
