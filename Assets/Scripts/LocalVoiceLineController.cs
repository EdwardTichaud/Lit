using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Joue des lignes de voix et affiche le texte au dessus du personnage.
public class LocalVoiceLineController : MonoBehaviour
{
    [Header("Voice Lines")]
    [Tooltip("CharacterData qui contient les voice lines.")]
    private CharacterData characterData;
    [Tooltip("Reconstruit le lookup au Awake/OnEnable.")]
    public bool autoBuildLookup = true;
    [Tooltip("Auto-resout le CharacterData si manquant.")]
    public bool autoResolveCharacterData = true;

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

    private readonly Dictionary<int, VoiceLineData> lookup = new Dictionary<int, VoiceLineData>();
    private Coroutine displayRoutine;
    private AudioSource activeSource;
    private AudioSource localSource;
    private Transform textInstanceRoot;
    private bool warnedMissingTextRoot;

    private const string TextRootTag = "LocalVoiceLines";

    private void Awake()
    {
        EnsureTextTarget();
        ResolveCharacterData();
        if (autoBuildLookup)
        {
            RebuildLookup();
        }
    }

    private void OnEnable()
    {
        ResolveCharacterData();
        if (autoBuildLookup)
        {
            RebuildLookup();
        }
    }

    private void LateUpdate()
    {
        UpdateTextTransform();
        UpdateAudioTransform();
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

        EnsureTextTarget();
        if (textTarget == null)
        {
            return false;
        }

        StopCurrentPlayback();

        textTarget.text = data.voiceLineText ?? string.Empty;
        Transform root = GetTextInstanceRoot();
        if (root != null && root.gameObject != null)
        {
            root.gameObject.SetActive(true);
        }
        else if (textTarget.gameObject != null)
        {
            textTarget.gameObject.SetActive(true);
        }

        float duration = ResolveTextDuration(data.voiceLineAudioClip);
        displayRoutine = StartCoroutine(HideAfterDelay(duration));

        PlayAudio(data.voiceLineAudioClip);
        return true;
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
