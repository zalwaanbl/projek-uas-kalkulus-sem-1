using UnityEngine;
using TMPro;

/// <summary>
/// Menampilkan data mobil di layar.
/// Ada juga perhitungan percepatan NUMERIK dari selisih kecepatan:
///     a ≈ (v_sekarang - v_sebelumnya) / dt
/// Ini adalah definisi turunan dalam bentuk paling sederhana,
/// dan hasilnya bisa dibandingkan dengan percepatan dari CarController.
/// </summary>
public class HUDManager : MonoBehaviour
{
    public CarController car;

    [Header("Teks UI (TextMeshPro)")]
    public TMP_Text speedText;         // contoh: "123 km/jam"
    public TMP_Text accelText;         // contoh: "a = 8.50 m/s²"
    public TMP_Text distanceText;      // contoh: "Jarak: 1.250 m"
    public TMP_Text timeText;          // contoh: "Waktu: 00:42.3"
    public TMP_Text numericAccelText;  // opsional: a hasil turunan numerik

    float prevSpeed;

    // Nilai terakhir yang sudah ditulis ke layar. Teks hanya dibangun ulang
    // saat angkanya berubah, jadi tidak ada string baru tiap frame (hemat GC).
    int lastSpeedInt = int.MinValue;
    int lastAccelKey = int.MinValue;
    int lastDistanceInt = int.MinValue;
    int lastTimeKey = int.MinValue;
    int lastNumericKey = int.MinValue;

    // Teks turunan diperbarui 10x/detik (sama dengan sampling grafik), bukan
    // tiap frame: setiap perubahan teks memicu rebuild canvas TextMeshPro.
    const float FastTextInterval = 0.1f;
    float nextFastTextTime;
    float lastNumericTime;

    void Start()
    {
        if (car != null) prevSpeed = car.Speed;
        lastNumericTime = Time.unscaledTime;
    }

    void LateUpdate()
    {
        if (car == null) return;

        // Perbarui teks turunan maksimal 10x/detik.
        bool fast = Time.unscaledTime >= nextFastTextTime;
        if (fast) nextFastTextTime = Time.unscaledTime + FastTextInterval;

        int speedInt = Mathf.RoundToInt(car.SpeedKmh);
        if (speedText != null && speedInt != lastSpeedInt)
        {
            lastSpeedInt = speedInt;
            speedText.text = speedInt + " km/jam";
        }

        if (fast)
        {
            int accelKey = Mathf.RoundToInt(car.Acceleration * 100f);
            if (accelText != null && accelKey != lastAccelKey)
            {
                lastAccelKey = accelKey;
                accelText.text = "a = " + car.Acceleration.ToString("0.00") + " m/s²";
            }
        }

        int distanceInt = Mathf.RoundToInt(car.Distance);
        if (distanceText != null && distanceInt != lastDistanceInt)
        {
            lastDistanceInt = distanceInt;
            distanceText.text = "Jarak: " + distanceInt + " m";
        }

        int timeKey = Mathf.FloorToInt(car.ElapsedTime * 10f);   // presisi 0,1 detik
        if (timeText != null && timeKey != lastTimeKey)
        {
            lastTimeKey = timeKey;
            int minutes = timeKey / 600;
            int tenths = timeKey % 600;
            timeText.text = "Waktu: " + minutes.ToString("00") + ":" +
                            (tenths / 10).ToString("00") + "." + (tenths % 10);
        }

        // Turunan numerik: a = Δv / Δt, dihitung per interval 0,1 detik
        if (fast)
        {
            float dtSample = Time.unscaledTime - lastNumericTime;
            if (numericAccelText != null && dtSample > 0f)
            {
                float aNumeric = (car.Speed - prevSpeed) / dtSample;
                int numericKey = Mathf.RoundToInt(aNumeric * 100f);
                if (numericKey != lastNumericKey)
                {
                    lastNumericKey = numericKey;
                    numericAccelText.text = "dv/dt ≈ " + aNumeric.ToString("0.00") + " m/s²";
                }
            }
            prevSpeed = car.Speed;
            lastNumericTime = Time.unscaledTime;
        }
    }
}