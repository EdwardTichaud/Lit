using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Joue des lignes de voix et affiche le texte en world space, avec support des connaissances.
public class LocalVoiceLineController : MonoBehaviour
{
    [System.Serializable]
    public class LocalVoiceLineEntry
    {
        [Tooltip("VoiceLineData declenchee par cette entree.")]
        public VoiceLineData voiceLine;
        [Tooltip("Connaissances requises pour declencher la ligne.")]
        public List<KnowledgeSO> requiredKnowledge = new List<KnowledgeSO>();
        [Tooltip("Connaissances debloquees lorsque la ligne est declenchee.")]
        public List<KnowledgeSO> unlockKnowledge = new List<KnowledgeSO>();
    }

    public enum KnowledgeSelectionMode
    {
        FirstMatch,
        MostSpecific
    }

    [Header("Voice Lines")]
    [Tooltip("CharacterData qui contient les voice lines.")]
    private CharacterData characterData;
    [Tooltip("Reconstruit le lookup au Awake/OnEnable.")]
    public bool autoBuildLookup = true;
    [Tooltip("Auto-resout le CharacterData si manquant.")]
    public bool autoResolveCharacterData = true;

    [Header("Voice Lines")]
    [Tooltip("Liste locale de voice lines (avec conditions de connaissances).")]
    [FormerlySerializedAs("entries")]
    public List<LocalVoiceLineEntry> voiceLines = new List<LocalVoiceLineEntry>();
    [Tooltip("Mode de selection quand plusieurs lignes sont valides.")]
    public KnowledgeSelectionMode knowledgeSelectionMode = KnowledgeSelectionMode.MostSpecific;

    [Header("Interaction")]
    [Tooltip("Ecoute l'input Interact pour declencher une ligne.")]
    public bool useInteractInput = false;
    [Tooltip("Exige un tag Player si aucun personnage de squad n'est trouve.")]
    public bool requirePlayerTag = true;
    [Tooltip("Utilise le bounds du collider pour estimer le rayon.")]
    public bool useColliderBounds = true;
    [Tooltip("Rayon manuel d'interaction.")]
    public float interactionRadius = 1.25f;
    [Tooltip("Padding ajoute au rayon du collider.")]
    public float colliderRadiusPadding = 0.1f;
    [Tooltip("Cooldown entre deux interactions (secondes).")]
    public float interactCooldown = 0f;

    [Header("Display")]
    [Tooltip("Point d'ancrage du texte (tete). Laisse vide pour utiliser ce transform.")]
    public Transform textAnchor;
    [Tooltip("Offset local du texte.")]
    public Vector3 textOffset = new Vector3(0f, 2f, 0f);
    [Tooltip("Prefab optionnel contenant un TMP_Text.")]
    public GameObject textPrefab;
    [Tooltip("Reference directe vers le TMP_Text instancie.")]
    [SerializeField] private TMP_Text textTarget;
    [Tooltip("Root des voice lines dans la scene (tag LocalVoiceLines).")]
    [SerializeField] private Transform textRoot;
    [Tooltip("Oriente le texte vers la camera.")]
    public bool faceCamera = true;
    [Tooltip("Duree par defaut d'affichage si pas d'audio valide.")]
    public float fallbackTextDuration = 1.5f;
    [Tooltip("Temps ajoute a la duree audio (lecture).")]
    public float extraTextDuration = 0.1f;
    [Tooltip("Masque le texte a la fin.")]
    public bool hideWhenDone = true;
    [Tooltip("Utilise le temps non-scale (UI).")]
    public bool useUnscaledTime = false;

    [Header("Audio")]
    [Tooltip("Utilise AudioManager si present.")]
    public bool useAudioManager = true;
    [Range(0f, 1f), Tooltip("Spatial blend si AudioManager absent.")]
    public float fallbackSpatialBlend = 1f;
    [Tooltip("Distance min si AudioManager absent.")]
    public float fallbackMinDistance = 1f;
    [Tooltip("Distance max si AudioManager absent.")]
    public float fallbackMaxDistance = 20f;

    [Header("Music Ducking")]
    [Tooltip("Reduit la musique pendant une voiceline.")]
    public bool duckMusicDuringVoiceLine = true;
    [Range(0f, 1f), Tooltip("Multiplicateur applique a la musique pendant une voiceline.")]
    public float musicDuckMultiplier = 0.5f;

    private readonly Dictionary<int, VoiceLineData> lookup = new Dictionary<int, VoiceLineData>();
    private Coroutine displayRoutine;
    private AudioSource activeSource;
    private AudioSource localSource;
    private Transform textInstanceRoot;
    private bool warnedMissingTextRoot;
    private Collider interactionCollider;
    private float nextInteractTime;
    private bool isMusicDucked;

    private const string TextRootTag = "LocalVoiceLines";

    private void Awake()
    {
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
        ResolveCharacterData();
        if (autoBuildLookup)
        {
            RebuildLookup();
        }

        if (useInteractInput)
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
        EndMusicDucking();
        if (!useInteractInput)
        {
            return;
        }

        LocalInputRouter.Interact -= OnInteractPerformed;
    }

    private void LateUpdate()
    {
        UpdateTextTransform();
        UpdateAudioTransform();
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
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

    public bool PlayEntry(int index)
    {
        if (voiceLines == null || index < 0 || index >= voiceLines.Count)
        {
            return false;
        }

        return PlayEntry(voiceLines[index]);
    }

    public bool PlayBestEntry()
    {
        LocalVoiceLineEntry entry = SelectEntryForKnowledge();
        if (entry == null)
        {
            return false;
        }

        return PlayEntry(entry);
    }

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
        displayRoutine = StartCoroutine(HideAfterDelay(duration));

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

        if (hideWhenDone)
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

    private IEnumerator HideAfterDelay(float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        if (hideWhenDone)
        {
            Transform root = GetTextInstanceRoot();
            if (root != null && root.gameObject != null)
            {
                root.gameObject.SetActive(false);
            }
            else if (textTarget != null && textTarget.gameObject != null)
            {
                textTarget.gameObject.SetActive(false);
            }
        }

        EndMusicDucking();
        displayRoutine = null;
    }

    private void PlayAudio(AudioClipSO clip)
    {
        if (clip == null || clip.audioClip == null)
        {
            activeSource = null;
            return;
        }

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
            WarnMissingTextRoot($"tag '{TextRootTag}' non defini.");
            return;
        }

        if (root != null)
        {
            textRoot = root.transform;
            return;
        }

        WarnMissingTextRoot($"aucun objet avec le tag '{TextRootTag}' dans la scene.");
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
}
