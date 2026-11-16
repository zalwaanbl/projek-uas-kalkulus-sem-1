using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Jalan tanpa akhir dengan mendaur ulang beberapa potongan (segment).
/// Segment yang sudah tertinggal di belakang mobil dipindah ke depan,
/// jadi jumlah objek di scene tetap, tidak membuat objek baru terus-menerus.
///
/// Mode 1 (tanpa aset): kalau "segmentPrefab" kosong, segment dibuat otomatis
///   dari kubus: aspal, garis tengah, curb merah-putih, jalur sepeda, zebra cross.
/// Mode 2 (dengan aset): isi "segmentPrefab" dengan prefab jalanmu sendiri
///   (pivot di ujung belakang, panjang = segmentLength).
///
/// Dekorasi (pohon & rumah) memakai model Resources/Scenery dan hanya
/// dipindah posisinya saat segment didaur ulang, tidak dibuat ulang.
/// </summary>
public class RoadSpawner : MonoBehaviour
{
    public CarController car;

    [Header("Segment")]
    public GameObject segmentPrefab;          // opsional
    public float segmentLength = 50f;         // m
    public int segmentCount = 12;             // total segment yang hidup
    [Tooltip("Segment didaur ulang setelah tertinggal sejauh ini di belakang mobil (m)")]
    public float behindDistance = 30f;

    [Header("Jalan otomatis (jika tanpa prefab)")]
    public float roadWidth = 10f;
    public float curbLength = 5f;
    public float grassWidth = 300f;
    public Material roadMaterial;             // opsional (mis. material aspal)
    public Material grassMaterial;            // opsional
    public Color roadColor = new Color(0.22f, 0.22f, 0.24f);
    public Color grassColor = new Color(0.25f, 0.55f, 0.2f);

    [Header("Jalur sepeda & zebra cross")]
    [Tooltip("Lebar jalur sepeda di tepi kanan aspal (m)")]
    public float bikeLaneWidth = 1.8f;
    public Color bikeLaneColor = new Color(0.42f, 0.2f, 0.16f);

    [Header("Dekorasi pinggir jalan (Resources/Scenery)")]
    public int decorPerSegment = 8;
    public float decorMinSide = 10f;          // jarak minimum dari tengah jalan (m)
    public float decorMaxSide = 40f;
    [Range(0f, 1f)] public float houseChance = 0.25f;
    public Vector2 treeHeightRange = new Vector2(4f, 8f);
    public Vector2 houseHeightRange = new Vector2(5f, 9f);

    class Seg
    {
        public Transform root;
        public List<Transform> decor = new List<Transform>();
    }

    readonly Queue<Seg> segments = new Queue<Seg>();
    Material matWhite, matRed, matRoad, matGrass;
    Material matBikeLane;
    Material createdRoad, createdGrass, createdBikeLane;   // material buatan sendiri yang perlu dihapus
    float nextZ;
    float lastDistance;

    void Awake()
    {
        matWhite = CommonUtil.ColorMaterial(Color.white);
        matRed = CommonUtil.ColorMaterial(new Color(0.85f, 0.1f, 0.1f));
        matRoad = roadMaterial != null ? roadMaterial : (createdRoad = CommonUtil.ColorMaterial(roadColor));
        matGrass = grassMaterial != null ? grassMaterial : (createdGrass = CommonUtil.ColorMaterial(grassColor));

        matBikeLane = createdBikeLane = CommonUtil.ColorMaterial(bikeLaneColor);
    }

    void OnDestroy()
    {
        DestroyMat(matWhite);
        DestroyMat(matRed);
        DestroyMat(createdRoad);
        DestroyMat(createdGrass);
        DestroyMat(createdBikeLane);
    }

    static void DestroyMat(Material m)
    {
        if (m != null) Destroy(m);
    }

    void Start()
    {
        for (int i = 0; i < segmentCount; i++)
            segments.Enqueue(CreateSegment());
        Layout();
    }

