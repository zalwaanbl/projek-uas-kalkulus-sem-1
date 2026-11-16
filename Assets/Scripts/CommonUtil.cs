using UnityEngine;

/// <summary>
/// Pembantu kecil yang dipakai beberapa script:
///   - ColorMaterial : membuat material berwarna, cocok untuk URP maupun Built-in
///   - ExpLerp       : interpolasi yang hasilnya sama di FPS berapa pun
/// </summary>
public static class CommonUtil
{
    /// <summary>
    /// Material berwarna yang aman di build URP. Jangan menyalin material kubus
    /// bawaan Unity: shader Standard tidak ada di build URP sehingga muncul ungu.
    /// </summary>
    public static Material ColorMaterial(Color color)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null)
        {
            Debug.LogWarning("CommonUtil.ColorMaterial: shader Lit/Standard tidak ditemukan.");
            return null;
        }

        Material m = new Material(sh) { name = "ColorMaterial" };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);   // URP Lit
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);           // Standard fallback
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
