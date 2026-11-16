using UnityEngine;

/// <summary>
/// Efek visual pada MODEL mobil (objek anak dari Car):
///   - badan mobil menunduk saat rem dan mendongak saat akselerasi
///     (memakai nilai Acceleration = turunan kecepatan!)
///   - badan miring (roll) saat belok
///   - roda berputar sesuai kecepatan: omega = v / r
///   - (minggu 3) model berkedip saat kebal setelah tabrakan
/// Pasang di objek Car, lalu isi "model" dengan anak berisi mesh mobil.
/// </summary>
public class CarVisual : MonoBehaviour
{
    public CarController car;
    public Transform model;

    [Header("Roda (opsional)")]
    public Transform[] wheels;
    public Vector3 wheelSpinAxis = Vector3.right;
    public float wheelRadius = 0.35f;   // m

    [Header("Gerak badan")]
    [Tooltip("Derajat kemiringan per 1 m/s² percepatan")]
    public float pitchPerAccel = 0.25f;
    public float maxPitch = 5f;
    public float maxRoll = 6f;
    public float smooth = 6f;

    [Header("Kedip saat kebal (setelah tabrakan)")]
    public float blinkRate = 14f;       // kedipan per detik

    float pitch, roll;
    Renderer[] renderers;
    bool lastVisible = true;

    void Start()
    {
        if (model != null)
            renderers = model.GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (car == null) return;
        float dt = Time.deltaTime;

        // Akselerasi positif -> hidung naik (sudut X negatif); rem -> hidung turun
        float targetPitch = Mathf.Clamp(-car.Acceleration * pitchPerAccel, -maxPitch, maxPitch);
        float targetRoll = car.Steer * maxRoll * Mathf.Clamp01(car.Speed / 10f);

        pitch = CommonUtil.ExpLerp(pitch, targetPitch, smooth, dt);
        roll = CommonUtil.ExpLerp(roll, targetRoll, smooth, dt);

        if (model != null)
            model.localRotation = Quaternion.Euler(pitch, 0f, roll);

        // Putaran roda: kecepatan sudut (rad/s) = v / r
        float spinDeg = car.Speed / Mathf.Max(0.01f, wheelRadius) * Mathf.Rad2Deg * dt;
        if (wheels != null)
        {
            foreach (Transform w in wheels)
            {
                if (w != null) w.Rotate(wheelSpinAxis, spinDeg, Space.Self);
            }
        }

        // Kedip saat kebal: renderer hanya diubah saat statusnya berganti,
        // bukan setiap frame.
        if (renderers != null)
        {
            bool visible = !car.IsInvulnerable || (Mathf.FloorToInt(Time.time * blinkRate) % 2 == 0);
            if (visible != lastVisible)
            {
                lastVisible = visible;
                foreach (Renderer r in renderers)
                {
                    if (r != null) r.enabled = visible;
                }
            }
        }
    }
}