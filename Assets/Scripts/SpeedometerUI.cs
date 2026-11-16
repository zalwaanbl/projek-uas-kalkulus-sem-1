using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Speedometer bergaya game: jarum berputar dan/atau bar yang berubah warna.
/// Jarum: buat Image jarum di dalam Canvas, atur Pivot ke pangkal jarum
/// (mis. 0.5, 0.1), lalu letakkan pangkalnya di pusat dial.
/// Bar: Image dengan Image Type = Filled.
/// </summary>
public class SpeedometerUI : MonoBehaviour
{
    public CarController car;

    [Header("Jarum (opsional)")]
    public RectTransform needle;
    public float maxKmh = 300f;
    public float angleAtZero = 120f;     // sudut jarum saat 0 km/jam
    public float angleAtMax = -120f;     // sudut jarum saat kecepatan maksimum

    [Header("Bar (opsional)")]
    public Image speedBar;
    public Gradient barColor;            // hijau -> kuning -> merah

    float shown;

    void Update()
    {
        if (car == null) return;

        // Dihaluskan supaya jarum tidak patah-patah
        shown = CommonUtil.ExpLerp(shown, car.SpeedKmh, 12f, Time.deltaTime);
        float t = Mathf.Clamp01(shown / Mathf.Max(1f, maxKmh));

        if (needle != null)
            needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(angleAtZero, angleAtMax, t));

        if (speedBar != null)
        {
            speedBar.fillAmount = t;
            speedBar.color = barColor.Evaluate(t);
        }
    }
}