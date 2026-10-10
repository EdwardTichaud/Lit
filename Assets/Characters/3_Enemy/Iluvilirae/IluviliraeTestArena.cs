using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>Isolated test scene only; never installed on a gameplay player.</summary>
public sealed class IluviliraeTestArena : MonoBehaviour
{
    public NavMeshData navigation;
    public Transform target;
    public LitBrainsEnemy enemy;
    public GameObject enemyPrefab;
    private NavMeshDataInstance navigationInstance;

    private void OnEnable()
    {
        if (navigation != null) navigationInstance = NavMesh.AddNavMeshData(navigation);
    }

    private void OnDisable()
    {
        if (navigationInstance.valid) navigationInstance.Remove();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        Vector3 direction = new Vector3(
            (keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed ? 1 : 0),
            0,
            (keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.downArrowKey.isPressed ? 1 : 0));
        if (target != null && direction.sqrMagnitude > 0 &&
            NavMesh.SamplePosition(target.position + direction.normalized * (4f * Time.deltaTime), out var hit, 0.2f, NavMesh.AllAreas))
            target.position = hit.position;
        if (keyboard.hKey.wasPressedThisFrame && enemy != null && enemy.gameObject.activeInHierarchy) enemy.TakeDamage(10);
        if (keyboard.kKey.wasPressedThisFrame && enemy != null && enemy.gameObject.activeInHierarchy) enemy.TakeDamage(enemy.CurrentHealth);
        if (keyboard.rKey.wasPressedThisFrame && enemyPrefab != null)
        {
            if (enemy != null) Destroy(enemy.gameObject);
            enemy = Instantiate(enemyPrefab, new Vector3(0, 0, -4), Quaternion.identity).GetComponent<LitBrainsEnemy>();
            enemy.detectionTargetOverride = target;
        }
    }

    private void OnGUI()
    {
        string title = enemy != null && enemy.characterData != null ? enemy.characterData.characterName : "Ennemi";
        GUI.Box(new Rect(12, 12, 540, 105), title + " — test Brains AI");
        GUI.Label(new Rect(25, 37, 510, 25), "Flèches : déplacer la cible | H : blessure | K : mort | R : recommencer");
        if (enemy != null)
        {
            GUI.Label(new Rect(25, 62, 510, 25), "PV : " + enemy.CurrentHealth + " | Détection : " + enemy.DetectionReason);
            GUI.Label(new Rect(25, 87, 510, 25), "Navigation : " + (enemy.NavigationReady ? "prête" : "en attente"));
        }
    }
}
