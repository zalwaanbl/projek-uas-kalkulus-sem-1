using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Katalog model pemandangan di Assets/Resources/Scenery.
/// Sumber & lisensi tiap paket: Assets/Resources/Scenery/CREDITS.md.
///
/// Semua model dinormalisasi saat dipasang: tinggi (atau panjang) disesuaikan,
/// alas diletakkan di y = 0, dan titik pusat di x/z. Jadi ukuran asli tiap
/// paket (meter, sentimeter, unit acak) tidak perlu dipikirkan pemanggil.
/// </summary>
public static class SceneryAssets
{
    public const string Root = "Scenery/";

    public static readonly string[] Trees =
    {
        "Trees/tree_default", "Trees/tree_detailed", "Trees/tree_cone",
        "Trees/tree_blocks", "Trees/tree_oak", "Trees/tree_palm",
        "Trees/tree_pineRoundA", "Trees/tree_tall", "Trees/tree_thin",
        "Trees/tree_small",
    };

    public static readonly string[] Houses =
    {
        "Houses/house_a", "Houses/house_b", "Houses/house_c",
    };

    public static readonly string[] Clouds =
    {
        "Clouds/cloud_a", "Clouds/cloud_b", "Clouds/cloud_c",
        "Clouds/cloud_d", "Clouds/cloud_e",
    };

    public static readonly string[] Bikes =
    {
        "Bikes/bicycle_a", "Bikes/bicycle_b",
    };

    // Bagian tubuh karakter Kenney blocky (satu OBJ per bagian supaya bisa
    // dilepas satu-satu saat ragdoll). Pivot tiap bagian ada di tengahnya.
    static readonly string[] bodyParts =
    {
        "People/body1", "People/head1", "People/armleft1",
        "People/armright1", "People/legleft1", "People/legright1",
    };

    static readonly Vector3[] bodyOffsets =
    {
        new Vector3(0f, 9f, 0f),    // badan
        new Vector3(0f, 14f, 0f),   // kepala
        new Vector3(3f, 9f, 0f),    // lengan kiri
        new Vector3(-3f, 9f, 0f),   // lengan kanan
        new Vector3(1f, 3f, 0f),    // kaki kiri
        new Vector3(-1f, 3f, 0f),   // kaki kanan
    };

    const float BodyModelHeight = 16f;   // tinggi model Kenney dalam satuan aslinya

    static readonly Color[] shirtColors =
    {
        new Color(0.85f, 0.25f, 0.25f), new Color(0.2f, 0.45f, 0.8f),
        new Color(0.95f, 0.72f, 0.15f), new Color(0.25f, 0.65f, 0.4f),
        new Color(0.6f, 0.3f, 0.75f), new Color(0.95f, 0.95f, 0.92f),
        new Color(0.15f, 0.15f, 0.18f), new Color(0.9f, 0.5f, 0.2f),
    };

    static readonly Color[] pantsColors =
    {
        new Color(0.15f, 0.18f, 0.3f), new Color(0.3f, 0.3f, 0.32f),
        new Color(0.25f, 0.35f, 0.5f), new Color(0.5f, 0.42f, 0.3f),
    };

    static readonly Color[] skinColors =
    {
        new Color(0.95f, 0.8f, 0.65f), new Color(0.82f, 0.62f, 0.45f),
        new Color(0.6f, 0.42f, 0.3f),
    };

    static readonly Color[] bikeColors =
    {
        new Color(0.85f, 0.15f, 0.15f), new Color(0.15f, 0.4f, 0.8f),
        new Color(0.2f, 0.7f, 0.35f), new Color(0.95f, 0.8f, 0.2f),
        new Color(0.55f, 0.55f, 0.6f),
    };

    static readonly Dictionary<Color, Material> palette = new Dictionary<Color, Material>();

    /// <summary>Material bersama untuk satu warna (dibuat sekali, dipakai ulang).</summary>
    public static Material Mat(Color color)
    {
        Material m;
        if (palette.TryGetValue(color, out m) && m != null) return m;
        m = CommonUtil.ColorMaterial(color);
        palette[color] = m;
        return m;
    }

