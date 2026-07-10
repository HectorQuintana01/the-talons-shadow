using UnityEngine;

/// <summary>
/// Minimal immediate-mode HUD: a center reticle.
/// - Dim dot: neutral aim.
/// - Bright dot + ring: the view is on a valid crow perch (send will land).
/// Drawn via OnGUI so there's no Canvas plumbing — plenty for the jam, and it
/// works identically in the editor and the WebGL build.
/// </summary>
public class TalonHUD : MonoBehaviour
{
    public Color neutralColor = new Color(1f, 1f, 1f, 0.35f);
    public Color perchColor = new Color(1f, 0.85f, 0.3f, 0.95f); // gold: "the crow can go there"
    public float dotSize = 5f;
    public float ringSize = 22f;
    public float ringThickness = 2f;

    CrowCompanion crow;
    Health playerHealth;
    Texture2D px; // 1x1 white — tinted via GUI.color

    // Distraction tells: "!" floats over enemies whose attention the crow stole.
    EnemyStalker[] stalkers = new EnemyStalker[0];
    EnemySentry[] sentries = new EnemySentry[0];
    float enemyRefreshAt;

    void Start()
    {
        crow = FindFirstObjectByType<CrowCompanion>();
        var tc = FindFirstObjectByType<TalonController>();
        if (tc != null) playerHealth = tc.GetComponent<Health>();
        px = new Texture2D(1, 1);
        px.SetPixel(0, 0, Color.white);
        px.Apply();
    }

    void RefreshEnemyCache()
    {
        // Enemies die (Destroy) — re-scan on a slow cadence rather than per frame.
        if (Time.unscaledTime < enemyRefreshAt) return;
        enemyRefreshAt = Time.unscaledTime + 1.5f;
        stalkers = FindObjectsByType<EnemyStalker>(FindObjectsSortMode.None);
        sentries = FindObjectsByType<EnemySentry>(FindObjectsSortMode.None);
    }

    void DrawDistractTell(Transform t, Camera cam, GUIStyle style)
    {
        Vector3 sp = cam.WorldToScreenPoint(t.position + Vector3.up * 1.6f);
        if (sp.z <= 0f) return; // behind the camera
        GUI.Label(new Rect(sp.x - 20f, Screen.height - sp.y - 20f, 40f, 40f), "!", style);
    }

    BossWarden boss;
    float bossRefreshAt;

    void DrawBossBar()
    {
        if (Time.unscaledTime >= bossRefreshAt) { boss = FindFirstObjectByType<BossWarden>(); bossRefreshAt = Time.unscaledTime + 1f; }
        if (boss == null || !boss.Alive) return;
        var bh = boss.GetComponent<Health>();
        if (bh == null) return;

        float w = Screen.width * 0.5f, h = 18f, x = (Screen.width - w) * 0.5f, y = 24f;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x - 3, y - 3, w + 6, h + 6), px);
        float frac = Mathf.Clamp01(bh.Current / bh.maxHealth);
        // Guarded = steely blue (can't hurt it); guard broken = hot red (strike now!).
        GUI.color = bh.guardActive
            ? new Color(0.35f, 0.45f, 0.7f, 0.9f)
            : new Color(0.9f, 0.2f, 0.15f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w * frac, h), px);

        // Wind-up telegraph pip below the bar during a charge/slam.
        if (boss.TelegraphFill > 0f && boss.TelegraphFill < 1f)
        {
            GUI.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(x, y + h + 3, w * boss.TelegraphFill, 5f), px);
        }

        var label = new GUIStyle();
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = Mathf.RoundToInt(Screen.height * 0.02f);
        label.fontStyle = FontStyle.Bold;
        label.normal.textColor = bh.guardActive ? new Color(0.7f, 0.8f, 1f) : new Color(1f, 0.85f, 0.85f);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y - Screen.height * 0.028f, w, Screen.height * 0.026f),
            bh.guardActive ? "THE TALON-WARDEN  —  guarded (send the crow)" : "THE TALON-WARDEN  —  EXPOSED", label);
    }

    void OnGUI()
    {
        if (!GameLoop.IsPlaying) return; // no HUD over title/pause/win cards
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        bool canPerch = crow != null && crow.HasPerchTarget();

        // "!" over distracted enemies — the crow's effect on the world, readable.
        RefreshEnemyCache();
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            var tell = new GUIStyle();
            tell.alignment = TextAnchor.MiddleCenter;
            tell.fontSize = Mathf.RoundToInt(Screen.height * 0.045f);
            tell.fontStyle = FontStyle.Bold;
            tell.normal.textColor = perchColor;
            foreach (var s in stalkers) if (s != null && s.IsDistracted) DrawDistractTell(s.transform, mainCam, tell);
            foreach (var s in sentries) if (s != null && s.IsDistracted) DrawDistractTell(s.transform, mainCam, tell);
        }

        // Health bar, top-left (day 4)
        if (playerHealth != null)
        {
            float w = 220f, h = 14f, pad = 16f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(pad - 2, pad - 2, w + 4, h + 4), px);
            float frac = Mathf.Clamp01(playerHealth.Current / playerHealth.maxHealth);
            GUI.color = Color.Lerp(new Color(0.75f, 0.12f, 0.1f, 0.9f),
                                   new Color(0.85f, 0.7f, 0.25f, 0.9f), frac);
            GUI.DrawTexture(new Rect(pad, pad, w * frac, h), px);
        }

        // Boss bar, top-center — only while the Warden lives (day 7 boss).
        DrawBossBar();

        // Center dot
        GUI.color = canPerch ? perchColor : neutralColor;
        float d = dotSize;
        GUI.DrawTexture(new Rect(cx - d * 0.5f, cy - d * 0.5f, d, d), px);

        // Perch ring: four thin edges of a square ring (cheap, reads as a frame)
        if (canPerch)
        {
            float r = ringSize, t = ringThickness;
            GUI.DrawTexture(new Rect(cx - r * 0.5f, cy - r * 0.5f, r, t), px);           // top
            GUI.DrawTexture(new Rect(cx - r * 0.5f, cy + r * 0.5f - t, r, t), px);       // bottom
            GUI.DrawTexture(new Rect(cx - r * 0.5f, cy - r * 0.5f, t, r), px);           // left
            GUI.DrawTexture(new Rect(cx + r * 0.5f - t, cy - r * 0.5f, t, r), px);       // right
        }

        // Shadow-step hint: peeking from a perch with the step off cooldown —
        // tell the player the option exists (discoverability, day-5).
        if (crow != null && crow.PeekHeld && crow.ShadowStepReady)
        {
            var style = new GUIStyle();
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = Mathf.RoundToInt(Screen.height * 0.024f);
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = perchColor;
            GUI.Label(new Rect(0, cy + 40f, Screen.width, 30f), "DASH — SHADOW STEP", style);
        }
        GUI.color = Color.white;
    }
}
