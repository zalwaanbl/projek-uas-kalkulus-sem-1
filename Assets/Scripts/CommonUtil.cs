using UnityEngine;

/// <summary>
/// Pembantu kecil yang dipakai beberapa script:
///   - ColorMaterial : membuat material berwarna, cocok untuk URP maupun Built-in
///   - ExpLerp       : interpolasi yang hasilnya sama di FPS berapa pun
/// </summary>
public static class CommonUtil
{
    /// <summary>Material berwarna memakai shader bawaan pipeline aktif.</summary>
    public static Material ColorMaterial(Color color)
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material m = new Material(temp.GetComponent<Renderer>().sharedMaterial);
        m.color = color;
        Object.Destroy(temp);
        return m;
    }

    /// <summary>
    /// Lerp eksponensial: gerak halus yang tidak bergantung frame rate.
    /// rate = kecepatan menuju target (per detik). dt = deltaTime.
    /// </summary>
    public static float ExpLerp(float a, float b, float rate, float dt)
    {
        return Mathf.Lerp(a, b, 1f - Mathf.Exp(-rate * dt));
    }

    public static Vector3 ExpLerp(Vector3 a, Vector3 b, float rate, float dt)
    {
        return Vector3.Lerp(a, b, 1f - Mathf.Exp(-rate * dt));
    }
}
