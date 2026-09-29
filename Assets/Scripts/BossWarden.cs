using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The Talon-Warden — the boss, and the game's whole sentence made into a fight.
/// v2 (the summit's teeth): the climb taught you that HEIGHT IS DEATH, so the
/// Warden is the ultimate expression of it. It doesn't just hit you — it tries to
/// throw you off the summit (railless 20x20 plaza). And it is STRIKE-IMMUNE while
/// its guard holds — the ONLY way to crack it is PLUME FLASH (SIGHT-tree finisher).
/// Land the crow near it and flash: the burst breaks the guard for a window.
///
/// States: Idle -> (Stalk <-> Charge / Slam) -> GuardBroken -> Dead.
/// Break guard 3x to kill. After the 2nd break it ENRAGES: faster, furious, red.
/// Runs on SCALED time (it's the world), so peek slow-mo crawls its telegraphs.
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

    [Header("Guard / plume")]
    public float distractRadius = 10f;  // (legacy reference; plumeRadius on the crow is what breaks it now)
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

    [Header("v2 — the summit's teeth")]
    public float chargeKnockback = 20f;   // launch you toward the edge
    public float slamKnockback = 16f;
    public float enrageSpeedMult = 1.5f;  // final third: faster
    public float enrageCooldownMult = 0.6f; // and more aggressive

    public WardenState State { get; private set; } = WardenState.Idle;
    public float GuardBreaksLeft { get; private set; } = 3f;
    /// <summary>0..1 telegraph fill for the HUD to draw a wind-up bar.</summary>
    public float TelegraphFill { get; private set; }
    public bool Alive => State != WardenState.Dead;
    public bool Enraged { get; private set; }

    NavMeshAgent agent;
    Health health;
    Transform player;
    Health playerHealth;
    CrowCompanion crow;
    LineRenderer chargeLine;
    Renderer[] bodyRenderers;

    float stateTimer;      // counts within Charge/Slam/GuardBroken phases
    float cooldownTimer;
    float guardBrokenUntil;
    Vector3 chargeDir;
    float baseStalkSpeed;
    float curCooldown;     // attackCooldown, possibly enrage-scaled

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        baseStalkSpeed = stalkSpeed;
        agent.speed = stalkSpeed;
        health = GetComponent<Health>();
        health.guardActive = true; // sealed until a Plume Flash cracks it
        curCooldown = attackCooldown;
        var tc = FindFirstObjectByType<TalonController>();
        if (tc != null) { player = tc.transform; playerHealth = tc.GetComponent<Health>(); }
        crow = FindFirstObjectByType<CrowCompanion>();
        bodyRenderers = GetComponentsInChildren<Renderer>(); // captured before the charge line exists

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

        // Enrage once, at the final third (2 breaks spent, 1 to go).
        if (!Enraged && GuardBreaksLeft <= 1f) Enrage();

        float dist = Vector3.Distance(transform.position, player.position);

        switch (State)
        {
            case WardenState.Idle:
                if (dist <= engageRange) { State = WardenState.Stalk; Roar(0.85f); }
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
                    transform.position += chargeDir * chargeSpeed * dt;
                    transform.rotation = Quaternion.LookRotation(chargeDir, Vector3.up);
                    if (playerHealth != null && Vector3.Distance(transform.position, player.position) < 2.2f)
                    {
                        playerHealth.TakeDamage(chargeDamage);
                        var tcC = playerHealth.GetComponent<TalonController>();
                        if (tcC != null)
                            tcC.AddImpulse(chargeDir * chargeKnockback + Vector3.up * 6f); // launched toward the edge
                        ThirdPersonCamera.Shake(0.45f);
                        CrowCompanion.RequestHitstop(0.06f);
                        EndAttack(WardenState.Stalk, curCooldown);
                    }
                    if (stateTimer <= 0f) EndAttack(WardenState.Stalk, curCooldown);
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
                agent.speed = CurrentSpeed() * 0.6f; // staggered, slower
                agent.SetDestination(player.position);
                FacePlayer(dt);
                if (Time.time >= guardBrokenUntil) RestoreGuard();
                break;
        }
    }

    /// <summary>Plume Flash is the KEY: a flash within range CRACKS the guard (and
    /// interrupts a committed wind-up in the same beat). No crow-perch break anymore
    /// — the summit demands your newest power. Called by CrowCompanion.TryPlumeFlash
    /// only when the burst actually overlaps the Warden.</summary>
    public void PlumeStrike()
    {
        if (State == WardenState.Dead) return;
        bool wasWinding = State == WardenState.Charge || State == WardenState.Slam;
        if (wasWinding) { TelegraphFill = 0f; chargeLine.enabled = false; }

        if (health.guardActive && GuardBreaksLeft > 0f && State != WardenState.GuardBroken)
        {
            BreakGuard();
        }
        else if (wasWinding)
        {
            // Already exposed, but the flash still staggers a wind-up into a breather.
            cooldownTimer = Mathf.Max(cooldownTimer, 0.6f);
            if (State != WardenState.GuardBroken) State = WardenState.Stalk;
            Sfx.Play("strike_whiff", transform.position, 0.5f);
        }
    }

    // Kept so any older call sites compile; routes to the new plume behavior.
    public void FlashInterrupt() { PlumeStrike(); }

    // ---- guard ----
    void BreakGuard()
    {
        State = WardenState.GuardBroken;
        health.guardActive = false;
        GuardBreaksLeft -= 1f;
        guardBrokenUntil = Time.time + guardBrokenTime;
        TelegraphFill = 0f;
        chargeLine.enabled = false;
        CrowCompanion.RequestHitstop(0.12f);
        ThirdPersonCamera.Shake(0.5f);
        Sfx.Play("caw", transform.position, 1f);
        Sfx.Play("enemy_death", transform.position, 0.55f); // a groan of exposure
    }

    void RestoreGuard()
    {
        if (State == WardenState.Dead) return;
        health.guardActive = true;
        agent.speed = CurrentSpeed();
        State = WardenState.Stalk;
        cooldownTimer = Enraged ? 0.6f : 1f;
    }

    void Enrage()
    {
        Enraged = true;
        curCooldown = attackCooldown * enrageCooldownMult;
        agent.speed = CurrentSpeed();
        Roar(1f);
        ThirdPersonCamera.Shake(0.5f);
        CrowCompanion.RequestHitstop(0.1f);
        // Furious red: tint the body + light it up. Runtime material instances, no asset touch.
        if (bodyRenderers != null)
            foreach (var r in bodyRenderers)
            {
                if (r == null || r is LineRenderer) continue;
                var m = r.material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.4f, 0.04f, 0.03f));
                if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(1f, 0.12f, 0.06f) * 2.2f); }
            }
    }

    float CurrentSpeed() { return Enraged ? baseStalkSpeed * enrageSpeedMult : baseStalkSpeed; }

    // ---- attacks ----
    void BeginCharge() { State = WardenState.Charge; stateTimer = chargeWindup; TelegraphFill = 0f; }
    void BeginSlam()   { State = WardenState.Slam;   stateTimer = slamWindup;   TelegraphFill = 0f; }

    void DoSlam()
    {
        if (playerHealth != null
            && Vector3.Distance(transform.position, player.position) <= slamRadius)
        {
            playerHealth.TakeDamage(slamDamage);
            var tcS = playerHealth.GetComponent<TalonController>();
            if (tcS != null)
            {
                Vector3 radial = player.position - transform.position;
                radial.y = 0f;
                tcS.AddImpulse(radial.normalized * slamKnockback + Vector3.up * 7f); // launch toward the edge
            }
        }
        ThirdPersonCamera.Shake(0.55f);
        Sfx.Play("enemy_death", transform.position, 0.7f); // heavy impact
        EndAttack(WardenState.Stalk, curCooldown);
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
        CrowCompanion.RequestHitstop(0.16f);
        ThirdPersonCamera.Shake(0.8f);
        Sfx.Play("win", transform.position, 0.9f);
        Sfx.Play("enemy_death", transform.position, 1f);
        // Opening the gate is handled by ShadowGateSeal watching Alive.
    }

    // ---- helpers ----
    void Roar(float vol) { Sfx.Play("enemy_death", transform.position, vol); } // the Warden's voice

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
