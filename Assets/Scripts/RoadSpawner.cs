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
    [Tooltip("Lebar trotoar di luar curb, kiri dan kanan (m)")]
    public float sidewalkWidth = 2.5f;
    public Color sidewalkColor = new Color(0.62f, 0.62f, 0.6f);

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

    class Decor
    {
        public Transform t;
        public float minSide;   // jarak minimum ke tengah jalan, sudah termasuk lebar objek
    }

    class Seg
    {
        public Transform root;
        public List<Decor> decor = new List<Decor>();
    }

    readonly Queue<Seg> segments = new Queue<Seg>();
    Material matWhite, matRed, matRoad, matGrass;
    Material matBikeLane, matSidewalk;
    Material createdRoad, createdGrass, createdBikeLane, createdSidewalk;   // material buatan sendiri yang perlu dihapus
    float nextZ;
    float lastDistance;

    /// <summary>Permukaan rumput (m). Dekorasi diletakkan tepat di sini.</summary>
    const float GroundY = -0.1f;

    void Awake()
    {
        matWhite = CommonUtil.ColorMaterial(Color.white);
        matRed = CommonUtil.ColorMaterial(new Color(0.85f, 0.1f, 0.1f));
        matRoad = roadMaterial != null ? roadMaterial : (createdRoad = CommonUtil.ColorMaterial(roadColor));
        matGrass = grassMaterial != null ? grassMaterial : (createdGrass = CommonUtil.ColorMaterial(grassColor));

        matBikeLane = createdBikeLane = CommonUtil.ColorMaterial(bikeLaneColor);
        matSidewalk = createdSidewalk = CommonUtil.ColorMaterial(sidewalkColor);
    }

    void OnDestroy()
    {
        DestroyMat(matWhite);
        DestroyMat(matRed);
        DestroyMat(createdRoad);
        DestroyMat(createdGrass);
        DestroyMat(createdBikeLane);
        DestroyMat(createdSidewalk);
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
        // Jangan pakai selisih kecil: mobil boleh mundur (Distance mengecil) tanpa
        // membuat jalan disusun ulang. Reset sungguhan = jarak melompat jauh.
        if (carZ < segments.Peek().root.position.z - 1f || car.Distance < lastDistance - 100f)
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
            if (d == null) continue;

            // Lebar model diukur supaya rumah/pohon besar tidak menutup trotoar
            // apalagi jalan. Batas = tepi luar trotoar + 1 m + setengah lebar model.
            float halfWidth = SceneryAssets.Bounds(d.gameObject).size.x * 0.5f;
            seg.decor.Add(new Decor
            {
                t = d,
                minSide = SidewalkOuterX + 1f + halfWidth,
            });
        }
        return seg;
    }

    void RandomizeDecor(Seg seg)
    {
        for (int i = 0; i < seg.decor.Count; i++)
        {
            Decor item = seg.decor[i];
            Transform d = item.t;
            if (d == null) continue;

            float side = (i % 2 == 0) ? -1f : 1f;   // selang-seling kiri dan kanan
            float min = Mathf.Max(decorMinSide, item.minSide);
            float max = Mathf.Max(min + 2f, decorMaxSide);
            float x = side * Random.Range(min, max);
            float z = Random.Range(0f, segmentLength);

            // Menghadap jalan: sisi kanan menghadap -X, sisi kiri menghadap +X.
            float yaw = (side > 0f ? -90f : 90f) + Random.Range(-20f, 20f);

            d.localPosition = new Vector3(x, GroundY, z);
            d.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }


    // ------------------------------------------------------------
    // Info untuk spawner lain (pejalan kaki & pesepeda)
    // ------------------------------------------------------------

    /// <summary>Posisi x tengah jalur sepeda (tepi kanan jalan).</summary>
    public float BikeLaneX => roadWidth * 0.5f - bikeLaneWidth * 0.5f;

    /// <summary>Posisi x tengah trotoar, tempat pejalan kaki berjalan.</summary>
    public float SidewalkX => roadWidth * 0.5f + 0.8f + sidewalkWidth * 0.5f;

    /// <summary>Tepi luar trotoar (m dari tengah jalan).</summary>
    public float SidewalkOuterX => roadWidth * 0.5f + 0.8f + sidewalkWidth;

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
        Cube(root, matGrass, new Vector3(0f, GroundY - 0.05f, segmentLength * 0.5f),
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

        // Trotoar kiri dan kanan, di luar curb. Pejalan kaki berjalan di sini.
        float walkX = SidewalkX;
        Cube(root, matSidewalk, new Vector3(-walkX, 0.03f, segmentLength * 0.5f),
             new Vector3(sidewalkWidth, 0.06f, segmentLength));
        Cube(root, matSidewalk, new Vector3(walkX, 0.03f, segmentLength * 0.5f),
             new Vector3(sidewalkWidth, 0.06f, segmentLength));

        // Zebra cross: satu per segment, di tengah. Garis putih memanjang searah
        // laju mobil (vertikal dari pandangan pengemudi), berbaris melintang jalan.
        // Pejalan kaki hanya menyeberang di sini.
        const float zebraDepth = 4f;   // panjang garis searah laju (m)
        const float barWidth = 0.6f;   // lebar tiap garis (m)
        const float barGap = 0.6f;
        int bars = Mathf.Max(1, Mathf.FloorToInt((roadWidth - 1f + barGap) / (barWidth + barGap)));
        float totalWidth = bars * barWidth + (bars - 1) * barGap;
        float x0 = -totalWidth * 0.5f + barWidth * 0.5f;
        for (int i = 0; i < bars; i++)
        {
            Cube(root, matWhite,
                 new Vector3(x0 + i * (barWidth + barGap), 0.02f, segmentLength * 0.5f),
                 new Vector3(barWidth, 0.02f, zebraDepth));
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