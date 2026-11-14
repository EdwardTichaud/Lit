using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Cloth))]
[DefaultExecutionOrder(9000)]
public sealed class LucianCapeCollision : MonoBehaviour
{
    [SerializeField] private Transform character;
    [SerializeField] private Transform pelvis;
    [SerializeField] private Transform chest;
    [SerializeField] private Transform leftThigh;
    [SerializeField] private Transform rightThigh;
    [SerializeField] private Transform leftCalf;
    [SerializeField] private Transform rightCalf;
    [SerializeField] private LayerMask groundLayers;

    private const int GroundRows = 2;
    private const int GroundColumns = 3;
    private const float GroundRadius = 0.45f;
    private const float GroundHalfSegment = 0.14f;
    private const float GroundClearance = 0.045f;
    private const float MinimumGroundNormal = 0.55f;
    private readonly CapsuleCollider[] groundCapsules = new CapsuleCollider[GroundRows * GroundColumns];
    private readonly List<CapsuleCollider> bodyCapsules = new List<CapsuleCollider>(6);

    private Cloth cloth;
    private GameObject collisionVolumes;
    private CapsuleCollider[] originalColliders;
    private CapsuleCollider[] bodyCollisionSet;
    private int activeGroundMask;

    private void OnEnable()
    {
        if (!character || !pelvis || !chest || !leftThigh || !rightThigh || !leftCalf || !rightCalf)
        {
            Debug.LogError("Lucian's cape is missing its collision rig references.", this);
            return;
        }

        cloth = GetComponent<Cloth>();
        originalColliders = cloth.capsuleColliders;

        collisionVolumes = new GameObject("Lucian Cape Collision Volumes");
        collisionVolumes.transform.SetParent(character, false);
        CreateBodyCapsule("Back", chest, 0.16f, 0.47f, new Vector3(0, 0.04f, 0));
        CreateBodyCapsule("Hips", pelvis, 0.145f, 0.43f, new Vector3(0, -0.035f, 0), 0);
        CreateBodyCapsule("Left thigh", leftThigh, 0.105f, 0.43f, Vector3.up * 0.19f);
        CreateBodyCapsule("Right thigh", rightThigh, 0.105f, 0.43f, Vector3.up * 0.19f);
        CreateBodyCapsule("Left calf", leftCalf, 0.075f, 0.43f, Vector3.up * 0.20f);
        CreateBodyCapsule("Right calf", rightCalf, 0.075f, 0.43f, Vector3.up * 0.20f);
        bodyCollisionSet = bodyCapsules.ToArray();
        activeGroundMask = 0;
        cloth.capsuleColliders = bodyCollisionSet;
        for (int i = 0; i < groundCapsules.Length; i++)
        {
            var capsule = CreateCapsule($"Hem ground {i + 1}", collisionVolumes.transform);
            capsule.direction = 2;
            capsule.enabled = false;
            groundCapsules[i] = capsule;
        }
    }

    private void OnDisable()
    {
        if (!collisionVolumes)
            return;

        cloth.capsuleColliders = originalColliders;
        collisionVolumes.SetActive(false);
        // Body volumes are parented to their bones so they follow the animation.
        foreach (CapsuleCollider volume in bodyCapsules)
        {
            volume.enabled = false;
            Destroy(volume.gameObject);
        }
        bodyCapsules.Clear();
        Destroy(collisionVolumes);
        collisionVolumes = null;
    }

    private void CreateBodyCapsule(string label, Transform bone, float radius, float height,
        Vector3 center, int direction = 1)
    {
        var capsule = CreateCapsule("Cape " + label, bone);
        capsule.radius = radius;
        capsule.height = height;
        capsule.center = center;
        capsule.direction = direction;
        bodyCapsules.Add(capsule);
    }

    private static CapsuleCollider CreateCapsule(string label, Transform parent)
    {
        var volume = new GameObject(label) { layer = LayerMask.NameToLayer("Ignore Raycast") };
        volume.transform.SetParent(parent, false);
        var capsule = volume.AddComponent<CapsuleCollider>();
        capsule.excludeLayers = ~0;
        capsule.layerOverridePriority = int.MaxValue;
        return capsule;
    }

    private void LateUpdate()
    {
        if (!collisionVolumes)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(character.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.5f)
            forward = Vector3.Cross(character.right, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float scale = Mathf.Abs(character.lossyScale.y);
        Vector3 origin = new Vector3(pelvis.position.x, character.position.y, pelvis.position.z);
        PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
        int groundMask = 0;
        int groundCount = 0;

        for (int row = 0; row < GroundRows; row++)
        {
            for (int column = 0; column < GroundColumns; column++)
            {
                Vector3 sample = origin + (-forward * (0.06f + row * 0.29f) +
                    right * ((column - 1) * 0.28f)) * scale;
                int index = row * GroundColumns + column;
                var capsule = groundCapsules[index];
                UpdateGroundCapsule(capsule, physicsScene, sample, right, scale);
                if (capsule.enabled)
                {
                    groundMask |= 1 << index;
                    groundCount++;
                }
            }
        }

        if (groundMask == activeGroundMask)
            return;

        activeGroundMask = groundMask;
        var activeColliders = new CapsuleCollider[bodyCollisionSet.Length + groundCount];
        bodyCollisionSet.CopyTo(activeColliders, 0);
        int next = bodyCollisionSet.Length;
        foreach (CapsuleCollider capsule in groundCapsules)
        {
            if (capsule.enabled)
                activeColliders[next++] = capsule;
        }
        cloth.capsuleColliders = activeColliders;
    }

    private void UpdateGroundCapsule(CapsuleCollider capsule, PhysicsScene physicsScene, Vector3 sample,
        Vector3 right, float scale)
    {
        if (!cloth.enabled || !FindGround(physicsScene, sample, scale, out RaycastHit center))
        {
            capsule.enabled = false;
            return;
        }

        Vector3 axis = Vector3.ProjectOnPlane(right, center.normal).normalized;
        float halfSegment = GroundHalfSegment;
        // Keep the long shape only if both ends follow the same local surface.
        if (!SupportsEnd(physicsScene, center, axis * (halfSegment * scale), scale) ||
            !SupportsEnd(physicsScene, center, -axis * (halfSegment * scale), scale))
            halfSegment = 0f;

        capsule.radius = GroundRadius;
        capsule.height = 2f * (GroundRadius + halfSegment);
        capsule.transform.SetPositionAndRotation(
            center.point - center.normal * ((GroundRadius - GroundClearance) * scale),
            Quaternion.LookRotation(axis, center.normal));
        capsule.enabled = true;
    }

    private bool SupportsEnd(PhysicsScene physicsScene, RaycastHit center, Vector3 offset, float scale)
    {
        Vector3 end = center.point + offset;
        return FindGround(physicsScene, end, scale, out RaycastHit hit) &&
            Mathf.Abs(hit.point.y - end.y) <= 0.07f * scale;
    }

    private bool FindGround(PhysicsScene physicsScene, Vector3 point, float scale, out RaycastHit hit)
    {
        return physicsScene.Raycast(point + Vector3.up * (0.75f * scale), Vector3.down, out hit,
                   1.55f * scale, groundLayers, QueryTriggerInteraction.Ignore) &&
               hit.normal.y >= MinimumGroundNormal &&
               !hit.collider.transform.IsChildOf(character);
    }
}
