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
    Material matCloud;

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<CarController>();
        Build();
    }

    void OnDestroy()
    {
        if (matCloud != null) Destroy(matCloud);
    }

    void Build()
    {
        if (container != null) Destroy(container.gameObject);
        container = new GameObject("Clouds").transform;
        container.SetParent(transform, false);

        if (matCloud == null) matCloud = CommonUtil.ColorMaterial(Color.white);
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
        Transform root = new GameObject("Cloud").transform;
        root.SetParent(container, false);

        // Awan = beberapa bola gepeng putih yang bertumpuk.
        int puffs = Random.Range(3, 5);
        for (int p = 0; p < puffs; p++)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(puff.GetComponent<Collider>());
            puff.transform.SetParent(root, false);
            puff.transform.localPosition = new Vector3(
                Random.Range(-3f, 3f),
                Random.Range(-0.6f, 0.6f),
                Random.Range(-1.5f, 1.5f));
            float s = Random.Range(2.5f, 4.5f);
            puff.transform.localScale = new Vector3(s, s * 0.6f, s);
            puff.GetComponent<Renderer>().sharedMaterial = matCloud;
        }
        return root;
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
            }

            float y = c.y + Mathf.Sin(t * bobFrequency * Mathf.PI * 2f + c.phase) * bobAmplitude;
            c.t.position = new Vector3(c.x, y, c.z);
        }
    }
}
