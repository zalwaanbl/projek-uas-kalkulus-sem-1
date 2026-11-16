#!/usr/bin/env python3
"""
Membuat grafik x(t), v(t), a(t) dari file CSV hasil game dan membandingkan
percepatan dari game (a) dengan turunan numerik kecepatan (dv/dt dari data v).

Cara pakai (di terminal / Command Prompt):
    pip install numpy matplotlib
    python plot_data.py data_balap.csv
    python plot_data.py data_balap.csv hasil.png     # nama file gambar sendiri

Lokasi file CSV dari game ditampilkan di layar hasil (tombol O membuka foldernya).
Kolom CSV: t_s, x_m, v_ms, a_ms2
"""
import csv
import sys

import numpy as np
import matplotlib

matplotlib.use("Agg")   # simpan ke file, tidak perlu jendela
import matplotlib.pyplot as plt


def baca_csv(path):
    t, x, v, a = [], [], [], []
    with open(path, newline="", encoding="utf-8") as f:
        for row in csv.DictReader(f):
            t.append(float(row["t_s"]))
            x.append(float(row["x_m"]))
            v.append(float(row["v_ms"]))
            a.append(float(row["a_ms2"]))
    return np.array(t), np.array(x), np.array(v), np.array(a)


def waktu_mencapai(t, v, target_ms):
    """Waktu pertama kali v >= target (dengan interpolasi linear), atau None."""
    idx = np.where(v >= target_ms)[0]
    if len(idx) == 0:
        return None
    i = idx[0]
    if i == 0:
        return float(t[0])
    frac = (target_ms - v[i - 1]) / (v[i] - v[i - 1])
    return float(t[i - 1] + frac * (t[i] - t[i - 1]))


def hitung_tabrakan(t, a, ambang=-20.0, jeda=0.5):
    """Hitung kejadian tabrakan = lonjakan percepatan di bawah ambang (dikelompokkan)."""
    waktu = []
    for ti in t[a < ambang]:
        if not waktu or ti - waktu[-1] > jeda:
            waktu.append(float(ti))
    return waktu


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    path = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else "grafik_balap.png"

    t, x, v, a = baca_csv(path)
    if len(t) < 3:
        print("Data terlalu sedikit untuk digambar.")
        sys.exit(1)

    # Turunan numerik dari DATA (inilah definisi turunan: selisih kecil / selisih waktu)
    v_dari_x = np.gradient(x, t)    # dx/dt
    a_dari_v = np.gradient(v, t)    # dv/dt

    # ---------- Grafik ----------
    fig, ax = plt.subplots(3, 1, figsize=(10, 9), sharex=True)

    ax[0].plot(t, x, color="tab:blue")
    ax[0].set_ylabel("x (m)")
    ax[0].set_title("Posisi x(t)")

    ax[1].plot(t, v, color="tab:green", label="v dari game")
    ax[1].plot(t, v_dari_x, "--", color="k", linewidth=1, label="dx/dt (turunan numerik)")
    ax[1].set_ylabel("v (m/s)")
    ax[1].set_title("Kecepatan v(t) = dx/dt")
    ax[1].legend(loc="lower right")

    ax[2].plot(t, a, color="tab:orange", label="a dari game")
    ax[2].plot(t, a_dari_v, "--", color="k", linewidth=1, label="dv/dt (turunan numerik)")
    ax[2].axhline(0, color="gray", linewidth=0.8)
    ax[2].set_ylabel("a (m/s²)")
    ax[2].set_xlabel("waktu t (s)")
    ax[2].set_title("Percepatan a(t) = dv/dt")
    ax[2].legend(loc="lower right")

    waktu_tabrak = hitung_tabrakan(t, a)
    for w in waktu_tabrak:
        for sumbu in ax:
            sumbu.axvline(w, color="red", alpha=0.25)

    for sumbu in ax:
        sumbu.grid(alpha=0.3)

    fig.tight_layout()
    fig.savefig(out, dpi=150)

    # ---------- Ringkasan angka (untuk laporan) ----------
    durasi = t[-1] - t[0]
    jarak = x[-1] - x[0]
    t100 = waktu_mencapai(t, v, 100 / 3.6)
    t200 = waktu_mencapai(t, v, 200 / 3.6)

    # Seberapa dekat 'a' dari game dengan dv/dt dari data (abaikan lonjakan tabrakan)
    mask = np.ones(len(t), dtype=bool)
    for w in waktu_tabrak:
        mask &= np.abs(t - w) > 0.6
    galat = np.abs(a - a_dari_v)[mask]

    print("=== Ringkasan data ===")
    print(f"Jumlah data            : {len(t)} baris")
    print(f"Durasi                 : {durasi:.2f} s")
    print(f"Jarak                  : {jarak:.1f} m")
    print(f"Kecepatan rata-rata    : {jarak / durasi:.2f} m/s  ({jarak / durasi * 3.6:.1f} km/jam)  [= jarak / waktu]")
    print(f"Kecepatan tertinggi    : {v.max():.2f} m/s  ({v.max() * 3.6:.1f} km/jam)")
    print(f"Percepatan maks        : {a.max():.2f} m/s²")
    print(f"Perlambatan terbesar   : {a.min():.2f} m/s²")
    print(f"Waktu 0 -> 100 km/jam  : {'-' if t100 is None else f'{t100:.2f} s'}")
    print(f"Waktu 0 -> 200 km/jam  : {'-' if t200 is None else f'{t200:.2f} s'}")
    print(f"Tabrakan terdeteksi    : {len(waktu_tabrak)} kali" +
          (" (t = " + ", ".join(f"{w:.1f}" for w in waktu_tabrak) + " s)" if waktu_tabrak else ""))
    if mask.sum() > 0:
        print(f"Selisih |a - dv/dt|    : rata-rata {galat.mean():.3f} m/s², maks {galat.max():.3f} m/s² (di luar tabrakan)")
    print(f"Grafik disimpan        : {out}")


if __name__ == "__main__":
    main()