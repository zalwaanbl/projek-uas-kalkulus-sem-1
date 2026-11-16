using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Mobil dengan gerak kinematik buatan sendiri (tanpa Rigidbody),
/// supaya hubungan TURUNAN terlihat jelas:
///
///   a = dv/dt   (percepatan  = turunan kecepatan terhadap waktu)
///   v = dx/dt   (kecepatan   = turunan posisi terhadap waktu)
///
/// Di kode (integrasi numerik / metode Euler):
///   v_baru = v_lama + a * dt
///   x_baru = x_lama + v * dt
///
/// VERSI MINGGU 3 - tambahan:
///   - controlsEnabled : input dimatikan saat menu / hitung mundur / selesai
///   - Crash()         : tabrakan = perlambatan besar dalam waktu singkat
///                       (muncul sebagai lonjakan negatif di grafik a(t))
///   - LateralX, MaxSpeedReached, CrashCount : dibaca GameManager & ObstacleSpawner
///
/// Kontrol: W / Panah Atas = gas, S / Panah Bawah / Spasi = rem,
///          A D / Panah Kiri Kanan = belok.
/// Satuan: meter dan detik (SI). Tampilan HUD dikonversi ke km/jam.
/// </summary>
public class CarController : MonoBehaviour
{
    [Header("Mesin & Rem (satuan SI)")]
    [Tooltip("Percepatan maksimum dari mesin saat gas penuh (m/s²)")]
    public float enginePower = 10f;
    [Tooltip("Perlambatan saat rem penuh (m/s²)")]
    public float brakePower = 25f;
    [Tooltip("Hambatan udara: makin cepat, makin besar (1/s)")]
    public float dragCoefficient = 0.1f;
    [Tooltip("Gesekan tetap saat gas dilepas (m/s²)")]
    public float rollingFriction = 1.5f;
    [Tooltip("Batas kecepatan (m/s). 83 m/s ≈ 300 km/jam")]
    public float maxSpeed = 83f;

    [Header("Kemudi")]
    public float steerSpeed = 8f;      // kecepatan geser ke samping (m/s)
    public float roadHalfWidth = 4f;   // setengah lebar jalan (m)
    public float maxTilt = 12f;        // sudut miring visual saat belok (derajat)

    [Header("Status permainan (diatur GameManager)")]
    [Tooltip("Jika dimatikan, gas/rem/belok diabaikan")]
    public bool controlsEnabled = true;
    [Tooltip("Jika input mati, mobil direm pelan (dipakai setelah garis finish)")]
    public bool autoBrake = false;
    [Tooltip("Tombol R mereset mobil. GameManager mematikan ini dan mengurus restart sendiri")]
    public bool allowResetKey = true;

    // ---- Data yang dibaca script lain (HUD, grafik, dll.) ----
    public float Speed { get; private set; }         // v  (m/s)
    public float Acceleration { get; private set; }  // a  (m/s²)
    public float Distance { get; private set; }      // x  (m)
    public float ElapsedTime { get; private set; }   // t  (detik, hanya berjalan saat controlsEnabled)
    public float SpeedKmh => Speed * 3.6f;
    public float Steer { get; private set; }         // input belok -1..1 (dipakai efek visual)
    public float LateralX => lateralX;               // posisi samping (m)
    public float MaxSpeedReached { get; private set; }
    public int CrashCount { get; private set; }
    public bool IsInvulnerable => invulnerableTimer > 0f;

    /// <summary>Dipanggil sekali setiap tabrakan yang berhasil.</summary>
    public event System.Action Crashed;

    float startX, startY, startZ;
    float lateralX;
    float yaw;

    // Tabrakan
    float crashTimer;
    float crashDecel;
    float invulnerableTimer;

