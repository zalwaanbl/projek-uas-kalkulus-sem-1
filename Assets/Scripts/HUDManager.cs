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

    void Start()
    {
        if (car != null) prevSpeed = car.Speed;
    }

    void LateUpdate()
    {
        if (car == null) return;

        if (speedText != null)
            speedText.text = Mathf.RoundToInt(car.SpeedKmh) + " km/jam";

        if (accelText != null)
            accelText.text = "a = " + car.Acceleration.ToString("0.00") + " m/s²";

        if (distanceText != null)
            distanceText.text = "Jarak: " + Mathf.RoundToInt(car.Distance) + " m";

        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(car.ElapsedTime / 60f);
            float seconds = car.ElapsedTime - minutes * 60f;
            timeText.text = "Waktu: " + minutes.ToString("00") + ":" + seconds.ToString("00.0");
        }

        // Turunan numerik: a = Δv / Δt
        if (numericAccelText != null && Time.deltaTime > 0f)
        {
            float aNumeric = (car.Speed - prevSpeed) / Time.deltaTime;
            numericAccelText.text = "dv/dt ≈ " + aNumeric.ToString("0.00") + " m/s²";
        }
        prevSpeed = car.Speed;
    }
}