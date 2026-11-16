using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Mengatur post-processing (URP) berdasarkan kecepatan:
/// vignette, chromatic aberration, dan motion blur menguat saat ngebut.
///
/// BUTUH URP. Jika project memakai Built-in Render Pipeline,
/// hapus file ini (script lain tetap bisa dipakai).
///
/// Di Global Volume, tambahkan override: Vignette, Chromatic Aberration,
/// Motion Blur (nilai awalnya boleh dibiarkan, script yang mengatur).
/// </summary>
public class SpeedPostFX : MonoBehaviour
{
    public CarController car;
    public Volume volume;

    [Header("Vignette")]
    public float vignetteMin = 0.2f;
    public float vignetteMax = 0.5f;

    [Header("Chromatic Aberration")]
    public float chromaMax = 0.6f;

    [Header("Motion Blur")]
    public float blurMax = 0.5f;

    Vignette vignette;
    ChromaticAberration chroma;
    MotionBlur blur;

    void Start()
    {
        if (volume == null) return;
        // volume.profile membuat salinan, jadi aset aslinya tidak ikut berubah
        VolumeProfile profile = volume.profile;
        profile.TryGet(out vignette);
        profile.TryGet(out chroma);
        profile.TryGet(out blur);
    }

    void Update()
    {
        if (car == null) return;
        float t = Mathf.Clamp01(car.Speed / Mathf.Max(0.01f, car.maxSpeed));
        float t2 = t * t;   // efek baru terasa kuat di kecepatan tinggi

        if (vignette != null)
            vignette.intensity.Override(Mathf.Lerp(vignetteMin, vignetteMax, t2));

        if (chroma != null)
            chroma.intensity.Override(chromaMax * t2);

        if (blur != null)
            blur.intensity.Override(blurMax * t2);
    }
}