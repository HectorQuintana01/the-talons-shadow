using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The Talon-Warden — the boss, and the game's whole sentence made into a fight.
/// It guards the Shadow Gate (sealed until it dies) and is STRIKE-IMMUNE while
/// its guard holds (Health.guardActive). The only way in is the crow: land the
/// crow on/near the Warden to break its guard for a window — then peek to read
/// its wind-ups from height, Shadow Step or dash in, and strike.
///
/// States: Idle → (Stalk ⇄ Charge / Slam) → GuardBroken → Dead.
/// - Charge: a long telegraphed rush; the aim-line is easiest to read from crow
///   height (SIGHT seed). Dodge it and it overshoots into recovery.
/// - Slam: a ground-pound AoE that punishes standing in melee range.
/// Break guard 3× (9 strikes) to kill. Runs on SCALED time (it's the world), so
/// peek slow-mo crawls its telegraphs — read, then commit.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class BossWarden : MonoBehaviour
{
    public enum WardenState { Idle, Stalk, Charge, Slam, GuardBroken, Dead }

    [Header("Engage")]
    public float engageRange = 26f;     // wakes when the player is this close
    public float stalkSpeed = 3.2f;
    public float attackRange = 4.5f;    // slam range
    public float chargeRange = 16f;     // will charge from up to here

    [Header("Guard / crow")]
    public float distractRadius = 10f;  // crow landing within this breaks the guard
    public float guardBrokenTime = 4f;  // strike window per break
    public float attackCooldown = 2.2f;

    [Header("Charge")]
    public float chargeWindup = 1.3f;   // telegraph (readable from crow height)
    public float chargeSpeed = 22f;
    public float chargeDuration = 0.8f;
    public float chargeDamage = 25f;

    [Header("Slam")]
    public float slamWindup = 1.0f;
    public float slamRadius = 5.5f;
    public float slamDamage = 30f;

    public WardenState State { get; private set; } = WardenState.Idle;
    public float GuardBreaksLeft { get; private set; } = 3f;
    /// <summary>0..1 telegraph fill for the HUD to draw a wind-up bar.</summary>
    public float TelegraphFill { get; private set; }
    public bool Alive => State != WardenState.Dead;

    NavMeshAgent agent;
    Health health;
    Transform player;
    Health playerHealth;
    CrowCompanion crow;
    LineRenderer chargeLine;

    float stateTimer;      // counts within Charge/Slam/GuardBroken phases
    float cooldownTimer;
    float guardBrokenUntil;
    bool crowWasPerched;
    Vector3 chargeDir;
    float lastHealth;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = stalkSpeed;
        health = GetComponent<Health>();
        health.guardActive = true; // sealed until the crow cracks it
        lastHealth = health.Current;
        var tc = FindFirstObjectByType<TalonController>();
        if (tc != null) { player = tc.transform; playerHealth = tc.GetComponent<Health>(); }
        crow = FindFirstObjectByType<CrowCompanion>();

        chargeLine = gameObject.AddComponent<LineRenderer>();
        chargeLine.positionCount = 2;
        chargeLine.startWidth = 0.12f; chargeLine.endWidth = 0.03f;
        chargeLine.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        chargeLine.enabled = false;
    }

    void Update()
    {
        if (!GameLoop.IsPlaying || State == WardenState.Dead) return;
        if (player == null) return;
        float dt = Time.deltaTime;
        cooldownTimer -= dt;

        // Death check (Health drops when guard is down and strikes land).
        if (health.Current <= 0f) { Die(); return; }

        // --- crow guard-break (edge-triggered on a fresh landing) ---
        bool perchedNow = crow != null && crow.IsPerched;
        if (perchedNow && !crowWasPerched && GuardBreaksLeft > 0f
            && Vector3.Distance(transform.position, crow.transform.position) <= distractRadius
            && State != WardenState.GuardBroken)
        {
            BreakGuard();
        }
        crowWasPerched = perchedNow;

        float dist = Vector3.Distance(transform.position, player.position);

        switch (State)
        {
            case WardenState.Idle:
                if (dist <= engageRange) State = WardenState.Stalk;
                break;

            case WardenState.Stalk:
                agent.isStopped = false;
                agent.SetDestination(player.position);
                FacePlayer(dt);
                if (cooldownTimer <= 0f)
                {
                    if (dist <= attackRange) BeginSlam();
                    else if (dist <= chargeRange) BeginCharge();
                }
                break;

            case WardenState.Charge:
                stateTimer -= dt;
                if (TelegraphFill < 1f) // winding up: rooted, aim line locked on
                {
                    TelegraphFill = 1f - Mathf.Clamp01(stateTimer / chargeWindup);
                    agent.isStopped = true;
                    FacePlayer(dt * 0.5f);
                    DrawChargeLine();
                    if (stateTimer <= 0f) // release
                    {
                        chargeDir = Flat(player.position - transform.position).normalized;
                        stateTimer = chargeDuration;
                        chargeLine.enabled = false;
                    }
                }
                else // charging
                {
                    agent.isStopped = true;
                    var cc = GetComponent<CharacterController>();
                    transform.position += chargeDir * chargeSpeed * dt;
                    transform.rotation = Quaternion.LookRotation(chargeDir, Vector3.up);
                    if (playerHealth != null && Vector3.Distance(transform.position, player.position) < 2.2f)
                    {
                        playerHealth.TakeDamage(chargeDamage);
                        ThirdPersonCamera.Shake(0.4f);
                        EndAttack(WardenState.Stalk, attackCooldown);
                    }
                    if (stateTimer <= 0f) EndAttack(WardenState.Stalk, attackCooldown);
                }
                break;

            case WardenState.Slam:
                stateTimer -= dt;
                TelegraphFill = 1f - Mathf.Clamp01(stateTimer / slamWindup);
                agent.isStopped = true;
                if (stateTimer <= 0f) DoSlam();
                break;

            case WardenState.GuardBroken:
                agent.isStopped = false;
                agent.speed = stalkSpeed * 0.6f; // staggered, slower
                agent.SetDestination(player.position);
                FacePlayer(dt);
                if (Time.time >= guardBrokenUntil) RestoreGuard();
                break;
        }
    }

    // ---- guard ----
    void BreakGuard()
    {
        State = WardenState.GuardBroken;
        health.guardActive = false;
        GuardBreaksLeft -= 1f;
        guardBrokenUntil = Time.time + guardBrokenTime;
        TelegraphFill = 0f;
        chargeLine.enabled = false;
        CrowCompanion.RequestHitstop(0.09f);
        ThirdPersonCamera.Shake(0.3f);
        Sfx.Play("caw", transform.position, 1f);
        Sfx.Play("enemy_death", transform.position, 0.4f); // a groan of exposure
    }

    void RestoreGuard()
    {
        if (State == WardenState.Dead) return;
        health.guardActive = true;
        agent.speed = stalkSpeed;
        State = WardenState.Stalk;
        cooldownTimer = 1f;
    }

    // ---- attacks ----
    void BeginCharge()
    {
        State = WardenState.Charge;
        stateTimer = chargeWindup;
        TelegraphFill = 0f;
    }

    void BeginSlam()
    {
        State = WardenState.Slam;
        stateTimer = slamWindup;
        TelegraphFill = 0f;
    }

    void DoSlam()
    {
        if (playerHealth != null
            && Vector3.Distance(transform.position, player.position) <= slamRadius)
            playerHealth.TakeDamage(slamDamage);
        ThirdPersonCamera.Shake(0.5f);
        Sfx.Play("enemy_death", transform.position, 0.7f); // heavy impact
        EndAttack(WardenState.Stalk, attackCooldown);
    }

    void EndAttack(WardenState next, float cd)
    {
        TelegraphFill = 0f;
        chargeLine.enabled = false;
        cooldownTimer = cd;
        // A guard-break can land mid-attack; don't stomp it.
        if (State != WardenState.GuardBroken) State = next;
    }

    void Die()
    {
        State = WardenState.Dead;
        agent.isStopped = true;
        chargeLine.enabled = false;
        health.guardActive = false;
        CrowCompanion.RequestHitstop(0.12f);
        ThirdPersonCamera.Shake(0.6f);
        Sfx.Play("win", transform.position, 0.9f);
        Sfx.Play("enemy_death", transform.position, 1f);
        // Opening the gate is handled by ShadowGateSeal watching Alive.
    }

    // ---- helpers ----
    void FacePlayer(float t)
    {
        Vector3 d = Flat(player.position - transform.position);
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(d.normalized, Vector3.up), 6f * t);
    }

    void DrawChargeLine()
    {
        chargeLine.enabled = true;
        Vector3 from = transform.position + Vector3.up * 0.4f;
        Vector3 to = from + Flat(player.position - transform.position).normalized * chargeRange;
        chargeLine.SetPosition(0, from);
        chargeLine.SetPosition(1, to);
        Color c = Color.Lerp(new Color(1f, 0.7f, 0.2f, 0.4f), new Color(1f, 0.1f, 0.05f, 0.95f), TelegraphFill);
        chargeLine.startColor = c; chargeLine.endColor = c;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
