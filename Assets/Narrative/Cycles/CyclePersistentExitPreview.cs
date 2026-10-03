using UnityEngine;

/// <summary>Dev-only presentation override. Persistent activation remains owned by CycleActivationId.</summary>
[RequireComponent(typeof(CycleActivationId))]
public sealed class CyclePersistentExitPreview : MonoBehaviour
{
    public string cycleId;
    public string resolvedStepId;
    private void LateUpdate()
    {
        var activation=GetComponent<CycleActivationId>();
        if(activation.target==null)return;
        foreach(var cycle in FindObjectsByType<CycleController>(FindObjectsInactive.Include))
            if(cycle.definition != null && cycle.definition.cycleId==cycleId && cycle.IsDevSimulationActive)
            { activation.target.SetActive(!cycle.IsSceneStepCompleted(resolvedStepId));return; }
    }
}
