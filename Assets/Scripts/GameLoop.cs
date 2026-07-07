using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Win condition + restart. Lives on the ShadowGate trigger volume: reach the
/// gate → you escape → R / Start reloads the scene. Losing has no screen — the
/// player just respawns (Health handles it); the gate is the only exit.
/// Resets time scale on load so a mid-peek restart can't leave the world slow.
/// </summary>
public class GameLoop : MonoBehaviour
{
    public static bool Won;

    void Awake()
    {
        Won = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!Won && other.GetComponentInParent<TalonController>() != null)
            Won = true;
    }

    void Update()
    {
        if (!Won) return;
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        bool restart = (kb != null && kb.rKey.wasPressedThisFrame)
                    || (gp != null && gp.startButton.wasPressedThisFrame);
        if (restart)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    void OnGUI()
    {
        if (!Won) return;
        float w = Screen.width, h = Screen.height;

        // Dim the world
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);

        var title = new GUIStyle();
        title.fontSize = Mathf.RoundToInt(h * 0.08f);
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.normal.textColor = new Color(1f, 0.85f, 0.3f);

        var sub = new GUIStyle(title);
        sub.fontSize = Mathf.RoundToInt(h * 0.03f);
        sub.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 0.9f);

        GUI.color = Color.white;
        GUI.Label(new Rect(0, h * 0.34f, w, h * 0.14f), "THE SHADOW ESCAPES", title);
        GUI.Label(new Rect(0, h * 0.52f, w, h * 0.08f), "R  /  Start  —  fly again", sub);
    }
}
