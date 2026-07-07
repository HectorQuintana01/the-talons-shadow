using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The crow — the player's attention given a body. The CROW verb: extend and return.
/// Send it to a perch (tap E / RB), peek through its eyes (hold right-mouse / LT),
/// recall it (tap Q / LB). Never a mode: all inputs stay live, release always returns.
/// States are plain code, no animator — bob and banking are done in Update.
/// Day-4 hook: IsPerched + transform.position let enemies get "distracted" by it.
/// </summary>
public class CrowCompanion : MonoBehaviour
{
    public enum CrowState { Follow, FlyTo, Perched }

    [Header("Follow")]
    public Vector3 followOffset = new Vector3(0.6f, 1.9f, -0.8f); // right/up/back of the player
    public float followLerp = 6f;
    public float bobAmplitude = 0.08f;
    public float bobFrequency = 2.2f;

    [Header("Send / Fly")]
    public float sendRange = 45f;
    public float flySpeed = 18f;          // world units per second along the arc
    public float arcHeight = 2.5f;

    [Header("Peek time dilation")]
    [Tooltip("World time scale while extended into the crow — slower than thought, not stopped.")]
    public float peekTimeScale = 0.35f;
    public float timeScaleLerp = 8f;      // how fast the world eases in/out of slow-mo (unscaled)

    [Header("Shadow Step (day 5 — taste of the Step tree)")]
    [Tooltip("Teleporting to the crow consumes the perch: attention collapses into presence.")]
    public float shadowStepCooldown = 2.5f;

    [Header("Refs (auto-found if empty)")]
    public Transform player;
    public Transform cameraTransform;

    public CrowState State { get; private set; } = CrowState.Follow;
    public bool IsPerched => State == CrowState.Perched;
    /// <summary>True while the player is holding the peek input (right mouse / LT).</summary>
    public bool PeekHeld { get; private set; }
    /// <summary>Where the peek camera should sit — just above the crow.</summary>
    public Vector3 EyePosition => transform.position + Vector3.up * 0.25f;

    Vector3 flyStart, flyTarget;
    float flyT;      // 0..1 along the current flight
    float flyDist;
    float scanSeed;
    float stepReadyAt;   // unscaled time when shadow step is next available
    bool peekLatch;      // after a step, swallow the held peek until it's released once

    void Awake()
    {
        if (player == null)
        {
            var tc = FindFirstObjectByType<TalonController>();
            if (tc != null) player = tc.transform;
        }
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        scanSeed = 0f;
    }