    void Update()
    {
        if (car == null || segments.Count == 0) return;
        float carZ = car.transform.position.z;

        // Mobil di-reset ke belakang (tombol R): susun ulang jalan dari awal.
        // Dua sinyal: posisi melompat mundur, atau jarak tempuh mengecil.
        if (carZ < segments.Peek().root.position.z - 1f || car.Distance + 0.001f < lastDistance)
        {
            Layout();
            lastDistance = car.Distance;
            return;
        }

        // Daur ulang segment yang sudah jauh di belakang
        while (segments.Peek().root.position.z + segmentLength < carZ - behindDistance)
        {
            Seg s = segments.Dequeue();
            s.root.position = new Vector3(0f, 0f, nextZ);
            nextZ += segmentLength;
            RandomizeDecor(s);
            segments.Enqueue(s);
        }

        lastDistance = car.Distance;
    }

    // Susun semua segment berurutan, dimulai satu segment di belakang mobil
    void Layout()
    {
        float startZ = (car != null ? car.transform.position.z : 0f) - segmentLength;
        nextZ = startZ;
        foreach (Seg s in segments)
        {
            s.root.position = new Vector3(0f, 0f, nextZ);
            nextZ += segmentLength;
            RandomizeDecor(s);
        }
    }

    // ------------------------------------------------------------
    Seg CreateSegment()
    {
        Seg seg = new Seg();

        if (segmentPrefab != null)
        {
            seg.root = Instantiate(segmentPrefab, transform).transform;
        }
        else
        {
            seg.root = BuildDefaultSegment();
            seg.root.SetParent(transform);
        }

        // Dekorasi dari Resources/Scenery: dibuat sekali per segment, lalu
        // dipindah-pindah saat segment didaur ulang (tidak instansiasi terus).
        int n = Mathf.Max(0, decorPerSegment);
        for (int i = 0; i < n; i++)
        {
            bool house = Random.value < houseChance;
            string path = house
                ? SceneryAssets.Houses[Random.Range(0, SceneryAssets.Houses.Length)]
                : SceneryAssets.Trees[Random.Range(0, SceneryAssets.Trees.Length)];
            float height = house
                ? Random.Range(houseHeightRange.x, houseHeightRange.y)
                : Random.Range(treeHeightRange.x, treeHeightRange.y);

            Transform d = SceneryAssets.Spawn(path, seg.root, Vector3.zero, 0f, height);
            if (d != null) seg.decor.Add(d);
        }
        return seg;
    }

