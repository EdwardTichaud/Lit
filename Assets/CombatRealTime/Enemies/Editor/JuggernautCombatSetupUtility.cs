using System;
using UnityEditor;
using UnityEngine;

/// <summary>Existing editor commands delegate to the single pattern installer.</summary>
public static class JuggernautCombatSetupUtility
{
    private const string PrefabPath = "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab";

    [MenuItem("Lit/Combat/Configure Juggernaut Combat AI")]
    public static void Configure() => JuggernautPatternSetup.Configure();

    [MenuItem("Lit/Combat/Validate Juggernaut Combat AI")]
    public static void Validate() => JuggernautPatternVerification.ValidateAssets();

    [MenuItem("Lit/Combat/Repair Juggernaut Runtime Contract")]
    private static void RepairRuntimeContract()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Quitter Play Mode avant la reparation.");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var body = root.GetComponent<Rigidbody>() ?? root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var capsule = root.GetComponent<CapsuleCollider>() ?? root.AddComponent<CapsuleCollider>();
            capsule.isTrigger = false;
            if (root.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
                root.AddComponent<UnityEngine.AI.NavMeshAgent>();
            if (root.GetComponent<EnemyAttackRecoverySafety>() == null)
                root.AddComponent<EnemyAttackRecoverySafety>();
            if (root.GetComponent<CombatEnemyRuntimeContract>() == null)
                root.AddComponent<CombatEnemyRuntimeContract>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Configure();
        Validate();
    }
}
