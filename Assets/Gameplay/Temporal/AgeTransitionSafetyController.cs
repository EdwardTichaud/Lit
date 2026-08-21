using System.Collections;
using UnityEngine;

/// <summary>
/// Transition locale breve apres une mise a jour d'Ancient Flame repliquee.
/// Elle bloque les entrees et les capacites pendant la reconstruction visuelle,
/// sans toucher a la position ni au regroupement des joueurs.
/// </summary>
[DisallowMultipleComponent]
public sealed class AgeTransitionSafetyController : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float inputLockDuration = 0.35f;
    private AgeManager ageManager;
    private Coroutine transitionRoutine;

    public static void EnsureFor(AgeManager manager)
    {
        if (manager != null && manager.GetComponent<AgeTransitionSafetyController>() == null)
        {
            manager.gameObject.AddComponent<AgeTransitionSafetyController>();
        }
    }

    private void OnEnable()
    {
        ageManager = GetComponent<AgeManager>();
        if (ageManager != null) ageManager.AgeChanged += OnAgeChanged;
    }

    private void OnDisable()
    {
        if (ageManager != null) ageManager.AgeChanged -= OnAgeChanged;
    }

    private void OnAgeChanged(AgeManager manager, int previousYear, int currentYear)
    {
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(ApplyTransition());
    }

    private IEnumerator ApplyTransition()
    {
        GameObject character = LocalPlayerUtils.GetControlledCharacter();
        SquadCharacterController controller = character != null ? character.GetComponentInParent<SquadCharacterController>() : null;
        LocalInputRouter.PushInteractionAndJumpSuppression(this);
        if (controller != null) controller.TryBeginUccExternalLock(true, true);

        // AgeManager a deja rafraichi les objets temporels, shaders et displays
        // avant cet evenement; laisser une frame couvre les observers tardifs.
        yield return null;
        yield return new WaitForSecondsRealtime(inputLockDuration);

        if (controller != null) controller.EndUccExternalLock();
        LocalInputRouter.PopInteractionAndJumpSuppression(this);
        transitionRoutine = null;
    }
}
