# SpeedRacerURP — Game Mobil Balap Tanpa Akhir

Game mobil balap lebar 3 lajur yang dibuat dengan Unity 6 untuk praktikum kalkulus semester 1. Seluruh fisika mobil ditulis sendiri dari nol dengan konsep turunan: percepatan adalah turunan kecepatan terhadap waktu, dan kecepatan adalah turunan posisi. Data `t`, `x`, `v`, `a` yang dihasilkan permainan bisa diekspor ke CSV lalu dijadikan grafik untuk laporan.

## Fitur

- Jalan tanpa akhir yang didaur ulang dari 12 potongan segment.
- Rintangan kendaraan di 3 lajur, dipilih acak dari 13 jenis model, dengan jarak yang makin rapat seiring jarak tempuh.
- Tabrakan diperiksa manual dengan kotak AABB, tanpa Rigidbody, sehingga mobil secepat apa pun tidak menembus rintangan.
- Efek kecepatan pada kamera dan efek visual pada badan mobil (mendongak saat gas, miring saat belok).
- Perekaman data CSV selama balapan, lengkap dengan pembuka folder dan grafik di layar hasil.
- Skrip Python untuk membuat grafik `x(t)`, `v(t)`, `a(t)` dan membandingkannya dengan turunan numerik `dv/dt`.

## Teknologi

| Komponen | Pilihan |
|---|---|
| Engine | Unity 6000.6.8f1 (Unity 6) |
| Render pipeline | Universal Render Pipeline (URP) 17.6 |
| Input | Unity Input System 1.20 |
| Aset 3D | Kenney Car Kit 3.1 (lisensi CC0) |
| Grafik | Python 3 + numpy + matplotlib |

## Prasyarat

- Unity Hub dan Unity **6000.6.8f1**. Versinya penting: seluruh proyek diatur untuk versi ini, dan membukanya dengan versi lain memaksa Unity mengimpor ulang seluruh aset.
- Python 3 dengan paket `numpy` dan `matplotlib`, hanya jika ingin membuat grafik dari data CSV:

```bash
pip install numpy matplotlib
```

- Git, untuk menyalin repositori.

## Memulai

```bash
git clone https://github.com/zalwaanbl/projek-uas-kalkulus-sem-1.git
cd projek-uas-kalkulus-sem-1
```

Lanjutkan dengan urutan berikut:

1. Buka Unity Hub, pilih **Open → Add project from disk**, lalu arahkan ke folder hasil clone.
2. Tunggu proses import pertama selesai. Proses ini menyiapkan `Library/` dan mengimpor sekitar 4 MB model FBX, jadi agak lama sekali buka. Normal.
3. Di Project window, buka `Assets/game.unity`.
4. Tekan tombol **Play**.

Kalau muncul dialog meminta upgrade editor, jawab **Continue with this version** bila masih di 6000.6.8f1. Dialog lain yang menyangkut paket cukup diikuti.

## Kontrol

| Tombol | Fungsi |
|---|---|
| `W` / `↑` | Gas |
| `S` / `↓` / `Spasi` | Rem |
| `A` `D` / `←` `→` | Belok |
| `ENTER` / `Spasi` | Mulai balapan dari menu |
| `R` | Main lagi |
| `G` | Grafik di layar hasil |
| `O` | Buka folder data CSV |
| `ESC` | Kembali ke menu |

## Konsep matematika di dalamnya

Gerakan mobil dihitung dengan integrasi Euler di `CarController.cs`:

```text
v_baru = v_lama + a * dt      (a = dv/dt, turunan kecepatan)
x_baru = x_lama + v * dt      (v = dx/dt, turunan posisi)
```

Percepatan itu sendiri tidak konstan: ia hasil gaya mesin dikurangi hambatan udara (sebanding `v²`) dan gesekan gulir yang konstan, semuanya dibagi massa. Karena itu kurva `v(t)` dan `a(t)` bukan garis lurus, dan justru perubahan bentuk itulah yang dianalisis di laporan.

Tabrakan ditulis sebagai perlambatan besar dalam 0,4 detik. Di grafik `a(t)`, tabrakan terlihat sebagai lonjakan negatif yang tajam — cara paling mudah mengenali waktu tabrakan dari data.

## Struktur proyek

```text
Assets/
├── game.unity            # scene utama yang dipakai
├── Scripts/
│   ├── CarController.cs    # fisika mobil, integrasi Euler, tabrakan
│   ├── CarVisual.cs        # pitch/roll badan + putaran roda + kedip kebal
│   ├── PlayerCarModel.cs   # memasang model FBX ke mobil pemain saat runtime
│   ├── ObstacleSpawner.cs  # rintangan lalu lintas, daur ulang, kotak tabrakan
│   ├── RoadSpawner.cs      # jalan tanpa akhir dari segment didaur ulang
│   ├── GameManager.cs      # alur menu → hitung mundur → balapan → hasil
│   ├── HUDManager.cs       # tampilan layar menu dan hasil
│   ├── SpeedometerUI.cs    # speedometer km/jam
│   ├── CameraFollow.cs     # kamera mengikuti mobil
│   ├── SpeedEffects.cs     # efek kecepatan (FOV, garis kecepatan)
│   ├── SpeedPostFX.cs      # post-processing saat kecepatan tinggi
│   ├── DataLogger.cs       # merekam t, x, v, a ke CSV
│   ├── GraphDrawer.cs      # grafik x(t), v(t), a(t) di dalam game
│   ├── CommonUtil.cs       # utilitas bersama (material warna, lerp)
│   ├── InputHelper.cs      # pembungkus Input System
│   └── plot_data.py        # grafik CSV untuk laporan
├── Resources/
│   └── KenneyCarKit/       # model kendaraan FBX + README aset
└── Settings/               # aset URP (PC dan Mobile renderer)
```

