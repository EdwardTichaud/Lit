using System.Collections;
using UnityEngine;

/// <summary>
/// Fait apparaitre un effet "First Strike" depuis la droite de l'écran,
/// applique un ralenti puis le fait disparaître vers la gauche.
/// </summary>
public class FirstStrikeEffect : MonoBehaviour
{
    [Header("Déplacement")]
    public float startX = 2000f;
    public float endX = -2000f;
    public float speed = 1000f;

    [Header("Ralenti")]
    public float slowMotionScale = 0.3f;
    public float slowMotionDuration = 0.5f;

    private float initialTimeScale;

    private void OnEnable()
    {
        Vector3 pos = transform.position;
        pos.x = startX;
        transform.position = pos;

        initialTimeScale = Time.timeScale;
        StartCoroutine(SlowMotion());
    }

    private IEnumerator SlowMotion()
    {
        Time.timeScale = slowMotionScale;
        yield return new WaitForSecondsRealtime(slowMotionDuration);
        Time.timeScale = initialTimeScale;
    }

    private void Update()
    {
        transform.position += Vector3.left * speed * Time.unscaledDeltaTime;

        if (transform.position.x <= endX)
            gameObject.SetActive(false);
    }
}
