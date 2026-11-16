using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pejalan kaki yang menyeberang HANYA di zebra cross.
///
/// - Tiap zebra ada di tengah segment jalan (lihat RoadSpawner.TryGetCrossingAhead).
/// - Alur tiap orang: berjalan di trotoar menuju zebra -> menunggu di tepi ->
///   menyeberang saat mobil mendekat -> sampai seberang, lalu pindah ke zebra
///   berikutnya di depan.
/// - Tidak memakai Rigidbody/Collider. Tabrakan dicek manual dengan kotak (AABB)
///   memakai rentang z antara frame lalu dan sekarang, jadi mobil secepat apa
///   pun tidak bisa menembus.
/// - Tertabrak = mobil Crash, dan tubuhnya dilempar sebagai ragdoll (Ragdoll.cs).
/// </summary>
public class PedestrianSpawner : MonoBehaviour
{
    public CarController car;
    public RoadSpawner road;

    [Header("Jumlah & penempatan")]
    public int count = 14;
    [Tooltip("Jarak pejalan kaki pertama di depan mobil (m)")]
    public float firstDistance = 80f;
    [Tooltip("Sejauh apa zebra dicari di depan mobil (m)")]
    public float crossingSearch = 900f;

    [Header("Gerak")]
    public float walkSpeed = 1.8f;      // berjalan di trotoar (m/s)
    public float crossSpeed = 2.2f;     // menyeberang (m/s)
    [Tooltip("Mulai menyeberang saat mobil sejauh ini dari zebra (m)")]
    public float crossTrigger = 60f;
    [Tooltip("Acak posisi z di dalam zebra saat menyeberang (m)")]
    public float crossSpread = 1.2f;
    [Tooltip("Lama menunggu di trotoar sebelum menyeberang (detik)")]
    public Vector2 waitRange = new Vector2(0f, 1.2f);
    public float respawnDelay = 1.4f;

    [Header("Ukuran tabrakan")]
    public Vector2 pedSize = new Vector2(0.7f, 0.7f);
    public Vector2 carSize = new Vector2(1.8f, 4f);
    [Range(0.5f, 1f)] public float hitboxScale = 0.85f;

    enum State { Walk, Wait, Cross, Dead }

    class Ped
    {
        public Transform t;
        public float x, z;          // posisi sekarang
        public float targetX;       // posisi seberang
        public float crossingZ;     // z zebra tempat menyeberang
        public float wait;
        public float respawn;
        public int side;            // -1 mulai dari kiri, +1 mulai dari kanan
        public State state;
        public float phase;         // fase ayunan langkah
        public Transform legL, legR, armL, armR;   // bagian tubuh untuk animasi jalan
    }

