using UnityEngine;

/// <summary>
/// A sentry's projectile: flies straight, damages the player on proximity,
/// dies on world contact or after its lifetime. Dodging through it with dash
/// i-frames is free — Health swallows the hit. Scaled time (it's the world).
/// </summary>
public class SentryBolt : MonoBehaviour
{
    Vector3 dir;
    float speed;
    float damage;
    float life = 4f;
    Health playerHealth;

    public void Init(Vector3 direction, float spd, float dmg)
    {
        dir = direction;
        speed = spd;
        damage = dmg;
        var tc = FindFirstObjectByType<TalonController>();
        if (tc != null) playerHealth = tc.GetComponent<Health>();
    }

    void Update()
    {
        if (!GameLoop.IsPlaying) return; // bolts freeze with the world
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }

        Vector3 step = dir * speed * dt;

        // World contact (walls, floor, pillars) kills the bolt.
        if (Physics.Raycast(transform.position, dir, out RaycastHit hit, step.magnitude + 0.1f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (playerHealth != null && hit.transform.root == playerHealth.transform.root)
                playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        transform.position += step;

        // Proximity hit on the player (CharacterController capsule is thin — be generous).
        if (playerHealth != null &&
            Vector3.Distance(transform.position, playerHealth.transform.position + Vector3.up * 0.9f) < 0.7f)
        {
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
