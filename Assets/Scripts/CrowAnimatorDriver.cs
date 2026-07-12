using UnityEngine;

/// <summary>
/// Bridges CrowCompanion.State to the crow model's Animator (Generic rig, 3 clips).
/// Follow -> Hover (gentle half-beat at your shoulder), FlyTo -> Flap (full beat),
/// Perched -> Perched (wings tucked, slow scan). CrossFades so transitions read as
/// the crow catching or folding its wings, not snapping. Runs on UNSCALED time via
/// the Animator update mode so the wings keep beating at thought-speed during peek.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CrowAnimatorDriver : MonoBehaviour
{
    public CrowCompanion crow;      // auto-found on parent if empty
    public float crossFade = 0.18f;

    Animator anim;
    int hoverH, flapH, perchH;
    int current;

    void Awake()
    {
        anim = GetComponent<Animator>();
        anim.updateMode = AnimatorUpdateMode.UnscaledTime; // beat through the slow-mo
        if (crow == null) crow = GetComponentInParent<CrowCompanion>();
        hoverH = Animator.StringToHash("Hover");
        flapH  = Animator.StringToHash("Flap");
        perchH = Animator.StringToHash("Perched");
    }

    void Update()
    {
        if (crow == null) return;
        int want = crow.State == CrowCompanion.CrowState.FlyTo ? flapH
                 : crow.State == CrowCompanion.CrowState.Perched ? perchH
                 : hoverH;
        if (want != current)
        {
            anim.CrossFade(want, crossFade, 0);
            current = want;
        }
    }
}
