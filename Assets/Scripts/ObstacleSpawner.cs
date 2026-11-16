using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rintangan (mobil lalu lintas) di 3 lajur, didaur ulang seperti segment jalan.
///
/// - Tidak memakai Rigidbody/Collider. Mobil pemain bergerak kinematik, jadi
///   tabrakan dicek sendiri dengan kotak (AABB) di sumbu x dan z.
///   Pengecekan z memakai rentang antara posisi frame lalu dan sekarang,
///   sehingga mobil secepat apa pun tidak bisa "menembus" rintangan.
/// - Rintangan boleh bergerak pelan searah mobil (trafficSpeed).
///   Kecepatan relatif = v_mobil - v_rintangan menentukan seberapa cepat mendekat.
/// - Makin jauh jarak tempuh, jarak antar rintangan makin rapat (kesulitan naik).
/// - Mode tanpa aset: kubus berwarna. Mode aset: isi "obstaclePrefab"
///   (pivot di dasar model, menghadap +Z).
///
/// Asumsi: mobil mulai di x = 0 dan lajur ada di dalam batas jalan mobil
/// (CarController.roadHalfWidth).
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    public CarController car;

    [Header("Rintangan")]
    [Tooltip("Opsional. Kosong = kubus berwarna otomatis")]
    public GameObject obstaclePrefab;
    public int count = 10;
    [Tooltip("Ukuran kotak tabrakan rintangan: X = lebar, Y = panjang (m)")]
    public Vector2 obstacleSize = new Vector2(1.8f, 4f);
    [Tooltip("Tinggi kubus otomatis (m)")]
    public float visualHeight = 1f;
    public Color[] colors =
    {
        new Color(0.85f, 0.15f, 0.15f),
        new Color(0.15f, 0.4f, 0.85f),
        new Color(0.95f, 0.75f, 0.1f),
        new Color(0.9f, 0.9f, 0.9f),
    };

    [Header("Mobil pemain (kotak tabrakan)")]
    public Vector2 carSize = new Vector2(1.8f, 4f);
    [Tooltip("Pengecil kotak tabrakan supaya terasa adil (1 = ukuran penuh)")]
    [Range(0.5f, 1f)] public float hitboxScale = 0.85f;

    [Header("Lajur (posisi X)")]
    public float[] laneX = { -3f, 0f, 3f };

    [Header("Penempatan & kesulitan")]
    [Tooltip("Jarak rintangan pertama di depan mobil (m)")]
    public float firstDistance = 250f;
    [Tooltip("Jarak acak antar rintangan (m)")]
    public Vector2 gapRange = new Vector2(60f, 140f);
    [Tooltip("Pengali jarak saat kesulitan maksimum (0.45 = jarak menyusut jadi 45%)")]
    [Range(0.2f, 1f)] public float hardestGapFactor = 0.45f;
    [Tooltip("Jarak tempuh (m) sampai kesulitan maksimum")]
    public float rampDistance = 3000f;
    public float despawnBehind = 40f;
    public float despawnAhead = 900f;

    [Header("Lalu lintas")]
    [Tooltip("Kecepatan rintangan searah mobil (m/s). 0 dan 0 = semua diam")]
    public Vector2 trafficSpeed = new Vector2(5f, 20f);

    [Header("Tabrakan")]
    [Range(0.1f, 0.9f)] public float keepSpeedFraction = 0.4f;   // sisa kecepatan setelah tabrakan
    public float crashDuration = 0.4f;                           // lama perlambatan (detik)
    public float invulnerableTime = 1.5f;                        // kebal setelah tabrakan (detik)
    public AudioClip crashClip;                                  // opsional

    [Header("Status (diatur GameManager)")]
    [Tooltip("Rintangan bergerak dan bisa menabrak. Centang manual untuk menguji tanpa GameManager")]
    public bool running = false;

    class Obs
    {
        public Transform t;
        public float x, z, speed;
        public Renderer[] rends;
    }

    readonly List<Obs> list = new List<Obs>();
    Transform container;
    Material[] mats;
    float yOffset;
    float prevCarZ;

    void Awake()
    {
        container = new GameObject("Obstacles").transform;
        container.SetParent(transform, false);

        if (obstaclePrefab == null)
        {
            mats = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++) mats[i] = MakeMaterial(colors[i]);
            yOffset = visualHeight * 0.5f;
        }
        else
        {
            yOffset = 0f;
        }

        for (int i = 0; i < count; i++)
            list.Add(CreateObstacle());
    }

    void Start()
    {
        ResetObstacles();
    }

    // ------------------------------------------------------------
    // API untuk GameManager
    // ------------------------------------------------------------

    /// <summary>Menyusun ulang semua rintangan di depan mobil (awal permainan).</summary>
    public void ResetObstacles()
    {
        if (car == null) return;
        float z = car.transform.position.z + firstDistance;
        foreach (Obs o in list)
        {
            Place(o, z);
            z += NextGap();
        }
        prevCarZ = car.transform.position.z;
    }

    public void SetVisible(bool visible)
    {
        if (container != null) container.gameObject.SetActive(visible);
    }

    // ------------------------------------------------------------

    void Update()
    {
        if (car == null || list.Count == 0) return;

        float dt = Time.deltaTime;
        float carZ = car.transform.position.z;

        if (running)
        {
            // Rintangan yang sudah tertabrak (nonaktif) diam sampai didaur ulang
            foreach (Obs o in list)
            {
                if (o.t.gameObject.activeSelf) o.z += o.speed * dt;
            }

            CheckCollisions(carZ);

            // Daur ulang
            foreach (Obs o in list)
            {
                if (o.z < carZ - despawnBehind)
                {
                    Recycle(o, carZ);
                }
                else if (o.z - carZ > despawnAhead)
                {
                    // Terlalu jauh di depan (mis. pemain diam di awal): taruh lebih dekat
                    Place(o, carZ + firstDistance + Random.Range(0f, 100f));
                }
            }
        }

        // Pasang posisi ke objek
        foreach (Obs o in list)
            o.t.position = new Vector3(o.x, yOffset, o.z);

        prevCarZ = carZ;
    }

    void CheckCollisions(float carZ)
    {
        float carX = car.LateralX;
        float halfCarW = carSize.x * 0.5f * hitboxScale;
        float halfCarL = carSize.y * 0.5f * hitboxScale;
        float halfW = obstacleSize.x * 0.5f * hitboxScale;
        float halfL = obstacleSize.y * 0.5f * hitboxScale;

        // Rentang z yang dilalui mobil sejak frame lalu
        float carMinZ = Mathf.Min(prevCarZ, carZ) - halfCarL;
        float carMaxZ = Mathf.Max(prevCarZ, carZ) + halfCarL;

        foreach (Obs o in list)
        {
            if (!o.t.gameObject.activeSelf) continue;

            bool overlapX = Mathf.Abs(o.x - carX) < (halfW + halfCarW);
            bool overlapZ = (o.z + halfL) > carMinZ && (o.z - halfL) < carMaxZ;
            if (!overlapX || !overlapZ) continue;

            if (car.Crash(keepSpeedFraction, crashDuration, invulnerableTime))
            {
                o.t.gameObject.SetActive(false);   // rintangan yang ditabrak menghilang
                PlayCrashSound();
            }
        }
    }

    void PlayCrashSound()
    {
        if (crashClip == null) return;
        Camera c = Camera.main;
        Vector3 pos = c != null ? c.transform.position : car.transform.position;
        AudioSource.PlayClipAtPoint(crashClip, pos);
    }

    // ------------------------------------------------------------

    Obs CreateObstacle()
    {
        Obs o = new Obs();
        GameObject go;

        if (obstaclePrefab != null)
        {
            go = Instantiate(obstaclePrefab, container);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(container, false);
            go.transform.localScale = new Vector3(obstacleSize.x, visualHeight, obstacleSize.y);
        }

        go.name = "Obstacle";
        o.t = go.transform;
        o.rends = go.GetComponentsInChildren<Renderer>();
        return o;
    }

    void Recycle(Obs o, float carZ)
    {
        float far = Mathf.Max(FarthestZ(), carZ + 150f);
        Place(o, far + NextGap());
    }

    void Place(Obs o, float z)
    {
        o.z = z;
        o.x = laneX[Random.Range(0, laneX.Length)];
        o.speed = Random.Range(trafficSpeed.x, trafficSpeed.y);
        o.t.gameObject.SetActive(true);

        if (mats != null && mats.Length > 0)
        {
            Material m = mats[Random.Range(0, mats.Length)];
            foreach (Renderer r in o.rends) r.sharedMaterial = m;
        }
    }

    float FarthestZ()
    {
        float far = float.MinValue;
        foreach (Obs o in list)
            if (o.z > far) far = o.z;
        return far;
    }

    float NextGap()
    {
        float progress = Mathf.Clamp01(car.Distance / Mathf.Max(1f, rampDistance));
        float factor = Mathf.Lerp(1f, hardestGapFactor, progress);
        return Random.Range(gapRange.x, gapRange.y) * factor;
    }

    // Membuat material dengan shader bawaan pipeline aktif (Built-in maupun URP)
    static Material MakeMaterial(Color color)
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material m = new Material(temp.GetComponent<Renderer>().sharedMaterial);
        m.color = color;
        Destroy(temp);
        return m;
    }
}