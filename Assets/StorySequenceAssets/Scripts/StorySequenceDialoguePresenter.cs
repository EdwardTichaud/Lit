using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lit.Story
{
    [DisallowMultipleComponent]
    public sealed class StorySequenceDialoguePresenter : MonoBehaviour
    {
        [Header("Optional Scene UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private TMP_Text continueHintText;

        [Header("Runtime UI")]
        [SerializeField] private bool createRuntimeUiWhenMissing = true;
        [SerializeField] private int sortingOrder = short.MaxValue - 30;
        [SerializeField] private Color panelColor = new Color(0.025f, 0.025f, 0.035f, 0.9f);
        [SerializeField] private Color speakerColor = new Color(0.95f, 0.78f, 0.35f, 1f);

        [Header("Playback")]
        [SerializeField, Min(0.1f)] private float fallbackDuration = 2f;
        [SerializeField, Min(0f)] private float extraDuration = 0.1f;
        [SerializeField, Min(0f)] private float uiFadeDuration = 0.15f;
        [SerializeField] private bool duckMusic = true;
        [SerializeField, Range(0f, 1f)] private float musicDuckMultiplier = 0.45f;

        private AudioSource activeSource;
        private bool activeClipLoops;
        private bool musicDucked;

        public IEnumerator Present(
            VoiceLineData line,
            string speakerName,
            Transform audioAnchor,
            bool waitForInteractAfterLine,
            float minimumDuration,
            bool useUnscaledTime,
            Func<bool> consumeSkipRequest)
        {
            if (line == null)
            {
                yield break;
            }

            EnsureUi();
            StopAudio();
            SetSpeaker(speakerName);
            SetDialogue(line.voiceLineText);
            SetContinueHint(false);

            List<VoiceLineData.VoiceLineTextCue> cues = BuildSortedCues(line);
            if (cues.Count > 0)
            {
                SetDialogue(cues[0].time <= 0f ? cues[0].text : line.voiceLineText);
            }

            BeginMusicDucking();
            PlayAudio(line.voiceLineAudioClip, audioAnchor);
            yield return FadeCanvasTo(1f, uiFadeDuration, useUnscaledTime);

            float totalDuration = ResolveDuration(line, cues, minimumDuration);
            float elapsed = 0f;
            int nextCue = 0;
            while (nextCue < cues.Count && cues[nextCue].time <= 0f)
            {
                SetDialogue(cues[nextCue].text);
                nextCue++;
            }

            bool skipped = false;
            while (elapsed < totalDuration)
            {
                if (consumeSkipRequest != null && consumeSkipRequest())
                {
                    skipped = true;
                    break;
                }

                elapsed = ResolvePlaybackTime(elapsed, useUnscaledTime);
                while (nextCue < cues.Count && elapsed >= Mathf.Max(0f, cues[nextCue].time))
                {
                    SetDialogue(cues[nextCue].text);
                    nextCue++;
                }

                yield return null;
            }

            if (!skipped && waitForInteractAfterLine)
            {
                SetContinueHint(true);
                while (consumeSkipRequest == null || !consumeSkipRequest())
                {
                    yield return null;
                }
            }

            StopAudio();
            EndMusicDucking();
            yield return FadeCanvasTo(0f, uiFadeDuration, useUnscaledTime);
            SetContinueHint(false);
        }

        public void HideImmediate()
        {
            StopAudio();
            EndMusicDucking();
            EnsureUi();
            SetCanvasAlpha(0f);
        }

        private float ResolvePlaybackTime(float elapsed, bool useUnscaledTime)
        {
            if (!activeClipLoops && activeSource != null && activeSource.isPlaying)
            {
                return activeSource.time;
            }

            return elapsed + (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private float ResolveDuration(
            VoiceLineData line,
            List<VoiceLineData.VoiceLineTextCue> cues,
            float minimumDuration)
        {
            float duration = Mathf.Max(0.1f, fallbackDuration, minimumDuration);
            AudioClipSO clip = line.voiceLineAudioClip;
            if (clip != null && clip.audioClip != null && !clip.loop)
            {
                duration = Mathf.Max(duration, clip.audioClip.length + extraDuration);
            }

            if (cues.Count > 0)
            {
                duration = Mathf.Max(duration, cues[cues.Count - 1].time + fallbackDuration);
            }

            return duration;
        }

        private static List<VoiceLineData.VoiceLineTextCue> BuildSortedCues(VoiceLineData line)
        {
            List<VoiceLineData.VoiceLineTextCue> result = new List<VoiceLineData.VoiceLineTextCue>();
            if (line == null || line.voiceLineCues == null)
            {
                return result;
            }

            for (int i = 0; i < line.voiceLineCues.Count; i++)
            {
                VoiceLineData.VoiceLineTextCue cue = line.voiceLineCues[i];
                if (cue != null)
                {
                    result.Add(cue);
                }
            }

            result.Sort((a, b) => a.time.CompareTo(b.time));
            return result;
        }

        private void PlayAudio(AudioClipSO clip, Transform anchor)
        {
            if (clip == null || clip.audioClip == null)
            {
                activeSource = null;
                activeClipLoops = false;
                return;
            }

            activeClipLoops = clip.loop;
            Vector3 position = anchor != null ? anchor.position : transform.position;
            if (AudioManager.Instance != null)
            {
                activeSource = AudioManager.Instance.PlayClip(clip, position);
                return;
            }

            AudioSource source = gameObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }

            source.spatialBlend = anchor != null ? 1f : 0f;
            source.transform.position = position;
            source.clip = clip.audioClip;
            source.loop = clip.loop;
            source.volume = Mathf.Clamp01(clip.volume);
            source.Play();
            activeSource = source;
        }

        private void StopAudio()
        {
            if (activeSource != null && activeSource.isPlaying)
            {
                activeSource.Stop();
            }

            activeSource = null;
            activeClipLoops = false;
        }

        private void BeginMusicDucking()
        {
            if (!duckMusic || musicDucked || AudioManager.Instance == null)
            {
                return;
            }

            AudioManager.Instance.BeginMusicDucking(musicDuckMultiplier);
            musicDucked = true;
        }

        private void EndMusicDucking()
        {
            if (!musicDucked)
            {
                return;
            }

            AudioManager.Instance?.EndMusicDucking();
            musicDucked = false;
        }

        private IEnumerator FadeCanvasTo(float target, float duration, bool useUnscaledTime)
        {
            EnsureUi();
            float start = canvasGroup != null ? canvasGroup.alpha : 0f;
            float fadeDuration = Mathf.Max(0f, duration);
            if (fadeDuration <= 0f)
            {
                SetCanvasAlpha(target);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                SetCanvasAlpha(Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeDuration)));
                yield return null;
            }

            SetCanvasAlpha(target);
        }

        private void EnsureUi()
        {
            if (canvasGroup != null && speakerText != null && dialogueText != null)
            {
                return;
            }

            if (!createRuntimeUiWhenMissing)
            {
                return;
            }

            GameObject canvasObject = new GameObject(
                "StorySequence_DialogueCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasObject.GetComponent<CanvasGroup>();

            GameObject panelObject = CreateUiObject(
                "DialoguePanel",
                canvasObject.transform,
                typeof(Image));
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.04f);
            panelRect.anchorMax = new Vector2(0.92f, 0.27f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelObject.GetComponent<Image>().color = panelColor;

            speakerText = CreateText("Speaker", panelRect, 34f, FontStyles.Bold);
            RectTransform speakerRect = speakerText.rectTransform;
            speakerRect.anchorMin = new Vector2(0.035f, 0.66f);
            speakerRect.anchorMax = new Vector2(0.72f, 0.94f);
            speakerRect.offsetMin = Vector2.zero;
            speakerRect.offsetMax = Vector2.zero;
            speakerText.color = speakerColor;

            dialogueText = CreateText("Dialogue", panelRect, 29f, FontStyles.Normal);
            RectTransform dialogueRect = dialogueText.rectTransform;
            dialogueRect.anchorMin = new Vector2(0.035f, 0.14f);
            dialogueRect.anchorMax = new Vector2(0.965f, 0.67f);
            dialogueRect.offsetMin = Vector2.zero;
            dialogueRect.offsetMax = Vector2.zero;

            continueHintText = CreateText("ContinueHint", panelRect, 19f, FontStyles.Italic);
            RectTransform hintRect = continueHintText.rectTransform;
            hintRect.anchorMin = new Vector2(0.68f, 0.02f);
            hintRect.anchorMax = new Vector2(0.965f, 0.18f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;
            continueHintText.alignment = TextAlignmentOptions.BottomRight;
            continueHintText.text = "Interact";

            SetCanvasAlpha(0f);
            SetContinueHint(false);
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
        {
            Type[] allComponents = new Type[components.Length + 2];
            allComponents[0] = typeof(RectTransform);
            allComponents[1] = typeof(CanvasRenderer);
            Array.Copy(components, 0, allComponents, 2, components.Length);
            GameObject result = new GameObject(name, allComponents);
            result.transform.SetParent(parent, false);
            return result;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            float fontSize,
            FontStyles style)
        {
            GameObject textObject = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private void SetSpeaker(string value)
        {
            if (speakerText != null)
            {
                speakerText.text = value ?? string.Empty;
            }
        }

        private void SetDialogue(string value)
        {
            if (dialogueText != null)
            {
                dialogueText.text = value ?? string.Empty;
            }
        }

        private void SetContinueHint(bool visible)
        {
            if (continueHintText != null)
            {
                continueHintText.gameObject.SetActive(visible);
            }
        }

        private void SetCanvasAlpha(float alpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = Mathf.Clamp01(alpha);
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void OnDisable()
        {
            StopAudio();
            EndMusicDucking();
        }
    }
}
