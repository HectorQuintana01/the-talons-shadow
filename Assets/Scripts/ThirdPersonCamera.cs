using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple orbit-follow third-person camera (no Cinemachine dependency).
/// Mouse or right stick orbits the target; the camera holds a fixed distance behind it.
/// This is the seam the CROW will hook into on day 3 — "see through the crow's eyes"
/// just means swapping/blending this target to the crow.
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
        Cursor.lockState = CursorLockMode.Locked; // WebGL engages this on first click
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Time.deltaTime;

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
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + Vector3.up * height;
        Vector3 desired = pivot - rot * Vector3.forward * distance;

        transform.position = Vector3.Lerp(transform.position, desired, followLerp * dt);
        transform.rotation = rot;
    }
}
