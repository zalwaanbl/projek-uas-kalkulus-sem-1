using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ragdoll ringan tanpa Rigidbody.
///
/// Tubuh tetap utuh: saat mobil menabrak pejalan kaki atau pesepeda, root
/// dilempar sebagai satu benda (kecepatan mobil + lemparan ke atas + sebaran
/// acak), lalu jatuh, memantul kecil, dan berputar.
///
/// Sendi tetap bekerja: bagian yang punya sendi (lengan dan kaki, dicari dari
/// nama anak "arm*" dan "leg*") berayun sendiri dan makin melambat, jadi
/// terlihat seperti sendi yang lemas. Tidak ada bagian yang terlepas dari badan.
///
/// Tidak memakai fisika Unity supaya tetap cocok dengan permainan yang semua
/// tabrakannya dicek manual (tanpa Rigidbody). Menghilang setelah beberapa detik.
/// </summary>
public class Ragdoll : MonoBehaviour
{
    struct Limb
    {
        public Transform t;
        public Quaternion rest;
        public Vector3 axis;
        public float angle;
        public float speed;
    }

    readonly List<Limb> limbs = new List<Limb>();
    Transform pivot;
    float life = 2.6f;
    float age;
    Vector3 vel;
    Vector3 spin;
    Vector3 baseScale;

    /// <summary>
    /// Lempar <paramref name="root"/> sebagai ragdoll. Badan tetap tersusun,
    /// hanya lengan dan kakinya yang ikut berayun di sendinya.
    /// </summary>
    public static Ragdoll Throw(Transform root, Vector3 velocity, float spread = 2.5f, float life = 2.6f)
    {
        if (root == null) return null;

        GameObject host = new GameObject("Ragdoll");
        Ragdoll r = host.AddComponent<Ragdoll>();
        r.life = life;

        // Pivot di tengah badan supaya putaran dan pantulan terlihat wajar
        // (bukan berputar di titik kaki).
        r.pivot = new GameObject("RagdollPivot").transform;
        r.pivot.SetParent(host.transform, false);
        float centerY = 0.9f * root.lossyScale.y;
        r.pivot.position = root.position + Vector3.up * centerY;
        r.pivot.rotation = root.rotation;
        root.SetParent(r.pivot, true);
        root.localPosition -= Vector3.up * centerY;

        r.baseScale = r.pivot.localScale;
        r.vel = velocity
                + Vector3.up * Random.Range(2f, 4f)
                + Random.insideUnitSphere * (spread * 0.25f);
        r.spin = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f),
                             Random.Range(-1f, 1f)) * Random.Range(180f, 420f);

        // Sumbu ayun tiap sendi sedikit berbeda supaya tidak seperti gunting.
        foreach (Transform part in root)
        {
            bool arm = part.name.StartsWith("arm");
            bool leg = part.name.StartsWith("leg");
            if (!arm && !leg) continue;

            r.limbs.Add(new Limb
            {
                t = part,
                rest = part.localRotation,
                axis = new Vector3(1f, 0f, Random.Range(-0.3f, 0.3f)).normalized,
                angle = Random.Range(0f, 360f),
                speed = Random.Range(240f, 480f) * (Random.value < 0.5f ? -1f : 1f),
            });
        }

        return r;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (pivot == null)
        {
            Destroy(gameObject);
            return;
        }

        vel += Physics.gravity * dt;
        Vector3 pos = pivot.position + vel * dt;

        float radius = 0.9f * pivot.lossyScale.y;
        if (pos.y < radius)
        {
            pos.y = radius;
            vel = new Vector3(vel.x * 0.6f, Mathf.Abs(vel.y) * 0.25f, vel.z * 0.6f);
            spin *= 0.7f;
        }

        pivot.position = pos;
        pivot.Rotate(spin * dt, Space.World);

        for (int i = 0; i < limbs.Count; i++)
        {
            Limb lb = limbs[i];
            lb.angle += lb.speed * dt;
            lb.speed = Mathf.MoveTowards(lb.speed, 0f, 240f * dt);
            lb.t.localRotation = lb.rest * Quaternion.AngleAxis(lb.angle, lb.axis);
            limbs[i] = lb;
        }

        // Menyusut halus di 0,5 detik terakhir supaya tidak hilang mendadak.
        float fade = age > life - 0.5f ? Mathf.Clamp01((life - age) / 0.5f) : 1f;
        pivot.localScale = baseScale * fade;

        if (age >= life) Destroy(gameObject);
    }
}
