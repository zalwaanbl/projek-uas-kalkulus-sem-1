using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ragdoll ringan tanpa Rigidbody/Collider.
///
/// Saat mobil menabrak pejalan kaki, seluruh tubuh (transform root) dilepas ke
/// dunia apa adanya lalu disimulasikan sendiri:
///   - kecepatan awal = arah mobil + lemparan ke atas + sebaran acak
///   - gaya berat menarik ke bawah
///   - menyentuh tanah = memantul kecil dan melambat
///   - berputar selama melayang, lalu menghilang setelah beberapa detik
///
/// Tidak memakai fisika Unity supaya tetap cocok dengan permainan yang semua
/// tabrakannya dicek manual (tanpa Rigidbody).
/// </summary>
public class Ragdoll : MonoBehaviour
{
    struct Piece
    {
        public Transform t;
        public Vector3 vel;
        public Vector3 spin;
        public Vector3 scale;
    }

    readonly List<Piece> pieces = new List<Piece>();
    float life = 2.6f;
    float age;

    /// <summary>
    /// Lempar <paramref name="root"/> sebagai ragdoll. Badan tetap utuh:
    /// root dipindah ke objek baru lalu jatuh dan berputar.
    /// </summary>
    public static Ragdoll Throw(Transform root, Vector3 velocity, float spread = 2.5f, float life = 2.6f)
    {
        if (root == null) return null;

        GameObject host = new GameObject("Ragdoll");
        Ragdoll r = host.AddComponent<Ragdoll>();
        r.life = life;

        // Satu potongan = seluruh tubuh. Bagian badan tidak dilepas, jadi orang
        // terlempar utuh lalu jatuh dan berputar.
        root.SetParent(host.transform, true);
        r.pieces.Add(new Piece
        {
            t = root,
            scale = root.localScale,
            vel = velocity
                  + Vector3.up * Random.Range(2f, 4f)
                  + Random.insideUnitSphere * (spread * 0.25f),
            spin = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f),
                               Random.Range(-1f, 1f)) * Random.Range(6f, 14f),
        });

        return r;
        return r;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        float fade = age > life - 0.5f ? Mathf.Clamp01((life - age) / 0.5f) : 1f;

        for (int i = 0; i < pieces.Count; i++)
        {
            Piece p = pieces[i];

            p.vel += Physics.gravity * dt;
            Vector3 pos = p.t.position + p.vel * dt;

            if (pos.y < 0.05f)
            {
                pos.y = 0.05f;
                p.vel = new Vector3(p.vel.x * 0.6f, Mathf.Abs(p.vel.y) * 0.25f, p.vel.z * 0.6f);
                p.spin *= 0.7f;
            }

            p.t.position = pos;
            p.t.Rotate(p.spin * dt, Space.World);
            p.t.localScale = p.scale * fade;
            pieces[i] = p;
        }

        if (age >= life) Destroy(gameObject);
    }
}