    void Update()
    {
        // The crow is attention — it moves at thought-speed, immune to the world's
        // slow-mo. Everything in here runs on UNSCALED time.
        float dt = Time.unscaledDeltaTime;
        ReadInputs();
        UpdateTimeDilation(dt);

        switch (State)
        {
            case CrowState.Follow:
                if (player != null)
                {
                    // Hover near the shoulder: offset in the player's frame + a gentle bob.
                    Vector3 goal = player.position
                                 + player.right * followOffset.x
                                 + Vector3.up * followOffset.y
                                 + player.forward * followOffset.z;
                    goal += Vector3.up * (Mathf.Sin(Time.unscaledTime * bobFrequency) * bobAmplitude);
                    transform.position = Vector3.Lerp(transform.position, goal, followLerp * dt);
                    // Look where the player looks — it's their attention, after all.
                    FaceDirection(player.forward, 8f, dt);
                }
                break;

            case CrowState.FlyTo:
                flyT += dt * (flySpeed / Mathf.Max(flyDist, 0.01f));
                float t = Mathf.Clamp01(flyT);
                Vector3 pos = Vector3.Lerp(flyStart, flyTarget, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;   // simple arc
                Vector3 vel = pos - transform.position;
                transform.position = pos;
                if (vel.sqrMagnitude > 0.0001f) FaceDirection(vel.normalized, 12f, dt);
                if (t >= 1f) { State = CrowState.Perched; scanSeed = Time.unscaledTime; }
                break;

            case CrowState.Perched:
                // Slow scanning sweep while perched — it's watching for you.
                float scan = Mathf.Sin((Time.unscaledTime - scanSeed) * 0.7f) * 60f;
                Quaternion baseRot = Quaternion.LookRotation(FlatToPlayer(), Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    baseRot * Quaternion.Euler(0f, scan, 0f), 3f * dt);
                break;
        }
    }

    void UpdateTimeDilation(float unscaledDt)
    {
        // While peeking the world crawls; the moment you return it breathes back to 1.
        float goal = PeekHeld ? peekTimeScale : 1f;
        float ts = Mathf.MoveTowards(Time.timeScale, goal, timeScaleLerp * unscaledDt);
        Time.timeScale = ts;
        Time.fixedDeltaTime = 0.02f * ts; // keep physics stepping in sync with the dilation
    }

    /// <summary>Shadow step is available: crow perched and off cooldown.</summary>
    public bool ShadowStepReady => State == CrowState.Perched && Time.unscaledTime >= stepReadyAt;

    /// <summary>
    /// Consume the perch and hand back the landing point beneath it. The crow breaks
    /// to Follow — you can't keep watching from the place you're now standing.
    /// </summary>
    public bool TryShadowStep(out Vector3 landing)
    {
        landing = default(Vector3);
        if (!ShadowStepReady) return false;
        Vector3 p = transform.position;
        if (Physics.Raycast(p, Vector3.down, out RaycastHit hit, 40f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            landing = hit.point;
        else
            landing = new Vector3(p.x, 0f, p.z);
        stepReadyAt = Time.unscaledTime + shadowStepCooldown;
        State = CrowState.Follow;
        peekLatch = true;
        return true;
    }

    /// <summary>True when the current view aims at a valid perch (drives the reticle).</summary>
    public bool HasPerchTarget()
    {
        Transform view = cameraTransform != null ? cameraTransform : transform;
        if (Physics.Raycast(view.position, view.forward, out RaycastHit hit, sendRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return player == null || hit.transform.root != player.root;
        return false;
    }

    void ReadInputs()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        var mouse = Mouse.current;

        // Peek: HOLD right mouse / left trigger. Release always returns — the pulse.
        bool peek = false;
        if (mouse != null && mouse.rightButton.isPressed) peek = true;
        if (gp != null && gp.leftTrigger.ReadValue() > 0.4f) peek = true;
        // After a shadow step the view must come home even if the trigger is still
        // held — the latch eats the input until the player releases once.
        if (peekLatch)
        {
            if (!peek) peekLatch = false;
            peek = false;
        }
        PeekHeld = peek;

        // Send: tap E / RB — raycast through the current view (works from a peek too:
        // you can hop the crow perch-to-perch while extended).
        bool send = (kb != null && kb.eKey.wasPressedThisFrame)
                 || (gp != null && gp.rightShoulder.wasPressedThisFrame);
        if (send) TrySend();

        // Recall: tap Q / LB.
        bool recall = (kb != null && kb.qKey.wasPressedThisFrame)
                   || (gp != null && gp.leftShoulder.wasPressedThisFrame);
        if (recall) State = CrowState.Follow;
    }

    void TrySend()
    {
        Transform view = cameraTransform != null ? cameraTransform : transform;
        // Ignore triggers; the crow perches on world geometry.
        if (Physics.Raycast(view.position, view.forward, out RaycastHit hit, sendRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // Don't perch on the player.
            if (player != null && hit.transform.root == player.root) return;
            flyStart = transform.position;
            flyTarget = hit.point + hit.normal * 0.3f;
            flyDist = Vector3.Distance(flyStart, flyTarget);
            flyT = 0f;
            State = CrowState.FlyTo;
        }
        // No hit → no perch. (Recall is Q/LB, deliberate rather than accidental.)
    }

    void FaceDirection(Vector3 dir, float speed, float dt)
    {
        dir.y *= 0.4f; // crows bank, they don't pitch straight down
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir.normalized, Vector3.up), speed * dt);
    }

    Vector3 FlatToPlayer()
    {
        if (player == null) return transform.forward;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude < 0.0001f ? transform.forward : d.normalized;
    }
}
