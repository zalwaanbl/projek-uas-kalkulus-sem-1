# Kenney Car Kit (CC0)

Aset 3D kendaraan untuk mobil pemain dan rintangan.

- Sumber: <https://kenney.nl/assets/car-kit> (unduhan langsung: <https://opengameart.org/content/car-kit>)
- Lisensi: Creative Commons Zero (CC0 1.0) — bebas dipakai, diubah, dan didistribusikan ulang, tanpa atribusi. Teks lisensi: `License.txt`.
- Format: FBX. Satuan 1 unit = 1 meter, pivot di dasar model (y = 0), menghadap sumbu **+Z**. Tidak ada Collider/Rigidbody.

## Cara pakai

Model dimuat saat runtime dari `Resources/KenneyCarKit/FBX` (tanpa Prefab), jadi
tidak perlu menyeret apa pun ke scene.

### Mobil pemain

Komponen `PlayerCarModel` sudah terpasang di objek `car`:

- `modelName: race` — ganti ke `sedan-sports`, `hatchback-sports`, `suv-luxury`, dst.
- `visual` diisi komponen `CarVisual`; `legacyModel` diisi kubus lama (otomatis dimatikan).
- `yawOffset` (derajat) hanya perlu diisi bila model menghadap terbalik.
- Roda yang terpisah di FBX (nama mengandung `wheel`) otomatis masuk ke `CarVisual.wheels`.

### Rintangan (`ObstacleSpawner`)

Field `obstacleModelPaths` di objek `obtacles` sudah berisi 13 model campuran
(sedan, taxi, police, van, suv, truck, delivery, ambulance, firetruck,
garbage-truck, hatchback-sports, box, cone). Model dipilih acak per rintangan,
dan kotak tabrakannya otomatis mengikuti ukuran model dari `VehicleAssets`
(`obstacleSize` hanya dipakai saat memakai `obstaclePrefab` atau kubus).

Menambah jenis kendaraan: tambahkan satu baris nama model ke `obstacleModelPaths`.
Ukuran default untuk model yang belum terdaftar di katalog: 1.6 x 3.0 m, tinggi 1.4 m.

Catatan penting: model harus berada di bawah folder `Assets/Resources` agar
`Resources.Load` menemukannya.

## Isi paket

45 model: `ambulance`, `box`, `cone`, `cone-flat`, `delivery`, `delivery-flat`, `firetruck`, `garbage-truck`, `hatchback-sports`, `kart-oobi/oodi/ooli/oopi/oozi`, `police`, `race`, `race-future`, `sedan`, `sedan-sports`, `suv`, `suv-luxury`, `taxi`, `tractor`, `tractor-police`, `tractor-shovel`, `truck`, `truck-flat`, `van`, `wheel-*`, serta `debris-*`.

## Sumber alternatif (bila butuh variasi gaya)

| Sumber | Lisensi | Format | Catatan |
|---|---|---|---|
| [Quaternius Cars Bundle](https://poly.pizza/bundle/Cars-Bundle-FE5IWe6OMk) | CC0 | FBX, GLB | 8 mobil, low poly |
| [RGS_Dev Free Low Poly Vehicles](https://rgsdev.itch.io/free-low-poly-vehicles-pack) | gratis | FBX | unduh via itch.io |
| [Unity Asset Store – Free Cartoon Car Pack](https://assetstore.unity.com/packages/3d/vehicles/land/free-cartoon-car-pack-simple-vehicles-282425) | Unity Asset Store EULA | package Unity | perlu akun Unity |

Catatan: Unity tidak mengimpor GLB secara bawaan; pakai varian FBX.
