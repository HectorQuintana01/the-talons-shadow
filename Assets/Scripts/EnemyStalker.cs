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
    public float contactDamage = 20f;
    public float attackCooldown = 1.0f;
    [Tooltip("Shove on lunge connect — the Bruiser variant cranks this to knock you off ledges.")]
    public float knockback = 7f;

    [Header("Lunge — the window (v2: telegraph, commit, recover)")]
    public float lungeTriggerRange = 3.0f; // starts winding up inside this
    public float windupTime = 0.55f;       // the tell: rear back + swell + amber flare
    public float lungeSpeed = 15f;
    public float lungeDuration = 0.3f;     // committed, direction locked at launch
    public float lungeHitRange = 1.4f;
    public float recoverTime = 0.8f;       // whiffed = exposed. This is YOUR window.

    [Header("Crow distraction")]
    public float distractRadius = 9f;
    public float distractDuration = 3f;

    NavMeshAgent agent;
    Transform player;
    Health playerHealth;
    CrowCompanion crow;
    Renderer[] renderers;
    MaterialPropertyBlock mpb;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    float attackTimer;
    float distractedUntil;
    bool crowWasPerched;
    // lunge phase timers (only one runs at a time)
    float windupLeft, lungeLeft, recoverLeft;
    Vector3 lungeDir;
    Vector3 baseScale;
    bool lungeHitLanded;

    float stunnedUntil;

    public bool IsDistracted => Time.time < distractedUntil;
    public bool IsWindingUp => windupLeft > 0f;
    public bool IsRecovering => recoverLeft > 0f;
    public bool IsStunned => Time.time < stunnedUntil;

    /// <summary>Plume Flash blinds it — frozen, no attacks, for the duration.</summary>
    public void Stun(float seconds)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        windupLeft = 0f; lungeLeft = 0f; recoverLeft = 0f;
        transform.localScale = baseScale;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        var tc = FindFirstObjectByType<TalonController>();
        if (tc != null) { player = tc.transform; playerHealth = tc.GetComponent<Health>(); }
        crow = FindFirstObjectByType<CrowCompanion>();
        renderers = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
    }

    void Update()
    {
        if (!GameLoop.IsPlaying) return; // paused agents must not bite
        if (player == null) return;
        float dt = Time.deltaTime;
        attackTimer -= dt;

        if (IsStunned)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            SetTint(true);                 // amber-lit, dazed
            return;
        }

        // A crow LANDING nearby grabs attention (edge-trigger, so it wears off).
        // A distraction mid-windup CANCELS the attack — the flash of wings wins.
        bool perchedNow = crow != null && crow.IsPerched;
        if (perchedNow && !crowWasPerched &&
            Vector3.Distance(transform.position, crow.transform.position) <= distractRadius)
        {
            distractedUntil = Time.time + distractDuration;
            windupLeft = 0f; lungeLeft = 0f;
            transform.localScale = baseScale;
            SetTint(false);
        }
        crowWasPerched = perchedNow;

        // ---- lunge phases run to completion before anything else ----
        if (windupLeft > 0f) { TickWindup(dt); return; }
        if (lungeLeft > 0f) { TickLunge(dt); return; }
        if (recoverLeft > 0f) { TickRecover(dt); return; }

        if (IsDistracted && crow != null)
        {
            // Stalk the crow's perch; the player is forgotten. (Big readable tell.)
            agent.isStopped = false;
            agent.SetDestination(GroundPoint(crow.transform.position));
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        if (distToPlayer <= sightRange)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
            // Close enough: begin the tell instead of biting instantly.
            if (distToPlayer <= lungeTriggerRange && attackTimer <= 0f)
            {
                windupLeft = windupTime;
                lungeHitLanded = false;
                agent.isStopped = true;
                Sfx.Play("strike_whiff", transform.position, 0.3f); // the hiss before the bite
            }
        }
        else
        {
            agent.SetDestination(transform.position); // hold position
        }
    }

    // The tell: rear back and swell, flaring amber — dodge is available the whole time.
    void TickWindup(float dt)
    {
        windupLeft -= dt;
        float t = 1f - Mathf.Clamp01(windupLeft / windupTime);
        transform.localScale = baseScale * (1f + 0.22f * t);
        SetTint(true);
        // Track the player while winding — direction locks only at LAUNCH.
        Vector3 face = player.position - transform.position; face.y = 0f;
        if (face.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(face.normalized, Vector3.up), 4f * dt);
        if (windupLeft <= 0f)
        {
            lungeDir = face.sqrMagnitude > 0.01f ? face.normalized : transform.forward;
            lungeLeft = lungeDuration;
            transform.localScale = baseScale;
            SetTint(false);
        }
    }

    // Committed: flies in a straight, already-dodged-or-not line.
    void TickLunge(float dt)
    {
        lungeLeft -= dt;
        transform.position += lungeDir * lungeSpeed * dt;
        if (!lungeHitLanded && playerHealth != null
            && Vector3.Distance(transform.position, player.position) <= lungeHitRange)
        {
            lungeHitLanded = true;
            playerHealth.TakeDamage(contactDamage);
            var tc = playerHealth.GetComponent<TalonController>();
            if (tc != null)
                tc.AddImpulse(lungeDir * knockback + Vector3.up * (knockback * 0.35f));
        }
        if (lungeLeft <= 0f)
        {
            recoverLeft = recoverTime;
            attackTimer = attackCooldown;
            // Snap back onto the navmesh in case the lunge left it near an edge.
            UnityEngine.AI.NavMeshHit navHit;
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out navHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                agent.Warp(navHit.position);
        }
    }

    // Whiffed (or landed) — either way it's spent. This is the player's window.
    void TickRecover(float dt)
    {
        recoverLeft -= dt;
        agent.isStopped = true;
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * 0.92f, 6f * dt);
        if (recoverLeft <= 0f)
        {
            transform.localScale = baseScale;
            agent.isStopped = false;
        }
    }

    void SetTint(bool on)
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (on)
            {
                mpb.SetColor(BaseColorId, new Color(1f, 0.55f, 0.1f) * 1.6f); // amber flare
                r.SetPropertyBlock(mpb);
            }
            else r.SetPropertyBlock(null);
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
