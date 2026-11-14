using UnityEngine;

public sealed class EnolaFractureTravel : MonoBehaviour
{
    public ZoneManifest destination;
    public Vector3 arrivalPosition;
    public float arrivalYaw;
    public string returnSpawnId = "EnolaFractureEntrance";
    public void Enter(SquadCharacterController actor)
    {
        if (actor != null && !GameFlowService.TravelToZone(destination,
            new[] { new Pose(arrivalPosition, Quaternion.Euler(0, arrivalYaw, 0)) }))
            InfoBoxUI.TryShowToast("La fracture n'est pas accessible pour le moment.");
    }
    public void Leave(SquadCharacterController actor)
    {
        if (actor != null && !GameFlowService.ReturnToHub(returnSpawnId))
            InfoBoxUI.TryShowToast("Le retour à la Maison n'est pas disponible pour le moment.");
    }
}
