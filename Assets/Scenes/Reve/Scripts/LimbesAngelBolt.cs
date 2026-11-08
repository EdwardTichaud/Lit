using System.Linq;
using UnityEngine;

public sealed class LimbesAngelBolt : MonoBehaviour
{
    public LimbesAngelBoss Owner { get; private set; }
    private Vector3 direction;
    private float speed, remaining = 6;
    private int damage;
    private bool authority;
    public void Launch(LimbesAngelBoss owner, Vector3 target, float velocity, int amount, bool canDamage)
    {
        Owner = owner;
        direction = (target - transform.position).normalized;
        speed = velocity;
        damage = amount;
        authority = canDamage;
    }
    private void Update()
    {
        if (Owner == null || Owner.IsBossResolved) { Destroy(gameObject); return; }
        if (Owner.Actor == null || Owner.Actor.IsFlameDormant) return;
        float delta = Owner.Actor.TimeDomain != null ? Owner.Actor.TimeDomain.DeltaTime : Time.deltaTime;
        remaining -= delta;
        if (remaining <= 0) { Destroy(gameObject); return; }
        float distance = speed * delta;
        if (authority)
            foreach (var hit in Physics.SphereCastAll(transform.position, .22f, direction, distance,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
            {
                if (hit.collider.transform.IsChildOf(Owner.transform)) continue;
                var player = hit.collider.GetComponentInParent<SquadCharacterController>();
                if (player != null) { player.ApplyDamage(damage, "Gargouille des Limbes — projectile"); Destroy(gameObject); return; }
                if (!hit.collider.isTrigger) { Destroy(gameObject); return; }
            }
        transform.position += direction * distance;
    }
}
