using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Lets death finish, dissolves the corpse, then disables its persistent root.</summary>
[DisallowMultipleComponent]
public sealed class EnemyDeathPresentation : MonoBehaviour
{
    [SerializeField, Min(0f)] private float minimumDeathDelay = 3f;
    [SerializeField, Min(0.1f)] private float dissolveSeconds = 2.8f;
    private EnemyController enemy;
    private Coroutine routine;
    private GhostDissolveController dissolve;
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private readonly List<Material> temporaryMaterials = new List<Material>();

    private void Awake() => enemy = GetComponent<EnemyController>();

    private void Update()
    {
        if (routine == null && enemy != null && enemy.Health != null && enemy.Health.IsDead)
            routine = StartCoroutine(Disappear());
    }

    private IEnumerator Disappear()
    {
        GetComponent<EnemyController>()?.StopNavigation();
        enemy.PlayDeathAnimation();
        float started = Time.unscaledTime;
        // Give grounding and the death state time to take ownership of the Animator.
        yield return null;
        while (enemy.Health.IsDead)
        {
            var scientist = GetComponent<ScientistEncounterController>();
            bool speaking = scientist != null && scientist.IsDeathPresentationPlaying;
            Animator animator = enemy.Animator;
            bool animationPlaying = animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled &&
                (animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f);
            // An invalid/stalled animation must not leave an immortal corpse.
            if (Time.unscaledTime - started >= Mathf.Max(12f, minimumDeathDelay)) animationPlaying = false;
            if (Time.unscaledTime - started >= minimumDeathDelay && !speaking && !animationPlaying) break;
            yield return null;
        }
        if (!enemy.Health.IsDead) { routine = null; yield break; }

        PrepareDissolveMaterials();
        // A dedicated controller owns corpse disappearance; do not trigger authored
        // ghost events that might destroy/despawn a network object on a client.
        dissolve = GetComponent<GhostDissolveController>();
        if (dissolve == null) dissolve = gameObject.AddComponent<GhostDissolveController>();
        dissolve.CollectRenderers();
        float elapsed = 0f;
        while (elapsed < dissolveSeconds && enemy.Health.IsDead)
        {
            elapsed += Time.unscaledDeltaTime;
            dissolve.SetDissolveAmount(1f - Mathf.SmoothStep(0f, 1f, elapsed / dissolveSeconds));
            yield return null;
        }
        routine = null;
        if (!enemy.Health.IsDead) { RestorePresentation(); yield break; }
        // Retain the object/health identity for world persistence and NGO. Every
        // peer with the replicated dead health performs the same local presentation.
        gameObject.SetActive(false);
    }

    private void PrepareDissolveMaterials()
    {
        Material template = Resources.Load<Material>("EnemyDeathDissolve");
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            Material[] materials = renderer.sharedMaterials;
            originalMaterials[renderer] = materials;
            var replacements = (Material[])materials.Clone();
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null || source.HasProperty("_DissolveAmount") || template == null) continue;
                var replacement = new Material(template);
                if (source.HasProperty("_BaseColor")) replacement.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                temporaryMaterials.Add(replacement);
                replacements[i] = replacement;
            }
            renderer.sharedMaterials = replacements;
        }
    }

    private void RestorePresentation()
    {
        if (dissolve != null) dissolve.SetDissolveAmount(1f);
        foreach (var entry in originalMaterials) if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
        originalMaterials.Clear();
        foreach (Material material in temporaryMaterials) if (material != null) Destroy(material);
        temporaryMaterials.Clear();
    }

    private void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        RestorePresentation();
    }
}
