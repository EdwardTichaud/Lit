using System.Collections;
using UnityEngine;

public sealed class GroundImpactWave : MonoBehaviour
{
    private LineRenderer ring;
    private Material material;
    public static void Show(Vector3 point, float radius)
    {
        var wave = new GameObject("Impact de lumiere").AddComponent<GroundImpactWave>();
        wave.transform.position = point + Vector3.up * .05f;
        wave.StartCoroutine(wave.Animate(radius));
    }
    private IEnumerator Animate(float radius)
    {
        ring = gameObject.AddComponent<LineRenderer>(); ring.loop = true; ring.useWorldSpace = false;
        ring.positionCount = 64; ring.widthMultiplier = .12f;
        var shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Sprites/Default");
        if (shader == null) { Destroy(gameObject); yield break; }
        material = new Material(shader); ring.sharedMaterial = material;
        if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", new Color(1, .85f, .3f));
        float elapsed = 0;
        while (elapsed < .35f)
        {
            float t = elapsed / .35f;
            for (int i = 0; i < 64; i++)
            { float a = i * Mathf.PI * 2 / 64; ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * t); }
            ring.widthMultiplier = .12f * (1 - t);
            elapsed += Time.deltaTime; yield return null;
        }
        Destroy(gameObject);
    }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
