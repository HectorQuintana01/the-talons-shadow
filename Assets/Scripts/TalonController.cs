using UnityEngine;
using UnityEngine.InputSystem; // Project is set to the New Input System (activeInputHandler=1)

/// <summary>
/// Third-person, camera-relative movement for TalonRogue.
/// The BODY verb — "commit": grounded run plus a Talon Dash (a quick dodge burst
/// with brief invulnerability we can read from enemies/the crow later).
/// Reads BOTH keyboard (WASD + Space/Shift) and gamepad (left stick + A/RB),
/// so Hector can play it on a controller.
/// (Named TalonController, not PlayerController, to avoid a name clash with a
/// PlayerController type inside Unity.VisualScripting.DocCodeExamples.)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class TalonController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;
    public float rotationLerp = 15f;   // how fast the body swings to face travel direction
    public float gravity = -20f;

    [Header("Talon Dash")]
    public float dashSpeed = 22f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.55f;

    [Header("Jump")]
    public float jumpHeight = 1.6f;      // peak height in world units
    public float coyoteTime = 0.12f;     // grace to still jump just after leaving a ledge
    public float jumpBuffer = 0.1f;      // press-before-landing forgiveness

    [Header("Talon Strike (melee)")]
    public float strikeDamage = 1f;      // enemies have 3 HP → 3 clean hits
    public float strikeRadius = 0.95f;   // overlap sphere around the strike point
    public float strikeReach = 1.15f;    // how far in front the sphere sits
    public float strikeCooldown = 0.45f;
    public float lungeSpeed = 10f;       // short forward carry so hits feel committed
    public float lungeDuration = 0.12f;

    [Header("Refs")]
    public Transform cameraTransform;  // auto-filled from Camera.main if left empty

    CharacterController cc;
    Animator animator;
    float verticalVel;                 // gravity accumulator on the Y axis
    float dashTimer;                   // counts down while dashing
    float cooldownTimer;
    Vector3 dashDir;
    float lungeTimer;                  // counts down during a strike's forward carry
    float strikeCdTimer;
    float strikeAnimTimer;             // brief animator speed-up so the hit reads
    float arrivalGrace;                // shadow-step landing: i-frames, no slide
    float landLockTimer;               // brief input beat on landing so a held stick
                                       // can't walk you straight off the perch
    float coyoteTimer;                 // >0 while a just-left ledge still allows a jump
    float jumpBufferTimer;             // >0 while a recent jump press waits for the ground

    public bool IsDashing => dashTimer > 0f;
    public bool IsInvulnerable => IsDashing || arrivalGrace > 0f; // dash + step-arrival i-frames

    CrowCompanion crow; // day 3: while peeking through the crow, the body stands still
    TrailRenderer trail; // juice: emits only while dashing / arriving from a step

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        trail = GetComponentInChildren<TrailRenderer>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Start()
    {
        crow = FindFirstObjectByType<CrowCompanion>();
    }

    void Update()
    {
        if (!GameLoop.IsPlaying) return; // title/pause/win: the body waits
        float dt = Time.deltaTime;
        // Heal refs lost to scene reloads (same reason as CrowCompanion.ResolvePerch).
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (crow == null) crow = FindFirstObjectByType<CrowCompanion>();
        // Extending your awareness costs your presence: while peeking through the
        // crow, the body is rooted (and vulnerable). Gravity still applies below.
        bool extended = crow != null && crow.PeekHeld;
        Vector2 moveInput = extended ? Vector2.zero : ReadMove();

        // Camera-relative direction, flattened onto the ground plane.
        Vector3 camF = cameraTransform ? cameraTransform.forward : Vector3.forward;
        Vector3 camR = cameraTransform ? cameraTransform.right : Vector3.right;
        camF.y = 0f; camR.y = 0f; camF.Normalize(); camR.Normalize();
        Vector3 wishDir = camF * moveInput.y + camR * moveInput.x;
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        // SHADOW STEP: dash pressed while extended into a perched crow — become
        // where your attention is. Consumes the perch. You arrive PLANTED: i-frames
        // without momentum (the old dash-slide launched you off pillar tops), and a
        // one-beat input lock so the stick you were holding can't walk you off.
        if (extended && crow != null && DashPressed())
        {
            Vector3 landing;
            if (crow.TryShadowStep(out landing))
            {
                cc.enabled = false; // CharacterController fights teleports unless disabled
                transform.position = landing + Vector3.up * 0.05f;
                cc.enabled = true;
                verticalVel = -2f;
                dashTimer = 0f;
                lungeTimer = 0f;
                arrivalGrace = 0.35f;
                landLockTimer = 0.12f;
                Sfx.Play("step", landing);
            }
        }
        arrivalGrace -= dt;
        landLockTimer -= dt;
        if (landLockTimer > 0f) wishDir = Vector3.zero; // the landing beat

        // Trigger a dash (commit to a direction: current input, else current facing).
        cooldownTimer -= dt;
        if (!extended && DashPressed() && dashTimer <= 0f && cooldownTimer <= 0f)
        {
            dashDir = wishDir.sqrMagnitude > 0.01f ? wishDir.normalized : transform.forward;
            dashTimer = dashDuration;
            cooldownTimer = dashCooldown;
            Sfx.Play("dash", transform.position, 0.6f);
        }

        // Talon Strike: commit the body forward and swing (priority: dash > lunge > run).
        strikeCdTimer -= dt;
        strikeAnimTimer -= dt;
        if (!extended && StrikePressed() && strikeCdTimer <= 0f && dashTimer <= 0f)
        {
            strikeCdTimer = strikeCooldown;
            lungeTimer = lungeDuration;
            strikeAnimTimer = 0.25f;
            DoStrike();
        }

        // Horizontal velocity: the dash overrides normal movement while it lasts.
        Vector3 horizontal;
        if (dashTimer > 0f)
        {
            dashTimer -= dt;
            horizontal = dashDir * dashSpeed;
        }
        else if (lungeTimer > 0f)
        {
            lungeTimer -= dt;
            horizontal = transform.forward * lungeSpeed;
        }
        else
        {
            horizontal = wishDir * moveSpeed;
        }

        // Jump — with coyote time (jump just after leaving a ledge) and a small
        // input buffer (press just before landing still fires). Same forgiveness
        // philosophy as the perch landing: flow over frame-perfect timing.
        // Rooted while extended into the crow.
        coyoteTimer = cc.isGrounded ? coyoteTime : coyoteTimer - dt;
        jumpBufferTimer -= dt;
        if (!extended && JumpPressed()) jumpBufferTimer = jumpBuffer;
        if (jumpBufferTimer > 0f && coyoteTimer > 0f && dashTimer <= 0f)
        {
            verticalVel = Mathf.Sqrt(2f * -gravity * jumpHeight); // exact height from gravity
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        // Gravity so the CharacterController stays pinned to the ground.
        if (cc.isGrounded && verticalVel < 0f) verticalVel = -2f;
        verticalVel += gravity * dt;

        Vector3 motion = horizontal;
        motion.y = verticalVel;
        cc.Move(motion * dt);

        // Turn the body toward where it's going (Elden-Ring style free movement).
        Vector3 faceDir = dashTimer > 0f ? dashDir : wishDir;
        if (faceDir.sqrMagnitude > 0.01f)
        {
            Quaternion goal = Quaternion.LookRotation(faceDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, goal, rotationLerp * dt);
        }

        // Animation: the FBX only has a run clip, so freeze it when idle and
        // scale playback with speed. Reads as run-when-moving, still-when-stopped.
        if (animator != null)
        {
            float planarSpeed = new Vector2(horizontal.x, horizontal.z).magnitude;
            animator.speed = strikeAnimTimer > 0f ? 1.8f
                : IsDashing ? 1.6f
                : Mathf.Clamp01(planarSpeed / moveSpeed);
        }

        // Dash trail: visible only while committed (dash or step arrival).
        if (trail != null) trail.emitting = IsDashing || arrivalGrace > 0f;
    }

    void DoStrike()
    {
        // Overlap a sphere in front of the chest; damage everything with Health
        // that isn't us. Enemies are 3 HP, strikes are 1 — three committed swings.
        Vector3 point = transform.position + Vector3.up * 0.9f + transform.forward * strikeReach;
        int hits = 0;
        foreach (var col in Physics.OverlapSphere(point, strikeRadius,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var h = col.GetComponentInParent<Health>();
            if (h != null && !h.isPlayer) { h.TakeDamage(strikeDamage); hits++; }
        }
        if (hits > 0)
        {
            // Connection juice: the world holds its breath, the camera kicks.
            CrowCompanion.RequestHitstop(0.055f);
            ThirdPersonCamera.Shake(0.09f);
            Sfx.Play("strike_hit", point);
        }
        else Sfx.Play("strike_whiff", point, 0.45f);
    }

    bool StrikePressed()
    {
        var mouse = Mouse.current;
        // A click while the cursor is unlocked is the pointer-lock RECAPTURE
        // click (browser stole the lock) — it must not also swing the talons.
        if (mouse != null && mouse.leftButton.wasPressedThisFrame
            && Cursor.lockState == CursorLockMode.Locked) return true;
        var gp = Gamepad.current;
        if (gp != null && gp.buttonWest.wasPressedThisFrame) return true; // X
        return false;
    }

    Vector2 ReadMove()
    {
        Vector2 v = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) v.y += 1f;
            if (kb.sKey.isPressed) v.y -= 1f;
            if (kb.dKey.isPressed) v.x += 1f;
            if (kb.aKey.isPressed) v.x -= 1f;
        }
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 stick = gp.leftStick.ReadValue(); // stick already has a deadzone processor
            if (stick.sqrMagnitude > v.sqrMagnitude) v = stick; // prefer the stick when tilted
        }
        return v;
    }

    bool DashPressed()
    {
        // Shift only on keyboard now — Space became Jump (day-6 map).
        var kb = Keyboard.current;
        if (kb != null && kb.leftShiftKey.wasPressedThisFrame) return true;
        var gp = Gamepad.current;
        // B (East) — RB is crow send, A (South) is Jump.
        if (gp != null && gp.buttonEast.wasPressedThisFrame) return true;
        return false;
    }

    bool JumpPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
        var gp = Gamepad.current;
        if (gp != null && gp.buttonSouth.wasPressedThisFrame) return true; // A
        return false;
    }
}
