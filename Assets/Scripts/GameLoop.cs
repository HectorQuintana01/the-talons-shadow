using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Game-state owner: Title → Playing ⇄ Paused → Won. Lives on the ShadowGate
/// trigger volume (reaching the gate wins).
/// WebGL hardening (day 6/S1):
/// - Title requires a CLICK to begin — that pointer gesture is what unlocks
///   browser audio (any-key does NOT unlock sound in Safari).
/// - Pause on P / gamepad Select (NOT Esc — Esc is the browser's pointer-lock
///   release and can't be trusted inside an iframe).
/// - Hold R (1s) / hold Start restarts at ANY time (softlock escape); after a
///   win a tap is enough.
/// - Cursor: unlocked on Title/Pause, locked while Playing; if the browser
///   steals the lock mid-run (Esc), we prompt "click to recapture".
/// - Title/Pause freeze the world via Time.timeScale=0; gameplay scripts also
///   gate on GameLoop.IsPlaying (belt and suspenders — a paused NavMeshAgent
///   must not bite).
/// - Statics (win flag, camera shake, hitstop) reset in Awake because statics
///   survive scene reloads.
/// </summary>
public class GameLoop : MonoBehaviour
{
    public enum GameState { Title, Playing, Paused, Won }

    public static GameState State { get; private set; } = GameState.Title;
    public static bool IsPlaying => State == GameState.Playing;
    public const string Version = "v7";

    float restartHold; // seconds R / Start has been held (unscaled)

