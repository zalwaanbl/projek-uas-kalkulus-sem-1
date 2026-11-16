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

    public static readonly string[] People =
    {
        "People/person", "People/adventurer",
    };

    // Warna baju/celana acak untuk model People/person (material bernama
    // "shirt" dan "pants"), supaya orang terlihat berbeda-beda walau meshenya sama.
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

    public static readonly string[] Clouds =
    {
        "Clouds/cloud_a", "Clouds/cloud_b", "Clouds/cloud_c",
        "Clouds/cloud_d", "Clouds/cloud_e",
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

        string name = path.Substring(path.LastIndexOf('/') + 1);

        // Holder dipakai supaya posisi bisa diubah-ubah pemanggil (dekorasi
        // dipindah saat segment didaur ulang) tanpa merusak offset "alas di
        // y = 0" milik model di dalamnya. Dulu offset itu ditulis ke transform
        // root yang sama, jadi tertimpa saat dekorasi dipindah -> rumah tenggelam.
        Transform holder = new GameObject(name).transform;
        holder.SetParent(parent, false);
        holder.localPosition = localPos;

        GameObject go = Object.Instantiate(src, holder);
        go.name = name + "_model";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        VehicleAssets.FixMaterials(go);
        if (tint.HasValue) Tint(go, tint.Value);

        if (byLength) FitLength(go, size);
        else FitHeight(go, size);

        holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return holder;
    }

    /// <summary>
    /// Orang low-poly acak (Human V1 / Adventurer). Tinggi ±<paramref name="height"/> m,
    /// menghadap +Z. Pakaian Human V1 diwarnai acak per orang.
    /// </summary>
    public static Transform SpawnPerson(Transform parent, Vector3 localPos, float yaw, float height)
    {
        string path = People[Random.Range(0, People.Length)];
        Transform t = Spawn(path, parent, localPos, yaw, height);
        if (t != null && path == "People/person") RecolorOutfit(t);
        return t;
    }

    static void RecolorOutfit(Transform root)
    {
        Color shirt = shirtColors[Random.Range(0, shirtColors.Length)];
        Color pants = pantsColors[Random.Range(0, pantsColors.Length)];

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;
                if (m.name.StartsWith("shirt")) mats[i] = Recolor(m, shirt);
                else if (m.name.StartsWith("pants")) mats[i] = Recolor(m, pants);
            }
            r.sharedMaterials = mats;
        }
    }

    static Material Recolor(Material src, Color c)
    {
        Material m = new Material(src);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else m.color = c;
        return m;
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

        // Offset dihitung dalam ruang parent (holder), jadi aman walau holder
        // sudah dipindah jauh atau diputar sesudahnya.
        Transform par = go.transform.parent;
        Vector3 center = par.InverseTransformPoint(b.center);
        Vector3 bottom = par.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
        go.transform.localPosition += new Vector3(-center.x, -bottom.y, -center.z);
    }
}
