using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Fait evaluer les Volumes HDRP locaux depuis le personnage controle plutot
/// que depuis la camera. Une camera a la troisieme personne ne peut ainsi pas
/// faire entrer visuellement le joueur dans une zone avant lui.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class LocalVolumeAnchor : MonoBehaviour
{
    [SerializeField, Tooltip("Ancre explicite. Laisser vide pour suivre le personnage controle.")]
    private Transform anchor;

    [SerializeField, Tooltip("Met automatiquement a jour l'ancre quand le personnage controle change.")]
    private bool useControlledCharacter = true;

    private Camera resolvedCamera;
    private HDAdditionalCameraData resolvedCameraData;
    private Transform previousVolumeAnchor;
    private bool ownsVolumeAnchor;

    private void LateUpdate()
    {
        if (useControlledCharacter)
        {
            GameObject controlledCharacter = LocalPlayerUtils.GetControlledCharacter();
            anchor = controlledCharacter != null ? controlledCharacter.transform : null;
        }

        if (anchor == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        if (resolvedCamera != mainCamera || resolvedCameraData == null)
        {
            RestorePreviousAnchor();
            resolvedCamera = mainCamera;
            resolvedCameraData = mainCamera.GetComponent<HDAdditionalCameraData>();
        }

        if (resolvedCameraData != null && resolvedCameraData.volumeAnchorOverride != anchor)
        {
            if (!ownsVolumeAnchor)
            {
                previousVolumeAnchor = resolvedCameraData.volumeAnchorOverride;
                ownsVolumeAnchor = true;
            }

            resolvedCameraData.volumeAnchorOverride = anchor;
        }
    }

    private void OnDisable()
    {
        RestorePreviousAnchor();
    }

    private void RestorePreviousAnchor()
    {
        if (resolvedCameraData != null && ownsVolumeAnchor &&
            resolvedCameraData.volumeAnchorOverride == anchor)
        {
            resolvedCameraData.volumeAnchorOverride = previousVolumeAnchor;
        }

        previousVolumeAnchor = null;
        ownsVolumeAnchor = false;
    }
}
