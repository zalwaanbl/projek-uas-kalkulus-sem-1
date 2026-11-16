using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ragdoll ringan tanpa Rigidbody.
///
/// Tubuh tetap utuh. Saat mobil menabrak:
///   - root dilempar searah mobil + lemparan ke atas + sebaran acak
///   - badan berputar (tumble) lalu memantul beberapa kali di tanah; tinggi
///     tumpu mengikuti orientasi badan, jadi pantulannya tidak seragam
///   - tiap pantulan menyisakan squash and stretch yang bergetar pegas
///   - lengan dan kaki adalah pegas teredam yang tertarik ke arah gaya efektif
///     (gravitasi, inersia, hambatan udara), sehingga tertinggal saat badan
///     melesat dan tersentak saat badan menghantam tanah
///   - setelah pantulan habis, badan menggelincir, berhenti, dan rebah
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
        public Vector3 ang;
        public Vector3 angVel;
        public float stiffness;
        public float damping;
        public float limit;
    }

    const float BounceThreshold = 1.5f;
    const float Restitution = 0.5f;
    const float LimbDrag = 0.8f;

    readonly List<Joint> joints = new List<Joint>();
    Transform pivot;
    float life = 2.6f;
    float age;

    float ground;
    float halfH = 0.9f;
    float halfW = 0.25f;
    Vector3 pos;
    Vector3 vel;
    Vector3 spin;

    int bounces;
    bool resting;
    Quaternion lieRot;

    float squash;
    float squashVel;
    Vector3 force;

    public static Ragdoll Throw(Transform root, Vector3 velocity, float spread = 2.5f, float life = 2.6f)
    {
        if (root == null) return null;

        GameObject host = new GameObject("Ragdoll");
        Ragdoll r = host.AddComponent<Ragdoll>();
        r.life = life;

        Bounds b = new Bounds(root.position, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer rr in root.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds) { b = rr.bounds; hasBounds = true; }
            else b.Encapsulate(rr.bounds);
        }

        float standY = hasBounds ? Mathf.Max(b.center.y - root.position.y, 0.2f) : 0.9f;
        r.halfH = hasBounds ? Mathf.Max(b.extents.y, 0.3f) : 0.9f;
        r.halfW = hasBounds ? Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.8f, 0.15f, r.halfH) : 0.25f;

        r.ground = root.position.y;
        r.pos = new Vector3(root.position.x, standY, root.position.z);
        host.transform.position = new Vector3(root.position.x, r.ground, root.position.z);

        r.pivot = new GameObject("RagdollPivot").transform;
        r.pivot.SetParent(host.transform, false);
        r.pivot.position = root.position + Vector3.up * standY;
        r.pivot.rotation = root.rotation;
        root.SetParent(r.pivot, true);

        r.vel = velocity
                + Vector3.up * Random.Range(2.5f, 4.5f)
                + Random.insideUnitSphere * (spread * 0.25f);

        float tumble = Random.Range(320f, 620f) * (Random.value < 0.5f ? -1f : 1f);
        r.spin = root.right * tumble
                 + root.up * Random.Range(-120f, 120f)
                 + root.forward * Random.Range(-40f, 40f);

        // Kumpulkan potongan tubuh berdasarkan nama (model Kenney blocky).
        Transform body = null, head = null;
        List<Transform> arms = new List<Transform>();
        List<Transform> legs = new List<Transform>();
        foreach (Transform part in root)
        {
            if (part.name.StartsWith("body")) body = part;
            else if (part.name.StartsWith("head")) head = part;
            else if (part.name.StartsWith("arm")) arms.Add(part);
            else if (part.name.StartsWith("leg")) legs.Add(part);
        }

        // Sendi pinggang: badan + kepala + lengan bergoyang relatif ke kaki.
        if (body != null)
        {
            Renderer br = body.GetComponentInChildren<Renderer>();
            float hipY = br != null ? br.bounds.min.y - root.position.y : r.halfH * 0.5f;

            Transform waist = new GameObject("pinggang").transform;
            waist.SetParent(root, true);
            waist.position = root.position + root.up * hipY;
            waist.rotation = root.rotation;

            body.SetParent(waist, true);
            if (head != null) head.SetParent(waist, true);
            foreach (Transform a in arms) a.SetParent(waist, true);

            AddJoint(r, waist, 55f, 5f, 1.1f, 4f);
        }

        // Sendi leher: kepala terkulai sendiri.
        if (head != null)
        {
            Renderer hr = head.GetComponentInChildren<Renderer>();
            float neckY = hr != null ? hr.bounds.min.y - root.position.y : r.halfH * 1.2f;

            Transform neck = new GameObject("leher").transform;
            neck.SetParent(head.parent, true);
            neck.position = root.position + root.up * neckY;
            neck.rotation = root.rotation;

            head.SetParent(neck, true);
            AddJoint(r, neck, 40f, 3.5f, 1.2f, 6f);
        }

        // Lengan dan kaki: dua sendi per anggota (bahu/paha + tengah), jadi
        // gerakannya berlapis dan terasa lebih elastis.
        foreach (Transform a in arms) AddLimb(r, root, a, true);
        foreach (Transform l in legs) AddLimb(r, root, l, false);

        return r;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        if (dt <= 0f) return;
        age += dt;

        if (pivot == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 startVel = vel;
        vel += Physics.gravity * dt;

        if (!resting) spin *= Mathf.Exp(-0.35f * dt);
        pivot.Rotate(spin * dt, Space.World);
        pos += vel * dt;

        // Tinggi tumpu bergantung pada seberapa tegak badan: tegak = setinggi
        // setengah badan, rebah = setengah tebal badan.
        float uy = Mathf.Abs(pivot.up.y);
        float contact = Mathf.Max(0.15f, halfH * uy + halfW * Mathf.Sqrt(Mathf.Max(0f, 1f - uy * uy)));

        if (pos.y <= contact)
        {
            pos.y = contact;
            float impact = -vel.y;

            if (!resting && impact > BounceThreshold)
            {
                bounces++;
                Vector3 horiz = new Vector3(vel.x, 0f, vel.z);

                vel = new Vector3(vel.x * 0.75f, impact * Restitution, vel.z * 0.75f);

                // Menggelinding: kecepatan datar diubah sebagian jadi putaran.
                spin = spin * 0.7f
                       + Vector3.Cross(Vector3.up, horiz) * (Mathf.Rad2Deg * 0.35f / Mathf.Max(halfW, 0.2f))
                       + Random.insideUnitSphere * (impact * 12f);
                spin = Vector3.ClampMagnitude(spin, 900f);

                squash = Mathf.Max(squash, Mathf.Min(impact * 0.03f, 0.3f));
                squashVel = 0f;

                foreach (Joint j in joints)
                    j.angVel += Random.onUnitSphere * (impact * 0.9f);
            }
            else
            {
                vel.y = 0f;
                if (!resting)
                {
                    resting = true;
                    Vector3 u = pivot.up;
                    Vector3 flat = Vector3.ProjectOnPlane(u, Vector3.up);
                    if (flat.sqrMagnitude < 0.04f) flat = Vector3.ProjectOnPlane(pivot.forward, Vector3.up);
                    if (flat.sqrMagnitude < 0.04f) flat = Vector3.right;
                    lieRot = Quaternion.FromToRotation(u, flat.normalized) * pivot.rotation;
                }
            }
        }

        if (resting)
        {
            float slide = Mathf.Exp(-3.5f * dt);
            vel.x *= slide;
            vel.z *= slide;
            spin = Vector3.Lerp(spin, Vector3.zero, 1f - Mathf.Exp(-5f * dt));
            pivot.rotation = Quaternion.Slerp(pivot.rotation, lieRot, 1f - Mathf.Exp(-6f * dt));
        }

        transform.position = new Vector3(pos.x, ground, pos.z);
        pivot.localPosition = new Vector3(0f, pos.y, 0f);

        UpdateJoints(dt, startVel);

        squashVel += (-260f * squash - 12f * squashVel) * dt;
        squash = Mathf.Clamp(squash + squashVel * dt, -0.25f, 0.35f);

        float fade = age > life - 0.5f ? Mathf.Clamp01((life - age) / 0.5f) : 1f;
        transform.localScale = new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f) * fade;

        if (age >= life) Destroy(gameObject);
    }

    void UpdateJoints(float dt, Vector3 startVel)
    {
        // Dalam jatuh bebas percepatan badan = gravitasi, jadi gaya efektif nol
        // dan anggota badan melayang; diam di tanah gaya efektif = gravitasi.
        Vector3 acc = (vel - startVel) / dt;
        Vector3 f = Physics.gravity - acc - vel * LimbDrag;
        force = Vector3.Lerp(force, f, 1f - Mathf.Exp(-15f * dt));

        float g = Physics.gravity.magnitude;
        float weight = Mathf.Clamp01(force.magnitude / g);
        Vector3 dirLocal = pivot.InverseTransformDirection(force.normalized);

        float limp = 1f + age * 0.6f;

        for (int i = 0; i < joints.Count; i++)
        {
            Joint j = joints[i];

            Vector3 target = Vector3.zero;
            if (weight > 0.001f)
            {
                Quaternion q = Quaternion.FromToRotation(Vector3.down, dirLocal);
                q.ToAngleAxis(out float deg, out Vector3 axis);
                if (deg > 180f) deg -= 360f;
                if (axis.sqrMagnitude > 0.0001f)
                    target = axis.normalized * (Mathf.Clamp(deg * Mathf.Deg2Rad, -j.limit, j.limit) * weight);
            }

            j.angVel += (j.stiffness * (target - j.ang) - j.damping * limp * j.angVel) * dt;
            j.ang += j.angVel * dt;

            float m = j.ang.magnitude;
            float maxAng = j.limit * 1.15f;
            if (m > maxAng)
            {
                j.ang *= maxAng / m;
                j.angVel *= 0.5f;
                m = maxAng;
            }

            j.t.localRotation = m > 0.0001f
                ? Quaternion.AngleAxis(m * Mathf.Rad2Deg, j.ang / m) * j.rest
                : j.rest;
        }
    }

    static void AddJoint(Ragdoll r, Transform t, float stiffness, float damping, float limit, float kick)
    {
        r.joints.Add(new Joint
        {
            t = t,
            rest = t.localRotation,
            angVel = Random.insideUnitSphere * kick,
            stiffness = stiffness * Random.Range(0.85f, 1.15f),
            damping = damping * Random.Range(0.85f, 1.15f),
            limit = limit,
        });
    }

    /// <summary>
    /// Satu anggota badan jadi dua sendi: bahu/paha di ujung atas, lalu sendi
    /// tengah. Rotasi keduanya bertumpuk, jadi terlihat melengkung saat
    /// terlempar (elastis), bukan kaku seperti satu potongan.
    /// </summary>
    static void AddLimb(Ragdoll r, Transform root, Transform part, bool arm)
    {
        Renderer rend = part.GetComponentInChildren<Renderer>();
        float half = rend != null ? rend.bounds.extents.y : 0.2f;

        Transform shoulder = new GameObject(part.name + "_sendi").transform;
        shoulder.SetParent(part.parent, true);
        shoulder.position = part.position + root.up * half;
        shoulder.rotation = part.rotation;
        part.SetParent(shoulder, true);
        AddJoint(r, shoulder, arm ? 45f : 70f, arm ? 2.6f : 4.5f, arm ? 2.6f : 1.3f, 9f);

        Transform mid = new GameObject(part.name + "_tengah").transform;
        mid.SetParent(shoulder, true);
        mid.position = part.position;
        mid.rotation = part.rotation;
        part.SetParent(mid, true);
        AddJoint(r, mid, arm ? 30f : 45f, arm ? 3f : 4.8f, arm ? 1.6f : 0.9f, 6f);
    }
}