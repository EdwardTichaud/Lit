using UnityEngine;

/// <summary>
/// Keeps a hierarchy of preview actors available for Timeline authoring in edit
/// mode while hiding every child at runtime.
/// </summary>
[DefaultExecutionOrder(-400)]
[DisallowMultipleComponent]
public sealed class LitTimelinePreviewActor : MonoBehaviour
{
    [SerializeField] private bool disableChildrenWhenPlaying = true;

    private void Awake()
    {
        if (!Application.isPlaying || !disableChildrenWhenPlaying)
        {
            return;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }
    }
}
