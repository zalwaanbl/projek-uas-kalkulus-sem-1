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

    static readonly Dictionary<string, Size> sizes = new Dictionary<string, Size>
    {
        { "race",           new Size { width = 1.30f, length = 2.56f, height = 0.73f } },
        { "race-future",    new Size { width = 1.30f, length = 2.60f, height = 0.75f } },
        { "sedan",          new Size { width = 1.50f, length = 2.55f, height = 1.30f } },
        { "sedan-sports",   new Size { width = 1.30f, length = 2.55f, height = 1.10f } },
        { "hatchback-sports", new Size { width = 1.30f, length = 2.55f, height = 1.10f } },
        { "taxi",           new Size { width = 1.50f, length = 2.75f, height = 1.50f } },
        { "police",         new Size { width = 1.50f, length = 3.10f, height = 1.30f } },
        { "ambulance",      new Size { width = 1.50f, length = 3.10f, height = 1.60f } },
        { "firetruck",      new Size { width = 1.60f, length = 3.45f, height = 1.60f } },
        { "garbage-truck",  new Size { width = 1.60f, length = 3.45f, height = 1.60f } },
        { "delivery",       new Size { width = 1.60f, length = 3.40f, height = 1.65f } },
        { "delivery-flat",  new Size { width = 1.50f, length = 3.10f, height = 1.60f } },
        { "van",            new Size { width = 1.50f, length = 2.75f, height = 1.35f } },
        { "suv",            new Size { width = 1.50f, length = 2.70f, height = 1.30f } },
        { "suv-luxury",     new Size { width = 1.50f, length = 2.70f, height = 1.30f } },
        { "truck",          new Size { width = 1.50f, length = 2.95f, height = 1.30f } },
        { "truck-flat",     new Size { width = 1.50f, length = 2.75f, height = 1.30f } },
        { "tractor",        new Size { width = 1.30f, length = 2.60f, height = 1.35f } },
        { "tractor-police", new Size { width = 1.30f, length = 2.60f, height = 1.35f } },
        { "tractor-shovel", new Size { width = 1.40f, length = 2.80f, height = 1.40f } },
        { "kart-oobi",      new Size { width = 1.10f, length = 1.90f, height = 0.90f } },
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
                if (old.HasProperty("_Color")) m.color = old.color;
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }
}
