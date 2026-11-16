using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ragdoll ringan tanpa Rigidbody.
///
/// Tubuh tetap utuh. Saat mobil menabrak:
///   - root dilempar searah mobil + lemparan ke atas + sebaran acak
///   - badan berputar ke depan (tumble), bukan sekadar acak
///   - lengan dan kaki berayun di SENDINYA (bahu dan pangkal paha, bukan di
///     tengah potongan), lalu makin melambat seperti sendi yang lemas
///   - menyentuh tanah = memantul kecil, gesekan menghentikan gulingan, dan
///     badan turun sampai tergeletak (titik tumpunya bukan lagi setinggi badan)
///   - menghilang setelah beberapa detik
///
/// Tidak memakai fisika Unity supaya tetap cocok dengan permainan yang semua
/// tabrakannya dicek manual (tanpa Rigidbody).
/// </summary>
public class Ragdoll : MonoBehaviour
{
    class Joint
    {
        public Transform t;
        public Quaternion rest;
        public Vector3 axis;
        public float amp;      // radian
        public float freq;     // rad/s
        public float phase;
    }

    readonly List<Joint> joints = new List<Joint>();
    Transform pivot;
    float life = 2.6f;
    float age;
    float groundY = 0.9f;      // tinggi pusat badan saat masih berdiri
    float standY;
    bool landed;
    Vector3 vel;
    Vector3 spin;
    Vector3 baseScale;

    /// <summary>
    /// Lempar <paramref name="root"/> sebagai ragdoll. Badan tetap tersusun,
    /// lengan/kaki berayun di sendinya, lalu badan tergeletak.
    /// </summary>
    public static Ragdoll Throw(Transform root, Vector3 velocity, float spread = 2.5f, float life = 2.6f)
    {
        if (root == null) return null;

        GameObject host = new GameObject("Ragdoll");
        Ragdoll r = host.AddComponent<Ragdoll>();
        r.life = life;

        // Tinggi pusat badan diambil dari kotak model, bukan ditebak.
        Bounds b = new Bounds(root.position, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer rr in root.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds) { b = rr.bounds; hasBounds = true; }
            else b.Encapsulate(rr.bounds);
        }
        float standY = hasBounds ? Mathf.Max(b.center.y - root.position.y, 0.2f) : 0.9f;
        r.standY = standY;
        r.groundY = standY;

        // Pivot di tengah badan: putaran dan pantulan terjadi di titik ini,
        // bukan di telapak kaki.
        r.pivot = new GameObject("RagdollPivot").transform;
        r.pivot.SetParent(host.transform, false);
        r.pivot.position = root.position + Vector3.up * r.standY;
        r.pivot.rotation = root.rotation;
        root.SetParent(r.pivot, true);
        root.localPosition -= Vector3.up * r.standY;
        r.baseScale = r.pivot.localScale;

        r.vel = velocity
                + Vector3.up * Random.Range(2f, 4f)
                + Random.insideUnitSphere * (spread * 0.25f);

        // Tertabrak dari belakang: badan terlempar depan-atas lalu berguling
        // ke depan (sumbu X dunia) dengan sedikit putaran samping.
        float tumble = Random.Range(320f, 620f) * (Random.value < 0.5f ? -1f : 1f);
        r.spin = root.right * tumble
                 + root.up * Random.Range(-120f, 120f)
                 + root.forward * Random.Range(-40f, 40f);

        // Lengan dan kaki: potongan dipindah ke sendi di ujung atasnya, jadi
        // berayun seperti lengan/kaki, bukan seperti baling-baling di tengah.
        List<Transform> parts = new List<Transform>();
        foreach (Transform part in root) parts.Add(part);

        foreach (Transform part in parts)
        {
            bool arm = part.name.StartsWith("arm");
            bool leg = part.name.StartsWith("leg");
            if (!arm && !leg) continue;

            Renderer rend = part.GetComponentInChildren<Renderer>();
            float half = rend != null ? rend.bounds.extents.y : 0.25f;

            Transform joint = new GameObject(part.name + "_sendi").transform;
            joint.SetParent(root, true);
            joint.position = part.position + root.up * half;
            joint.rotation = part.rotation;
            part.SetParent(joint, true);

            r.joints.Add(new Joint
            {
                t = part,
                rest = part.localRotation,
                axis = new Vector3(1f, 0f, Random.Range(-0.25f, 0.25f)).normalized,
                amp = arm ? Random.Range(0.9f, 1.5f) : Random.Range(0.5f, 0.9f),
                freq = Random.Range(8f, 13f),
                phase = Random.Range(0f, Mathf.PI * 2f),
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

        // Titik tumpu turun setelah mendarat: badan berpindah dari "berdiri"
        // ke "tergeletak", jadi tidak melayang setinggi pinggang.
        groundY = Mathf.MoveTowards(groundY, 0.3f * pivot.lossyScale.y, 1.1f * dt);

        if (pos.y < groundY)
        {
            pos.y = groundY;
            if (!landed)
            {
                landed = true;
                // Pantulan pertama + hentakan: sebagian energi putar hilang.
                vel = new Vector3(vel.x * 0.55f, Mathf.Abs(vel.y) * 0.22f, vel.z * 0.55f);
                spin *= 0.35f;
            }
            else
            {
                vel = new Vector3(vel.x * 0.8f, 0f, vel.z * 0.8f);
                spin = Vector3.Lerp(spin, Vector3.zero, 6f * dt);
            }
        }

        pivot.position = pos;
        pivot.Rotate(spin * dt, Space.World);

        // Sendi: ayunan teredam, bukan putaran konstan.
        float damp = Mathf.Exp(-2.6f * age);
        for (int i = 0; i < joints.Count; i++)
        {
            Joint j = joints[i];
            float angle = j.amp * damp * Mathf.Sin(j.freq * age + j.phase);
            j.t.localRotation = j.rest * Quaternion.AngleAxis(angle * Mathf.Rad2Deg, j.axis);
            joints[i] = j;
        }

        // Menyusut halus di 0,5 detik terakhir supaya tidak hilang mendadak.
        float fade = age > life - 0.5f ? Mathf.Clamp01((life - age) / 0.5f) : 1f;
        pivot.localScale = baseScale * fade;

        if (age >= life) Destroy(gameObject);
    }
}
