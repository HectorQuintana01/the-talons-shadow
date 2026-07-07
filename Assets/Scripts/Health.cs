using UnityEngine;

/// <summary>
/// One health component for everyone — player and enemies (SOP: one system, reused).
/// - Player: respawns at its start position on death (fade/polish is day 6).
/// - Enemies: destroyed on death (effects are day 6).
/// Dash i-frames are honored here: damage is swallowed while TalonController.IsInvulnerable.
/// </summary>
public class Health : MonoBehaviour
{
    public float maxHealth = 3f;
    public bool isPlayer = false;

    public float Current { get; private set; }
    public System.Action<Health> Died;   // optional hook (GameLoop uses this on day 5)

    TalonController talon;               // only present on the player
    Vector3 spawnPos;
    Quaternion spawnRot;
    float hurtCooldown;                  // brief grace after a hit so contact damage can't melt

    void Awake()
    {
        Current = maxHealth;
        talon = GetComponent<TalonController>();
        spawnPos = transform.position;
        spawnRot = transform.rotation;
    }

    void Update()
    {
        if (hurtCooldown > 0f) hurtCooldown -= Time.deltaTime;

        // Kill floor: anything that escapes the arena (shadow-stepping onto a
        // boundary wall and hopping off, or any future noclip hole) comes home
        // instead of falling forever. The Dead Eddie back-wall lesson.
        if (isPlayer && transform.position.y < -10f) Respawn();
    }

    public void TakeDamage(float amount)
    {
        if (hurtCooldown > 0f) return;
        if (talon != null && talon.IsInvulnerable) return; // dash i-frames are real now

        Current -= amount;
        hurtCooldown = isPlayer ? 0.6f : 0.1f;

        if (Current <= 0f)
        {
            Died?.Invoke(this);
            if (isPlayer) Respawn();
            else Destroy(gameObject);
        }
    }

    void Respawn()
    {
        // CharacterController must be disabled to teleport, or it fights the move.
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(spawnPos, spawnRot);
        if (cc != null) cc.enabled = true;
        Current = maxHealth;
        hurtCooldown = 1.5f; // spawn grace
    }
}
