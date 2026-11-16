using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pejalan kaki di pinggir jalan.
///
/// - Spawn di rumput samping jalan (x = sideMin..sideMax), berjalan sejajar jalan (+Z).
/// - Pejalan kaki yang dekat jalan akan menyeberang (dari sisi ke sisi) saat mobil
///   mendekat ke dalam jarak crossTrigger.
/// - Tidak memakai Rigidbody/Collider. Tabrakan dicek manual dengan kotak (AABB)
///   seperti ObstacleSpawner: rentang z antara frame lalu dan sekarang supaya
///   mobil secepat apa pun tidak bisa menembus.
/// - Menabrak pejalan kaki = Crash (mengurangi nyawa lewat car.Crash()).
/// </summary>
public class PedestrianSpawner : MonoBehaviour
{
    public CarController car;

    [Header("Jumlah & penempatan")]
    public int count = 12;
    [Tooltip("Jarak pertama pejalan kaki di depan mobil (m)")]
    public float firstDistance = 200f;
    [Tooltip("Jarak pejalan kaki dari tengah jalan (m), pinggir jalan")]
    public Vector2 sideRange = new Vector2(6f, 16f);
    [Tooltip("Jarak acak antar pejalan kaki (m)")]
    public Vector2 gapRange = new Vector2(120f, 250f);

    [Header("Gerak")]
    public float walkSpeed = 1.5f;      // berjalan sejajar jalan (m/s)
    public float crossSpeed = 2f;       // menyeberang (m/s)
    [Tooltip("Mulai menyeberang saat mobil di depan sejauh ini (m)")]
    public float crossTrigger = 40f;
    [Tooltip("Pejalan kaki yang sisiannya di luar ini hanya hiasan, tidak menyeberang")]
    public float maxCrossSide = 10f;
    public float despawnBehind = 40f;
    public float despawnAhead = 900f;

    [Header("Ukuran tabrakan")]
    public Vector2 pedSize = new Vector2(0.6f, 0.6f);
    public Vector2 carSize = new Vector2(1.8f, 4f);
    [Range(0.5f, 1f)] public float hitboxScale = 0.85f;
    public float keepSpeedFraction = 0.4f;
    public float crashDuration = 0.4f;
    public float invulnerableTime = 1.5f;

    enum State { Walk, Cross }

    class Ped
    {
        public Transform t;
        public float x, z, sideSign;          // posisi & sisi jalan
        public float baseX;                   // x saat berjalan (tidak menyeberang)
        public float targetX;                 // tujuan x saat menyeberang
        public State state;
        public bool canCross;                 // boleh menyeberang (diketar dekat jalan)
        public bool crossed;                  // sudah menyeberang ke sisi lain
    }

