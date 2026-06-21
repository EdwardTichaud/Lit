using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Lit.Story
{
    [DisallowMultipleComponent]
    public sealed class StorySequenceFadeController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fadeImage;
        [SerializeField] private Color fadeColor = Color.black;
        [SerializeField] private int sortingOrder = short.MaxValue - 20;

        public float Alpha => canvasGroup != null ? canvasGroup.alpha : 0f;

        private void Awake()
        {
            EnsureUi();
        }

        public void SetImmediate(float alpha)
        {
            EnsureUi();
            ApplyAlpha(Mathf.Clamp01(alpha));
        }

        public IEnumerator FadeTo(
            float targetAlpha,
            float duration,
            bool useUnscaledTime,
            Func<bool> consumeSkipRequest = null)
        {
            EnsureUi();
            float startAlpha = Alpha;
            float target = Mathf.Clamp01(targetAlpha);
            float fadeDuration = Mathf.Max(0f, duration);
            if (fadeDuration <= 0f)
            {
                ApplyAlpha(target);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                if (consumeSkipRequest != null && consumeSkipRequest())
                {
                    ApplyAlpha(target);
                    yield break;
                }

                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));
                ApplyAlpha(Mathf.Lerp(startAlpha, target, t));
                yield return null;
            }

            ApplyAlpha(target);
        }

        private void EnsureUi()
        {
            if (canvasGroup != null && fadeImage != null)
            {
                fadeImage.color = fadeColor;
                return;
            }

            GameObject canvasObject = new GameObject(
                "StorySequence_FadeCanvas",
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
            canvasGroup.alpha = 0f;

            GameObject imageObject = new GameObject(
                "Black",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            fadeImage = imageObject.GetComponent<Image>();
            fadeImage.color = fadeColor;
            ApplyAlpha(0f);
        }

        private void ApplyAlpha(float alpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = alpha;
            bool blocksInput = alpha > 0.001f;
            canvasGroup.blocksRaycasts = blocksInput;
            canvasGroup.interactable = blocksInput;
        }
    }
}
