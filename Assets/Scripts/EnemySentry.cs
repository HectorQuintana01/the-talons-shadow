using UnityEngine;

/// <summary>
/// Sentry — a stationary turret with a visible aim line. Tracks the player when
/// in range with line of sight, winds up (line heats to red), and fires a bolt.
/// It's the reason cover and the crow matter: a perched crow nearby pulls its
/// aim (it tracks the crow instead — wasted shots), opening your approach.
/// Runs on SCALED time: during a peek its windup crawls — read it, then move.
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemySentry : MonoBehaviour
{
    [Header("Attack")]
    public float range = 22f;
    public float windupTime = 1.4f;
    public float refireDelay = 1.2f;
    public float boltDamage = 15f;
    public float boltSpeed = 18f;

    [Header("Crow distraction")]
    public float distractRadius = 12f;
    public float distractDuration = 3.5f;

    [Header("Wiring (auto-built if empty)")]
    public Transform muzzle;             // where bolts leave; defaults to top of the body

    Transform player;
    CrowCompanion crow;
    LineRenderer aimLine;
    float windup;                        // 0..windupTime while locked on
    float refire;
    float distractedUntil;
    bool crowWasPerched;

    public bool IsDistracted => Time.time < distractedUntil;

    void Start()
    {
        var tc = FindFirstObjectByType<TalonController>();
        if (tc != null) player = tc.transform;
        crow = FindFirstObjectByType<CrowCompanion>();
        if (muzzle == null) muzzle = transform;

        aimLine = GetComponent<LineRenderer>();
        if (aimLine == null) aimLine = gameObject.AddComponent<LineRenderer>();
        aimLine.positionCount = 2;
        aimLine.startWidth = 0.05f;
        aimLine.endWidth = 0.02f;
        aimLine.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        aimLine.enabled = false;
    }

    void Update()
    {
        if (!GameLoop.IsPlaying) return; // no winding up through the pause card
        if (player == null) return;
        refire -= Time.deltaTime;

        // Crow landing nearby yanks the sentry's attention (same edge-trigger as Stalker).
        bool perchedNow = crow != null && crow.IsPerched;
        if (perchedNow && !crowWasPerched &&
            Vector3.Distance(transform.position, crow.transform.position) <= distractRadius)
        {
            distractedUntil = Time.time + distractDuration;
            windup = 0f; // losing focus resets the shot
        }
        crowWasPerched = perchedNow;

        Transform mark = IsDistracted && crow != null ? crow.transform : player;
        Vector3 aimPoint = mark.position + Vector3.up * 0.6f;
        float dist = Vector3.Distance(muzzle.position, aimPoint);
        bool hasLos = dist <= range && HasLineOfSight(aimPoint, mark);

        if (!hasLos)
        {
            windup = Mathf.Max(0f, windup - Time.deltaTime * 2f);
            aimLine.enabled = false;
            return;
        }

        // Track and show the aim line, heating from gold to red as the shot charges.
        aimLine.enabled = true;
        aimLine.SetPosition(0, muzzle.position);
        aimLine.SetPosition(1, aimPoint);
        windup += Time.deltaTime;
        float heat = Mathf.Clamp01(windup / windupTime);
        Color c = Color.Lerp(new Color(1f, 0.8f, 0.3f, 0.5f), new Color(1f, 0.15f, 0.1f, 0.9f), heat);
        aimLine.startColor = c;
        aimLine.endColor = c;

        if (windup >= windupTime && refire <= 0f)
        {
            Fire(aimPoint);
            windup = 0f;
            refire = refireDelay;
        }
    }

    bool HasLineOfSight(Vector3 aimPoint, Transform mark)
    {
        Vector3 dir = aimPoint - muzzle.position;
        if (Physics.Raycast(muzzle.position, dir.normalized, out RaycastHit hit, dir.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.transform == mark || hit.transform.root == mark.root;
        return true;
    }

    void Fire(Vector3 aimPoint)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "SentryBolt";
        go.transform.position = muzzle.position;
        go.transform.localScale = Vector3.one * 0.25f;
        Destroy(go.GetComponent<Collider>()); // bolt does its own proximity check
        var bolt = go.AddComponent<SentryBolt>();
        bolt.Init((aimPoint - muzzle.position).normalized, boltSpeed, boltDamage);
        Sfx.Play("bolt", muzzle.position, 0.7f);
    }
}