    void RandomizeDecor(Seg seg)
    {
        for (int i = 0; i < seg.decor.Count; i++)
        {
            Transform d = seg.decor[i];
            if (d == null) continue;

            float side = (i % 2 == 0) ? -1f : 1f;   // selang-seling kiri dan kanan
            float x = side * Random.Range(decorMinSide, decorMaxSide);
            float z = Random.Range(0f, segmentLength);

            // Menghadap jalan: sisi kanan menghadap -X, sisi kiri menghadap +X.
            float yaw = (side > 0f ? -90f : 90f) + Random.Range(-20f, 20f);

            d.localPosition = new Vector3(x, 0f, z);
            d.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }


    // ------------------------------------------------------------
    // Info untuk spawner lain (pejalan kaki & pesepeda)
    // ------------------------------------------------------------

    /// <summary>Posisi x tengah jalur sepeda (tepi kanan jalan).</summary>
    public float BikeLaneX => roadWidth * 0.5f - bikeLaneWidth * 0.5f;

    /// <summary>Posisi x trotoar tempat pejalan kaki menunggu sebelum menyeberang.</summary>
    public float SidewalkX => roadWidth * 0.5f + 0.7f;

    /// <summary>Zebra cross hanya ada kalau jalan dibuat otomatis (tanpa prefab).</summary>
    public bool HasCrossings => segmentPrefab == null;

    /// <summary>
    /// Zebra cross terdekat di depan <paramref name="fromZ"/>. Satu zebra ada di
    /// tengah tiap segment. False kalau tidak ada yang masuk jangkauan.
    /// </summary>
    public bool TryGetCrossingAhead(float fromZ, float maxDistance, out float z)
    {
        z = 0f;
        bool found = false;
        foreach (Seg s in segments)
        {
            float center = s.root.position.z + segmentLength * 0.5f;
            if (center <= fromZ || center - fromZ > maxDistance) continue;
            if (!found || center < z) { z = center; found = true; }
        }
        return found;
    }

    Transform BuildDefaultSegment()
    {
        Transform root = new GameObject("RoadSegment").transform;

        // Rumput (di bawah)
        Cube(root, matGrass, new Vector3(0f, -0.15f, segmentLength * 0.5f),
             new Vector3(grassWidth, 0.1f, segmentLength));

        // Aspal (permukaan atas di y = 0)
        Cube(root, matRoad, new Vector3(0f, -0.05f, segmentLength * 0.5f),
             new Vector3(roadWidth, 0.1f, segmentLength));

        // Garis tengah putus-putus
        int dashes = Mathf.Max(1, Mathf.RoundToInt(segmentLength / 10f));
        float dashStep = segmentLength / dashes;
        for (int i = 0; i < dashes; i++)
        {
            Cube(root, matWhite, new Vector3(0f, 0.01f, (i + 0.5f) * dashStep),
                 new Vector3(0.25f, 0.02f, dashStep * 0.5f));
        }

        // Curb merah-putih di kiri dan kanan
        int curbs = Mathf.Max(1, Mathf.RoundToInt(segmentLength / curbLength));
        float curbStep = segmentLength / curbs;
        for (int i = 0; i < curbs; i++)
        {
            Material m = (i % 2 == 0) ? matRed : matWhite;
            float z = (i + 0.5f) * curbStep;
            float x = roadWidth * 0.5f + 0.4f;
            Cube(root, m, new Vector3(-x, 0.02f, z), new Vector3(0.8f, 0.06f, curbStep));
            Cube(root, m, new Vector3(x, 0.02f, z), new Vector3(0.8f, 0.06f, curbStep));
        }

        // Zebra cross: satu per segment, di tengah. Pejalan kaki hanya menyeberang di sini.
        int bars = 5;
        float barDepth = 0.5f;
        float barGap = 0.5f;
        float crossDepth = bars * barDepth + (bars - 1) * barGap;
        float crossZ0 = segmentLength * 0.5f - crossDepth * 0.5f;
        for (int i = 0; i < bars; i++)
        {
            Cube(root, matWhite,
                 new Vector3(0f, 0.02f, crossZ0 + i * (barDepth + barGap) + barDepth * 0.5f),
                 new Vector3(roadWidth - 1f, 0.02f, barDepth));
        }

        // Jalur sepeda: strip aspal berbeda warna di tepi kanan, dengan garis pemisah.
        float laneOuter = roadWidth * 0.5f;
        float laneInner = laneOuter - bikeLaneWidth;
        Cube(root, matBikeLane,
             new Vector3((laneInner + laneOuter) * 0.5f, 0.005f, segmentLength * 0.5f),
             new Vector3(bikeLaneWidth, 0.02f, segmentLength));
        Cube(root, matWhite,
             new Vector3(laneInner + 0.05f, 0.02f, segmentLength * 0.5f),
             new Vector3(0.1f, 0.02f, segmentLength));
        return root;
    }

    static void Cube(Transform parent, Material mat, Vector3 localPos, Vector3 scale)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(c.GetComponent<Collider>());
        c.transform.SetParent(parent, false);
        c.transform.localPosition = localPos;
        c.transform.localScale = scale;
        c.GetComponent<Renderer>().sharedMaterial = mat;
    }

}