using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Awan yang melayang pelan di langit (efek paralaks).
///
/// Awan tidak ikut kecepatan mobil: mereka bergerak sendiri di sumbu Z dengan
/// kecepatan kecil, jadi relatif terhadap mobil awan tampak mundur pelan.
/// Awan didaur ulang: yang tertinggal di belakang mobil atau melayang terlalu
/// jauh ditarik kembali ke jarak pandang. Semua awan dibuat dari bola-bola
/// gepeng berwarna putih, tanpa collider (tidak menabrak apa pun).
/// </summary>
public class CloudSpawner : MonoBehaviour
{
    public CarController car;

    [Header("Jumlah & penempatan")]
    public int count = 12;
    [Tooltip("Jarak samping dari tengah jalan (m), kiri/kanan acak")]
    public Vector2 sideRange = new Vector2(40f, 120f);
    public Vector2 heightRange = new Vector2(25f, 45f);
    [Tooltip("Jarak awan di depan mobil saat ditempatkan (m)")]
    public Vector2 aheadRange = new Vector2(150f, 600f);

    [Header("Gerak")]
    [Tooltip("Kecepatan melayang awan (m/s, searah +Z)")]
    public Vector2 driftSpeed = new Vector2(1.5f, 4f);
    [Tooltip("Amplitudo naik-turun awan (m)")]
    public float bobAmplitude = 1.5f;
    [Tooltip("Frekuensi naik-turun (Hz)")]
    public float bobFrequency = 0.15f;

    class Cloud
    {
        public Transform t;
        public float x, y, z, speed, phase;
    }

    readonly List<Cloud> list = new List<Cloud>();
    Transform container;

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<CarController>();
        Build();
    }


    void Build()
    {
        if (container != null) Destroy(container.gameObject);
        container = new GameObject("Clouds").transform;
        container.SetParent(transform, false);


        list.Clear();

        int n = Mathf.Max(1, count);
        for (int i = 0; i < n; i++)
        {
            Cloud c = new Cloud { t = CreateCloud() };
            list.Add(c);
        }
        Layout();
    }

    Transform CreateCloud()
    {
        string path = SceneryAssets.Clouds[Random.Range(0, SceneryAssets.Clouds.Length)];
        Transform t = SceneryAssets.Spawn(path, container, Vector3.zero,
                                          Random.Range(0f, 360f), Random.Range(24f, 46f), Color.white);
        if (t == null) return null;
        t.name = "Cloud";
        return t;
    }

    void Layout()
    {
        float carZ = car != null ? car.transform.position.z : 0f;
        foreach (Cloud c in list)
        {
            c.x = (Random.value < 0.5f ? -1f : 1f) * Random.Range(sideRange.x, sideRange.y);
            c.y = Random.Range(heightRange.x, heightRange.y);
            c.z = carZ + Random.Range(aheadRange.x, aheadRange.y);
            c.speed = Random.Range(driftSpeed.x, driftSpeed.y);
            c.phase = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float carZ = car != null ? car.transform.position.z : 0f;
        float t = Time.time;

        foreach (Cloud c in list)
        {
            c.z += c.speed * dt;

            // Daur ulang: tertinggal di belakang, atau melayang terlalu jauh.
            if (c.z < carZ - 60f || c.z > carZ + aheadRange.y + 200f)
            {
                c.x = (Random.value < 0.5f ? -1f : 1f) * Random.Range(sideRange.x, sideRange.y);
                c.y = Random.Range(heightRange.x, heightRange.y);
                c.z = carZ + Random.Range(aheadRange.x, aheadRange.y);
                c.speed = Random.Range(driftSpeed.x, driftSpeed.y);

                // Model dipakai ulang: bentuk awan sudah bervariasi dari pilihan
                // acak di awal, jadi daur ulang tidak membuat objek/material baru.
            }
            if (c.t == null) continue;

            float y = c.y + Mathf.Sin(t * bobFrequency * Mathf.PI * 2f + c.phase) * bobAmplitude;
            c.t.position = new Vector3(c.x, y, c.z);
        }
    }
}