    // Posisi awal diambil di Awake (bukan Start) supaya aman dipanggil
    // ResetCar() oleh script lain pada frame pertama.
    void Awake()
    {
        Vector3 p = transform.position;
        startX = p.x; startY = p.y; startZ = p.z;
        lateralX = startX;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (allowResetKey && WasResetPressed())
        {
            ResetCar();
            return;
        }

        float throttle = controlsEnabled ? GetThrottle() : 0f;
        float brake = controlsEnabled ? GetBrake() : (autoBrake ? 0.4f : 0f);
        float steer = controlsEnabled ? GetSteer() : 0f;
        Steer = steer;

        // 1) Hitung percepatan dari gaya-gaya yang bekerja
        float a = throttle * enginePower          // dorongan mesin
                - brake * brakePower              // rem
                - dragCoefficient * Speed;        // hambatan udara
        if (throttle <= 0f && Speed > 0f)
            a -= rollingFriction;                 // gesekan saat gas dilepas

        // Tabrakan: perlambatan besar yang berlangsung singkat
        if (crashTimer > 0f)
        {
            a -= crashDecel;
            crashTimer -= dt;
        }
        if (invulnerableTimer > 0f)
            invulnerableTimer -= dt;

        // 2) Percepatan -> Kecepatan  (v = v + a*dt)
        float newSpeed = Mathf.Clamp(Speed + a * dt, 0f, maxSpeed);

        // Percepatan efektif (setelah dibatasi 0..maxSpeed)
        Acceleration = (newSpeed - Speed) / dt;
        Speed = newSpeed;
        if (Speed > MaxSpeedReached) MaxSpeedReached = Speed;

        // 3) Kecepatan -> Posisi  (x = x + v*dt)
        Distance += Speed * dt;
        if (controlsEnabled) ElapsedTime += dt;

        // Belok: makin cepat jalan, makin bisa bergeser (mobil diam tidak bisa belok)
        float steerFactor = Mathf.Clamp01(Speed / 10f);
        lateralX += steer * steerSpeed * steerFactor * dt;
        lateralX = Mathf.Clamp(lateralX, startX - roadHalfWidth, startX + roadHalfWidth);

        // Miring visual
        float targetYaw = steer * maxTilt * steerFactor;
        yaw = Mathf.Lerp(yaw, targetYaw, 8f * dt);

        transform.position = new Vector3(lateralX, startY, startZ + Distance);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    /// <summary>
    /// Tabrakan. Kecepatan dikurangi sampai "keepSpeedFraction" dari kecepatan semula
    /// dalam "duration" detik (jadi a(t) punya lonjakan negatif, bukan loncatan v).
    /// Mengembalikan false jika sedang kebal (tabrakan sebelumnya baru terjadi).
    /// </summary>
    public bool Crash(float keepSpeedFraction = 0.4f, float duration = 0.4f, float invulnerableTime = 1.5f)
    {
        if (IsInvulnerable) return false;

        duration = Mathf.Max(0.05f, duration);
        crashTimer = duration;
        crashDecel = Speed * (1f - Mathf.Clamp01(keepSpeedFraction)) / duration;
        invulnerableTimer = invulnerableTime;
        CrashCount++;

        if (Crashed != null) Crashed();
        return true;
    }

    public void ResetCar()
    {
        Speed = 0f;
        Acceleration = 0f;
        Distance = 0f;
        ElapsedTime = 0f;
        Steer = 0f;
        MaxSpeedReached = 0f;
        CrashCount = 0;
        crashTimer = 0f;
        crashDecel = 0f;
        invulnerableTimer = 0f;
        lateralX = startX;
        yaw = 0f;
        transform.position = new Vector3(startX, startY, startZ);
        transform.rotation = Quaternion.identity;
    }

    // ------------------------------------------------------------
    // Input: otomatis mendukung Input System baru maupun Input Manager lama
    // ------------------------------------------------------------
#if ENABLE_INPUT_SYSTEM
    float GetThrottle()
    {
        var k = Keyboard.current;
        return (k != null && (k.wKey.isPressed || k.upArrowKey.isPressed)) ? 1f : 0f;
    }

    float GetBrake()
    {
        var k = Keyboard.current;
        return (k != null && (k.sKey.isPressed || k.downArrowKey.isPressed || k.spaceKey.isPressed)) ? 1f : 0f;
    }

    float GetSteer()
    {
        var k = Keyboard.current;
        if (k == null) return 0f;
        float s = 0f;
        if (k.aKey.isPressed || k.leftArrowKey.isPressed) s -= 1f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed) s += 1f;
        return s;
    }

    bool WasResetPressed()
    {
        var k = Keyboard.current;
        return k != null && k.rKey.wasPressedThisFrame;
    }
#else
    float GetThrottle()
    {
        return (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f;
    }

    float GetBrake()
    {
        return (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Space)) ? 1f : 0f;
    }

    float GetSteer()
    {
        float s = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) s -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) s += 1f;
        return s;
    }

    bool WasResetPressed()
    {
        return Input.GetKeyDown(KeyCode.R);
    }
#endif
}