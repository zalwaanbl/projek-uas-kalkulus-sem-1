using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Grafik real-time x(t), v(t), atau a(t) pada UI (RawImage).
/// Menampilkan data N detik terakhir dan bergeser ke kiri seiring waktu.
///
/// Cara pakai: pasang 3 kali pada 3 objek RawImage, pilih "quantity" berbeda.
/// Hubungan yang bisa dilihat langsung:
///   - kemiringan kurva x(t) di suatu titik  = v pada saat itu
///   - kemiringan kurva v(t) di suatu titik  = a pada saat itu
///   - saat a = 0, kurva v(t) mendatar (titik puncak / kecepatan konstan)
///
/// Gambar dibuat ke Texture2D (tanpa library tambahan, aman di URP maupun Built-in).
/// </summary>
public class GraphDrawer : MonoBehaviour
{
    public enum Quantity { Position, Speed, Acceleration }

    public CarController car;
    public Quantity quantity = Quantity.Speed;

    [Header("UI")]
    [Tooltip("RawImage tempat grafik digambar. Kosong = RawImage di objek ini")]
    public RawImage target;
    public TMP_Text label;      // contoh: "v(t) = 55.2 m/s"
    public TMP_Text maxLabel;   // opsional: batas atas sumbu Y
    public TMP_Text minLabel;   // opsional: batas bawah sumbu Y

    [Header("Ukuran tekstur (piksel)")]
    public int width = 360;
    public int height = 140;

    [Header("Data")]
    public float windowSeconds = 10f;
    public float sampleRate = 30f;
    [Tooltip("Khusus Speed: tampilkan km/jam (jika mati: m/s, sama satuan dengan a)")]
    public bool speedInKmh = false;

    [Header("Sumbu Y")]
    [Tooltip("Rentang minimum sumbu Y supaya garis tidak terlalu 'zoom' saat data hampir datar")]
    public float minSpan = 10f;
    public float smoothing = 6f;

    [Header("Warna")]
    public bool autoColor = true;      // Position = biru muda, Speed = hijau, Acceleration = oranye
    public Color lineColor = Color.white;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
    public Color gridColor = new Color(1f, 1f, 1f, 0.15f);
    public Color zeroColor = new Color(1f, 1f, 1f, 0.5f);

    Texture2D tex;
    Color32[] pixels;
    Color32 bgC, gridC, zeroC, lineC;

    // Buffer melingkar (ring buffer)
    float[] buf;
    int capacity, count, head;

    float sampleTimer;
    float lastElapsed;
    float yMin, yMax;
    bool rangeInit;
    int labelKey = int.MinValue, maxKey = int.MinValue, minKey = int.MinValue;

    void Awake()
    {
        if (target == null) target = GetComponent<RawImage>();

        width = Mathf.Max(16, width);
        height = Mathf.Max(16, height);

        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        pixels = new Color32[width * height];
        if (target != null) target.texture = tex;

        capacity = Mathf.Max(2, Mathf.RoundToInt(windowSeconds * sampleRate));
        buf = new float[capacity];

        if (autoColor)
        {
            switch (quantity)
            {
                case Quantity.Position:     lineColor = new Color(0.35f, 0.8f, 1f); break;
                case Quantity.Speed:        lineColor = new Color(0.35f, 1f, 0.45f); break;
                case Quantity.Acceleration: lineColor = new Color(1f, 0.6f, 0.2f); break;
            }
        }
        lineColor.a = 1f;

        bgC = backgroundColor;
        gridC = Over(backgroundColor, gridColor);
        zeroC = Over(backgroundColor, zeroColor);
        lineC = lineColor;
    }

    void OnDestroy()
    {
        if (tex != null) Destroy(tex);
    }

    void Update()
    {
        if (car == null || target == null) return;

        // Permainan di-reset (waktu kembali ke 0): kosongkan grafik
        if (car.ElapsedTime + 0.001f < lastElapsed) ClearBuffer();
        lastElapsed = car.ElapsedTime;

        float step = 1f / Mathf.Max(1f, sampleRate);
        sampleTimer += Time.deltaTime;
        if (sampleTimer > 1f) sampleTimer = step;   // jaga-jaga setelah freeze

        // Gambar ulang hanya saat ada sampel baru (maksimal sampleRate kali per
        // detik), bukan setiap frame: tiap Redraw mengunggah width*height piksel
        // ke GPU, jadi 60 kali per detik untuk 3 grafik = beban besar.
        bool newSample = false;
        while (sampleTimer >= step)
        {
            sampleTimer -= step;
            Push(CurrentValue());
            newSample = true;
        }

        if (newSample) Redraw();
    }

    // ------------------------------------------------------------

    float CurrentValue()
    {
        switch (quantity)
        {
            case Quantity.Position: return car.Distance;
            case Quantity.Speed:    return speedInKmh ? car.SpeedKmh : car.Speed;
            default:                return car.Acceleration;
        }
    }

    void Push(float v)
    {
        buf[head] = v;
        head = (head + 1) % capacity;
        if (count < capacity) count++;
    }

    float Get(int i)   // i = 0 adalah data terlama
    {
        int idx = ((head - count + i) % capacity + capacity) % capacity;
        return buf[idx];
    }

