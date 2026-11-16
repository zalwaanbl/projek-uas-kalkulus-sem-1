using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Jalan tanpa akhir dengan mendaur ulang beberapa potongan (segment).
/// Segment yang sudah tertinggal di belakang mobil dipindah ke depan,
/// jadi jumlah objek di scene tetap, tidak membuat objek baru terus-menerus.
///
/// Mode 1 (tanpa aset): kalau "segmentPrefab" kosong, segment dibuat otomatis
///   dari kubus (aspal, garis putus-putus, curb merah-putih, rumput).
/// Mode 2 (dengan aset): isi "segmentPrefab" dengan prefab jalanmu sendiri
///   (pivot di ujung belakang, panjang = segmentLength).
///
/// Dekorasi (pohon, tiang, dll.) diisi lewat "decorPrefabs" dan
/// diacak ulang posisinya setiap segment didaur ulang.
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

    [Header("Dekorasi pinggir jalan")]
    public GameObject[] decorPrefabs;
    public int decorPerSegment = 8;
    public float decorMinSide = 12f;          // jarak minimum dari tengah jalan (m), trees need clearance
    public float decorMaxSide = 40f;
    public Vector2 decorScaleRange = new Vector2(0.9f, 1.4f);

    class Seg
    {
        public Transform root;
        public List<Transform> decor = new List<Transform>();
    }

    readonly Queue<Seg> segments = new Queue<Seg>();
    Material matWhite, matRed, matRoad, matGrass;
    Material matTrunk, matFoliage, matHouseBase, matRoof;
    Material createdRoad, createdGrass;   // hanya material buatan sendiri yang perlu dihapus
    float nextZ;
    float lastDistance;

    void Awake()
    {
        matWhite = CommonUtil.ColorMaterial(Color.white);
        matRed = CommonUtil.ColorMaterial(new Color(0.85f, 0.1f, 0.1f));
        matRoad = roadMaterial != null ? roadMaterial : (createdRoad = CommonUtil.ColorMaterial(roadColor));
        matGrass = grassMaterial != null ? grassMaterial : (createdGrass = CommonUtil.ColorMaterial(grassColor));

        // Material dekorasi prosedural dibuat sekali saja, bukan per objek.
        matTrunk = CommonUtil.ColorMaterial(new Color(0.4f, 0.2f, 0.1f));
        matFoliage = CommonUtil.ColorMaterial(new Color(0.2f, 0.6f, 0.2f));
        matHouseBase = CommonUtil.ColorMaterial(new Color(0.9f, 0.9f, 0.8f));
        matRoof = CommonUtil.ColorMaterial(new Color(0.6f, 0.2f, 0.1f));
    }

    void OnDestroy()
    {
        DestroyMat(matWhite);
        DestroyMat(matRed);
        DestroyMat(createdRoad);
        DestroyMat(createdGrass);
        DestroyMat(matTrunk);
        DestroyMat(matFoliage);
        DestroyMat(matHouseBase);
        DestroyMat(matRoof);
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

        if (decorPrefabs != null && decorPrefabs.Length > 0)
        {
            for (int i = 0; i < decorPerSegment; i++)
            {
                GameObject prefab = decorPrefabs[Random.Range(0, decorPrefabs.Length)];
                if (prefab == null) continue;
                Transform d = Instantiate(prefab, seg.root).transform;
                seg.decor.Add(d);
            }
        }
        return seg;
    }

    void RandomizeDecor(Seg seg)
    {
        // If no prefabs assigned, create procedural scenery
        if (decorPrefabs == null || decorPrefabs.Length == 0)
        {
            // Hapus semua dekorasi lama
            foreach (Transform d in seg.decor)
                if (d != null) Destroy(d.gameObject);
            seg.decor.Clear();

            // Buat pohon/house secara prosedural
            for (int i = 0; i < decorPerSegment; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;   // kiri dan kanan
                float x = side * Random.Range(decorMinSide, decorMaxSide);
                float z = Random.Range(0f, segmentLength);
                float scale = Random.Range(decorScaleRange.x, decorScaleRange.y);

                // 50% pohon, 50% rumah
                if (Random.value < 0.5f)
                {
                    CreateTree(seg, x, z, scale);
                }
                else
                {
                    CreateHouse(seg, x, z, scale);
                }
            }
            return;
        }

        // Dengan prefab: acak ulang posisi dekorasi yang sudah ada
        for (int i = 0; i < seg.decor.Count; i++)
        {
            float side = (i % 2 == 0) ? -1f : 1f;   // kiri dan kanan
            float x = side * Random.Range(decorMinSide, decorMaxSide);
            float z = Random.Range(0f, segmentLength);
            Transform d = seg.decor[i];
            d.localPosition = new Vector3(x, 0f, z);
            d.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            d.localScale = Vector3.one * Random.Range(decorScaleRange.x, decorScaleRange.y);
        }
    }

    // ------------------------------------------------------------
    // Pembuatan procedural tree dan house
    // ------------------------------------------------------------

    void CreateTree(Seg seg, float x, float z, float scale)
    {
        // Trunk (cylinder). Silinder bawaan setinggi 2 satuan, jadi localScale.y = scale
        // memberi tinggi 2*scale; diletakkan center di scale supaya alas menyentuh tanah (y=0).
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(trunk.GetComponent<Collider>());
        trunk.transform.SetParent(seg.root, false);
        trunk.transform.localPosition = new Vector3(x, scale, z);
        trunk.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        trunk.transform.localScale = new Vector3(0.6f * scale, scale, 0.6f * scale);
        trunk.GetComponent<Renderer>().sharedMaterial = matTrunk;

        // Foliage (sphere) di atas trunk. Di-parent ke seg.root (bukan trunk yang berskala)
        // supaya skalanya tidak menumpuk. Diameter bola = 3*scale (radius 1.5*scale).
        GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(foliage.GetComponent<Collider>());
        foliage.transform.SetParent(seg.root, false);
        foliage.transform.localPosition = new Vector3(x, 2f * scale + 1.5f * scale, z);
        foliage.transform.localScale = Vector3.one * (3f * scale);
        foliage.GetComponent<Renderer>().sharedMaterial = matFoliage;

        seg.decor.Add(trunk.transform);
        seg.decor.Add(foliage.transform);
    }

    void CreateHouse(Seg seg, float x, float z, float scale)
    {
        // Base (kubus), alas di tanah (y = 0), tinggi 2.5*scale.
        GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(baseObj.GetComponent<Collider>());
        baseObj.transform.SetParent(seg.root, false);
        baseObj.transform.localPosition = new Vector3(x, 1.25f * scale, z);
        baseObj.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        baseObj.transform.localScale = new Vector3(4f * scale, 2.5f * scale, 3f * scale);
        baseObj.GetComponent<Renderer>().sharedMaterial = matHouseBase;

        // Roof (kubus diputar 45° sebagai atap miring). Parent ke seg.root, bukan base.
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(roof.GetComponent<Collider>());
        roof.transform.SetParent(seg.root, false);
        roof.transform.localPosition = new Vector3(x, 2.5f * scale + 0.7f * scale, z);
        roof.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        roof.transform.localScale = new Vector3(2.8f * scale, 2.8f * scale, 3f * scale);
        roof.GetComponent<Renderer>().sharedMaterial = matRoof;

        seg.decor.Add(baseObj.transform);
        seg.decor.Add(roof.transform);
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