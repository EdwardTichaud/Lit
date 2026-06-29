using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Timeline;

public class CameraMotifSignalReceiver : MonoBehaviour
{
    [Header("Signal Binding")]
    [SerializeField] private SignalAsset setCameraMotifSignal;
    [SerializeField] private SignalAsset lockCameraMotifSignal;
    [SerializeField] private SignalAsset unlockCameraMotifSignal;
    [SerializeField] private CameraMotifSO cameraMotif;

    private SignalReceiver signalReceiver;
    private readonly HashSet<SignalAsset> registeredSignals = new();

    private void Awake()
    {
        EnsureSignalReceiver();
        RegisterSignals();
    }

    private void OnEnable()
    {
        EnsureSignalReceiver();
        RegisterSignals();
    }

    public void SetMotif(CameraMotifSO motif)
    {
        cameraMotif = motif;
    }

    public void SIG_SetCameraMotif()
    {
        if (cameraMotif == null)
        {
            Debug.LogWarning("[CameraMotifSignalReceiver] No CameraMotifSO assigned.");
            return;
        }

        BattleCameraManager.Instance?.SetCameraMotif(cameraMotif);
    }

    public void SIG_LockCameraMotif()
    {
        if (cameraMotif == null)
        {
            Debug.LogWarning("[CameraMotifSignalReceiver] No CameraMotifSO assigned.");
            return;
        }

        BattleCameraManager.Instance?.LockCameraMotif(cameraMotif);
    }

    public void SIG_UnlockCameraMotif()
    {
        BattleCameraManager.Instance?.UnlockCameraMotif(cameraMotif);
    }

    private void EnsureSignalReceiver()
    {
        if (signalReceiver != null)
            return;

        signalReceiver = GetComponent<SignalReceiver>();
        if (signalReceiver == null)
            signalReceiver = gameObject.AddComponent<SignalReceiver>();
    }

    private void RegisterSignals()
    {
        RegisterSignal(setCameraMotifSignal, SIG_SetCameraMotif);
        RegisterSignal(lockCameraMotifSignal, SIG_LockCameraMotif);
        RegisterSignal(unlockCameraMotifSignal, SIG_UnlockCameraMotif);
    }

    private void RegisterSignal(SignalAsset signal, UnityAction reaction)
    {
        if (signalReceiver == null || signal == null)
            return;

        if (registeredSignals.Contains(signal))
            return;

        var existing = signalReceiver.GetReaction(signal);
        if (existing != null)
        {
            existing.AddListener(reaction);
            registeredSignals.Add(signal);
            return;
        }

        var evt = new UnityEvent();
        evt.AddListener(reaction);
        signalReceiver.AddReaction(signal, evt);
        registeredSignals.Add(signal);
    }
}
