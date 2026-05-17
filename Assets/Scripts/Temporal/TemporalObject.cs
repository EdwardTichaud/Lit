using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TemporalState
{
    [Header("Age Range")]
    public TemporalAge minimumAge = TemporalAge.Age000;
    public TemporalAge maximumAge = TemporalAge.Age666;

    [Header("Activation")]
    [Tooltip("Racine activee quand cette plage temporelle matche.")]
    public GameObject stateRoot;

    [Header("Visual Overrides")]
    public MeshFilter meshTarget;
    public Mesh mesh;
    public Renderer rendererTarget;
    public Material[] materials;

    [Header("Interaction Overrides")]
    public bool driveColliders;
    public bool collidersEnabledWhenActive = true;
    public Collider[] colliders;
    public bool driveBehaviours;
    public bool behavioursEnabledWhenActive = true;
    public Behaviour[] behaviours;

    [TextArea]
    public string narrativeNote;

    public bool Matches(TemporalAge age)
    {
        int value = TemporalAgeUtility.AgeToInt(age);
        int min = TemporalAgeUtility.AgeToInt(minimumAge);
        int max = TemporalAgeUtility.AgeToInt(maximumAge);
        return value >= min && value <= max;
    }
}

[DisallowMultipleComponent]
public class TemporalObject : MonoBehaviour
{
    [Header("Age Source")]
    [SerializeField] private TemporalZone zone;
    [SerializeField] private bool autoFindZoneInParents = true;
    [SerializeField] private TemporalTorch localTorch;
    [SerializeField, Tooltip("Si une torche locale est renseignee, elle prime sur l'age dominant.")]
    private bool preferLocalTorch = true;
    [SerializeField] private bool useManualAge;
    [SerializeField] private TemporalAge manualAge = TemporalAge.Age666;

    [Header("States")]
    [SerializeField] private List<TemporalState> states = new List<TemporalState>();

    [Header("Narrative Tags")]
    [SerializeField] private List<HumanModificationTag> humanModifications = new List<HumanModificationTag>();
    [SerializeField, TextArea] private string narrativeNote;

    public TemporalZone Zone => zone;
    public TemporalTorch LocalTorch => localTorch;
    public IReadOnlyList<TemporalState> States => states;
    public IReadOnlyList<HumanModificationTag> HumanModifications => humanModifications;
    public string NarrativeNote => narrativeNote;

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();

        if (zone != null)
        {
            zone.RegisterObject(this);
        }

        ApplyResolvedAge();
    }

    private void OnDisable()
    {
        if (zone != null)
        {
            zone.UnregisterObject(this);
        }

        Unsubscribe();
    }

    private void OnValidate()
    {
        NormalizeStateRanges();
    }

    public void ApplyResolvedAge()
    {
        ApplyAge(ResolveAge());
    }

    public void ApplyAge(TemporalAge age)
    {
        if (states == null || states.Count == 0)
        {
            return;
        }

        for (int i = 0; i < states.Count; i++)
        {
            TemporalState state = states[i];
            if (state == null)
            {
                continue;
            }

            bool active = state.Matches(age);
            ApplyState(state, active);
        }
    }

    public TemporalAge ResolveAge()
    {
        if (useManualAge)
        {
            return manualAge;
        }

        if (preferLocalTorch && localTorch != null)
        {
            return localTorch.TargetAge;
        }

        if (zone != null)
        {
            return zone.CurrentAge;
        }

        return manualAge;
    }

    private void ApplyState(TemporalState state, bool active)
    {
        if (state.stateRoot != null && state.stateRoot.activeSelf != active)
        {
            state.stateRoot.SetActive(active);
        }

        if (!active)
        {
            ApplyColliderState(state, false);
            ApplyBehaviourState(state, false);
            return;
        }

        if (state.meshTarget != null && state.mesh != null)
        {
            state.meshTarget.sharedMesh = state.mesh;
        }

        if (state.rendererTarget != null && state.materials != null && state.materials.Length > 0)
        {
            state.rendererTarget.sharedMaterials = state.materials;
        }

        ApplyColliderState(state, state.collidersEnabledWhenActive);
        ApplyBehaviourState(state, state.behavioursEnabledWhenActive);
    }

    private static void ApplyColliderState(TemporalState state, bool active)
    {
        if (!state.driveColliders || state.colliders == null)
        {
            return;
        }

        for (int i = 0; i < state.colliders.Length; i++)
        {
            Collider collider = state.colliders[i];
            if (collider != null)
            {
                collider.enabled = active;
            }
        }
    }

    private static void ApplyBehaviourState(TemporalState state, bool active)
    {
        if (!state.driveBehaviours || state.behaviours == null)
        {
            return;
        }

        for (int i = 0; i < state.behaviours.Length; i++)
        {
            Behaviour behaviour = state.behaviours[i];
            if (behaviour != null)
            {
                behaviour.enabled = active;
            }
        }
    }

    private void ResolveReferences()
    {
        if (zone == null && autoFindZoneInParents)
        {
            zone = GetComponentInParent<TemporalZone>(true);
        }

        if (localTorch == null)
        {
            localTorch = GetComponentInParent<TemporalTorch>(true);
        }
    }

    private void Subscribe()
    {
        if (zone != null)
        {
            zone.AgeChanged += OnZoneAgeChanged;
        }

        if (localTorch != null)
        {
            localTorch.TargetAgeChanged += OnTorchTargetAgeChanged;
        }
    }

    private void Unsubscribe()
    {
        if (zone != null)
        {
            zone.AgeChanged -= OnZoneAgeChanged;
        }

        if (localTorch != null)
        {
            localTorch.TargetAgeChanged -= OnTorchTargetAgeChanged;
        }
    }

    private void OnZoneAgeChanged(TemporalZone temporalZone, TemporalAge previous, TemporalAge current)
    {
        if (!useManualAge && (!preferLocalTorch || localTorch == null))
        {
            ApplyAge(current);
        }
    }

    private void OnTorchTargetAgeChanged(TemporalTorch torch, TemporalAge targetAge)
    {
        if (!useManualAge && preferLocalTorch)
        {
            ApplyAge(targetAge);
        }
    }

    private void NormalizeStateRanges()
    {
        if (states == null)
        {
            return;
        }

        for (int i = 0; i < states.Count; i++)
        {
            TemporalState state = states[i];
            if (state == null)
            {
                continue;
            }

            if (TemporalAgeUtility.AgeToInt(state.maximumAge) < TemporalAgeUtility.AgeToInt(state.minimumAge))
            {
                state.maximumAge = state.minimumAge;
            }
        }
    }
}
