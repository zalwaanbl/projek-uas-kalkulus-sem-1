using UnityEngine;

/// <summary>
/// Efek yang mengikuti kecepatan mobil (tidak butuh URP):
///   - garis kecepatan (Particle System di depan kamera)
///   - suara mesin: pitch dan volume naik seiring kecepatan
/// Semua nilai dihitung dari t = Speed / maxSpeed (0..1).
/// </summary>
public class SpeedEffects : MonoBehaviour
{
    public CarController car;

    [Header("Garis kecepatan")]
    public ParticleSystem speedLines;
    [Range(0f, 1f)] public float linesStart = 0.35f;   // mulai muncul di fraksi kecepatan ini
    public float maxEmission = 140f;                   // partikel per detik di kecepatan penuh
    public float minLineSpeed = 40f;
    public float maxLineSpeed = 140f;

    [Header("Suara mesin")]
    public AudioSource engineAudio;
    public float minPitch = 0.8f;
    public float maxPitch = 2.2f;
    public float minVolume = 0.35f;
    public float maxVolume = 1f;

    void Update()
    {
        if (car == null) return;
        float t = Mathf.Clamp01(car.Speed / Mathf.Max(0.01f, car.maxSpeed));

        if (speedLines != null)
        {
            float k = Mathf.InverseLerp(linesStart, 1f, t);
            var emission = speedLines.emission;
            emission.rateOverTime = k * maxEmission;

            var main = speedLines.main;
            main.startSpeed = Mathf.Lerp(minLineSpeed, maxLineSpeed, k);
        }

        if (engineAudio != null)
        {
            engineAudio.pitch = Mathf.Lerp(minPitch, maxPitch, t);
            engineAudio.volume = Mathf.Lerp(minVolume, maxVolume, t);
        }
    }
}