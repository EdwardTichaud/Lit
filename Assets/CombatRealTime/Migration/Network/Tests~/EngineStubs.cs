// Minimal managed test doubles, NOT an emulation of Unity/NGO lifecycle or RPCs.
// Production files are also compiled separately against real installed assemblies.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object { public string name; }
    public class ScriptableObject : Object { }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>() where T : class => GetComponent<T>() ?? transform.parent?.GetComponentInParent<T>();
    }
    public class Behaviour : Component
    {
        public bool isActiveAndEnabled = true;
    }
    public class MonoBehaviour : Behaviour
    {
        protected static void DontDestroyOnLoad(GameObject value) { }
    }
    public class GameObject : Object
    {
        private readonly Dictionary<Type, Component> components = new Dictionary<Type, Component>();
        public bool activeInHierarchy = true;
        public SceneManagement.Scene scene = new SceneManagement.Scene(1);
        public readonly Transform transform;
        public GameObject(string name = "test") { transform = new Transform { gameObject = this }; components.Add(typeof(Transform), transform); }
        public T AddComponent<T>() where T : Component, new()
        {
            var value = new T { gameObject = this }; components.Add(typeof(T), value); return value;
        }
        public T GetComponent<T>() where T : class => components.TryGetValue(typeof(T), out var value) ? value as T : null;
    }
    public class Transform : Component
    {
        public Transform parent;
        public Vector3 position;
        public bool IsChildOf(Transform other) => parent != null && (parent == other || parent.IsChildOf(other));
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3 normalized
        {
            get { float length = MathF.Sqrt(x*x + y*y + z*z); return length > 0 ? this / length : default; }
        }
        public static Vector3 operator /(Vector3 a, float scale) => new Vector3(a.x / scale, a.y / scale, a.z / scale);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x, a.y-b.y, a.z-b.z);
    }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Abs(float value) => Math.Abs(value);
    }
    public static class Time
    {
        public static double unscaledTimeAsDouble;
        public static double timeAsDouble => unscaledTimeAsDouble;
    }
    public static class Application { public static bool isPlaying = true; }
    public static class Debug
    {
        public static readonly List<Exception> Exceptions = new List<Exception>();
        public static void LogException(Exception exception, Object context = null) => Exceptions.Add(exception);
    }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }
    public class DisallowMultipleComponent : Attribute { }
    public class CreateAssetMenuAttribute : Attribute { public string menuName; }
    public class SerializeField : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float minimum, float maximum) { } }
    public struct LayerMask { public static implicit operator LayerMask(int value) => default; }
    public class AnimationClip : Object
    {
        public bool isLooping;
        public float length = 1;
        public AnimationEvent[] events = Array.Empty<AnimationEvent>();
    }
    public class AnimationEvent
    {
        public string functionName;
        public float time;
        public int intParameter;
        public Object objectReferenceParameter;
    }
}

namespace UnityEngine.SceneManagement
{
    public readonly struct Scene
    {
        private readonly int id;
        public bool isLoaded { get; }
        public Scene(int id, bool loaded = true) { this.id = id; isLoaded = loaded; }
        public bool IsValid() => id != 0;
        public static bool operator ==(Scene a, Scene b) => a.id == b.id;
        public static bool operator !=(Scene a, Scene b) => a.id != b.id;
        public override bool Equals(object value) => value is Scene scene && scene == this;
        public override int GetHashCode() => id;
    }
    public static class SceneManager
    {
        public static event Action<Scene, Scene> activeSceneChanged;
        public static event Action<Scene> sceneUnloaded;
        public static void Change() => activeSceneChanged?.Invoke(new Scene(1), new Scene(2));
        public static void Unload() => sceneUnloaded?.Invoke(new Scene(2));
        public static void UnloadParticipant() => sceneUnloaded?.Invoke(new Scene(1));
    }
}

namespace Unity.Netcode
{
    public class NetworkManager : UnityEngine.MonoBehaviour
    {
        public static NetworkManager Singleton;
        public bool IsListening = true, IsServer = true, ShutdownInProgress;
        public readonly Dictionary<ulong, object> ConnectedClients = new Dictionary<ulong, object>();
        public readonly SpawnManager SpawnManager = new SpawnManager();
        public event Action<ulong> OnClientDisconnectCallback;
        public void Disconnect(ulong id) { ConnectedClients.Remove(id); OnClientDisconnectCallback?.Invoke(id); }
    }
    public class SpawnManager { public readonly Dictionary<ulong, NetworkObject> SpawnedObjects = new Dictionary<ulong, NetworkObject>(); }
    public class NetworkObject : UnityEngine.MonoBehaviour
    {
        public bool IsSpawned = true;
        public ulong NetworkObjectId, OwnerClientId;
        public NetworkManager NetworkManager;
    }
}

public class SquadCharacterController : UnityEngine.MonoBehaviour { public int CurrentHp = 100; }
public interface ICombatTarget
{
    UnityEngine.Transform Root { get; }
    bool IsDead { get; }
}

public enum NavMeshWorldState { Unloaded, Loading, Building, Validating, Ready, Failed, Invalidating }
public class NavMeshWorldService
{
    public static NavMeshWorldService Instance;
    public event Action<NavMeshWorldState> StateChanged;
    public void Invalidate() => StateChanged?.Invoke(NavMeshWorldState.Invalidating);
}
