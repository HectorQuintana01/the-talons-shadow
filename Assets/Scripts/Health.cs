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

    /// <summary>When true, damage is swallowed (the Warden's guard). Set by BossWarden.</summary>
    public bool guardActive = false;

    public float Current { get; private set; }
    public System.Action<Health> Died;   // optional hook (GameLoop uses this on day 5)

    TalonController talon;               // only present on the player
    Vector3 spawnPos;
    Quaternion spawnRot;
    float hurtCooldown;                  // brief grace after a hit so contact damage can't melt

    // Hit flash (juice): renderers blink white for a beat when damage lands.
    Renderer[] renderers;
    MaterialPropertyBlock mpb;
    float flashTimer;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        Current = maxHealth;
        talon = GetComponent<TalonController>();
        spawnPos = transform.position;
        spawnRot = transform.rotation;
        renderers = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (hurtCooldown > 0f) hurtCooldown -= Time.deltaTime;

        // Hit flash decay (unscaled so it reads through hitstop/slow-mo).
        if (flashTimer > 0f)
        {
            flashTimer -= Time.unscaledDeltaTime;
            if (flashTimer <= 0f) SetFlash(false);
        }

        // Kill floor: anything that escapes the arena (shadow-stepping onto a
        // boundary wall and hopping off, or any future noclip hole) comes home
        // instead of falling forever. The Dead Eddie back-wall lesson.
        if (isPlayer && transform.position.y < -10f) Respawn();
    }

    void SetFlash(bool on)
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (on)
            {
                mpb.SetColor(BaseColorId, Color.white * 2.5f);
                r.SetPropertyBlock(mpb);
            }
            else r.SetPropertyBlock(null);
        }
    }

    public void TakeDamage(float amount)
    {
        if (hurtCooldown > 0f) return;
        if (talon != null && talon.IsInvulnerable) return; // dash i-frames are real now
        if (guardActive)                                   // the Warden's guard: crow breaks it
        {
            Sfx.Play("strike_whiff", transform.position, 0.35f); // "clang" — no damage
            flashTimer = 0.05f; SetFlash(true);
            return;
        }

        Current -= amount;
        hurtCooldown = isPlayer ? 0.6f : 0.1f;

        // Feedback: everyone flashes; the player's hits also rock the camera.
        flashTimer = 0.09f;
        SetFlash(true);
        if (isPlayer)
        {
            ThirdPersonCamera.Shake(0.16f);
            Sfx.Play("hurt", transform.position);
        }

        if (Current <= 0f)
        {
            Died?.Invoke(this);
            if (isPlayer) Respawn();
            else
            {
                Sfx.Play("enemy_death", transform.position);
                Destroy(gameObject);
            }
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