    void ClearBuffer()
    {
        count = 0;
        head = 0;
        rangeInit = false;
        sampleTimer = 0f;
    }

    void Redraw()
    {
        // 1) Cari rentang sumbu Y dari data yang tampil
        float lo = 0f, hi = 1f;
        if (count > 0)
        {
            lo = float.MaxValue;
            hi = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                float v = Get(i);
                if (v < lo) lo = v;
                if (v > hi) hi = v;
            }
        }
        if (quantity != Quantity.Position)
        {
            lo = Mathf.Min(lo, 0f);   // sumbu nol selalu terlihat untuk v dan a
            hi = Mathf.Max(hi, 0f);
        }
        if (hi - lo < minSpan)
        {
            float mid = (hi + lo) * 0.5f;
            lo = mid - minSpan * 0.5f;
            hi = mid + minSpan * 0.5f;
        }
        float pad = (hi - lo) * 0.08f;
        lo -= pad;
        hi += pad;

        if (!rangeInit)
        {
            yMin = lo;
            yMax = hi;
            rangeInit = true;
        }
        else
        {
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            yMin = Mathf.Lerp(yMin, lo, k);
            yMax = Mathf.Lerp(yMax, hi, k);
        }

        // 2) Latar
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bgC;

        // 3) Garis bantu horizontal (25%, 50%, 75%)
        for (int g = 1; g <= 3; g++)
        {
            int row = Mathf.RoundToInt((height - 1) * g * 0.25f);
            HLine(row, gridC);
        }
        if (yMin < 0f && yMax > 0f)
            HLine(YToPixel(0f), zeroC);

        // 4) Kurva (data terbaru di sisi kanan)
        if (count >= 2)
        {
            int prevX = 0, prevY = 0;
            for (int i = 0; i < count; i++)
            {
                int stepsBack = count - 1 - i;
                int px = Mathf.RoundToInt((width - 1) - stepsBack * (width - 1) / (float)(capacity - 1));
                int py = YToPixel(Get(i));
                if (i > 0) DrawLine(prevX, prevY, px, py);
                prevX = px;
                prevY = py;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false);

        // 5) Teks
        UpdateLabels();
    }

    void UpdateLabels()
    {
        // Teks hanya dibangun ulang saat angka yang tampil berubah,
        // jadi tidak ada alokasi string setiap frame.
        if (label != null)
        {
            int key = CurrentLabelKey();
            if (key != labelKey)
            {
                labelKey = key;
                label.text = CurrentLabelText();
            }
        }
        if (maxLabel != null)
        {
            int k = Mathf.RoundToInt(yMax);
            if (k != maxKey) { maxKey = k; maxLabel.text = k.ToString(); }
        }
        if (minLabel != null)
        {
            int k = Mathf.RoundToInt(yMin);
            if (k != minKey) { minKey = k; minLabel.text = k.ToString(); }
        }
    }

    int CurrentLabelKey()
    {
        switch (quantity)
        {
            case Quantity.Position: return Mathf.RoundToInt(car.Distance);
            case Quantity.Speed:    return speedInKmh
                                        ? Mathf.RoundToInt(car.SpeedKmh)
                                        : Mathf.RoundToInt(car.Speed * 10f);
            default:                return Mathf.RoundToInt(car.Acceleration * 10f);
        }
    }

    string CurrentLabelText()
    {
        switch (quantity)
        {
            case Quantity.Position:
                return "x(t) = " + Mathf.RoundToInt(car.Distance) + " m";
            case Quantity.Speed:
                return speedInKmh
                    ? "v(t) = " + Mathf.RoundToInt(car.SpeedKmh) + " km/jam"
                    : "v(t) = " + car.Speed.ToString("0.0") + " m/s";
            default:
                return "a(t) = " + car.Acceleration.ToString("0.0") + " m/s²";
        }
    }

    // ------------------------------------------------------------
    // Menggambar
    // ------------------------------------------------------------

    int YToPixel(float v)
    {
        float span = Mathf.Max(0.0001f, yMax - yMin);
        float n = (v - yMin) / span;
        n = Mathf.Clamp(n, -2f, 3f);   // batasi supaya garis di luar layar tidak sangat panjang
        return Mathf.RoundToInt(n * (height - 1));
    }

    void HLine(int y, Color32 c)
    {
        if (y < 0 || y >= height) return;
        int start = y * width;
        for (int x = 0; x < width; x++) pixels[start + x] = c;
    }

    void Plot(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        pixels[y * width + x] = lineC;
    }

    // Garis Bresenham, tebal 2 piksel
    void DrawLine(int x0, int y0, int x1, int y1)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            Plot(x0, y0);
            Plot(x0, y0 + 1);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    // Menempelkan warna transparan "fg" di atas "bg" (alpha compositing biasa)
    static Color Over(Color bg, Color fg)
    {
        float a = fg.a + bg.a * (1f - fg.a);
        if (a <= 0.0001f) return new Color(0f, 0f, 0f, 0f);
        Color c = (fg * fg.a + bg * bg.a * (1f - fg.a)) / a;
        c.a = a;
        return c;
    }
}