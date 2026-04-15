using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RainTools
{
    private const string MenuRoot = "Tools/Lit/Weather/";

    [MenuItem(MenuRoot + "Create Rain System")]
    [MenuItem("GameObject/Lit/Weather/Rain System", false, 30)]
    public static void CreateRainSystem()
    {
        GameObject root = new GameObject("RainSystem");
        Undo.RegisterCreatedObjectUndo(root, "Create Rain System");

        RainSystem system = root.AddComponent<RainSystem>();
        ParticleSystem rain = CreateRainDrops(root.transform);
        ParticleSystem splashes = CreateRainSplashes(root.transform);

        SerializedObject serializedSystem = new SerializedObject(system);
        serializedSystem.FindProperty("rainParticleSystem").objectReferenceValue = rain;
        serializedSystem.FindProperty("splashParticleSystem").objectReferenceValue = splashes;
        serializedSystem.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    [MenuItem(MenuRoot + "Create Rain Zone")]
    [MenuItem("GameObject/Lit/Weather/Rain Zone", false, 31)]
    public static void CreateRainZone()
    {
        GameObject zone = new GameObject("RainZone");
        Undo.RegisterCreatedObjectUndo(zone, "Create Rain Zone");

        BoxCollider collider = zone.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(20f, 8f, 20f);
        zone.AddComponent<RainZone>();

        GameObject parent = Selection.activeGameObject;
        if (parent != null)
        {
            Undo.SetTransformParent(zone.transform, parent.transform, "Parent Rain Zone");
            zone.transform.localPosition = Vector3.zero;
        }

        Selection.activeGameObject = zone;
        EditorSceneManager.MarkSceneDirty(zone.scene);
    }

    private static ParticleSystem CreateRainDrops(Transform parent)
    {
        GameObject gameObject = new GameObject("Rain_Drops");
        Undo.RegisterCreatedObjectUndo(gameObject, "Create Rain Drops");
        Undo.SetTransformParent(gameObject.transform, parent, "Parent Rain Drops");
        gameObject.transform.localPosition = new Vector3(0f, 9f, 0f);

        ParticleSystem system = gameObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.25f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
        main.maxParticles = 1800;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(24f, 0.1f, 24f);

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(-20f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystem.CollisionModule collision = system.collision;
        collision.enabled = false;
        ParticleSystem.TriggerModule trigger = system.trigger;
        trigger.enabled = false;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.5f;
        renderer.velocityScale = 0.1f;
        renderer.cameraVelocityScale = 0f;

        return system;
    }

    private static ParticleSystem CreateRainSplashes(Transform parent)
    {
        GameObject gameObject = new GameObject("Rain_Splashes");
        Undo.RegisterCreatedObjectUndo(gameObject, "Create Rain Splashes");
        Undo.SetTransformParent(gameObject.transform, parent, "Parent Rain Splashes");

        ParticleSystem system = gameObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.maxParticles = 600;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystem.CollisionModule collision = system.collision;
        collision.enabled = false;
        ParticleSystem.TriggerModule trigger = system.trigger;
        trigger.enabled = false;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        return system;
    }
}
