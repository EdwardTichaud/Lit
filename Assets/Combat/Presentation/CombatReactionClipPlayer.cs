using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Lit un clip de reaction combat directement sur un Animator, sans exiger de state dans son controller.
public sealed class CombatReactionClipPlayer : MonoBehaviour
{
    private PlayableGraph graph;
    private Coroutine playRoutine;

    public static float Play(Animator animator, AnimationClip clip)
    {
        if (animator == null || !animator.isActiveAndEnabled || clip == null)
        {
            return 0f;
        }

        CombatReactionClipPlayer player = animator.GetComponent<CombatReactionClipPlayer>();
        if (player == null)
        {
            player = animator.gameObject.AddComponent<CombatReactionClipPlayer>();
        }

        player.PlayClip(animator, clip);
        return Mathf.Max(0.05f, clip.length);
    }

    private void OnDisable()
    {
        StopClip();
    }

    private void OnDestroy()
    {
        StopClip();
    }

    private void PlayClip(Animator animator, AnimationClip clip)
    {
        StopClip();

        graph = PlayableGraph.Create($"{nameof(CombatReactionClipPlayer)}_{clip.name}");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);
        clipPlayable.SetTime(0d);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "CombatReactionClip", animator);
        output.SetSourcePlayable(clipPlayable);

        graph.Play();
        playRoutine = StartCoroutine(PlayRoutine(Mathf.Max(0.05f, clip.length)));
    }

    private IEnumerator PlayRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && graph.IsValid())
        {
            float deltaTime = Mathf.Max(0f, TimeManager.GetCombatPresentationDeltaTime());
            if (deltaTime > 0f)
            {
                graph.Evaluate(deltaTime);
                elapsed += deltaTime;
            }

            yield return null;
        }

        playRoutine = null;
        DestroyGraph();
    }

    private void StopClip()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        DestroyGraph();
    }

    private void DestroyGraph()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }
}
