using UnityEngine;

/// <summary>
/// Efek suara prosedural (tanpa file audio): tabrak orang, tabrak mobil,
/// dan angin ngebut. Semua clip dibuat sekali saat pertama dipakai, memakai
/// noise + osilator sederhana, jadi tetap jalan walau project belum punya aset
/// suara.
///
/// Pemakaian dari script lain:
///   Sfx.HitPerson();        // menabrak pejalan kaki
///   Sfx.HitVehicle();       // menabrak mobil/rintangan
///   Sfx.SetSpeed(t);        // t = 0..1 (kecepatan / maxSpeed)
/// </summary>
public static class Sfx
{
    const int Rate = 44100;

    static AudioSource oneShot;
    static AudioSource wind;
    static AudioClip crashClip, thudClip, windClip;
    static bool ready;

    static void Ensure()
    {
        if (ready) return;
        ready = true;

        GameObject go = new GameObject("Sfx");
        Object.DontDestroyOnLoad(go);

        oneShot = go.AddComponent<AudioSource>();
        oneShot.playOnAwake = false;
        oneShot.spatialBlend = 0f;

        wind = go.AddComponent<AudioSource>();
        wind.playOnAwake = false;
        wind.loop = true;
        wind.volume = 0f;
        wind.spatialBlend = 0f;
        wind.clip = windClip = BuildWind();
        wind.Play();

        crashClip = BuildCrash();
        thudClip = BuildThud();
    }

    /// <summary>Tabrakan mobil: dentum + gemeretak logam pendek.</summary>
    public static void HitVehicle()
    {
        Ensure();
        oneShot.PlayOneShot(crashClip, 0.9f);
    }

    /// <summary>Menabrak pejalan kaki: bunyi tumpul, lebih pelan dari tabrakan mobil.</summary>
    public static void HitPerson()
    {
        Ensure();
        oneShot.PlayOneShot(thudClip, 0.75f);
    }

    /// <summary>
    /// Angin ngebut. <paramref name="t"/> = kecepatan / maxSpeed (0..1).
    /// Mulai terdengar di atas ±45% kecepatan maksimum.
    /// </summary>
    public static void SetSpeed(float t)
    {
        Ensure();
        t = Mathf.Clamp01(t);
        float gain = Mathf.Clamp01((t - 0.45f) / 0.55f);
        wind.volume = gain * 0.55f;
        wind.pitch = 0.8f + 0.55f * t;
    }

    // ------------------------------------------------------------

    static AudioClip BuildCrash()
    {
        const float dur = 0.55f;
        int n = (int)(Rate * dur);
        float[] d = new float[n];
        var rnd = new System.Random(12345);
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;

            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.35f;                    // noise "berat" (logam)

            float thump = Mathf.Sin(2f * Mathf.PI * 62f * t) * Mathf.Exp(-t * 16f);
            float knock = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 9f) * 0.5f;
            float clang = Mathf.Sin(2f * Mathf.PI * 520f * t) * Mathf.Exp(-t * 7f) * 0.22f;
            float grit = lp * Mathf.Exp(-t * 10f) * 0.75f;

            d[i] = Mathf.Clamp(thump * 0.8f + knock + clang + grit, -1f, 1f);
        }
        return Pcm("SfxHitVehicle", d);
    }

    static AudioClip BuildThud()
    {
        const float dur = 0.28f;
        int n = (int)(Rate * dur);
        float[] d = new float[n];
        var rnd = new System.Random(777);
        float lp = 0f;
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;

            // Frekuensi turun 130 Hz -> 45 Hz: bunyi benda tumpul kena badan.
            float freq = Mathf.Lerp(130f, 45f, Mathf.Clamp01(t / dur));
            phase += 2f * Mathf.PI * freq / Rate;

            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.12f;

            float body = Mathf.Sin(phase) * Mathf.Exp(-t * 18f);
            float slap = lp * Mathf.Exp(-t * 40f) * 0.6f;

            d[i] = Mathf.Clamp(body * 0.85f + slap, -1f, 1f);
        }
        return Pcm("SfxHitPerson", d);
    }

    static AudioClip BuildWind()
    {
        const float dur = 2f;
        int n = (int)(Rate * dur);
        float[] d = new float[n];
        var rnd = new System.Random(4242);
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.08f;

            // Gelombang pelan supaya angin tidak terdengar rata.
            float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.6f * t);
            d[i] = Mathf.Clamp(lp * 2.2f * swell, -1f, 1f);
        }
        return Pcm("SfxWind", d);
    }

    static AudioClip Pcm(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
