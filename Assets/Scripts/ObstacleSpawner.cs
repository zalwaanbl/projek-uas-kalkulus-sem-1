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
/// - Rintangan selalu berupa model kendaraan dari "obstacleModelPaths"
///   (nama model di Resources/KenneyCarKit/FBX) atau "obstaclePrefab".
///   Pivot model di dasar (y = 0), menghadap +Z. Ukuran kotak tabrakan
///   mengikuti katalog VehicleAssets per model.
///
/// Asumsi: mobil mulai di x = 0 dan lajur ada di dalam batas jalan mobil
/// (CarController.roadHalfWidth).
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    public CarController car;

    [Header("Rintangan")]
    [Tooltip("Opsional. Kosong = pakai daftar model kendaraan di bawah")]
    public GameObject obstaclePrefab;
    [Tooltip("Daftar nama model kendaraan Resources/KenneyCarKit/FBX (tanpa ekstensi)")]
    public string[] obstacleModelPaths = { };
    public int count = 10;
    [Tooltip("Ukuran kotak tabrakan rintangan: X = lebar, Y = panjang (m)")]
    public Vector2 obstacleSize = new Vector2(1.8f, 4f);

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

    [Header("Pindah lajur (animasi belok)")]
    [Tooltip("Rintangan boleh berpindah lajur dengan gerak belok halus")]
    public bool laneChangeEnabled = true;
    [Tooltip("Selang acak antar percobaan pindah lajur (detik)")]
    public Vector2 laneChangeInterval = new Vector2(2f, 6f);
    [Tooltip("Lama satu animasi pindah lajur (detik)")]
    public Vector2 laneChangeDuration = new Vector2(0.9f, 1.4f);
    [Tooltip("Jarak minimum rintangan di depan mobil sebelum boleh pindah lajur (m)")]
    public float laneChangeMinAhead = 40f;
    [Tooltip("Peluang pindah lajur tiap percobaan (0..1)")]
    [Range(0f, 1f)] public float laneChangeChance = 0.5f;
    [Tooltip("Sudut belok maksimum saat pindah lajur (derajat)")]
    public float maxLaneChangeYaw = 25f;

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
        public float halfW, halfL;          // setengah lebar & panjang kotak tabrakan (m)
        public Renderer[] rends;

        // Pindah lajur
        public int laneIndex;
        public float fromX, targetX;        // x awal & tujuan saat animasi belok
        public float laneT = 1f;            // 0..1, 1 = sudah di lajur tujuan
        public float laneDuration = 1f;
        public float nextLaneChange;
        public float yaw;                   // sudut belok visual (derajat)
    }

    readonly List<Obs> list = new List<Obs>();
    Transform container;
    float prevCarZ;

    void Awake()
    {
        // Validasi awal supaya nama model yang salah kelihatan di Console,
        // bukan hanya rintangan tampil kosong.
        if (obstaclePrefab == null)
            foreach (string path in obstacleModelPaths)
                if (VehicleAssets.Load(path) == null)
                    Debug.LogWarning("ObstacleSpawner: model tidak ada di Resources: " + VehicleAssets.Root + path);
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
        if (container != null) Destroy(container.gameObject);
        container = new GameObject("Obstacles").transform;
        container.SetParent(transform, false);

        list.Clear();
        int n = Mathf.Max(count, 1);
        for (int i = 0; i < n; i++)
        {
            Obs o = CreateObstacle();
            if (o != null) list.Add(o);
        }

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

            UpdateLaneChanges(dt, carZ);
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
        {
            o.t.position = new Vector3(o.x, 0f, o.z);
            o.t.rotation = Quaternion.Euler(0f, o.yaw, 0f);
        }

        prevCarZ = carZ;
    }

    void CheckCollisions(float carZ)
    {
        float carX = car.LateralX;
        float halfCarW = carSize.x * 0.5f * hitboxScale;
        float halfCarL = carSize.y * 0.5f * hitboxScale;
        float scale = hitboxScale;

        // Rentang z yang dilalui mobil sejak frame lalu
        float carMinZ = Mathf.Min(prevCarZ, carZ) - halfCarL;
        float carMaxZ = Mathf.Max(prevCarZ, carZ) + halfCarL;

        foreach (Obs o in list)
        {
            if (!o.t.gameObject.activeSelf) continue;

            bool overlapX = Mathf.Abs(o.x - carX) < (o.halfW * scale + halfCarW);
            bool overlapZ = (o.z + o.halfL * scale) > carMinZ && (o.z - o.halfL * scale) < carMaxZ;
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

        string modelName = null;
        GameObject model = null;

        if (obstaclePrefab == null)
        {
            if (obstacleModelPaths.Length > 0)
            {
                modelName = obstacleModelPaths[Random.Range(0, obstacleModelPaths.Length)];
                model = VehicleAssets.Load(modelName);
                if (model == null)
                    Debug.LogWarning("ObstacleSpawner: model tidak ditemukan: " + VehicleAssets.Root + modelName);
            }

            // Cadangan: tetap kendaraan, bukan kubus/cone, supaya rintangan
            // tidak pernah tampil berupa kotak.
            if (model == null)
            {
                modelName = "sedan";
                model = VehicleAssets.Load(modelName);
            }

            if (model == null)
            {
                Debug.LogError("ObstacleSpawner: tidak ada model kendaraan di " + VehicleAssets.Root);
                return null;
            }

            go = Instantiate(model, container);
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            VehicleAssets.FixMaterials(go);
            Vector2 fp = VehicleAssets.Footprint(modelName);
            // Koreksi skala bila FBX diimpor dengan satuan berbeda (mis. cm).
            VehicleAssets.EnforceFootprint(go, fp);
            o.halfW = fp.x * 0.5f;
            o.halfL = fp.y * 0.5f;
        }
        else
        {
            go = Instantiate(obstaclePrefab, container);
            o.halfW = obstacleSize.x * 0.5f;
            o.halfL = obstacleSize.y * 0.5f;
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
        o.laneIndex = Random.Range(0, laneX.Length);
        o.x = laneX[o.laneIndex];
        o.fromX = o.x;
        o.targetX = o.x;
        o.laneT = 1f;
        o.yaw = 0f;
        o.laneDuration = 1f;
        o.nextLaneChange = Random.Range(laneChangeInterval.x, laneChangeInterval.y);
        o.speed = Random.Range(trafficSpeed.x, trafficSpeed.y);
        o.t.gameObject.SetActive(true);
    }

    /// <summary>
    /// Animasi pindah lajur: posisi x diinterpolasi halus (SmoothStep) dari lajur
    /// asal ke lajur tujuan, dan objek dimiringkan sesuai arah belok. Karena x
    /// bergerak kontinu, kotak tabrakan ikut bergeser realtime.
    /// </summary>
    void UpdateLaneChanges(float dt, float carZ)
    {
        if (!laneChangeEnabled) return;

        foreach (Obs o in list)
        {
            if (!o.t.gameObject.activeSelf) continue;

            if (o.laneT < 1f)
            {
                o.laneT = Mathf.Min(1f, o.laneT + dt / Mathf.Max(0.05f, o.laneDuration));
                float k = Mathf.SmoothStep(0f, 1f, o.laneT);
                float nx = Mathf.Lerp(o.fromX, o.targetX, k);
                float sideVel = (nx - o.x) / Mathf.Max(0.0001f, dt);
                o.x = nx;

                // Hadap ke arah gerak samping; clamp supaya tidak terlalu miring.
                float yaw = Mathf.Atan2(sideVel, Mathf.Max(1f, o.speed)) * Mathf.Rad2Deg;
                o.yaw = Mathf.Clamp(yaw, -maxLaneChangeYaw, maxLaneChangeYaw);
            }
            else
            {
                // Kembalikan setir ke lurus setelah selesai belok.
                o.yaw = Mathf.MoveTowards(o.yaw, 0f, 120f * dt);

                o.nextLaneChange -= dt;
                if (o.nextLaneChange > 0f) continue;
                o.nextLaneChange = Random.Range(laneChangeInterval.x, laneChangeInterval.y);

                float ahead = o.z - carZ;
                bool safeDistance = ahead > laneChangeMinAhead && ahead < despawnAhead * 0.6f;
                if (safeDistance && Random.value < laneChangeChance)
                    TryStartLaneChange(o);
            }
        }
    }

    /// <summary>Pilih lajur tujuan yang kosong, lalu mulai animasi belok.</summary>
    void TryStartLaneChange(Obs o)
    {
        if (laneX.Length < 2) return;

        int dir = Random.value < 0.5f ? -1 : 1;
        int target = o.laneIndex + dir;
        if (target < 0 || target >= laneX.Length) target = o.laneIndex - dir;
        if (target < 0 || target >= laneX.Length || target == o.laneIndex) return;

        // Jangan pindah ke lajur yang sudah ada rintangan lain dekat z yang sama.
        foreach (Obs b in list)
        {
            if (b == o || !b.t.gameObject.activeSelf) continue;
            if (b.laneIndex == target && Mathf.Abs(b.z - o.z) < 14f) return;
        }

        o.laneIndex = target;
        o.fromX = o.x;
        o.targetX = laneX[target];
        o.laneT = 0f;
        o.laneDuration = Random.Range(laneChangeDuration.x, laneChangeDuration.y);
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
}
