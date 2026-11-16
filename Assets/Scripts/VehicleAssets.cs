using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Katalog model kendaraan Kenney Car Kit (lisensi CC0) yang ada di
/// Assets/Resources/KenneyCarKit/FBX.
///
/// Semua model: pivot di dasar (y = 0), menghadap sumbu +Z, 1 unit = 1 meter.
/// Ukuran di bawah diukur dari mesh aslinya (lebar, panjang, tinggi) dalam meter,
/// dipakai untuk menyetel kotak tabrakan ObstacleSpawner.
/// </summary>
public static class VehicleAssets
{
    public const string Root = "KenneyCarKit/FBX/";

    struct Size
    {
        public float width, length, height;
    }

    // Ukuran nyata (meter). Bentuk Kenney memang pendek-gemuk, jadi tiap
    // model diskalakan per sumbu (bukan seragam) agar proporsinya masuk akal
    // di samping pejalan kaki setinggi ±1,7 m.
    static readonly Dictionary<string, Size> sizes = new Dictionary<string, Size>
    {
        { "race",           new Size { width = 1.85f, length = 4.20f, height = 1.05f } },
        { "race-future",    new Size { width = 1.90f, length = 4.30f, height = 1.10f } },
        { "sedan",          new Size { width = 1.85f, length = 4.50f, height = 1.45f } },
        { "sedan-sports",   new Size { width = 1.90f, length = 4.50f, height = 1.30f } },
        { "hatchback-sports", new Size { width = 1.80f, length = 4.10f, height = 1.45f } },
        { "taxi",           new Size { width = 1.85f, length = 4.60f, height = 1.50f } },
        { "police",         new Size { width = 1.90f, length = 4.90f, height = 1.50f } },
        { "ambulance",      new Size { width = 2.00f, length = 5.30f, height = 2.10f } },
        { "firetruck",      new Size { width = 2.20f, length = 6.00f, height = 2.40f } },
        { "garbage-truck",  new Size { width = 2.20f, length = 6.00f, height = 2.40f } },
        { "delivery",       new Size { width = 2.10f, length = 5.80f, height = 2.30f } },
        { "delivery-flat",  new Size { width = 2.00f, length = 5.20f, height = 2.10f } },
        { "van",            new Size { width = 1.90f, length = 4.70f, height = 1.90f } },
        { "suv",            new Size { width = 1.90f, length = 4.60f, height = 1.70f } },
        { "suv-luxury",     new Size { width = 1.90f, length = 4.60f, height = 1.70f } },
        { "truck",          new Size { width = 1.90f, length = 4.80f, height = 1.70f } },
        { "truck-flat",     new Size { width = 1.90f, length = 4.80f, height = 1.70f } },
        { "tractor",        new Size { width = 1.80f, length = 3.60f, height = 2.00f } },
        { "tractor-police", new Size { width = 1.80f, length = 3.60f, height = 2.00f } },
        { "tractor-shovel", new Size { width = 1.90f, length = 4.00f, height = 2.10f } },
        { "kart-oobi",      new Size { width = 1.30f, length = 2.60f, height = 0.90f } },
        { "cone",           new Size { width = 0.48f, length = 0.48f, height = 0.60f } },
        { "cone-flat",      new Size { width = 0.48f, length = 0.48f, height = 0.20f } },
        { "box",            new Size { width = 0.71f, length = 0.71f, height = 0.71f } },
    };

    /// <summary>Muat model (tanpa ekstensi). Null bila nama tidak ada di Resources.</summary>
    public static GameObject Load(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return Resources.Load<GameObject>(Root + name);
    }

    /// <summary>Lebar (x) dan panjang (z) model dalam meter. Default 1.6 x 3.0.</summary>
    public static Vector2 Footprint(string name)
    {
        if (name != null && sizes.TryGetValue(name, out Size s)) return new Vector2(s.width, s.length);
        return new Vector2(1.6f, 3.0f);
    }

    /// <summary>Tinggi model dalam meter. Default 1.4.</summary>
    public static float Height(string name)
    {
        if (name != null && sizes.TryGetValue(name, out Size s)) return s.height;
        return 1.4f;
    }

    /// <summary>
    /// Jaga ukuran model tetap sesuai katalog: kalau ukuran asli FBX menyimpang
    /// besar (mis. unit FBX dalam cm sehingga model jadi 1/100), skala objek
    /// dikoreksi agar lebar dan panjangnya cocok. Ukuran yang sudah pas tidak
    /// disentuh. Dipanggil setelah Instantiate supaya model tidak bisa
    /// muncul terlalu kecil/besar dan menabrak diri sendiri.
    /// </summary>
    public static void EnforceFootprint(GameObject go, Vector2 footprint, float height = 0f)
    {
        if (go == null) return;

        Renderer[] rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        if (b.size.x < 0.01f || b.size.z < 0.01f) return;

        Vector3 s = go.transform.localScale;

        // Dengan tinggi target: skala per sumbu, supaya mobil tidak jadi
        // sependek model aslinya tapi tetap tidak melar berlebihan.
        if (height > 0.01f && b.size.y > 0.01f)
        {
            s = new Vector3(s.x * footprint.x / b.size.x,
                            s.y * height / b.size.y,
                            s.z * footprint.y / b.size.z);
            go.transform.localScale = s;
            return;
        }

        float fx = footprint.x / b.size.x;
        float fz = footprint.y / b.size.z;
        if (Mathf.Abs(fx - 1f) < 0.25f && Mathf.Abs(fz - 1f) < 0.25f) return;

        // Rata-rata faktor dipakai untuk ketiga sumbu supaya bentuk tidak melar.
        float f = (fx + fz) * 0.5f;
        go.transform.localScale = new Vector3(s.x * f, s.y * f, s.z * f);
    }

    /// <summary>
    /// Ganti material bawaan FBX (shader Standard) ke URP Lit supaya tidak
    /// tampil magenta di proyek URP. Tekstur dan warna lama tetap dipakai.
    /// </summary>
    public static void FixMaterials(GameObject go)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material old = mats[i];
                if (old == null || old.shader == lit) continue;

                Material m = new Material(lit) { name = old.name };
                if (old.HasProperty("_MainTex") && old.mainTexture != null)
                    m.SetTexture("_BaseMap", old.mainTexture);
                if (old.HasProperty("_Color"))
                {
                    Color c = old.color;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    else m.color = c;
                }
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }
}