Pembagian kerja antar komponen pada satu frame:

```text
CarController (Update)
  → hitung v baru dan x baru, catat LateralX dan MaxSpeedReached
ObstacleSpawner (Update)
  → geser rintangan yang bergerak, cek tabrakan AABB, daur ulang yang lewat
RoadSpawner (Update)
  → deteksi jarak yang turun, geser segment ke depan
DataLogger (Update)
  → tiap 0.05 s: catat t, x, v, a
CarVisual (Update)
  → miringkan badan dari Acceleration, putar roda dari Speed
```

Semua gerak dikerjakan manual, jadi tidak ada Rigidbody sama sekali di scene. Mobil pemain bergerak sepanjang sumbu `z` (ke depan) dan `x` (belok), dengan satuan meter dan detik; HUD baru mengubahnya ke km/jam.

## Aset 3D

Model kendaraan berasal dari [Kenney Car Kit](https://kenney.nl/assets/car-kit) versi 3.1, lisensi CC0, jadi bebas dipakai tanpa atribusi. Detailnya ada di `Assets/Resources/KenneyCarKit/README.md`. Beberapa hal yang penting diketahui kalau ingin mengganti model:

- Model dimuat saat runtime lewat `Resources.Load`, bukan lewat Prefab. Mobil pemain diatur di komponen `PlayerCarModel` (field `modelName`, saat ini `race`), rintangan diatur di `ObstacleSpawner` (field `obstacleModelPaths`).
- Kotak tabrakan rintangan mengikuti katalog ukuran di `VehicleAssets.cs`, yang diukur langsung dari mesh aslinya. Menambah model baru = tambah satu baris nama di `obstacleModelPaths` dan satu entri di katalog bila ukurannya beda dari default.
- Semua model menghadap sumbu `+Z` dengan pivot di dasar, cocok dengan arah maju mobil.

## Merekam data dan membuat grafik

Selama balapan, `DataLogger` mencatat `t_s`, `x_m`, `v_ms`, `a_ms2` tiap 0,05 detik, lalu menyimpannya saat balapan selesai. Lokasi filenya ditampilkan di layar hasil (Windows: `C:\Users\NAMA\AppData\LocalLow\<perusahaan>\<proyek>\`), dan tombol `O` membuka foldernya langsung.

Grafik untuk laporan dibuat dengan skrip Python:

```bash
python Assets/Scripts/plot_data.py data_balap.csv
python Assets/Scripts/plot_data.py data_balap.csv hasil.png   # nama sendiri
```

Skrip ini menggambar tiga kurva sekaligus, lalu menempelkan garis kejadian tabrakan (percepatan di bawah −20 m/s², dikelompokkan kalau terjadi berdekatan) dan waktu mencapai 60 km/jam sebagai pembanding antar percobaan.

Ada juga grafik di dalam game: setelah balapan, tekan `G`. Grafiknya digambar langsung dengan `Texture2D`, tanpa perlu keluar dari game.

## Membangun untuk Android

Render pipeline sudah menyediakan aset Mobile Renderer (`Assets/Settings/Mobile_Renderer.asset`), tapi hal-hal berikut belum disetel:

1. File → Build Profiles → pilih Android, lalu **Switch Platform**.
2. Player Settings: isi Company Name dan Product Name (lokasi folder CSV mengikuti keduanya).
3. Build. WebGL tidak didukung karena `DataLogger` menulis file — data perekaman butuh akses filesystem yang WebGL tidak punya.

## Pemecahan masalah

**Mobil bergerak tapi layar hitam.** Buka `Assets/Settings/PC_Renderer.asset` dan pastikan Unity masih memakainya di Graphics Settings (Project Settings → Graphics → Scriptable Render Pipeline Settings mengarah ke `PC_RPAsset`).

**Rintangan terlihat magenta.** Material model FBX gagal dikonversi ke URP. Konversi otomatisnya berjalan di `VehicleAssets.FixMaterials` saat game dimulai; kalau masih magenta, cek apakah shader `Universal Render Pipeline/Lit` ada di project.

**File CSV tidak muncul.** Pastikan balapan sudah selesai (bukan sekadar di-Stop dari editor), karena file baru disimpan di layar hasil. Path lengkapnya juga tercetak di Console.

**Model tidak muncul dan Console menampilkan warning "model tidak ditemukan".** Cek bahwa folder aset ada di `Assets/Resources/KenneyCarKit/FBX` dan nama di `modelName` atau `obstacleModelPaths` persis sama dengan nama file tanpa `.fbx`.

## Lisensi

Kode dan scene di repositori ini untuk keperluan praktikum. Aset 3D Kenney Car Kit lisensinya CC0 — bebas dipakai, diubah, dan didistribusikan ulang tanpa perlu atribusi; teks lisensinya menyertai aset di `Assets/Resources/KenneyCarKit/License.txt`.
