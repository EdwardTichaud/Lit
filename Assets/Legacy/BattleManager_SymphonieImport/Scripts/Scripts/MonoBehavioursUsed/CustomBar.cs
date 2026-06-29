using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Composant UI pour afficher la barre personalisée d'une unité.
/// </summary>
public class CustomBar : MonoBehaviour
{
    [Header("Référence Slider")]
    public Slider slider;

    [Header("Effet de tremblement")]
    public float shakeDuration = 0.15f;
    public float shakeMagnitude = 2f;

    private Coroutine shakeRoutine;
    private bool isShaking;
    private RectTransform target;
    private Vector3 originalPos;

    private void Awake()
    {
        if (slider != null)
        {
            target = slider.GetComponent<RectTransform>();
            if (target != null)
                originalPos = target.localPosition;
        }
    }

    public void SetMaxValue(float max)
    {
        if (slider != null)
        {
            slider.maxValue = max;
            slider.value = max;
        }
    }

    public void SetValue(float current)
    {
        if (slider != null)
        {
            slider.value = current;

            if (current >= slider.maxValue)
                StartShake();
            else
                StopShake();
        }
    }

    private void StartShake()
    {
        // Si l'objet est désactivé dans la hiérarchie, on évite de lancer la coroutine
        // afin d'empêcher l'erreur "Coroutine couldn't be started".
        if (!isActiveAndEnabled)
            return;

        if (isShaking)
            return;

        isShaking = true;
        if (shakeRoutine == null)
            shakeRoutine = StartCoroutine(ShakeCoroutine());
    }

    private void StopShake()
    {
        if (!isShaking)
            return;

        isShaking = false;
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }
        if (target != null)
            target.localPosition = originalPos;
    }

    private IEnumerator ShakeCoroutine()
    {
        if (target == null)
            yield break;

        while (isShaking)
        {
            float x = Random.Range(-shakeMagnitude, shakeMagnitude);
            float y = Random.Range(-shakeMagnitude, shakeMagnitude);
            target.localPosition = originalPos + new Vector3(x, y, 0f);
            yield return new WaitForSeconds(shakeDuration);
        }

        target.localPosition = originalPos;
        shakeRoutine = null;
    }
}
