using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Stalker — a NavMesh chaser. Sees the player inside sightRange, chases, and
/// deals contact damage in melee reach. The crow counters it: when the crow
/// perches nearby, the Stalker is DISTRACTED — it stalks toward the perch and
/// ignores the player for a few seconds. That's your strike window.
/// The "Shade" variant is this same script with faster speed / lower health.
/// Runs on SCALED time — enemies are the world, so the peek slow-mo owns them.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyStalker : MonoBehaviour
{
    [Header("Hunt")]
    public float sightRange = 14f;
    public float contactRange = 1.4f;
    public float contactDamage = 20f;
    public float attackCooldown = 1.0f;

    [Header("Crow distraction")]
    public float distractRadius = 9f;
    public float distractDuration = 3f;

    NavMeshAgent agent;
    Transform player;
    Health playerHealth;
    CrowCompanion crow;
    Renderer[] renderers;
    float attackTimer;
    float distractedUntil;
    bool crowWasPerched;

    public bool IsDistracted => Time.time < distractedUntil;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        var tc = FindFirstObjectByType<TalonController>();
        if (tc != null) { player = tc.transform; playerHealth = tc.GetComponent<Health>(); }
        crow = FindFirstObjectByType<CrowCompanion>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (player == null) return;
        attackTimer -= Time.deltaTime;

        // A crow LANDING nearby grabs attention (edge-trigger, so it wears off).
        bool perchedNow = crow != null && crow.IsPerched;
        if (perchedNow && !crowWasPerched &&
            Vector3.Distance(transform.position, crow.transform.position) <= distractRadius)
        {
            distractedUntil = Time.time + distractDuration;
        }
        crowWasPerched = perchedNow;

        if (IsDistracted && crow != null)
        {
            // Stalk the crow's perch; the player is forgotten. (Big readable tell.)
            agent.SetDestination(GroundPoint(crow.transform.position));
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        if (distToPlayer <= sightRange)
        {
            agent.SetDestination(player.position);
            if (distToPlayer <= contactRange && attackTimer <= 0f && playerHealth != null)
            {
                playerHealth.TakeDamage(contactDamage);
                attackTimer = attackCooldown;
            }
        }
        else
        {
            agent.SetDestination(transform.position); // hold position
        }
    }

    Vector3 GroundPoint(Vector3 p)
    {
        // The crow perches up high; the stalker paces beneath it.
        if (Physics.Raycast(p, Vector3.down, out RaycastHit hit, 30f))
            return hit.point;
        return new Vector3(p.x, transform.position.y, p.z);
    }
}
