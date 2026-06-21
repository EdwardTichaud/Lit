using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data asset describing one reusable facial expression.
/// </summary>
[CreateAssetMenu(fileName = "FacialExpressionPreset", menuName = "Scriptable Objects/Facial/Expression Preset")]
public class FacialExpressionPreset : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Gameplay key used to trigger this preset.")]
    public FacialEmotion emotion = FacialEmotion.Idle;

    [Tooltip("Passive expressions stay active. Active expressions play once and return.")]
    public FacialExpressionMode mode = FacialExpressionMode.PassiveLoop;

    [Header("Timing")]
    [Min(0f), Tooltip("Blend-in duration in seconds.")]
    public float fadeInDuration = 0.15f;

    [Min(0f), Tooltip("Hold duration for ActiveOneShot expressions.")]
    public float holdDuration = 0.4f;

    [Min(0f), Tooltip("Blend-out duration for ActiveOneShot expressions.")]
    public float fadeOutDuration = 0.2f;

    [Header("Return")]
    [Tooltip("When an ActiveOneShot finishes, return to the current passive expression instead of Idle.")]
    public bool returnToPreviousPassiveExpression = true;

    [Header("BlendShapes")]
    [Tooltip("Target BlendShape weights for this expression.")]
    public List<FacialBlendShapeWeight> blendShapes = new List<FacialBlendShapeWeight>();

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        holdDuration = Mathf.Max(0f, holdDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);

        if (blendShapes == null)
        {
            blendShapes = new List<FacialBlendShapeWeight>();
            return;
        }

        for (int i = 0; i < blendShapes.Count; i++)
        {
            if (blendShapes[i] == null)
            {
                blendShapes[i] = new FacialBlendShapeWeight();
                continue;
            }

            blendShapes[i].weight = Mathf.Clamp(blendShapes[i].weight, 0f, 100f);
        }
    }
}