    void Awake()
    {
        State = GameState.Title;
        Time.timeScale = 0f; // world is a frozen tableau behind the title card
        Time.fixedDeltaTime = 0.02f;
        ThirdPersonCamera.ResetJuice();
        CrowCompanion.ResetJuice();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (State == GameState.Playing && other.GetComponentInParent<TalonController>() != null)
        {
            State = GameState.Won;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Sfx.PlayUI("win");
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        var mouse = Mouse.current;
        float udt = Time.unscaledDeltaTime;

        switch (State)
        {
            case GameState.Title:
                // CLICK to begin — the audio-unlock gesture. Gamepad A also
                // starts (audio just stays locked until the first click; the
                // page click that focused the iframe usually already counted).
                bool begin = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                          || (gp != null && gp.buttonSouth.wasPressedThisFrame);
                if (begin) SetPlaying();
                break;

            case GameState.Playing:
                // Pause: P / Select.
                if ((kb != null && kb.pKey.wasPressedThisFrame)
                    || (gp != null && gp.selectButton.wasPressedThisFrame))
                {
                    State = GameState.Paused;
                    Time.timeScale = 0f;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
                }
                // Pointer-lock recapture: the browser released it (Esc).
                if (Cursor.lockState != CursorLockMode.Locked
                    && mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                break;

            case GameState.Paused:
                if ((kb != null && kb.pKey.wasPressedThisFrame)
                    || (gp != null && gp.selectButton.wasPressedThisFrame))
                {
                    SetPlaying();
                }
                break;

            case GameState.Won:
                bool tap = (kb != null && kb.rKey.wasPressedThisFrame)
                        || (gp != null && gp.startButton.wasPressedThisFrame);
                if (tap) Reload();
                break;
        }

        // Hold-to-restart works from Playing and Paused (softlock escape).
        if (State == GameState.Playing || State == GameState.Paused)
        {
            bool holding = (kb != null && kb.rKey.isPressed)
                        || (gp != null && gp.startButton.isPressed);
            restartHold = holding ? restartHold + udt : 0f;
            if (restartHold >= 1f) Reload();
        }
    }

    void SetPlaying()
    {
        State = GameState.Playing;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Reload()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- UI ----------

    void OnGUI()
    {
        float w = Screen.width, h = Screen.height;

        // Version tag, always visible (feedback maps to builds).
        var tiny = Style(h * 0.016f, new Color(1f, 1f, 1f, 0.4f), FontStyle.Normal);
        GUI.Label(new Rect(0, h - h * 0.03f, w - 8, h * 0.025f), Version, RightAlign(tiny));

        switch (State)
        {
            case GameState.Title: DrawTitle(w, h); break;
            case GameState.Paused: DrawPaused(w, h); break;
            case GameState.Won: DrawWon(w, h); break;
            case GameState.Playing:
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Dim(w, h, 0.25f);
                    GUI.Label(new Rect(0, h * 0.45f, w, h * 0.1f),
                        "click to recapture the shadow",
                        Style(h * 0.03f, new Color(1f, 0.85f, 0.3f), FontStyle.Bold));
                }
                break;
        }
    }

    void DrawTitle(float w, float h)
    {
        Dim(w, h, 0.65f);
        GUI.Label(new Rect(0, h * 0.16f, w, h * 0.12f), "THE TALON'S SHADOW",
            Style(h * 0.075f, new Color(1f, 0.85f, 0.3f), FontStyle.Bold));
        GUI.Label(new Rect(0, h * 0.27f, w, h * 0.05f), "your attention has a body",
            Style(h * 0.026f, new Color(0.85f, 0.85f, 0.9f, 0.9f), FontStyle.Italic));

        // Fixed three-column table (proportional fonts can't line up a big string).
        string[][] rows = {
            new[]{"MOVE", "WASD", "left stick"},
            new[]{"CAMERA", "mouse", "right stick"},
            new[]{"JUMP", "space", "A"},
            new[]{"DASH", "left shift", "B"},
            new[]{"STRIKE", "left click", "X"},
            new[]{"SEND CROW", "E", "RB"},
            new[]{"PEEK", "hold right-mouse", "hold LT"},
            new[]{"SHADOW STEP", "dash while peeking a perch", ""},
            new[]{"PLUME FLASH", "F", "Y"},
            new[]{"RECALL", "Q", "LB"},
            new[]{"PAUSE / RESTART", "P  /  hold R", "select / hold start"},
        };
        var cellL = Style(h * 0.022f, new Color(1f, 0.85f, 0.3f, 0.9f), FontStyle.Bold);
        cellL.alignment = TextAnchor.MiddleRight;
        var cellM = Style(h * 0.022f, new Color(0.9f, 0.9f, 0.9f, 0.95f), FontStyle.Normal);
        cellM.alignment = TextAnchor.MiddleLeft;
        var cellR = Style(h * 0.022f, new Color(0.75f, 0.75f, 0.85f, 0.95f), FontStyle.Normal);
        cellR.alignment = TextAnchor.MiddleLeft;
        float rowH = h * 0.034f, tableTop = h * 0.36f;
        for (int i = 0; i < rows.Length; i++)
        {
            float y = tableTop + i * rowH;
            GUI.Label(new Rect(w * 0.08f, y, w * 0.24f, rowH), rows[i][0], cellL);
            GUI.Label(new Rect(w * 0.36f, y, w * 0.30f, rowH), rows[i][1], cellM);
            GUI.Label(new Rect(w * 0.68f, y, w * 0.26f, rowH), rows[i][2], cellR);
        }

        GUI.Label(new Rect(0, h * 0.78f, w, h * 0.08f), "—  CLICK TO BEGIN  —",
            Style(h * 0.035f, new Color(1f, 0.85f, 0.3f), FontStyle.Bold));

        if (Touchscreen.current != null)
            GUI.Label(new Rect(0, h * 0.88f, w, h * 0.05f),
                "this game needs a desktop with keyboard or gamepad",
                Style(h * 0.022f, new Color(1f, 0.5f, 0.4f, 0.95f), FontStyle.Bold));
    }

    void DrawPaused(float w, float h)
    {
        Dim(w, h, 0.55f);
        GUI.Label(new Rect(0, h * 0.34f, w, h * 0.12f), "PAUSED",
            Style(h * 0.06f, new Color(0.9f, 0.9f, 0.95f), FontStyle.Bold));
        GUI.Label(new Rect(0, h * 0.5f, w, h * 0.08f),
            "P / select — resume        hold R / start — restart",
            Style(h * 0.026f, new Color(0.85f, 0.85f, 0.9f, 0.9f), FontStyle.Normal));
    }

    void DrawWon(float w, float h)
    {
        Dim(w, h, 0.6f);
        GUI.Label(new Rect(0, h * 0.34f, w, h * 0.14f), "THE SHADOW ESCAPES",
            Style(h * 0.08f, new Color(1f, 0.85f, 0.3f), FontStyle.Bold));
        GUI.Label(new Rect(0, h * 0.52f, w, h * 0.08f), "R  /  start  —  fly again",
            Style(h * 0.03f, new Color(0.9f, 0.9f, 0.9f, 0.9f), FontStyle.Normal));
    }

    // ---------- style helpers ----------

    static void Dim(float w, float h, float a)
    {
        GUI.color = new Color(0f, 0f, 0f, a);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    static GUIStyle Style(float size, Color color, FontStyle fs)
    {
        var s = new GUIStyle();
        s.fontSize = Mathf.RoundToInt(size);
        s.fontStyle = fs;
        s.alignment = TextAnchor.MiddleCenter;
        s.normal.textColor = color;
        return s;
    }

    static GUIStyle RightAlign(GUIStyle s)
    {
        s.alignment = TextAnchor.MiddleRight;
        return s;
    }
}
