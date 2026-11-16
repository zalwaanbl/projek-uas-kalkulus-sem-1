using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pesepeda di jalur khusus sepeda (strip paling kanan aspal, lihat
/// RoadSpawner.BikeLaneX). Jalur ini bekas bahu jalan: mobil hanya bisa
/// menyentuhnya kalau pemain menempel ke tepi kanan.
///
/// - Model sepeda + pengemudi dari Resources/Scenery (CC0/CC-BY, lihat CREDITS.md).
/// - Bergerak searah lalu lintas (+Z), lebih pelan dari mobil.
/// - Tidak memakai Rigidbody/Collider; tabrakan dicek manual dengan kotak (AABB)
///   memakai rentang z frame lalu..sekarang supaya tidak bisa ditembus.
/// - Tertabrak = mobil Crash, dan sepeda beserta pengemudinya terlempar
///   sebagai ragdoll (Ragdoll.cs).
/// </summary>
public class CyclistSpawner : MonoBehaviour
{
    public CarController car;
    public RoadSpawner road;

    [Header("Jumlah & penempatan")]
    public int count = 8;
    [Tooltip("Jarak pesepeda pertama di depan mobil (m)")]
    public float firstDistance = 300f;
    [Tooltip("Jarak acak antar pesepeda (m)")]
    public Vector2 gapRange = new Vector2(120f, 280f);

    [Header("Gerak")]
    public float bikeSpeed = 5f;        // m/s, searah lalu lintas (+Z)
    public float despawnBehind = 40f;
    public float despawnAhead = 900f;
    public float respawnDelay = 1.4f;

    [Header("Ukuran tabrakan")]
    public Vector2 bikeSize = new Vector2(0.8f, 2.6f);   // lebar, panjang (sepeda + pengemudi)
    public Vector2 carSize = new Vector2(1.8f, 4f);
    [Range(0.5f, 1f)] public float hitboxScale = 0.85f;
    public float keepSpeedFraction = 0.4f;
    public float crashDuration = 0.4f;
    public float invulnerableTime = 1.5f;

    class Cyclist
    {
        public Transform t;
        public float x, z, speed, respawn;
    }

    readonly List<Cyclist> list = new List<Cyclist>();
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
        container = new GameObject("Pesepeda").transform;
        container.SetParent(transform, false);
        list.Clear();

        int n = Mathf.Max(count, 1);
        for (int i = 0; i < n; i++)
        {
            Cyclist c = new Cyclist { t = CreateCyclist() };
            list.Add(c);
        }

        float carZ = car != null ? car.transform.position.z : 0f;
        float z = carZ + firstDistance;
        foreach (Cyclist c in list)
        {
            Place(c, z);
            z += Random.Range(gapRange.x, gapRange.y);
        }
        prevCarZ = carZ;
    }

    Transform CreateCyclist()
    {
        Transform root = new GameObject("Pesepeda").transform;
        root.SetParent(container, false);

        SceneryAssets.SpawnBike(root, Vector3.zero, 0f, 1.9f);
        // Pengemudi di atas sadel: lebih kecil, naik dari alas sepeda, menghadap setang.
        SceneryAssets.SpawnPerson(root, new Vector3(0f, 0.42f, -0.35f), 0f, 1.35f);
        return root;
    }

    float LaneX()
    {
        return road != null ? road.BikeLaneX : 5.1f;
    }

    void Place(Cyclist c, float z)
    {
        c.z = z;
        c.x = LaneX() + Random.Range(-0.25f, 0.25f);
        c.speed = bikeSpeed * Random.Range(0.8f, 1.25f);
        if (c.t == null) c.t = CreateCyclist();
        c.t.gameObject.SetActive(true);
        c.t.position = new Vector3(c.x, 0f, c.z);
        c.t.rotation = Quaternion.identity;
    }

    void Update()
    {
        if (car == null || list.Count == 0) return;

        float carZ = car.transform.position.z;

        // Mobil di-reset: susun ulang supaya tidak menghantam semua pesepeda sekaligus.
        if (carZ + 5f < prevCarZ)
        {
            float z = carZ + firstDistance;
            foreach (Cyclist c in list) { Place(c, z); z += Random.Range(gapRange.x, gapRange.y); }
            prevCarZ = carZ;
            return;
        }

        if (!car.controlsEnabled) return;   // hanya bergerak saat balapan

        float dt = Time.deltaTime;

        foreach (Cyclist c in list)
        {
            if (c.t == null)
            {
                c.respawn -= dt;
                if (c.respawn <= 0f) Place(c, carZ + firstDistance + Random.Range(0f, 200f));
                continue;
            }

            c.z += c.speed * dt;
            c.t.position = new Vector3(c.x, 0f, c.z);

            if (c.z < carZ - despawnBehind) Place(c, carZ + 150f + Random.Range(gapRange.x, gapRange.y));
            else if (c.z - carZ > despawnAhead) Place(c, carZ + firstDistance + Random.Range(0f, 100f));
        }

        CheckCollisions(carZ);
        prevCarZ = carZ;
    }

    void CheckCollisions(float carZ)
    {
        float carX = car.LateralX;
        float halfCarW = carSize.x * 0.5f * hitboxScale;
        float halfCarL = carSize.y * 0.5f * hitboxScale;
        float halfW = bikeSize.x * 0.5f * hitboxScale;
        float halfL = bikeSize.y * 0.5f * hitboxScale;

        float carMinZ = Mathf.Min(prevCarZ, carZ) - halfCarL;
        float carMaxZ = Mathf.Max(prevCarZ, carZ) + halfCarL;

        foreach (Cyclist c in list)
        {
            if (c.t == null) continue;

            bool overlapX = Mathf.Abs(c.x - carX) < (halfW + halfCarW);
            bool overlapZ = (c.z + halfL) > carMinZ && (c.z - halfL) < carMaxZ;
            if (!overlapX || !overlapZ) continue;

            if (car.Crash(keepSpeedFraction, crashDuration, invulnerableTime))
            {
                Transform body = c.t;
                c.t = null;
                c.respawn = respawnDelay;

                Vector3 throwVel = car.transform.forward * (car.Speed * 0.5f);
                Ragdoll.Throw(body, throwVel);
            }
        }
    }
}
