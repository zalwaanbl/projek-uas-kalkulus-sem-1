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
    public float decorMinSide = 8f;           // jarak minimum dari tengah jalan (m)
    public float decorMaxSide = 35f;
    public Vector2 decorScaleRange = new Vector2(0.9f, 1.4f);

    class Seg
    {
        public Transform root;
        public List<Transform> decor = new List<Transform>();
    }

    readonly Queue<Seg> segments = new Queue<Seg>();
    Material matWhite, matRed, matRoad, matGrass;
    float nextZ;

    void Awake()
    {
        matWhite = MakeMaterial(Color.white);
        matRed = MakeMaterial(new Color(0.85f, 0.1f, 0.1f));
        matRoad = roadMaterial != null ? roadMaterial : MakeMaterial(roadColor);
        matGrass = grassMaterial != null ? grassMaterial : MakeMaterial(grassColor);
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

        // Mobil di-reset ke belakang (tombol R): susun ulang jalan dari awal
        if (carZ < segments.Peek().root.position.z - 1f)
        {
            Layout();
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
        for (int i = 0; i < seg.decor.Count; i++)
        {
            float side = (i % 2 == 0) ? -1f : 1f;   // selang-seling kiri dan kanan
            float x = side * Random.Range(decorMinSide, decorMaxSide);
            float z = Random.Range(0f, segmentLength);
            Transform d = seg.decor[i];
            d.localPosition = new Vector3(x, 0f, z);
            d.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            d.localScale = Vector3.one * Random.Range(decorScaleRange.x, decorScaleRange.y);
        }
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