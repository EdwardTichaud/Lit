using Opsive.UltimateCharacterController.ThirdPersonController.Camera.ViewTypes;
using UnityEngine;

/// <summary>
/// UCC-owned third-person view used while a combat target is locked.
/// UCC invokes this view to write the camera Transform, preserving its collision solver.
/// </summary>
[System.Serializable]
public sealed class CombatLockAdventureViewType : Adventure
{
    [SerializeField, Range(1f, 720f)] private float maximumLockRotationDegreesPerSecond = 180f;

    public void CopyGameplaySettingsFrom(ThirdPerson source)
    {
        FieldOfView = source.FieldOfView;
        FieldOfViewDamping = source.FieldOfViewDamping;
        ForwardAxis = source.ForwardAxis;
        LookOffset = source.LookOffset;
        LookOffsetSmoothing = source.LookOffsetSmoothing;
        CollisionRadius = source.CollisionRadius;
        CollisionAnchorOffset = source.CollisionAnchorOffset;
        RotationSpeed = source.RotationSpeed;
        SecondaryRotationSpeed = source.SecondaryRotationSpeed;
        HorizontalPivotFreedom = source.HorizontalPivotFreedom;
        PitchLimit = source.PitchLimit;
    }

    public override Quaternion LateRotate(bool immediateUpdate)
    {
        Quaternion fallbackRotation = base.LateRotate(immediateUpdate);
        CombatLockUccCameraAdapter adapter = m_GameObject.GetComponent<CombatLockUccCameraAdapter>();
        if (adapter == null || !adapter.TryGetLookPoint(out Vector3 lookPoint))
        {
            return fallbackRotation;
        }

        Vector3 direction = lookPoint - m_Transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return fallbackRotation;
        }

        Vector3 up = m_CharacterLocomotion != null ? m_CharacterLocomotion.Up : Vector3.up;
        Quaternion targetRotation = Quaternion.LookRotation(direction, up);
        if (immediateUpdate)
        {
            return targetRotation;
        }

        return Quaternion.RotateTowards(
            fallbackRotation,
            targetRotation,
            maximumLockRotationDegreesPerSecond * Time.deltaTime);
    }
}
