using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Memasang model FBX Kenney Car Kit ke mobil pemain saat runtime
/// (tanpa perlu Prefab). Pasang komponen ini di objek "car".
///
/// - Model lama (kubus bawaan game.unity) dinonaktifkan lewat field legacyModel.
/// - Wrapper "ModelRoot" dipakai sebagai CarVisual.model supaya rotasi
///   pitch/roll dari CarVisual tidak menimpa offset hadap model.
/// - Roda yang terpisah di dalam FBX (nama mengandung "wheel") otomatis
///   dimasukkan ke CarVisual.wheels agar ikut berputar.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlayerCarModel : MonoBehaviour
{
    [Tooltip("Nama model di Resources/KenneyCarKit/FBX (tanpa ekstensi)")]
    public string modelName = "race";
    [Tooltip("Putaran tambahan model terhadap sumbu Y (derajat), bila hadap terbalik")]
    public float yawOffset = 0f;
    public CarVisual visual;
    [Tooltip("Objek model lama (kubus) yang dimatikan saat model aset terpasang")]
    public GameObject legacyModel;

    void Awake()
    {
        GameObject prefab = VehicleAssets.Load(modelName);
        if (prefab == null)
        {
            Debug.LogWarning("PlayerCarModel: model tidak ditemukan di Resources: " + VehicleAssets.Root + modelName);
            return;
        }

        Transform root = new GameObject("ModelRoot").transform;
        root.SetParent(transform, false);

        GameObject go = Instantiate(prefab, root, false);
        go.name = "Model_" + modelName;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(0f, yawOffset, 0f);

        // Model hanya visual; tabrakan dihitung sendiri oleh ObstacleSpawner.
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);

        if (legacyModel != null) legacyModel.SetActive(false);

        if (visual == null) visual = GetComponent<CarVisual>();
        if (visual == null) return;

        visual.model = root;

        List<Transform> wheels = new List<Transform>();
        foreach (Transform t in go.GetComponentsInChildren<Transform>())
            if (t != go.transform && t.name.ToLowerInvariant().Contains("wheel"))
                wheels.Add(t);
        if (wheels.Count > 0) visual.wheels = wheels.ToArray();
    }
}
