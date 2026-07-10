using UnityEngine;

/// <summary>
/// Seals the Shadow Gate until the Warden falls. Disables the gate's win-trigger
/// collider and shows a sealing barrier; when the boss dies, the barrier drops
/// and the gate becomes passable. If there's no Warden in the scene (e.g. the
/// boss was re-cut), the gate is open from the start — graceful degradation.
/// </summary>
public class ShadowGateSeal : MonoBehaviour
{
    public Collider gateTrigger;   // the ShadowGate win volume
    public Renderer barrier;       // a translucent wall across the arch while sealed

    BossWarden warden;
    bool opened;

    void Start()
    {
        warden = FindFirstObjectByType<BossWarden>();
        bool sealed_ = warden != null;
        if (gateTrigger != null) gateTrigger.enabled = !sealed_;
        if (barrier != null) barrier.enabled = sealed_;
        opened = !sealed_;
    }

    void Update()
    {
        if (opened) return;
        if (warden == null || !warden.Alive)
        {
            opened = true;
            if (gateTrigger != null) gateTrigger.enabled = true;
            if (barrier != null) barrier.enabled = false;
            ThirdPersonCamera.Shake(0.25f);
        }
    }
}
