using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum GameState { Menu, Countdown, Playing, Finished, GameOver }

/// <summary>
/// Mengatur alur permainan:
///   Menu -> Hitung mundur (3-2-1-GO!) -> Balapan -> Selesai (hasil + data CSV)
///
/// Aturan: capai jarak "finishDistance" secepat mungkin.
/// Menabrak rintangan = kecepatan anjlok (perlambatan besar), jadi waktu bertambah.
/// Nyawa: maxCrashes (default 5) tabrakan -> GAME OVER.
///
/// Tombol: ENTER/SPASI mulai | R ulang | G grafik | O buka folder data | ESC menu
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Komponen")]
    public CarController car;
    public ObstacleSpawner obstacles;
    public DataLogger logger;

    [Header("Aturan")]
    [Tooltip("Jarak garis finish (m)")]
    public float finishDistance = 5000f;
    [Tooltip("Jumlah tabrakan yang dimaafkan. Mencapai angka ini = game over")]
    public int maxCrashes = 5;
    public int countdownFrom = 3;

    [Header("UI - Menu & hasil")]
    public GameObject menuPanel;
    public TMP_Text bestText;          // "Rekor: 01:12.3"
    public TMP_Text countdownText;     // 3, 2, 1, GO!
    public TMP_Text progressText;      // "Sisa: 1250 m   Tabrakan: 2"
    public GameObject resultPanel;
    public TMP_Text resultText;
    public Image crashFlash;           // Image merah layar penuh (alpha diatur script)

    [Header("UI - Kelompok objek")]
    [Tooltip("Objek HUD yang tampil saat balapan (SpeedText, AccelText, ProgressText, dll.)")]
    public GameObject[] hudObjects;
    [Tooltip("Objek grafik (3 RawImage). Bisa disembunyikan dengan tombol G")]
    public GameObject[] graphObjects;

    public GameState State { get; private set; } = GameState.Menu;

    [Header("Kendaraan pemain")]
    [Tooltip("Dipakai untuk ganti model mobil/motor dari menu")]
    public PlayerCarModel player;

    const string BestKey = "BestTime";
    const string VehicleKey = "PlayerVehicle";
    static readonly string[] VehicleNames = { "race", "motor" };
    static readonly string[] VehicleLabels = { "Mobil", "Motor" };
    int vehicleIndex;
    bool graphsVisible = true;
    float flashAlpha;
    string lastDataPath = "";

    // Nilai terakhir pada teks progres; string hanya dibangun saat berubah.
    int lastRemainInt = int.MinValue;
    int lastCrashes = int.MinValue;

    void Start()
    {
        if (car == null || obstacles == null || logger == null)
        {
            Debug.LogError("GameManager: kolom Car, Obstacles, dan Logger harus diisi.");
            enabled = false;
            return;
        }

        car.allowResetKey = false;      // restart diurus GameManager
        car.Crashed += OnCrashed;

        if (player == null) player = FindAnyObjectByType<PlayerCarModel>();
        ApplyVehicle(PlayerPrefs.GetInt(VehicleKey, 0));

        ShowMenu();
    }

    void OnDestroy()
    {
        if (car != null) car.Crashed -= OnCrashed;
    }

    // ------------------------------------------------------------
    // Alur
    // ------------------------------------------------------------

    void ShowMenu()
    {
        StopAllCoroutines();
        State = GameState.Menu;

        car.ResetCar();
        car.controlsEnabled = false;
        car.autoBrake = false;

        obstacles.running = false;
        obstacles.ResetObstacles();
        obstacles.SetVisible(false);

        logger.Clear();

        SetActive(menuPanel, true);
        SetActive(resultPanel, false);
        SetHud(false);
        SetText(countdownText, "");
        SetText(progressText, "");

        RefreshBestText();
    }

    /// <summary>0 = mobil, 1 = motor. Langsung terlihat di belakang menu.</summary>
    void ApplyVehicle(int index)
    {
        vehicleIndex = Mathf.Clamp(index, 0, VehicleNames.Length - 1);
        string name = VehicleNames[vehicleIndex];

        if (player != null) player.SetVehicle(name);
        if (obstacles != null) obstacles.carSize = VehicleAssets.Footprint(name);

        PlayerPrefs.SetInt(VehicleKey, vehicleIndex);
        RefreshBestText();
    }

    void RefreshBestText()
    {
        float best = PlayerPrefs.GetFloat(BestKey, 0f);
        string record = best > 0f ? "Rekor: " + FormatTime(best) : "Rekor: -";
        SetText(bestText, record + "\nKendaraan: " + VehicleLabels[vehicleIndex] + "  (1 = mobil, 2 = motor)");
    }

    void StartRun()
    {
        StopAllCoroutines();
        StartCoroutine(RunRoutine());
    }

    IEnumerator RunRoutine()
    {
        State = GameState.Countdown;

        car.ResetCar();
        car.controlsEnabled = false;
        car.autoBrake = false;

        obstacles.running = false;
        obstacles.ResetObstacles();
        obstacles.SetVisible(true);

        logger.Clear();

        SetActive(menuPanel, false);
        SetActive(resultPanel, false);
        SetHud(true);
        SetText(progressText, "Sisa: " + Mathf.RoundToInt(finishDistance) + " m     Nyawa: " + maxCrashes + "/" + maxCrashes);
        lastRemainInt = int.MinValue;
        lastCrashes = int.MinValue;

        for (int n = countdownFrom; n >= 1; n--)
        {
            SetText(countdownText, n.ToString());
            yield return new WaitForSeconds(1f);
        }

        SetText(countdownText, "GO!");
        State = GameState.Playing;
        car.controlsEnabled = true;
        obstacles.running = true;
        logger.StartRecording();

        yield return new WaitForSeconds(0.8f);
        if (State == GameState.Playing) SetText(countdownText, "");
    }

    void Finish() { EndRun(true); }
    void GameOver() { EndRun(false); }

    /// <summary>Selesai karena finis (finished=true) atau karena nyawa habis.</summary>
    void EndRun(bool finished)
    {
        State = finished ? GameState.Finished : GameState.GameOver;

        car.controlsEnabled = false;   // ElapsedTime berhenti di sini
        car.autoBrake = true;          // mobil direm pelan
        obstacles.running = false;
        logger.StopRecording();

        // Ambil hasil sekarang (sebelum mobil berhenti)
        float time = car.ElapsedTime;
        float distance = car.Distance;
        float avgKmh = distance / Mathf.Max(0.01f, time) * 3.6f;   // v rata-rata = jarak / waktu
        float maxKmh = car.MaxSpeedReached * 3.6f;
        int crashes = car.CrashCount;

        lastDataPath = logger.Save();

        string s;
        if (finished)
        {
            float best = PlayerPrefs.GetFloat(BestKey, 0f);
            bool newRecord = best <= 0f || time < best;
            if (newRecord)
            {
                PlayerPrefs.SetFloat(BestKey, time);
                PlayerPrefs.Save();
                best = time;
            }

            s = "SELESAI!\n\n";
            s += "Waktu: " + FormatTime(time) + (newRecord ? "   REKOR BARU!" : "") + "\n";
            s += "Rekor: " + FormatTime(best) + "\n";
        }
        else
        {
            s = "GAME OVER!\n\n";
            s += "Nyawa habis (" + maxCrashes + " tabrakan).\n";
            s += "Bertahan: " + FormatTime(time) + "\n";
        }

        s += "Jarak: " + Mathf.RoundToInt(distance) + " m\n";
        s += "Kecepatan rata-rata: " + Mathf.RoundToInt(avgKmh) + " km/jam\n";
        s += "Kecepatan tertinggi: " + Mathf.RoundToInt(maxKmh) + " km/jam\n";
        s += "Jumlah tabrakan: " + crashes + "\n\n";
        s += string.IsNullOrEmpty(lastDataPath)
            ? "Data CSV: tidak tersimpan\n"
            : "Data CSV: " + lastDataPath + "\n";
        s += "\nR / ENTER: main lagi   O: buka folder data   ESC: menu";

        SetText(resultText, s);
        SetActive(resultPanel, true);
        SetText(countdownText, "");
        SetText(progressText, "");
    }

    // ------------------------------------------------------------

    void Update()
    {
        if (InputHelper.GraphTogglePressed())
        {
            graphsVisible = !graphsVisible;
            SetGraphs(State != GameState.Menu && graphsVisible);
        }

        // Kilatan merah saat tabrakan
        if (crashFlash != null)
        {
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, 2.5f * Time.deltaTime);
            Color c = crashFlash.color;
            c.a = flashAlpha;
            crashFlash.color = c;
        }

        switch (State)
        {
            case GameState.Menu:
                if (InputHelper.VehicleCarPressed()) ApplyVehicle(0);
                else if (InputHelper.VehicleBikePressed()) ApplyVehicle(1);
                if (InputHelper.StartPressed()) StartRun();
                break;

            case GameState.Countdown:
                if (InputHelper.MenuPressed()) ShowMenu();
                break;

            case GameState.Playing:
                float remain = Mathf.Max(0f, finishDistance - car.Distance);
                int remainInt = Mathf.CeilToInt(remain);
                int crashes = car.CrashCount;
                if (remainInt != lastRemainInt || crashes != lastCrashes)
                {
                    lastRemainInt = remainInt;
                    lastCrashes = crashes;
                    int nyawa = Mathf.Max(0, maxCrashes - crashes);
                    SetText(progressText, "Sisa: " + remainInt + " m     Nyawa: " + nyawa + "/" + maxCrashes);
                }

                if (crashes >= maxCrashes) { GameOver(); break; }
                if (car.Distance >= finishDistance) { Finish(); break; }
                if (InputHelper.RestartPressed()) StartRun();
                else if (InputHelper.MenuPressed()) ShowMenu();
                break;

            case GameState.Finished:
            case GameState.GameOver:
                if (InputHelper.RestartPressed() || InputHelper.ConfirmPressed()) StartRun();
                else if (InputHelper.MenuPressed()) ShowMenu();
                else if (InputHelper.OpenFolderPressed()) logger.OpenFolder();
                break;
        }

        // Angin ngebut: volumenya ikut kecepatan mobil, mati saat tidak balapan.
        Sfx.SetSpeed(State == GameState.Playing ? car.Speed / Mathf.Max(1f, car.maxSpeed) : 0f);
    }

    void OnCrashed()
    {
        flashAlpha = 0.45f;
    }

    // ------------------------------------------------------------
    // Pembantu UI
    // ------------------------------------------------------------

    void SetHud(bool visible)
    {
        if (hudObjects != null)
            foreach (GameObject g in hudObjects) SetActive(g, visible);
        SetGraphs(visible && graphsVisible);
    }

    void SetGraphs(bool visible)
    {
        if (graphObjects != null)
            foreach (GameObject g in graphObjects) SetActive(g, visible);
    }

    static void SetActive(GameObject g, bool active)
    {
        if (g != null && g.activeSelf != active) g.SetActive(active);
    }

    static void SetText(TMP_Text t, string s)
    {
        if (t != null) t.text = s;
    }

    static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float sec = seconds - minutes * 60f;
        return minutes.ToString("00") + ":" + sec.ToString("00.0");
    }
}