    readonly List<Ped> list = new List<Ped>();
    Transform container;
    float prevCarZ;

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<CarController>();
        if (road == null) road = FindAnyObjectByType<RoadSpawner>();
        Build();
    }

    void OnDestroy()
    {
        if (container != null) Destroy(container.gameObject);
    }

    void Build()
    {
        if (container != null) Destroy(container.gameObject);
        container = new GameObject("PejalanKaki").transform;
        container.SetParent(transform, false);
        list.Clear();

        int n = Mathf.Max(count, 1);
        for (int i = 0; i < n; i++) list.Add(new Ped());

        float carZ = car != null ? car.transform.position.z : 0f;
        float z = carZ + firstDistance;
        foreach (Ped p in list)
        {
            PlaceAtCrossing(p, z);
            z += Random.Range(25f, 70f);
        }
        prevCarZ = carZ;
    }

    /// <summary>Tempatkan satu orang di zebra terdekat setelah <paramref name="fromZ"/>.</summary>
    bool PlaceAtCrossing(Ped p, float fromZ)
    {
        if (road == null || !road.HasCrossings) return false;

        float crossingZ;
        if (!road.TryGetCrossingAhead(fromZ, crossingSearch, out crossingZ)) return false;

        if (p.t == null)
        {
            p.t = SceneryAssets.SpawnPerson(container, Vector3.zero, 0f, Random.Range(1.55f, 1.9f));
            if (p.t == null) return false;

            p.phase = Random.Range(0f, Mathf.PI * 2f);
            p.legL = p.t.Find("legleft1");
            p.legR = p.t.Find("legright1");
            p.armL = p.t.Find("armleft1");
            p.armR = p.t.Find("armright1");
        }

        p.crossingZ = crossingZ;
        p.side = Random.value < 0.5f ? -1 : 1;
        p.x = p.side * road.SidewalkX;
        p.targetX = -p.x;
        p.z = crossingZ - Random.Range(6f, 18f);   // mendekati zebra dari belakang
        p.wait = 0f;
        p.state = State.Walk;

        p.t.gameObject.SetActive(true);
        p.t.position = new Vector3(p.x, 0f, p.z);
        p.t.rotation = Quaternion.identity;
        return true;
    }

    void Update()
    {
        if (car == null || list.Count == 0) return;

        float dt = Time.deltaTime;
        float carZ = car.transform.position.z;

        // Mobil di-reset (tombol R / mulai baru): susun ulang semua orang di zebra
        // berikutnya dan lewati cek tabrakan frame ini.
        if (carZ + 5f < prevCarZ)
        {
            float z = carZ + firstDistance;
            foreach (Ped p in list)
            {
                DropBody(p);
                PlaceAtCrossing(p, z);
                z += Random.Range(25f, 70f);
            }
            prevCarZ = carZ;
            return;
        }

        if (!car.controlsEnabled)
        {
            prevCarZ = carZ;
            return;
        }

        foreach (Ped p in list)
        {
            if (p.state == State.Dead)
            {
                p.respawn -= dt;
                if (p.respawn <= 0f) PlaceAtCrossing(p, carZ + 80f);
                continue;
            }
            if (p.t == null) continue;

            Step(p, dt, carZ);
        }

        CheckCollisions(carZ);
        prevCarZ = carZ;
    }

    void Step(Ped p, float dt, float carZ)
    {
        // Sudah lewat jauh di belakang mobil: pindah ke zebra depan.
        if (p.z < carZ - 30f)
        {
            PlaceAtCrossing(p, carZ + 120f);
            return;
        }

        if (p.state == State.Walk)
        {
            p.z = Mathf.MoveTowards(p.z, p.crossingZ, walkSpeed * dt);
            if (Mathf.Abs(p.z - p.crossingZ) < 0.05f)
            {
                p.state = State.Wait;
                p.wait = Random.Range(waitRange.x, waitRange.y);
            }
        }
        else if (p.state == State.Wait)
        {
            p.wait -= dt;
            float ahead = p.crossingZ - carZ;
            if (p.wait <= 0f && ahead > 4f && ahead < crossTrigger)
            {
                p.state = State.Cross;
                p.z = p.crossingZ + Random.Range(-crossSpread, crossSpread);
            }
        }
        else if (p.state == State.Cross)
        {
            p.x = Mathf.MoveTowards(p.x, p.targetX, crossSpeed * dt);
            if (Mathf.Abs(p.x - p.targetX) < 0.05f)
            {
                PlaceAtCrossing(p, carZ + 120f);   // sampai seberang
                return;
            }
        }

        // Animasi jalan: kaki dan lengan diayun berlawanan arah. Saat menunggu
        // di tepi zebra tubuhnya diam.
        float swing = p.state == State.Wait
            ? 0f
            : 32f * Mathf.Sin(Time.time * 7f + p.phase);
        if (p.legL != null) p.legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        if (p.legR != null) p.legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        if (p.armL != null) p.armL.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);
        if (p.armR != null) p.armR.localRotation = Quaternion.Euler(swing * 0.8f, 0f, 0f);

        // 0,06 = tinggi permukaan trotoar; bob kecil supaya kaki tidak tembus.
        float bob = 0.02f * Mathf.Sin(Time.time * 7f + p.z);
        p.t.position = new Vector3(p.x, 0.08f + bob, p.z);

        // Berjalan menyusuri trotoar menghadap +Z, menyeberang menghadap arah tujuan.
        float yaw = p.state == State.Walk ? 0f : (p.x > p.targetX ? -90f : 90f);
        p.t.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void CheckCollisions(float carZ)
    {
        float carX = car.LateralX;
        float halfCarW = carSize.x * 0.5f * hitboxScale;
        float halfCarL = carSize.y * 0.5f * hitboxScale;
        float halfPedW = pedSize.x * 0.5f * hitboxScale;
        float halfPedL = pedSize.y * 0.5f * hitboxScale;

        float carMinZ = Mathf.Min(prevCarZ, carZ) - halfCarL;
        float carMaxZ = Mathf.Max(prevCarZ, carZ) + halfCarL;

        foreach (Ped p in list)
        {
            if (p.t == null || p.state == State.Dead) continue;

            bool overlapX = Mathf.Abs(p.x - carX) < (halfPedW + halfCarW);
            bool overlapZ = (p.z + halfPedL) > carMinZ && (p.z - halfPedL) < carMaxZ;
            if (!overlapX || !overlapZ) continue;

            // Tabrak pejalan kaki TIDAK mengurangi kecepatan/nyawa: orangnya saja
            // yang terlempar sebagai ragdoll.
            Transform body = p.t;
            p.t = null;
            p.state = State.Dead;
            p.respawn = respawnDelay;

            Vector3 throwVel = car.transform.forward * Mathf.Max(2f, car.Speed * 0.55f);
            Ragdoll.Throw(body, throwVel);
        }
    }

    static void DropBody(Ped p)
    {
        if (p.t != null)
        {
            Destroy(p.t.gameObject);
            p.t = null;
        }
        p.state = State.Dead;
        p.respawn = 0f;
    }
}
