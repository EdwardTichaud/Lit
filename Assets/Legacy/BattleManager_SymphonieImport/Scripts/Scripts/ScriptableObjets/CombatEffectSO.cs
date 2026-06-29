using UnityEngine;

[CreateAssetMenu(fileName = "CombatEffect", menuName = "Symphonie/Combat Effect")]
public class CombatEffectSO : ScriptableObject
{
    [Header("Effet visuel")]
    [Tooltip("Prefab instancié lorsqu'un état de combat démarre.")]
    public GameObject effectPrefab;

    [Tooltip("Décalage appliqué à l'instance par rapport au pivot de l'unité.")]
    public Vector3 spawnOffset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("Durée de vie de l'instance (0 = laissé tel quel).")]
    public float lifetime = 5f;
}
