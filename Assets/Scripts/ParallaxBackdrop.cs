using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Latar pegunungan ber-parallax: gunung/bukit jauh bergerak lebih lambat
/// daripada jalan, jadi terasa ada kedalaman tanpa perlu skybox.
///
/// Cara kerja: tiap objek disimpan pada "jarak pandang" (depth) dari mobil.
/// Depth menyusut (1 - parallax) kali jarak tempuh mobil, jadi lapisan jauh
/// hampir diam di horizon, lapisan dekat bergeser lebih cepat. Saat depth
/// habis, objek didaur ke belakang lapisan yang sama.
///
/// Model: Assets/Resources/Scenery/Background (lihat CREDITS.md).
/// </summary>
public class ParallaxBackdrop : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public string[] paths;
        [Tooltip("1 = ikut mobil (diam di horizon), 0 = diam di dunia")]
        [Range(0f, 1f)] public float parallax = 0.95f;
        [Tooltip("Jarak objek di depan mobil (m): x = paling dekat, y = paling jauh")]
        public Vector2 depth = new Vector2(420f, 900f);
        public int count = 5;
        [Tooltip("Tinggi model setelah diskalakan (m)")]
        public float height = 160f;
        [Tooltip("Sebaran posisi x (m)")]
        public float spread = 420f;
        [Tooltip("Jarak minimum dari tengah jalan (m) supaya tidak menutupi jalan")]
        public float minSide = 140f;
    }

    public CarController car;

    public Layer[] layers =
    {
        new Layer
        {
            paths = new[] { "Background/mountains" }, parallax = 0.96f,
            depth = new Vector2(620f, 950f), count = 4, height = 180f,
            spread = 520f, minSide = 180f,
        },
        new Layer
        {
            paths = new[] { "Background/mountain" }, parallax = 0.90f,
            depth = new Vector2(380f, 640f), count = 4, height = 130f,
            spread = 380f, minSide = 150f,
        },
        new Layer
        {
            paths = new[] { "Background/valley" }, parallax = 0.82f,
            depth = new Vector2(250f, 420f), count = 2, height = 95f,
            spread = 260f, minSide = 150f,
        },
    };

    class Item
    {
        public Transform t;
        public Layer layer;
        public float depth;
        public float x;
    }

    readonly List<Item> items = new List<Item>();
    Transform container;
    float prevCarZ;

    void Start()
    {
        if (car == null) car = FindAnyObjectByType<CarController>();

        container = new GameObject("LatarPegunungan").transform;
        container.SetParent(transform, false);

        foreach (Layer layer in layers)
        {
            if (layer.paths == null || layer.paths.Length == 0) continue;
            for (int i = 0; i < layer.count; i++)
                Spawn(layer, Random.Range(layer.depth.x, layer.depth.y));
        }

        float carZ = car != null ? car.transform.position.z : 0f;
        prevCarZ = carZ;
        Place(carZ);
    }

    void OnDestroy()
    {
        if (container != null) Destroy(container.gameObject);
    }

    void Spawn(Layer layer, float depth)
    {
        string path = layer.paths[Random.Range(0, layer.paths.Length)];
        float yaw = Random.value < 0.5f ? 0f : 180f;
        Transform t = SceneryAssets.Spawn(path, container, Vector3.zero, yaw, layer.height);
        if (t == null) return;

        items.Add(new Item
        {
            t = t,
            layer = layer,
            depth = depth,
            x = (Random.value < 0.5f ? -1f : 1f) * Random.Range(layer.minSide, layer.spread),
        });
    }

    void Update()
    {
        if (car == null || items.Count == 0) return;

        float carZ = car.transform.position.z;
        float delta = carZ - prevCarZ;
        prevCarZ = carZ;

        // Mobil reset ke belakang (tombol R): jangan biarkan latar melompat jauh.
        if (delta < -50f) delta = 0f;

        foreach (Item it in items)
        {
            Layer layer = it.layer;
            it.depth -= (1f - layer.parallax) * delta;

            float span = layer.depth.y - layer.depth.x;
            if (it.depth < layer.depth.x) it.depth += span;
            else if (it.depth > layer.depth.y) it.depth -= span;
        }

        Place(carZ);
    }

    void Place(float carZ)
    {
        foreach (Item it in items)
            if (it.t != null)
                it.t.position = new Vector3(it.x, 0f, carZ + it.depth);
    }
}
