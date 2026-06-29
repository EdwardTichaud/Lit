using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Affiche un signal visuel indiquant qu'une interception est possible.
/// </summary>
public class InterceptionSignal : MonoBehaviour
{
    public Image progressImage;
    public float duration = 1f;

    private Coroutine fillRoutine;

    public void StartSignal(float d)
    {
        duration = d;
        if (progressImage != null)
            progressImage.fillAmount = 0f;
        if (fillRoutine != null)
            StopCoroutine(fillRoutine);
        fillRoutine = StartCoroutine(FillRoutine());
    }

    private IEnumerator FillRoutine()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (progressImage != null)
                progressImage.fillAmount = elapsed / duration;
            yield return null;
        }
        Destroy(gameObject);
    }

    public void StopSignal()
    {
        if (fillRoutine != null)
            StopCoroutine(fillRoutine);
        Destroy(gameObject);
    }
}
