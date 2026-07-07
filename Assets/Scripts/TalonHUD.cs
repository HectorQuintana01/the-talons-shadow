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
    Texture2D px; // 1x1 white — tinted via GUI.color

    void Start()
    {
        crow = FindFirstObjectByType<CrowCompanion>();
        px = new Texture2D(1, 1);
        px.SetPixel(0, 0, Color.white);
        px.Apply();
    }

    void OnGUI()
    {
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        bool canPerch = crow != null && crow.HasPerchTarget();

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
