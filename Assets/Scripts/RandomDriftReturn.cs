using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomDriftReturn : MonoBehaviour
{
    public enum ModeSelection
    {
        ChildrenOnly,  // seulement les enfants directs
        Descendants,   // toute la descendance (limitable par profondeurMax)
        GameObject     // le GameObject qui porte ce composant
    }

    public enum AutoTriggerBoundsSource
    {
        RenderersThenColliders,
        RenderersOnly,
        CollidersOnly
    }

    [Header("Sélection des cibles")]
    [Tooltip("Choix de la portée: ce GameObject, enfants directs ou toute la descendance.")]
    public ModeSelection modeDeSelection = ModeSelection.ChildrenOnly;

    [Tooltip("Profondeur max quand 'Descendants' est choisi. 1 = enfants directs, 2 = petits-enfants, etc. 0 = illimité.")]
    [Min(0)] public int profondeurMax = 0;

    [Header("Joueur")]
    [Tooltip("Si non renseigné, recherche par tag 'Player'.")]
    public Transform joueur;

    [Tooltip("Distance où le joueur est considéré 'proche' (déclenche le retour).")]
    public float rayonProche = 3f;

    [Tooltip("Distance où le joueur est considéré 'loin' (autorise la dérive). Doit être > rayonProche.")]
    public float rayonLoin = 4f;

    [Header("Personnages / Flamme")]
    [Tooltip("Utilise les personnages de la squad plutot qu'un simple Transform joueur.")]
    public bool detecterPersonnages = true;

    [Tooltip("Si true, le retour n'est autorise que si le personnage detecte porte une flamme valide.")]
    public bool requireMatchingFlame = true;

    [Tooltip("Affiche dans la console pourquoi un personnage est accepte/refuse.")]
    public bool debugFlameDetection;

    [Tooltip("Intervalle minimum entre deux logs debug.")]
    [Min(0.1f)] public float debugLogInterval = 1f;

    [Tooltip("Utilise le collider trigger du GameObject parent qui porte ce composant comme zone de maintien/retour, en plus des rayons par morceau.")]
    public bool useTriggerAreaForReturn = true;

    [Header("Zone trigger automatique")]
    [Tooltip("Ajoute automatiquement un BoxCollider trigger sur le parent si aucun trigger n'existe deja sur ce GameObject.")]
    public bool autoCreateParentTrigger = true;

    [Tooltip("Redimensionne automatiquement le BoxCollider trigger du parent autour des enfants.")]
    public bool autoResizeParentTrigger = true;

    [Tooltip("Autorise aussi le redimensionnement d'un trigger deja place manuellement sur le parent.")]
    public bool autoResizeExistingParentTrigger = false;

    [Tooltip("Type de bounds utilise pour dimensionner automatiquement le trigger.")]
    public AutoTriggerBoundsSource autoTriggerBoundsSource = AutoTriggerBoundsSource.RenderersThenColliders;

    [Tooltip("Inclut les enfants inactifs dans le calcul de la zone automatique.")]
    public bool includeInactiveChildrenInAutoTrigger = true;

    [Tooltip("Marge ajoutee autour des bounds calculees, en espace local du parent.")]
    public Vector3 autoTriggerPadding = new Vector3(2f, 2f, 2f);

    [Tooltip("Taille minimale du trigger automatique sur chaque axe.")]
    [Min(0.01f)] public float minAutoTriggerSize = 0.5f;

    [Header("Dérive")]
    [Tooltip("Vitesse de dérive (m/s) quand le joueur est loin.")]
    public float driftSpeed = 0.2f;

    [Tooltip("Distance max de dérive depuis la position d'origine. Mettre 50 pour ton cas.")]
    public float maxDriftDistance = 50f;

    [Header("Retour")]
    [Tooltip("Durée du retour (s) quand le joueur est proche. Mettre 0.5 pour ton cas.")]
    public float dureeRetour = 0.5f;

    [Header("Rotation (optionnel)")]
    [Tooltip("Tournis léger (°/s) pour l'effet d'espace. 0 pour désactiver.")]
    public float rotationResiduelleDegPerSec = 10f;

    [Tooltip("Si true, les objets continuent de tourner même à l'origine. Sinon ils restent immobiles à l'origine.")]
    public bool rotateInPlace = true;

    private enum Etat { Derive, ArretMax, Retour, IdleAOrigine }

    private class Morceau
    {
        public Transform tr;
        public Vector3 posOrigine;
        public Quaternion rotOrigine;

        public Vector3 directionFixe;   // Fixée une fois pour toutes
        public Vector3 rotAxis;         // Axe de tournis

        public Etat etat;
        public Coroutine routine;
    }

    private readonly List<Morceau> morceaux = new List<Morceau>();
    private readonly List<SquadCharacterController> characterCandidates = new List<SquadCharacterController>();
    private readonly Dictionary<SquadCharacterController, int> triggerCharacterOverlapCounts = new Dictionary<SquadCharacterController, int>();
    private readonly List<SquadCharacterController> triggerCharactersScratch = new List<SquadCharacterController>();
    private BoxCollider autoParentTrigger;
    private float nextDebugLogTime;
    private int characterCandidatesFrame = -1;

    void OnValidate()
    {
        if (rayonLoin < rayonProche) rayonLoin = rayonProche + 0.5f;
        if (maxDriftDistance < 0f) maxDriftDistance = 0f;
        if (dureeRetour < 0f) dureeRetour = 0f;
        if (driftSpeed < 0f) driftSpeed = 0f;
        if (rotationResiduelleDegPerSec < 0f) rotationResiduelleDegPerSec = 0f;
        if (debugLogInterval < 0.1f) debugLogInterval = 0.1f;
        autoTriggerPadding.x = Mathf.Max(0f, autoTriggerPadding.x);
        autoTriggerPadding.y = Mathf.Max(0f, autoTriggerPadding.y);
        autoTriggerPadding.z = Mathf.Max(0f, autoTriggerPadding.z);
        minAutoTriggerSize = Mathf.Max(0.01f, minAutoTriggerSize);
        if (modeDeSelection == ModeSelection.ChildrenOnly) profondeurMax = 1; // cohérent
    }

    void Start()
    {
        ConfigureParentTrigger();
        ReconstruireMorceaux();
    }

    void Update()
    {
        // Trouve le joueur si besoin
        EnsureFallbackPlayer();
        bool matchingTriggerCharacter = useTriggerAreaForReturn && HasMatchingCharacterInTrigger(out _);

        foreach (var m in morceaux)
        {
            if (matchingTriggerCharacter)
            {
                RetournerOuMaintenirMorceau(m);
                continue;
            }

            // CHANGEMENT: on autorise la rotation aussi pendant le retour
            bool canRotate =
                (m.etat == Etat.Derive || m.etat == Etat.ArretMax || m.etat == Etat.Retour) ||
                (rotateInPlace && m.etat == Etat.IdleAOrigine);

            if (canRotate && rotationResiduelleDegPerSec > 0f)
                m.tr.Rotate(m.rotAxis, rotationResiduelleDegPerSec * Time.deltaTime, Space.Self);

            bool matchingNear = HasMatchingCharacterInRange(m.posOrigine, rayonProche, out _);
            bool matchingNotFar = HasMatchingCharacterInRange(m.posOrigine, rayonLoin, out _);

            switch (m.etat)
            {
                case Etat.Derive:
                    if (matchingNear)
                    {
                        BasculerRoutine(m, RoutineRetour(m));
                        break;
                    }
                    m.tr.position += m.directionFixe * driftSpeed * Time.deltaTime;

                    Vector3 offset = m.tr.position - m.posOrigine;
                    float dist = offset.magnitude;
                    if (dist >= maxDriftDistance)
                    {
                        m.tr.position = m.posOrigine + m.directionFixe * maxDriftDistance;
                        m.etat = Etat.ArretMax;
                    }
                    break;

                case Etat.ArretMax:
                    if (matchingNear)
                        BasculerRoutine(m, RoutineRetour(m));
                    break;

                case Etat.IdleAOrigine:
                    if (!matchingNotFar)
                        m.etat = Etat.Derive; // repart en dérive (même direction)
                    break;

                case Etat.Retour:
                    // géré par la coroutine (position), la rotation continue via canRotate ci-dessus.
                    // Si le personnage valide sort de zone ou change de flamme, on repart en derive.
                    if (!matchingNotFar)
                    {
                        if (m.routine != null)
                        {
                            StopCoroutine(m.routine);
                            m.routine = null;
                        }

                        m.etat = Etat.Derive;
                    }
                    break;
            }
        }
    }

    private void BasculerRoutine(Morceau m, IEnumerator routine)
    {
        if (m.routine != null) StopCoroutine(m.routine);
        m.routine = StartCoroutine(routine);
    }

    private void MaintenirMorceauAOrigine(Morceau m)
    {
        if (m.routine != null)
        {
            StopCoroutine(m.routine);
            m.routine = null;
        }

        m.tr.position = m.posOrigine;
        m.tr.rotation = m.rotOrigine;
        m.etat = Etat.IdleAOrigine;
    }

    private void RetournerOuMaintenirMorceau(Morceau m)
    {
        if (m.etat == Etat.Retour)
        {
            return;
        }

        if (MorceauEstAOrigine(m))
        {
            MaintenirMorceauAOrigine(m);
            return;
        }

        BasculerRoutine(m, RoutineRetour(m));
    }

    private bool MorceauEstAOrigine(Morceau m)
    {
        const float positionToleranceSqr = 0.0001f;
        const float rotationToleranceDeg = 0.1f;

        return (m.tr.position - m.posOrigine).sqrMagnitude <= positionToleranceSqr
            && Quaternion.Angle(m.tr.rotation, m.rotOrigine) <= rotationToleranceDeg;
    }

    private IEnumerator RoutineRetour(Morceau m)
    {
        m.etat = Etat.Retour;

        if (dureeRetour <= 0f)
        {
            m.tr.position = m.posOrigine;
            m.tr.rotation = m.rotOrigine; // réalignement final
            m.etat = Etat.IdleAOrigine;
            m.routine = null;
            yield break;
        }

        Vector3 startPos = m.tr.position;
        Quaternion startRot = m.tr.rotation; // conservé si jamais tu veux revenir à l'ancien comportement

        float t = 0f;
        float inv = 1f / Mathf.Max(0.0001f, dureeRetour);

        while (t < 1f)
        {
            t += Time.deltaTime * inv;
            float k = Mathf.SmoothStep(0f, 1f, t);

            // CHANGEMENT: on ne touche plus à la rotation ici (elle continue de tourner dans Update)
            // On ne lerp que la position vers l'origine.
            m.tr.position = Vector3.LerpUnclamped(startPos, m.posOrigine, k);

            yield return null;
        }

        // Snap propre à la fin: position + rotation d'origine
        m.tr.position = m.posOrigine;
        m.tr.rotation = m.rotOrigine;

        m.etat = Etat.IdleAOrigine;
        m.routine = null;
    }

    // ======== Construction de la liste des cibles ========

    [ContextMenu("Reconstruire la liste")]
    public void ReconstruireMorceaux()
    {
        morceaux.Clear();

        switch (modeDeSelection)
        {
            case ModeSelection.GameObject:
                AjouterMorceau(transform, allowSelf: true);
                break;

            case ModeSelection.ChildrenOnly:
                foreach (Transform enfant in transform)
                    AjouterMorceau(enfant);
                break;

            case ModeSelection.Descendants:
                if (profondeurMax <= 0)
                {
                    foreach (var tr in EnumererDescendanceIllimitee(transform))
                        AjouterMorceau(tr);
                }
                else
                {
                    foreach (var tr in EnumererDescendanceLimitee(transform, profondeurMax))
                        AjouterMorceau(tr);
                }
                break;
        }
    }

    private void AjouterMorceau(Transform tr, bool allowSelf = false)
    {
        if (tr == transform && !allowSelf) return;
        var m = new Morceau
        {
            tr = tr,
            posOrigine = tr.position,
            rotOrigine = tr.rotation,
            directionFixe = Random.onUnitSphere.normalized,
            rotAxis = Random.onUnitSphere.normalized,
            etat = Etat.Derive,
            routine = null
        };
        morceaux.Add(m);
    }

    [ContextMenu("Configurer le trigger parent")]
    public void ConfigureParentTrigger()
    {
        if (!useTriggerAreaForReturn)
        {
            return;
        }

        bool createdTrigger = false;
        Collider parentTrigger = GetParentTriggerCollider();
        if (parentTrigger == null && autoCreateParentTrigger)
        {
            autoParentTrigger = gameObject.AddComponent<BoxCollider>();
            autoParentTrigger.isTrigger = true;
            parentTrigger = autoParentTrigger;
            createdTrigger = true;
        }

        if (parentTrigger == null)
        {
            return;
        }

        BoxCollider boxTrigger = parentTrigger as BoxCollider;
        if (boxTrigger == null
            || !autoResizeParentTrigger
            || (!createdTrigger && !autoResizeExistingParentTrigger))
        {
            return;
        }

        if (!TryCalculateAutoTriggerBounds(out Bounds localBounds))
        {
            return;
        }

        Vector3 paddedSize = localBounds.size + autoTriggerPadding * 2f;
        paddedSize.x = Mathf.Max(minAutoTriggerSize, paddedSize.x);
        paddedSize.y = Mathf.Max(minAutoTriggerSize, paddedSize.y);
        paddedSize.z = Mathf.Max(minAutoTriggerSize, paddedSize.z);

        boxTrigger.center = localBounds.center;
        boxTrigger.size = paddedSize;
        autoParentTrigger = boxTrigger;
    }

    private Collider GetParentTriggerCollider()
    {
        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && collider.isTrigger)
            {
                return collider;
            }
        }

        return null;
    }

    private bool TryCalculateAutoTriggerBounds(out Bounds localBounds)
    {
        localBounds = default;

        switch (autoTriggerBoundsSource)
        {
            case AutoTriggerBoundsSource.RenderersOnly:
                return TryCalculateRendererBounds(out localBounds);

            case AutoTriggerBoundsSource.CollidersOnly:
                return TryCalculateColliderBounds(out localBounds);

            case AutoTriggerBoundsSource.RenderersThenColliders:
            default:
                return TryCalculateRendererBounds(out localBounds)
                    || TryCalculateColliderBounds(out localBounds);
        }
    }

    private bool TryCalculateRendererBounds(out Bounds localBounds)
    {
        localBounds = default;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactiveChildrenInAutoTrigger);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.transform == transform)
            {
                continue;
            }

            EncapsulateWorldBounds(renderer.bounds, ref localBounds, ref hasBounds);
        }

        return hasBounds;
    }

    private bool TryCalculateColliderBounds(out Bounds localBounds)
    {
        localBounds = default;
        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactiveChildrenInAutoTrigger);
        bool hasBounds = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.transform == transform || collider.isTrigger)
            {
                continue;
            }

            EncapsulateWorldBounds(collider.bounds, ref localBounds, ref hasBounds);
        }

        return hasBounds;
    }

    private void EncapsulateWorldBounds(Bounds worldBounds, ref Bounds localBounds, ref bool hasBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        EncapsulateWorldPoint(new Vector3(min.x, min.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(min.x, min.y, max.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(min.x, max.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(min.x, max.y, max.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(max.x, min.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(max.x, min.y, max.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(max.x, max.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateWorldPoint(new Vector3(max.x, max.y, max.z), ref localBounds, ref hasBounds);
    }

    private void EncapsulateWorldPoint(Vector3 worldPoint, ref Bounds localBounds, ref bool hasBounds)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (!hasBounds)
        {
            localBounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        localBounds.Encapsulate(localPoint);
    }

    // ======== Detection personnages + flamme ========

    private void EnsureFallbackPlayer()
    {
        if (joueur != null)
        {
            return;
        }

        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        if (obj != null)
        {
            joueur = obj.transform;
        }
    }

    private bool HasMatchingCharacterInRange(Vector3 referencePosition, float radius, out SquadCharacterController match)
    {
        match = null;
        float radiusSqr = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);

        if (detecterPersonnages)
        {
            EnsureCharacterCandidates();
            for (int i = 0; i < characterCandidates.Count; i++)
            {
                SquadCharacterController controller = characterCandidates[i];
                if (controller == null)
                {
                    continue;
                }

                float distanceSqr = (controller.transform.position - referencePosition).sqrMagnitude;
                if (distanceSqr > radiusSqr)
                {
                    continue;
                }

                if (IsFlameConditionValid(controller, out string rejection))
                {
                    match = controller;
                    DebugFlame($"accepted '{controller.name}' at {Mathf.Sqrt(distanceSqr):0.00}m.");
                    return true;
                }

                DebugFlame($"rejected '{controller.name}': {rejection}");
            }

            return false;
        }

        if (joueur == null)
        {
            return false;
        }

        if ((joueur.position - referencePosition).sqrMagnitude > radiusSqr)
        {
            return false;
        }

        SquadCharacterController fallbackController = joueur.GetComponentInParent<SquadCharacterController>();
        if (fallbackController == null)
        {
            fallbackController = joueur.GetComponentInChildren<SquadCharacterController>(true);
        }

        if (fallbackController == null)
        {
            return !requireMatchingFlame;
        }

        if (!IsFlameConditionValid(fallbackController, out string fallbackRejection))
        {
            DebugFlame($"rejected fallback player '{fallbackController.name}': {fallbackRejection}");
            return false;
        }

        match = fallbackController;
        return true;
    }

    private void CollectCharacterCandidates()
    {
        characterCandidatesFrame = Time.frameCount;
        characterCandidates.Clear();

        SquadManager manager = SquadManager.Instance;
        if (manager != null && manager.squadCharacters != null)
        {
            for (int i = 0; i < manager.squadCharacters.Count; i++)
            {
                AddCharacterCandidate(manager.squadCharacters[i]);
            }
        }

        IReadOnlyList<SquadCharacterController> activeCharacters = SquadCharacterController.ActiveCharacters;
        if (activeCharacters != null)
        {
            for (int i = 0; i < activeCharacters.Count; i++)
            {
                AddCharacterCandidate(activeCharacters[i]);
            }
        }

        if (joueur != null)
        {
            AddCharacterCandidate(joueur.gameObject);
        }
    }

    private void EnsureCharacterCandidates()
    {
        if (characterCandidatesFrame == Time.frameCount)
        {
            return;
        }

        CollectCharacterCandidates();
    }

    private void AddCharacterCandidate(GameObject character)
    {
        if (character == null)
        {
            return;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            controller = character.GetComponentInChildren<SquadCharacterController>(true);
        }

        AddCharacterCandidate(controller);
    }

    private void AddCharacterCandidate(SquadCharacterController controller)
    {
        if (controller == null || characterCandidates.Contains(controller))
        {
            return;
        }

        characterCandidates.Add(controller);
    }

    private void OnDisable()
    {
        triggerCharacterOverlapCounts.Clear();
        triggerCharactersScratch.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterTriggerCollider(other, incrementOverlapCount: true);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterTriggerCollider(other, incrementOverlapCount: false);
    }

    private void OnTriggerExit(Collider other)
    {
        SquadCharacterController controller = ResolveCharacterFromCollider(other);
        if (controller == null || !triggerCharacterOverlapCounts.TryGetValue(controller, out int count))
        {
            return;
        }

        if (count <= 1)
        {
            triggerCharacterOverlapCounts.Remove(controller);
            return;
        }

        triggerCharacterOverlapCounts[controller] = count - 1;
    }

    private void RegisterTriggerCollider(Collider other, bool incrementOverlapCount)
    {
        SquadCharacterController controller = ResolveCharacterFromCollider(other);
        if (controller == null)
        {
            return;
        }

        if (!triggerCharacterOverlapCounts.TryGetValue(controller, out int count))
        {
            triggerCharacterOverlapCounts.Add(controller, 1);
            return;
        }

        if (incrementOverlapCount)
        {
            triggerCharacterOverlapCounts[controller] = count + 1;
        }
    }

    private SquadCharacterController ResolveCharacterFromCollider(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        SquadCharacterController controller = other.GetComponentInParent<SquadCharacterController>();
        if (controller != null)
        {
            return controller;
        }

        Rigidbody attachedRigidbody = other.attachedRigidbody;
        return attachedRigidbody != null
            ? attachedRigidbody.GetComponentInParent<SquadCharacterController>()
            : null;
    }

    private bool HasMatchingCharacterInTrigger(out SquadCharacterController match)
    {
        match = null;
        if (triggerCharacterOverlapCounts.Count == 0)
        {
            return false;
        }

        triggerCharactersScratch.Clear();
        foreach (SquadCharacterController controller in triggerCharacterOverlapCounts.Keys)
        {
            triggerCharactersScratch.Add(controller);
        }

        for (int i = triggerCharactersScratch.Count - 1; i >= 0; i--)
        {
            SquadCharacterController controller = triggerCharactersScratch[i];
            if (controller == null || !controller.isActiveAndEnabled)
            {
                triggerCharacterOverlapCounts.Remove(controller);
                continue;
            }

            if (IsFlameConditionValid(controller, out string rejection))
            {
                match = controller;
                DebugFlame($"accepted parent trigger character '{controller.name}'.");
                return true;
            }

            DebugFlame($"rejected parent trigger character '{controller.name}': {rejection}");
        }

        return false;
    }

    private bool IsFlameConditionValid(SquadCharacterController controller, out string rejection)
    {
        rejection = null;
        if (!requireMatchingFlame)
        {
            return true;
        }

        if (controller == null)
        {
            rejection = "no SquadCharacterController.";
            return false;
        }

        if (!controller.IsFlameEquipped)
        {
            rejection = "flame is not equipped.";
            return false;
        }

        return true;
    }

    private void DebugFlame(string message)
    {
        if (!debugFlameDetection || Time.time < nextDebugLogTime)
        {
            return;
        }

        nextDebugLogTime = Time.time + debugLogInterval;
        Debug.Log($"RandomDriftReturn: {message}", this);
    }

    // Descendance illimitée (DFS)
    private IEnumerable<Transform> EnumererDescendanceIllimitee(Transform root)
    {
        foreach (Transform child in root)
        {
            yield return child;
            foreach (var sub in EnumererDescendanceIllimitee(child))
                yield return sub;
        }
    }

    // Descendance limitée (BFS)
    private IEnumerable<Transform> EnumererDescendanceLimitee(Transform root, int profondeur)
    {
        var queue = new Queue<(Transform t, int d)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var (t, d) = queue.Dequeue();
            if (d >= profondeur) continue;

            foreach (Transform child in t)
            {
                yield return child;
                queue.Enqueue((child, d + 1));
            }
        }
    }
}

// Ancien nom conserve pour ne pas casser les scenes/prefabs qui auraient deja reference ce script.
public class SimpleDriftReturn_ConstantDirection : RandomDriftReturn
{
}
