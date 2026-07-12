using UnityEngine;

/// <summary>
/// The Plume Flash burst: an expanding translucent sphere + a point-light pop
/// that fades and self-destructs. Spawned by CrowCompanion.DoPlumeFlash. Runs on
/// UNSCALED time so it reads at full speed even during peek slow-mo.
/// </summary>
public class PlumeVfx : MonoBehaviour
{
    public float radius = 6f;
    public float life = 0.45f;

    float age;
    Renderer rend;
    Light pop;
    MaterialPropertyBlock mpb;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public static void Spawn(Vector3 pos, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "PlumeFlash";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = pos;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(0.7f, 0.5f, 1f, 0.5f);
        go.GetComponent<Renderer>().material = mat;
        var v = go.AddComponent<PlumeVfx>();
        v.radius = radius;
        var lightGo = new GameObject("PlumeLight");
        lightGo.transform.SetParent(go.transform, false);
        v.pop = lightGo.AddComponent<Light>();
        v.pop.color = new Color(0.7f, 0.5f, 1f);
        v.pop.range = radius * 2.2f;
        v.pop.intensity = 8f;
    }

    void Start()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        transform.localScale = Vector3.one * 0.2f;
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / life);
        float s = Mathf.Lerp(0.2f, radius * 2f, t);   // expand to the stun diameter
        transform.localScale = Vector3.one * s;
        float a = (1f - t) * 0.5f;                     // fade out
        if (rend != null) { mpb.SetColor(BaseColorId, new Color(0.7f, 0.5f, 1f, a)); rend.SetPropertyBlock(mpb); }
        if (pop != null) pop.intensity = Mathf.Lerp(8f, 0f, t);
        if (t >= 1f) Destroy(gameObject);
    }
}
