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
    [Tooltip("How far the CROW can fly per send — from its current position to the perch. The Ascent's ladder rule: altitude is earned one hop at a time.")]
    public float sendRange = 22f;
    [Tooltip("How far the view ray probes for perch candidates (can exceed sendRange — that's how the reticle knows a perch is 'too far').")]
    public float probeRange = 60f;
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
    float peekLockedUntil; // unscaled: pain locks out re-extension briefly (rattled)

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
        // Gated by game state: on title/pause GameLoop owns Time.timeScale and
        // this script must not fight it (it's the only other timeScale writer).
        if (!GameLoop.IsPlaying) { PeekHeld = false; return; }
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

    // --- Hitstop (juice): a single writer owns Time.timeScale, so hitstop lives
    // here and composes with the peek dilation instead of fighting it. ---
    static float hitstopUntil;
    public static void RequestHitstop(float seconds)
    {
        hitstopUntil = Mathf.Max(hitstopUntil, Time.unscaledTime + seconds);
    }

    /// <summary>Statics survive scene reloads — GameLoop.Awake clears them.</summary>
    public static void ResetJuice() { hitstopUntil = 0f; }

    void UpdateTimeDilation(float unscaledDt)
    {
        // While peeking the world crawls; the moment you return it breathes back to 1.
        float goal = PeekHeld ? peekTimeScale : 1f;
        float ts;
        if (Time.unscaledTime < hitstopUntil)
            ts = 0.05f; // the hit lands, the world holds its breath
        else
            ts = Mathf.MoveTowards(Time.timeScale, goal, timeScaleLerp * unscaledDt);
        Time.timeScale = ts;
        Time.fixedDeltaTime = 0.02f * ts; // keep physics stepping in sync with the dilation
    }

    /// <summary>
    /// Pain rips your attention home: getting hit while extended breaks the peek
    /// instantly (the held trigger is swallowed until released) and locks
    /// re-extension for a beat — you're rattled. Enemies can interrupt the flow.
    /// </summary>
    public void ForceReturn(float lockout = 0.6f)
    {
        if (PeekHeld) ThirdPersonCamera.Shake(0.12f); // the snap-back sting
        PeekHeld = false;
        peekLatch = true;
        peekLockedUntil = Time.unscaledTime + lockout;
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
        {
            landing = hit.point;
            // Center-snap: on small platforms (pillar tops) you land planted in the
            // middle, replacing the crow — not teetering on whatever rim the ray hit.
            // Long thin surfaces (wall tops) center you across the thin axis only.
            Bounds b = hit.collider.bounds;
            if (b.extents.x < 1.5f) landing.x = b.center.x;
            if (b.extents.z < 1.5f) landing.z = b.center.z;
        }
        else
            landing = new Vector3(p.x, 0f, p.z);
        stepReadyAt = Time.unscaledTime + shadowStepCooldown;
        State = CrowState.Follow;
        peekLatch = true;
        return true;
    }

    /// <summary>Tri-state aim result: what the reticle shows and what send will do.</summary>
    public enum PerchAim { None, Valid, TooFar }

    /// <summary>True when the current view aims at a REACHABLE perch (drives send).
    /// Same resolver as TrySend — the reticle can never lie.</summary>
    public bool HasPerchTarget()
    {
        Vector3 p;
        return AimPerch(out p) == PerchAim.Valid;
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
        if (Time.unscaledTime < peekLockedUntil) peek = false; // still rattled
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
        Vector3 perch;
        if (AimPerch(out perch) != PerchAim.Valid) return; // none or too far → refuse
        flyStart = transform.position;
        flyTarget = perch;
        flyDist = Vector3.Distance(flyStart, flyTarget);
        flyT = 0f;
        State = CrowState.FlyTo;
        Sfx.Play("caw", transform.position, 0.7f);
    }

    /// <summary>
    /// THE one perch resolver — send and reticle both use it, so what glows gold
    /// is always exactly what the crow will do. "Perch platforms" by geometry:
    /// - the ray ignores the player (fixes the reticle dying while standing still)
    /// - upward faces (normal.y > 0.6) are perches
    /// - side hits EDGE-SNAP onto the top of the thing that was hit
    /// - anything else refuses the crow
    /// THE ASCENT RULE (v2): the view PROBES far (probeRange) but the crow only
    /// FLIES sendRange from its own body — a visible perch beyond its wings
    /// returns TooFar (red reticle). Altitude is earned one hop at a time.
    /// Perching on tops also guarantees Shadow Step lands the player ON surfaces.
    /// </summary>
    public PerchAim AimPerch(out Vector3 perch)
    {
        perch = default(Vector3);
        // Heal the camera reference — after a scene reload (R restart) Awake can
        // run while the old camera is being destroyed, leaving this null and the
        // crow aiming from its own body. Never trust it blindly.
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform == null) return PerchAim.None; // no camera, no aim
        Transform view = cameraTransform;
        var hits = Physics.RaycastAll(view.position, view.forward, probeRange,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return PerchAim.None;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (player != null && hit.transform.root == player.root) continue; // see past yourself
            if (hit.transform.root == transform.root) continue;                // and past the crow

            if (hit.normal.y > 0.6f)
            {
                perch = hit.point + hit.normal * 0.3f;
                return Classify(perch);
            }

            // Edge check: hit a side — probe down onto the TOP of what we hit,
            // slightly inset so the probe lands on the surface, not the rim.
            Bounds b = hit.collider.bounds;
            Vector3 flatNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            Vector3 inset = hit.point - (flatNormal.sqrMagnitude > 0.001f
                ? flatNormal.normalized * 0.45f : Vector3.zero);
            Vector3 probe = new Vector3(inset.x, b.max.y + 0.6f, inset.z);
            RaycastHit topHit;
            if (Physics.Raycast(probe, Vector3.down, out topHit, 1.5f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && topHit.normal.y > 0.6f
                && (player == null || topHit.transform.root != player.root))
            {
                perch = topHit.point + Vector3.up * 0.3f;
                return Classify(perch);
            }
            return PerchAim.None; // first real obstacle wasn't perchable — view blocked
        }
        return PerchAim.None;
    }

    PerchAim Classify(Vector3 perch)
    {
        // Reach is measured from the CROW — it's the bird that has to fly there.
        return Vector3.Distance(transform.position, perch) <= sendRange
            ? PerchAim.Valid : PerchAim.TooFar;
    }

    /// <summary>Back-compat wrapper (Shadow Step callers etc.).</summary>
    public bool ResolvePerch(out Vector3 perch)
    {
        return AimPerch(out perch) == PerchAim.Valid;
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
