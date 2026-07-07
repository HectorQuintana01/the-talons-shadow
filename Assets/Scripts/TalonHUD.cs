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

    void Start()
    {
        crow = FindFirstObjectByType<CrowCompanion>();
        var tc = FindFirstObjectByType<TalonController>();
        if (tc != null) playerHealth = tc.GetComponent<Health>();
        px = new Texture2D(1, 1);
        px.SetPixel(0, 0, Color.white);
        px.Apply();
    }

    void OnGUI()
    {
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        bool canPerch = crow != null && crow.HasPerchTarget();

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
        GUI.color = Color.white;
    }
}