    /// <summary>
    /// Pasang model dari Resources. Ukuran dinormalisasi ke <paramref name="size"/>
    /// (tinggi, atau panjang z kalau <paramref name="byLength"/>), alas di y = 0.
    /// Return null kalau aset tidak ditemukan.
    /// </summary>
    public static Transform Spawn(string path, Transform parent, Vector3 localPos,
                                  float yaw, float size, Color? tint = null, bool byLength = false)
    {
        GameObject src = Resources.Load<GameObject>(Root + path);
        if (src == null)
        {
            Debug.LogWarning("SceneryAssets: aset tidak ada di Resources: " + Root + path);
            return null;
        }

        GameObject go = Object.Instantiate(src);
        go.name = path.Substring(path.LastIndexOf('/') + 1);
        VehicleAssets.FixMaterials(go);
        if (tint.HasValue) Tint(go, tint.Value);
        if (byLength) FitLength(go, size);
        else FitHeight(go, size);

        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return go.transform;
    }

    /// <summary>Orang utuh (6 bagian tubuh) dengan warna acak. Tinggi ±<paramref name="height"/> m.</summary>
    public static Transform SpawnPerson(Transform parent, Vector3 localPos, float yaw, float height)
    {
        Transform root = new GameObject("Orang").transform;
        root.SetParent(parent, false);
        root.localPosition = localPos;
        root.localRotation = Quaternion.Euler(0f, yaw, 0f);

        Color shirt = shirtColors[Random.Range(0, shirtColors.Length)];
        Color pants = pantsColors[Random.Range(0, pantsColors.Length)];
        Color skin = skinColors[Random.Range(0, skinColors.Length)];
        float s = height / BodyModelHeight;

        for (int i = 0; i < bodyParts.Length; i++)
        {
            GameObject src = Resources.Load<GameObject>(Root + bodyParts[i]);
            if (src == null) continue;

            GameObject part = Object.Instantiate(src, root);
            part.transform.localPosition = bodyOffsets[i] * s;
            part.transform.localScale = Vector3.one * s;
            VehicleAssets.FixMaterials(part);

            Color c;
            if (i == 1) c = skin;                       // kepala
            else if (i == 0 || i == 2 || i == 3) c = shirt;  // badan + lengan
            else c = pants;                              // kaki
            Tint(part, c);
        }
        return root;
    }

    /// <summary>
    /// Sepeda dengan warna rangka acak, panjang ±<paramref name="length"/> m.
    /// Model aslinya menghadap -Z, jadi diputar 180° supaya menghadap arah laju (+Z).
    /// </summary>
    public static Transform SpawnBike(Transform parent, Vector3 localPos, float yaw, float length)
    {
        Color frame = bikeColors[Random.Range(0, bikeColors.Length)];
        Transform t = Spawn(Bikes[Random.Range(0, Bikes.Length)], parent, localPos, yaw + 180f, length, frame, true);
        return t;
    }

    public static void Tint(GameObject go, Color color)
    {
        Material m = Mat(color);
        if (m == null) return;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }

    public static Bounds Bounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// <summary>Skala model sampai tingginya = height, lalu taruh alas di y = 0 dan pusat di x/z = 0.</summary>
    public static void FitHeight(GameObject go, float height) => Fit(go, height, true);

    /// <summary>Skala model sampai panjang z-nya = length, alas di y = 0, pusat di x/z = 0.</summary>
    public static void FitLength(GameObject go, float length) => Fit(go, length, false);

    static void Fit(GameObject go, float size, bool vertical)
    {
        Bounds b = Bounds(go);
        float span = vertical ? b.size.y : b.size.z;
        if (span > 0.0001f)
        {
            go.transform.localScale = go.transform.localScale * (size / span);
            b = Bounds(go);
        }
        go.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
    }
}
