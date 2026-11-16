using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pecahan singkat saat kendaraan hancur ditabrak: beberapa kubus kecil
/// berhamburan dari bekas model, terkena gravitasi, berputar, lalu menyusut
/// dan hilang. Tanpa Rigidbody, sama seperti efek lain di game ini.
/// </summary>
public class Debris : MonoBehaviour
{
    class Piece
    {
        public Transform t;
        public Vector3 vel;
        public Vector3 spin;
        public float life;
    }

    readonly List<Piece> pieces = new List<Piece>();
    static Material shared;

    /// <summary>Hamburkan pecahan dari kotak <paramref name="b"/> (posisi dunia).</summary>
    public static void Burst(Bounds b, Color color, int count = 9)
    {
        if (shared == null) shared = CommonUtil.ColorMaterial(Color.white);

        GameObject host = new GameObject("Pecahan");
        Debris d = host.AddComponent<Debris>();

        for (int i = 0; i < count; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(host.transform, false);
            cube.transform.position = new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.center.y - b.extents.y * 0.3f, b.max.y),
                Random.Range(b.min.z, b.max.z));
            cube.transform.localScale = Vector3.one * Random.Range(0.12f, 0.32f);
            cube.transform.rotation = Random.rotation;

            Material m = new Material(shared);
            m.name = "Pecahan";
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            else m.color = color;
            cube.GetComponent<Renderer>().sharedMaterial = m;

            d.pieces.Add(new Piece
            {
                t = cube.transform,
                vel = new Vector3(Random.Range(-3.5f, 3.5f),
                                  Random.Range(2f, 5f),
                                  Random.Range(-1.5f, 4.5f)),
                spin = Random.insideUnitSphere * Random.Range(4f, 12f),
                life = Random.Range(0.8f, 1.3f),
            });
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool alive = false;

        for (int i = 0; i < pieces.Count; i++)
        {
            Piece p = pieces[i];
            if (p.t == null) continue;

            p.life -= dt;
            if (p.life <= 0f)
            {
                Destroy(p.t.gameObject);
                continue;
            }
            alive = true;

            p.vel += Physics.gravity * dt;
            Vector3 pos = p.t.position + p.vel * dt;
            if (pos.y < 0.06f)
            {
                pos.y = 0.06f;
                p.vel = new Vector3(p.vel.x * 0.5f, 0f, p.vel.z * 0.5f);
                p.spin *= 0.5f;
            }
            p.t.position = pos;
            p.t.Rotate(p.spin * dt, Space.World);
            p.t.localScale *= Mathf.Clamp01(1f - dt * 1.4f);

            pieces[i] = p;
        }

        if (!alive) Destroy(gameObject);
    }
}
