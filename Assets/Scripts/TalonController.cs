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

    public bool IsDashing => dashTimer > 0f;
    public bool IsInvulnerable => IsDashing; // i-frames during the dash

    CrowCompanion crow; // day 3: while peeking through the crow, the body stands still

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Start()
    {
        crow = FindFirstObjectByType<CrowCompanion>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
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

        // Trigger a dash (commit to a direction: current input, else current facing).
        cooldownTimer -= dt;
        if (!extended && DashPressed() && dashTimer <= 0f && cooldownTimer <= 0f)
        {
            dashDir = wishDir.sqrMagnitude > 0.01f ? wishDir.normalized : transform.forward;
            dashTimer = dashDuration;
            cooldownTimer = dashCooldown;
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
    }

    void DoStrike()
    {
        // Overlap a sphere in front of the chest; damage everything with Health
        // that isn't us. Enemies are 3 HP, strikes are 1 — three committed swings.
        Vector3 point = transform.position + Vector3.up * 0.9f + transform.forward * strikeReach;
        foreach (var col in Physics.OverlapSphere(point, strikeRadius,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var h = col.GetComponentInParent<Health>();
            if (h != null && !h.isPlayer) h.TakeDamage(strikeDamage);
        }
    }

    bool StrikePressed()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
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
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame)) return true;
        var gp = Gamepad.current;
        // B only — RB now belongs to the crow send (day 3 input map).
        if (gp != null && gp.buttonEast.wasPressedThisFrame) return true;
        return false;
    }
}
