using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple orbit-follow third-person camera (no Cinemachine dependency).
/// Mouse or right stick orbits the target; the camera holds a fixed distance behind it.
/// CROW PEEK (day 3): while the peek input is held, the camera blends out to the
/// crow's eyes and becomes a free-look first-person view from its perch — extend
/// and return, like a pulse. Extending is slightly slower than returning
/// (the inhale is sharper than the exhale). No mode, no UI: hold = out, release = back.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;               // the player
    public float distance = 6f;
    public float height = 1.5f;            // aim point above the target's feet (chest/head)
    public float mouseSensitivity = 0.12f;
    public float stickSensitivity = 180f;  // degrees per second
    public float minPitch = -15f;
    public float maxPitch = 65f;
    public float followLerp = 20f;

    [Header("Crow peek")]
    public float peekDistance = 0.15f;     // ~first-person from the crow
    public float peekFov = 72f;            // slight widen: awareness opens up
    public float extendTime = 0.25f;       // body -> crow
    public float returnTime = 0.15f;       // crow -> body (snappier)
    public float peekMaxPitch = 80f;       // perched high, you want to look down

    CrowCompanion crow;                    // auto-found; peek input lives there
    Camera cam;
    float baseFov;
    float peekBlend;                       // 0 = body view, 1 = crow view

    float yaw;
    float pitch = 20f;

    void Start()
    {
        if (target == null)
        {
            var pc = FindFirstObjectByType<TalonController>();
            if (pc != null) target = pc.transform;
        }
        if (target != null) yaw = target.eulerAngles.y;
        crow = FindFirstObjectByType<CrowCompanion>();
        cam = GetComponent<Camera>();
        baseFov = cam != null ? cam.fieldOfView : 60f;
        Cursor.lockState = CursorLockMode.Locked; // WebGL engages this on first click
    }

    void LateUpdate()
    {
        if (target == null) return;
        // Unscaled: the camera is part of your attention, not the world — it stays
        // fully responsive during the crow's slow-mo.
        float dt = Time.unscaledDeltaTime;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * mouseSensitivity;
            pitch -= d.y * mouseSensitivity;
        }
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 look = gp.rightStick.ReadValue();
            yaw += look.x * stickSensitivity * dt;
            pitch -= look.y * stickSensitivity * dt;
        }

        // The pulse: drive the blend toward 1 while peek is held, back to 0 on release.
        bool peeking = crow != null && crow.PeekHeld;
        float rate = peeking ? (1f / Mathf.Max(extendTime, 0.01f))
                             : (1f / Mathf.Max(returnTime, 0.01f));
        peekBlend = Mathf.MoveTowards(peekBlend, peeking ? 1f : 0f, rate * dt);

        // From a perch you need to look further down than from behind the body.
        float maxP = Mathf.Lerp(maxPitch, peekMaxPitch, peekBlend);
        pitch = Mathf.Clamp(pitch, minPitch, maxP);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 bodyPivot = target.position + Vector3.up * height;
        Vector3 pivot = crow != null
            ? Vector3.Lerp(bodyPivot, crow.EyePosition, peekBlend)
            : bodyPivot;
        float dist = Mathf.Lerp(distance, peekDistance, peekBlend);
        Vector3 desired = pivot - rot * Vector3.forward * dist;

        transform.position = Vector3.Lerp(transform.position, desired, followLerp * dt);
        transform.rotation = rot;

        if (cam != null)
            cam.fieldOfView = Mathf.Lerp(baseFov, peekFov, peekBlend);
    }
}
