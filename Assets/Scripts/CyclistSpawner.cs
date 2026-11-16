using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pesepeda di bahu jalan (shoulder).
///
/// - Spawn di luar curb (x = 5.5..7.5 m dari tengah), jadi hanya menyentuh
///   mobil kalau pemain menempel ke tepi jalan.
/// - Bergerak searah lalu lintas (+Z) lebih pelan dari mobil.
/// - Tidak memakai Rigidbody/Collider; tabrakan dicek manual dengan kotak (AABB)
///   seperti ObstacleSpawner (pakai rentang z frame lalu..sekarang supaya tidak tembus).
/// - Menabrak pesepeda = Crash (mengurangi nyawa).
/// </summary>
public class CyclistSpawner : MonoBehaviour
{
    public CarController car;

    [Header("Jumlah & penempatan")]
    public int count = 8;
    [Tooltip("Jarak pesepeda pertama di depan mobil (m)")]
    public float firstDistance = 300f;
    [Tooltip("Jarak dari tengah jalan (m), di bahu jalan")]
    public Vector2 sideRange = new Vector2(4.5f, 6f);
    [Tooltip("Jarak acak antar pesepeda (m)")]
    public Vector2 gapRange = new Vector2(120f, 280f);

    [Header("Gerak")]
    public float bikeSpeed = 5f;       // m/s, searah lalu lintas (+Z)
    public float despawnBehind = 40f;
    public float despawnAhead = 900f;

    [Header("Ukuran tabrakan")]
    public Vector2 bikeSize = new Vector2(0.6f, 4f);    // lebar, panjang (sepeda + pengemudi)
    public Vector2 carSize = new Vector2(1.8f, 4f);
    [Range(0.5f, 1f)] public float hitboxScale = 0.85f;
    public float keepSpeedFraction = 0.4f;
    public float crashDuration = 0.4f;
    public float invulnerableTime = 1.5f;

    class Cyclist
    {
        public Transform t;
        public float x, z;
    }

    readonly List<Cyclist> list = new List<Cyclist>();
    Transform container;
    float prevCarZ;

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<CarController>();
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
        for (int i = 0; i < n; i++) list.Add(new Cyclist { t = CreateCyclist() });

        float carZ = car != null ? car.transform.position.z : 0f;
        float z = carZ + firstDistance;
        foreach (Cyclist c in list)
        {
            Place(c, z);
            z += NextGap();
        }
        prevCarZ = carZ;
    }

    Transform CreateCyclist()
    {
        Transform root = new GameObject("Cyclist").transform;
        root.SetParent(container, false);

        // Pengemudi (kubus)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(root, false);
        body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        body.transform.localScale = new Vector3(0.4f, 1.1f, 0.7f);
        body.GetComponent<Renderer>().sharedMaterial = CommonUtil.ColorMaterial(new Color(0.1f, 0.1f, 0.1f));

        // Roda depan & belakang (silinder diputar 90° mengelilingi X → menggelinding di Z)
        CreateWheel(root, 0.4f);
        CreateWheel(root, -0.4f);

        return root;
    }

    void CreateWheel(Transform root, float localZ)
    {
        GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(wheel.GetComponent<Collider>());
        wheel.transform.SetParent(root, false);
        wheel.transform.localPosition = new Vector3(0f, 0.35f, localZ);
        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // sumbu silinder jadi mendatar (X)
        wheel.transform.localScale = new Vector3(0.7f, 0.06f, 0.7f);    // tebal roda tipis
        wheel.GetComponent<Renderer>().sharedMaterial = CommonUtil.ColorMaterial(new Color(0.15f, 0.15f, 0.15f));
    }

    float NextGap() => Random.Range(gapRange.x, gapRange.y);

    void Place(Cyclist c, float z)
    {
        c.z = z;
        float side = Random.value < 0.5f ? -1f : 1f;
        c.x = side * Random.Range(sideRange.x, sideRange.y);
        c.t.gameObject.SetActive(true);
        c.t.position = new Vector3(c.x, 0f, c.z);
    }

    void Update()
    {
        if (car == null || list.Count == 0) return;

        float carZ = car.transform.position.z;

        // Mobil di-reset: susun ulang supaya tidak menghantam semua pesepeda sekaligus.
        if (carZ + 5f < prevCarZ)
        {
            float z = carZ + firstDistance;
            foreach (Cyclist c in list) { Place(c, z); z += NextGap(); }
            prevCarZ = carZ;
            return;
        }

        if (!car.controlsEnabled) return;   // hanya bergerak saat balapan

        float dt = Time.deltaTime;

        foreach (Cyclist c in list)
        {
            c.z += bikeSpeed * dt;
            c.t.position = new Vector3(c.x, 0f, c.z);

            if (c.z < carZ - despawnBehind) Place(c, carZ + 150f + NextGap());
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
        float scale = hitboxScale;

        float carMinZ = Mathf.Min(prevCarZ, carZ) - halfCarL;
        float carMaxZ = Mathf.Max(prevCarZ, carZ) + halfCarL;

        float halfW = bikeSize.x * 0.5f;
        float halfL = bikeSize.y * 0.5f;

        foreach (Cyclist c in list)
        {
            if (!c.t.gameObject.activeSelf) continue;

            bool overlapX = Mathf.Abs(c.x - carX) < (halfW * scale + halfCarW);
            bool overlapZ = (c.z + halfL * scale) > carMinZ && (c.z - halfL * scale) < carMaxZ;
            if (!overlapX || !overlapZ) continue;

            if (car.Crash(keepSpeedFraction, crashDuration, invulnerableTime))
                c.t.gameObject.SetActive(false);   // pesepeda yang ditabrak hilang
        }
    }
}
