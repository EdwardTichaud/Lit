using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Replaces Timeline bindings authored against a scene preview actor with the
/// actual local player instantiated by Bootstrap at runtime.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
public sealed class LitTimelineLocalPlayerBinder : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField, Tooltip("Acteur utilise uniquement pour authorer et previsualiser les pistes dans cette scene.")]
    private Transform editorPreviewActor;
    [SerializeField, Tooltip("Masque l'acteur de previsualisation pendant le jeu : seul le joueur UCC runtime doit etre visible.")]
    private bool hidePreviewActorAtRuntime = true;
    [SerializeField, Tooltip("Regenere le graphe si le joueur arrive alors que la Timeline est deja en lecture.")]
    private bool rebuildPlayingGraph = true;

    private Transform boundPlayer;

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
    }

    private void Awake()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
    }

    private void OnEnable()
    {
        // The scene preview actor must remain the Timeline binding while
        // authoring. LocalPlayerContext belongs to the Bootstrap runtime and
        // may point to a different player object in edit mode.
        if (!Application.isPlaying)
        {
            return;
        }

        LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;
        SetPreviewActorRuntimeVisibility(false);
        BindLocalPlayer(LocalPlayerContext.LocalCharacterRoot);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            LocalPlayerContext.LocalCharacterChanged -= OnLocalCharacterChanged;
            SetPreviewActorRuntimeVisibility(true);
        }
    }

    /// <summary>Can be called from a Timeline signal immediately before Play.</summary>
    public void BindNow()
    {
        BindLocalPlayer(LocalPlayerContext.LocalCharacterRoot);
    }

    private void OnLocalCharacterChanged(Transform playerRoot)
    {
        BindLocalPlayer(playerRoot);
    }

    private void BindLocalPlayer(Transform playerRoot)
    {
        if (director == null || director.playableAsset == null || editorPreviewActor == null || playerRoot == null)
        {
            return;
        }

        bool changed = false;
        foreach (PlayableBinding output in director.playableAsset.outputs)
        {
            UnityEngine.Object previewBinding = director.GetGenericBinding(output.sourceObject);
            if (!IsBoundToPreviewActor(previewBinding))
            {
                continue;
            }

            UnityEngine.Object runtimeBinding = ResolveRuntimeBinding(previewBinding, playerRoot);
            if (runtimeBinding == null)
            {
                Debug.LogWarning($"[TimelinePlayerBinder] Binding '{output.streamName}' introuvable sur le joueur local.", this);
                continue;
            }

            director.SetGenericBinding(output.sourceObject, runtimeBinding);
            changed = true;
        }

        boundPlayer = playerRoot;
        if (changed && rebuildPlayingGraph && director.state == PlayState.Playing)
        {
            director.RebuildGraph();
        }
    }

    private void SetPreviewActorRuntimeVisibility(bool visible)
    {
        if (!hidePreviewActorAtRuntime || editorPreviewActor == null)
        {
            return;
        }

        // Never hide the real player if this component is accidentally
        // configured with it. The preview actor is an authoring-only stand-in.
        if (editorPreviewActor == LocalPlayerContext.LocalCharacterRoot)
        {
            return;
        }

        editorPreviewActor.gameObject.SetActive(visible);
    }

    private bool IsBoundToPreviewActor(UnityEngine.Object binding)
    {
        Transform transform = GetBindingTransform(binding);
        return transform != null && (transform == editorPreviewActor || transform.IsChildOf(editorPreviewActor));
    }

    private UnityEngine.Object ResolveRuntimeBinding(UnityEngine.Object previewBinding, Transform playerRoot)
    {
        if (previewBinding is GameObject)
        {
            return ResolveEquivalentTransform(GetBindingTransform(previewBinding), playerRoot)?.gameObject;
        }

        if (previewBinding is Component previewComponent)
        {
            Transform runtimeTransform = ResolveEquivalentTransform(previewComponent.transform, playerRoot);
            if (runtimeTransform != null)
            {
                Component runtimeComponent = runtimeTransform.GetComponent(previewComponent.GetType());
                if (runtimeComponent != null)
                {
                    return runtimeComponent;
                }
            }

            // Animation tracks normally bind an Animator. The local player's
            // visual hierarchy can differ slightly from the preview prefab.
            if (previewComponent is Animator)
            {
                return playerRoot.GetComponentInChildren<Animator>(true);
            }
        }

        return null;
    }

    private Transform ResolveEquivalentTransform(Transform previewTransform, Transform playerRoot)
    {
        if (previewTransform == null)
        {
            return null;
        }

        string path = GetRelativePath(editorPreviewActor, previewTransform);
        if (path == null)
        {
            return null;
        }

        return string.IsNullOrEmpty(path) ? playerRoot : playerRoot.Find(path);
    }

    private static Transform GetBindingTransform(UnityEngine.Object binding)
    {
        return binding switch
        {
            GameObject gameObject => gameObject.transform,
            Component component => component.transform,
            _ => null
        };
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (root == null || target == null || (target != root && !target.IsChildOf(root)))
        {
            return null;
        }

        if (target == root)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return current == root ? path : null;
    }
}
