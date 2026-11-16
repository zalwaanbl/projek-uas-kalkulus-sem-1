using UnityEngine;

/// <summary>
/// Kamera mengikuti mobil dari belakang (versi minggu 3).
/// Efek kecepatan yang dipicu oleh car.Speed:
///   - FOV membesar
///   - kamera mundur sedikit
///   - kamera bergetar halus di kecepatan tinggi
/// Tambahan minggu 3: guncangan kuat sesaat saat mobil menabrak.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public CarController car;
    public Vector3 offset = new Vector3(0f, 3f, -7f);
    public float followSmooth = 10f;

    [Header("FOV & jarak")]
    public float baseFov = 60f;
    public float maxFov = 85f;
    [Tooltip("Tambahan jarak mundur saat kecepatan maksimum (m)")]
    public float extraDistance = 2f;

    [Header("Guncangan")]
    [Tooltip("Besar getaran maksimum (m)")]
    public float shakeAmount = 0.06f;
    public float shakeFrequency = 25f;
    [Tooltip("Getaran mulai muncul di atas fraksi kecepatan ini (0..1)")]
    [Range(0f, 1f)] public float shakeStart = 0.6f;

    [Header("Guncangan tabrakan")]
    public float crashShake = 0.35f;        // besar getaran saat tabrakan (m)
    public float crashShakeDuration = 0.5f; // lama getaran (detik)
    public float crashShakeFrequency = 40f;

    Camera cam;
    Vector3 smoothPos;
    bool initialized;
    float kick;   // 1 saat baru tabrakan, turun ke 0

    void Start()
    {
        cam = GetComponent<Camera>();
        if (car != null) car.Crashed += OnCrashed;
    }

    void OnDestroy()
    {
        if (car != null) car.Crashed -= OnCrashed;
    }

    void OnCrashed()
    {
        kick = 1f;
    }

    void LateUpdate()
    {
        if (car == null) return;

        float t = Mathf.Clamp01(car.Speed / Mathf.Max(0.01f, car.maxSpeed));
        float dt = Time.deltaTime;

        // Posisi target (kamera mundur sedikit saat ngebut)
        Vector3 off = offset + new Vector3(0f, 0f, -extraDistance * t);
        Vector3 target = car.transform.position + off;

        if (!initialized || Vector3.Distance(smoothPos, target) > 50f)
        {
            smoothPos = target;       // setelah reset mobil, langsung loncat
            initialized = true;
        }
        smoothPos = Vector3.Lerp(smoothPos, target, followSmooth * dt);

        // Getaran kecepatan (Perlin noise), ditambahkan SETELAH smoothing agar tidak menumpuk
        float shakeT = Mathf.InverseLerp(shakeStart, 1f, t);
        float amp = shakeAmount * shakeT * shakeT;
        float n = Time.time * shakeFrequency;
        Vector3 shake = new Vector3(
            Mathf.PerlinNoise(n, 0f) - 0.5f,
            Mathf.PerlinNoise(0f, n) - 0.5f,
            0f) * 2f * amp;

        // Getaran tabrakan (lebih cepat dan kuat, meredup)
        if (kick > 0f)
        {
            float cn = Time.time * crashShakeFrequency;
            shake += new Vector3(
                Mathf.PerlinNoise(cn, 10f) - 0.5f,
                Mathf.PerlinNoise(10f, cn) - 0.5f,
                0f) * 2f * crashShake * kick;
            kick = Mathf.MoveTowards(kick, 0f, dt / Mathf.Max(0.05f, crashShakeDuration));
        }

        transform.position = smoothPos + shake;
        transform.LookAt(car.transform.position + Vector3.up * 1f);

        if (cam != null)
        {
            float targetFov = Mathf.Lerp(baseFov, maxFov, t);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 5f * dt);
        }
    }
}