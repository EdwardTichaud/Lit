using UnityEngine;

/// <summary>
/// Inspector helper for manually testing facial emotions.
/// </summary>
[DisallowMultipleComponent]
public class FacialExpressionDebugger : MonoBehaviour
{
    [SerializeField, Tooltip("Controller to drive. If empty, searches on this GameObject and its parents.")]
    private FacialExpressionController controller;

    private void Reset()
    {
        ResolveController();
    }

    private void OnValidate()
    {
        if (controller == null)
        {
            ResolveController();
        }
    }

    [ContextMenu("Test Fear")]
    public void TestFear()
    {
        Play(FacialEmotion.Fear);
    }

    [ContextMenu("Test Anger")]
    public void TestAnger()
    {
        Play(FacialEmotion.Anger);
    }

    [ContextMenu("Test Laugh")]
    public void TestLaugh()
    {
        Play(FacialEmotion.Laugh);
    }

    [ContextMenu("Test Surprise")]
    public void TestSurprise()
    {
        Play(FacialEmotion.Surprise);
    }

    [ContextMenu("Test Smirk")]
    public void TestSmirk()
    {
        Play(FacialEmotion.Smirk);
    }

    [ContextMenu("Test Suspicious")]
    public void TestSuspicious()
    {
        Play(FacialEmotion.Suspicious);
    }

    [ContextMenu("Test HalfSmile")]
    public void TestHalfSmile()
    {
        Play(FacialEmotion.HalfSmile);
    }

    [ContextMenu("Reset Idle")]
    public void ResetIdle()
    {
        FacialExpressionController resolvedController = ResolveController();
        if (resolvedController != null)
        {
            resolvedController.ReturnToIdle();
        }
    }

    [ContextMenu("Print Available BlendShapes")]
    public void PrintAvailableBlendShapes()
    {
        FacialExpressionController resolvedController = ResolveController();
        if (resolvedController != null)
        {
            resolvedController.PrintAvailableBlendShapes();
        }
    }

    [ContextMenu("Validate Presets")]
    public void ValidatePresets()
    {
        FacialExpressionController resolvedController = ResolveController();
        if (resolvedController != null)
        {
            resolvedController.ValidatePresets();
        }
    }

    private void Play(FacialEmotion emotion)
    {
        FacialExpressionController resolvedController = ResolveController();
        if (resolvedController == null)
        {
            return;
        }

        resolvedController.PlayEmotion(emotion);
    }

    private FacialExpressionController ResolveController()
    {
        if (controller != null)
        {
            return controller;
        }

        controller = GetComponent<FacialExpressionController>();
        if (controller != null)
        {
            return controller;
        }

        controller = GetComponentInParent<FacialExpressionController>();
        if (controller != null)
        {
            return controller;
        }

        controller = GetComponentInChildren<FacialExpressionController>();
        if (controller == null)
        {
            Debug.LogWarning("[Facial] FacialExpressionDebugger could not find a FacialExpressionController.", this);
        }

        return controller;
    }
}
