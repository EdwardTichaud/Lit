using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class LitInfluenceReceiver : MonoBehaviour, ILitInfluenceReceiver
{
    [SerializeField, Tooltip("Reagit aux braseros allumes.")]
    private bool reactToBraseros = true;
    [SerializeField, Tooltip("Reagit aux torches allumees.")]
    private bool reactToTorches = true;
    [SerializeField, Tooltip("Ecrit un log quand l'influence change.")]
    private bool logInfluenceChanges;

    [Header("Events")]
    [SerializeField] private UnityEvent onFirstLitInfluenceEnter = new UnityEvent();
    [SerializeField] private UnityEvent onLastLitInfluenceExit = new UnityEvent();
    [SerializeField] private UnityEvent onLitInfluenceStay = new UnityEvent();
    [SerializeField] private UnityEvent onLitInfluenceChanged = new UnityEvent();

    private readonly HashSet<int> activeSourceIds = new HashSet<int>();

    public bool HasLitInfluence => activeSourceIds.Count > 0;
    public int ActiveLitInfluenceCount => activeSourceIds.Count;

    public void OnLitInfluenceEnter(LitInfluenceInfo info)
    {
        if (!ShouldReactTo(info))
        {
            return;
        }

        bool wasInfluenced = HasLitInfluence;
        activeSourceIds.Add(info.SourceId);

        if (!wasInfluenced && HasLitInfluence)
        {
            onFirstLitInfluenceEnter.Invoke();
        }

        onLitInfluenceChanged.Invoke();
        LogInfluence("enter", info);
    }

    public void OnLitInfluenceStay(LitInfluenceInfo info)
    {
        if (!ShouldReactTo(info))
        {
            return;
        }

        onLitInfluenceStay.Invoke();
    }

    public void OnLitInfluenceExit(LitInfluenceInfo info)
    {
        bool wasInfluenced = HasLitInfluence;
        bool removed = activeSourceIds.Remove(info.SourceId);
        if (!removed)
        {
            return;
        }

        if (wasInfluenced && !HasLitInfluence)
        {
            onLastLitInfluenceExit.Invoke();
        }

        onLitInfluenceChanged.Invoke();
        LogInfluence("exit", info);
    }

    private bool ShouldReactTo(LitInfluenceInfo info)
    {
        switch (info.SourceKind)
        {
            case LitInfluenceSourceKind.Brasero:
                return reactToBraseros;

            case LitInfluenceSourceKind.Torch:
                return reactToTorches;

            default:
                return false;
        }
    }

    private void LogInfluence(string phase, LitInfluenceInfo info)
    {
        if (!logInfluenceChanges)
        {
            return;
        }

        string sourceName = info.SourceObject != null ? info.SourceObject.name : "<missing>";
        Debug.Log(
            $"[LitInfluence] {phase} receiver='{name}' source='{sourceName}' kind={info.SourceKind} activeSources={activeSourceIds.Count}",
            this);
    }
}
