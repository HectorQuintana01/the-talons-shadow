using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal one-shot SFX player. Clips live in Assets/Resources/Sfx/&lt;name&gt;
/// (CC0 set — provenance in Assets/Audio/CREDITS.md). Missing clips no-op
/// silently, so gameplay code can be wired before the audio files exist.
/// 3D one-shots via PlayClipAtPoint; UI sounds play at the camera.
/// </summary>
public static class Sfx
{
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

    public static void Play(string name, Vector3 pos, float vol = 0.8f)
    {
        var clip = Get(name);
        if (clip != null) AudioSource.PlayClipAtPoint(clip, pos, vol);
    }

    public static void PlayUI(string name, float vol = 0.8f)
    {
        var cam = Camera.main;
        Play(name, cam != null ? cam.transform.position : Vector3.zero, vol);
    }

    static AudioClip Get(string name)
    {
        AudioClip clip;
        if (cache.TryGetValue(name, out clip)) return clip;
        clip = Resources.Load<AudioClip>("Sfx/" + name);
        cache[name] = clip; // caches null too — one lookup per missing clip
        return clip;
    }
}
