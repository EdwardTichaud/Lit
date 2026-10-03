using UnityEngine;

/// <summary>Persistent environment adapter, with a local Dev preview that never mutates world variables.</summary>
[RequireComponent(typeof(CycleActivationId))]
public sealed class EtienneExitGate : MonoBehaviour
{
    private CycleActivationId activation;
    private void Awake() => activation = GetComponent<CycleActivationId>();
    private void LateUpdate()
    {
        if (activation == null || activation.target == null) return;
        foreach (CycleController cycle in FindObjectsByType<CycleController>(FindObjectsSortMode.None))
        {
            if (cycle.definition == null || cycle.definition.cycleId != "district1.etienne" || !cycle.IsDevSimulationActive) continue;
            activation.target.SetActive(!cycle.IsSceneStepCompleted("etienne_spoken"));
            return;
        }
    }
}
