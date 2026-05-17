// Role:
// Allows a scene object to change visual/interactive state depending on a temporal age.
// Usage:
// Attach to props, doors, traces, meshes, or readable objects that should vary by
// TemporalZone age or by a local TemporalTorch reveal.
// Responsibilities:
// Match configured TemporalState ranges, switch roots/meshes/materials, and enable
// or disable colliders/behaviours without requiring a custom script per prop.
// Dependencies:
// TemporalAgeUtility, TemporalZone, TemporalTorch, HumanModificationTag.
// Precautions:
// This component can activate/deactivate scene objects. Test each configured state
// in Play Mode after changing ranges or references.
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One age range and its optional visual/interaction overrides for a TemporalObject.
/// </summary>
[Serializable]
public class TemporalState
{
    [Header("Age Range")]
    /// <summary>First age where this state is active.</summary>
    public TemporalAge minimumAge = TemporalAge.Age000;
    /// <summary>Last age where this state is active.</summary>
    public TemporalAge maximumAge = TemporalAge.Age666;

    [Header("Activation")]
    /// <summary>Root GameObject enabled only when this state is active.</summary>
    [Tooltip("Racine activee quand cette plage temporelle matche.")]
    public GameObject stateRoot;

    [Header("Visual Overrides")]
    /// <summary>MeshFilter that receives the state mesh when active.</summary>
    public MeshFilter meshTarget;
    /// <summary>Mesh applied to meshTarget when this state is active.</summary>
    public Mesh mesh;
    /// <summary>Renderer that receives the state materials when active.</summary>
    public Renderer rendererTarget;
    /// <summary>Materials applied to rendererTarget when this state is active.</summary>
    public Material[] materials;

    [Header("Interaction Overrides")]
    /// <summary>If true, this state controls the listed colliders.</summary>
    public bool driveColliders;
    /// <summary>Collider enabled value while this state is active.</summary>
    public bool collidersEnabledWhenActive = true;
    /// <summary>Colliders controlled by this state.</summary>
    public Collider[] colliders;
    /// <summary>If true, this state controls the listed behaviours.</summary>
    public bool driveBehaviours;
    /// <summary>Behaviour enabled value while this state is active.</summary>
    public bool behavioursEnabledWhenActive = true;
    /// <summary>Behaviours controlled by this state.</summary>
    public Behaviour[] behaviours;

    /// <summary>Designer note explaining the human or architectural change.</summary>
    [TextArea]
    public string narrativeNote;

    /// <summary>
    /// Returns true if the provided age is inside this state's inclusive range.
    /// </summary>
    public bool Matches(TemporalAge age)
    {
        int value = TemporalAgeUtility.AgeToInt(age);
        int min = TemporalAgeUtility.AgeToInt(minimumAge);
        int max = TemporalAgeUtility.AgeToInt(maximumAge);
        return value >= min && value <= max;
    }
}

/// <summary>
/// Applies temporal states to a scene object based on zone age, torch age, or manual age.
/// </summary>
[DisallowMultipleComponent]
public class TemporalObject : MonoBehaviour
{
    [Header("Age Source")]
    /// <summary>Dominant zone age source.</summary>
    [SerializeField] private TemporalZone zone;
    /// <summary>If true, searches parent objects for a TemporalZone.</summary>
    [SerializeField] private bool autoFindZoneInParents = true;
    /// <summary>Optional local torch age source.</summary>
    [SerializeField] private TemporalTorch localTorch;
    /// <summary>If true, local torch age overrides zone age.</summary>
    [SerializeField, Tooltip("Si une torche locale est renseignee, elle prime sur l'age dominant.")]
    private bool preferLocalTorch = true;
    /// <summary>If true, ignores zone and torch and uses manualAge.</summary>
    [SerializeField] private bool useManualAge;
    /// <summary>Manual fallback age used for tests or isolated objects.</summary>
    [SerializeField] private TemporalAge manualAge = TemporalAge.Age666;

    [Header("States")]
    /// <summary>Configured states applied according to the resolved age.</summary>
    [SerializeField] private List<TemporalState> states = new List<TemporalState>();

    [Header("Narrative Tags")]
    /// <summary>Human modifications represented by this object.</summary>
    [SerializeField] private List<HumanModificationTag> humanModifications = new List<HumanModificationTag>();
    /// <summary>Freeform designer note for narrative intent.</summary>
    [SerializeField, TextArea] private string narrativeNote;

    /// <summary>Dominant zone currently connected to this object.</summary>
    public TemporalZone Zone => zone;
    /// <summary>Local torch currently connected to this object.</summary>
    public TemporalTorch LocalTorch => localTorch;
    /// <summary>Temporal states configured on this object.</summary>
    public IReadOnlyList<TemporalState> States => states;
    /// <summary>Human modification tags attached to this object.</summary>
    public IReadOnlyList<HumanModificationTag> HumanModifications => humanModifications;
    /// <summary>Freeform narrative note for designers.</summary>
    public string NarrativeNote => narrativeNote;

    private void OnEnable()
    {
        // Unity calls OnEnable when the component becomes active.
        // Register with the zone so zone changes can update this object automatically.
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
        // Undo registration and event subscriptions before Unity disables/destroys the object.
        if (zone != null)
        {
            zone.UnregisterObject(this);
        }

        Unsubscribe();
    }

    private void OnValidate()
    {
        // Editor safety: prevent impossible ranges while designers edit the inspector.
        NormalizeStateRanges();
    }

    /// <summary>
    /// Resolves the active age from manual, torch, or zone sources and applies it.
    /// </summary>
    public void ApplyResolvedAge()
    {
        ApplyAge(ResolveAge());
    }

    /// <summary>
    /// Applies all matching/non-matching state changes for the provided age.
    /// </summary>
    public void ApplyAge(TemporalAge age)
    {
        if (states == null || states.Count == 0)
        {
            return;
        }

        for (int i = 0; i < states.Count; i++)
        {
            // Multiple states may match; this allows layered roots or separate collider rules.
            TemporalState state = states[i];
            if (state == null)
            {
                continue;
            }

            bool active = state.Matches(age);
            ApplyState(state, active);
        }
    }

    /// <summary>
    /// Chooses the age source in priority order: manual, local torch, zone, fallback.
    /// </summary>
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
        // stateRoot is the cheapest way to swap whole prop variants.
        if (state.stateRoot != null && state.stateRoot.activeSelf != active)
        {
            state.stateRoot.SetActive(active);
        }

        if (!active)
        {
            // Inactive states should not keep hidden colliders or behaviours alive.
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
        // If a local torch is preferred, zone changes are ignored until the torch is absent.
        if (!useManualAge && (!preferLocalTorch || localTorch == null))
        {
            ApplyAge(current);
        }
    }

    private void OnTorchTargetAgeChanged(TemporalTorch torch, TemporalAge targetAge)
    {
        // The torch only drives this object when local reveal has priority.
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
