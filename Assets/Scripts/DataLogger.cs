using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Merekam t, x(t), v(t), a(t) selama balapan lalu menyimpannya ke file CSV.
/// File ini dipakai untuk membuat grafik di Python/Excel (bahan laporan)
/// dan untuk membandingkan a dari game dengan turunan numerik dv/dt dari data.
///
/// Lokasi file: Application.persistentDataPath
///   Windows: C:\Users\NAMA\AppData\LocalLow\NAMAPERUSAHAAN\NAMAPROJECT\
///   (di Editor, path lengkapnya juga ditampilkan di layar hasil dan di Console)
/// Tidak berfungsi di build WebGL (tidak ada akses file).
/// </summary>
public class DataLogger : MonoBehaviour
{
    public CarController car;
    [Tooltip("Selang perekaman (detik). 0.05 = 20 data per detik")]
    public float interval = 0.05f;
    public string fileName = "data_balap.csv";

    struct Sample
    {
        public float t, x, v, a;
    }

    readonly List<Sample> samples = new List<Sample>();
    bool recording;
    float timer;

    public int SampleCount => samples.Count;
    public string LastPath { get; private set; }

    public void Clear()
    {
        samples.Clear();
        recording = false;
        timer = 0f;
    }

    public void StartRecording()
    {
        samples.Clear();
        timer = 0f;
        recording = true;
        Record();
    }

    public void StopRecording()
    {
        if (recording) Record();   // data terakhir
        recording = false;
    }

    void Update()
    {
        if (!recording || car == null) return;

        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer -= interval;
            Record();
        }
    }

    void Record()
    {
        if (car == null) return;
        samples.Add(new Sample
        {
            t = car.ElapsedTime,
            x = car.Distance,
            v = car.Speed,
            a = car.Acceleration
        });
    }

    /// <summary>Menyimpan CSV. Mengembalikan path file, atau string kosong jika gagal.</summary>
    public string Save()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return "";
#else
        if (samples.Count == 0) return "";
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("t_s,x_m,v_ms,a_ms2");
            CultureInfo inv = CultureInfo.InvariantCulture;   // titik desimal, aman untuk Excel/Python
            foreach (Sample s in samples)
            {
                sb.Append(s.t.ToString("0.000", inv)).Append(',')
                  .Append(s.x.ToString("0.000", inv)).Append(',')
                  .Append(s.v.ToString("0.000", inv)).Append(',')
                  .Append(s.a.ToString("0.000", inv)).AppendLine();
            }

            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllText(path, sb.ToString());
            LastPath = path;
            Debug.Log("Data balap disimpan: " + path);
            return path;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Gagal menyimpan CSV: " + e.Message);
            return "";
        }
#endif
    }

    /// <summary>Membuka folder tempat CSV disimpan.</summary>
    public void OpenFolder()
    {
        string folder = Application.persistentDataPath.Replace('\\', '/');
        Application.OpenURL("file:///" + folder);
    }
}