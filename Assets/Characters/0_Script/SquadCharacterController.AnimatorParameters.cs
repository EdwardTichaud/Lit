using System;
using UnityEngine;

public partial class SquadCharacterController
{
    private readonly AnimatorParameterLookup animatorParameterLookup = new AnimatorParameterLookup();
    private void SetAnimatorBoolIfValid(string parameterName, bool value)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterName, value);
        }
    }

    private void SetAnimatorFloatIfValid(string parameterName, float value)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(parameterName, value);
        }
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        return animatorParameterLookup.Has(animator, parameterName, expectedType);
    }
}