    readonly List<Ped> list = new List<Ped>();
    Transform container;
    float prevCarZ;

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<CarController>();
        Build();
    }

    void Build()
    {
        if (container != null) Destroy(container.gameObject);
        container = new GameObject("PejalanKaki").transform;
        container.SetParent(transform, false);
        list.Clear();

        int n = Mathf.Max(count, 1);
        for (int i = 0; i < n; i++)
        {
            Ped p = new Ped { t = CreatePedestrian() };
            list.Add(p);
        }
        Layout();
    }

    Transform CreatePedestrian()
    {
        var root = new GameObject("Pedestrian").transform;
        root.SetParent(container, false);

        // Tubuh (kapsul): radius 0.3m, tinggi 1.8m -> skala (0.3, 0.9, 0.3)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(root, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.3f, 0.9f, 0.3f);
        body.GetComponent<Renderer>().sharedMaterial = CommonUtil.ColorMaterial(Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f));

        // Kepala (bola kecil di atas tubuh)
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(root, false);
        head.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        head.transform.localScale = Vector3.one * 0.28f;
        head.GetComponent<Renderer>().sharedMaterial = CommonUtil.ColorMaterial(new Color(0.9f, 0.85f, 0.75f));

        return root;
    }

    void Layout()
    {
        float carZ = car != null ? car.transform.position.z : 0f;
        float z = carZ + firstDistance;
        foreach (Ped p in list)
        {
            Place(p, z);
            z += NextGap();
        }
        prevCarZ = carZ;
    }

    void Place(Ped p, float z)
    {
        p.z = z;
        p.sideSign = (Random.value < 0.5f) ? -1f : 1f;
        p.baseX = p.sideSign * Random.Range(sideRange.x, sideRange.y);
        p.x = p.baseX;
        p.targetX = p.baseX;
        p.canCross = Mathf.Abs(p.baseX) < maxCrossSide;
        p.state = State.Walk;
        p.crossed = false;
        p.t.position = new Vector3(p.x, 0f, p.z);
        p.t.rotation = Quaternion.Euler(0f, 0f, 0f); // hadap +Z
        p.t.gameObject.SetActive(true);
    }

    float NextGap()
    {
        return Random.Range(gapRange.x, gapRange.y);
    }

    void Update()
    {
        if (car == null || list.Count == 0) return;

        float dt = Time.deltaTime;
        float carZ = car.transform.position.z;

        // Mobil di-reset (tombol R / mulai baru): susun ulang dan lewati cek tabrakan
        // frame ini supaya rentang z lama tidak menghantam semua pejalan kaki sekaligus.
        if (carZ + 5f < prevCarZ)
        {
            Layout();
            prevCarZ = carZ;
            return;
        }

        bool playing = car.controlsEnabled;
        if (playing)
        {
            foreach (Ped p in list)
            {
                if (p.t.gameObject.activeSelf)
                {
                    // Berjalan sejajar jalan (+Z) dengan bob kecil untuk efek jalan kaki.
                    p.z += walkSpeed * dt;
                    float y = 0.1f * Mathf.Sin(Time.time * 6f + p.z);
                    p.t.position = new Vector3(p.x, y, p.z);

                    // Mulai menyeberang saat mobil mendekat dan pejalan kaki boleh menyeberang.
                    if (!p.crossed && p.canCross && p.state == State.Walk)
                    {
                        float ahead = p.z - carZ;
                        if (ahead > 5f && ahead < crossTrigger)
                        {
                            p.state = State.Cross;
                            p.targetX = -p.sideSign * Mathf.Abs(p.baseX); // sisi seberang
                        }
                    }

                    // Gerak menyeberang.
                    if (p.state == State.Cross)
                    {
                        p.x = Mathf.MoveTowards(p.x, p.targetX, crossSpeed * dt);
                        p.t.position = new Vector3(p.x, 0.1f * Mathf.Sin(Time.time * 6f + p.z), p.z);
                        if (Mathf.Abs(p.x - p.targetX) < 0.05f)
                        {
                            p.state = State.Walk;
                            p.baseX = p.targetX;
                            p.crossed = true;
                        }
                    }
                }

                // Daur ulang juga pejalan kaki yang sedang nonaktif (habis ditabrak)
                // supaya pool tidak habis.
                if (p.z < carZ - despawnBehind) Recycle(p, carZ);
                else if (p.z - carZ > despawnAhead) Place(p, carZ + firstDistance + Random.Range(0f, 80f));
            }

            CheckCollisions(carZ);
        }

        prevCarZ = carZ;
    }

    void Recycle(Ped p, float carZ)
    {
        float far = carZ + 150f;
        Place(p, far + NextGap());
    }

    void CheckCollisions(float carZ)
    {
        float carX = car.LateralX;
        float halfCarW = carSize.x * 0.5f * hitboxScale;
        float halfCarL = carSize.y * 0.5f * hitboxScale;
        float scale = hitboxScale;

        float carMinZ = Mathf.Min(prevCarZ, carZ) - halfCarL;
        float carMaxZ = Mathf.Max(prevCarZ, carZ) + halfCarL;

        foreach (Ped p in list)
        {
            if (!p.t.gameObject.activeSelf) continue;

            bool overlapX = Mathf.Abs(p.x - carX) < (pedSize.x * 0.5f * scale + halfCarW);
            bool overlapZ = (p.z + halfCarL * scale) > carMinZ && (p.z - halfCarL * scale) < carMaxZ;
            if (!overlapX || !overlapZ) continue;

            if (car.Crash(keepSpeedFraction, crashDuration, invulnerableTime))
            {
                p.t.gameObject.SetActive(false); // pejalan kaki yang ditabrak hilang
            }
        }
    }
}
