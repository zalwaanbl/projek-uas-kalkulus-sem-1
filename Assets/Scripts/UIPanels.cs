using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel/box UI rounded (9-slice) untuk menu, hasil, dan HUD.
/// Hanya tampilan: logika gameplay/fisika/CSV tidak diubah.
/// Teks lama dipakai ulang sehingga referensi GameManager/HUDManager tetap valid.
/// </summary>
public class UIPanels : MonoBehaviour
{
    static Sprite rounded;
    static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded());

    Canvas canvas;
    GameManager gm;

    RectTransform menuContent, resultContent, resultRows;
    TMP_Text resultSrc, csvText, hintText, resultHeader;
    string lastResult = "\u0000";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindObjectOfType<UIPanels>() != null) return;
        var go = new GameObject("UIPanels");
        DontDestroyOnLoad(go);
        go.AddComponent<UIPanels>();
    }

    void Start()
    {
        canvas = FindObjectOfType<Canvas>();
        gm = FindObjectOfType<GameManager>();
        if (canvas == null) { Debug.LogWarning("UIPanels: Canvas tidak ditemukan."); enabled = false; return; }

        EnsureScaler();
        BuildMenu();
        BuildResult();
        BuildHud();
    }

    void EnsureScaler()
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    TMP_Text FindText(string name)
    {
        foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true))
            if (t.name == name) return t;
        return null;
    }

    // ------------------------------------------------------------------ MENU
    void BuildMenu()
    {
        Transform parent = gm != null && gm.menuPanel != null ? gm.menuPanel.transform : canvas.transform;
        ClearImage(parent);
        menuContent = MakeBox("MenuBox", parent, 640f, 24, new Color(0f, 0f, 0f, 0.65f));

        var title = FindText("TitleText");
        if (title != null)
        {
            Move(title.transform, menuContent);
            title.fontSize = 54; title.fontStyle = FontStyles.Bold;
            title.color = Color.white; title.alignment = TextAlignmentOptions.Center;
        }

        var info = FindText("InfoText");
        if (info != null)
        {
            Move(info.transform, menuContent);
            info.fontSize = 24; info.color = new Color(0.85f, 0.87f, 0.9f);
            info.alignment = TextAlignmentOptions.Center;
        }

        var best = gm != null ? gm.bestText : FindText("BestText");
        if (best != null)
        {
            var rekor = MakeBox("RekorBox", menuContent, 0f, 10, new Color(1f, 0.78f, 0.2f, 0.14f));
            Move(best.transform, rekor);
            best.fontSize = 26; best.fontStyle = FontStyles.Bold;
            best.color = new Color(1f, 0.85f, 0.35f);
            best.alignment = TextAlignmentOptions.Center;
        }
    }

    // ------------------------------------------------------------------ HASIL
    void BuildResult()
    {
        Transform parent = gm != null && gm.resultPanel != null ? gm.resultPanel.transform : canvas.transform;
        ClearImage(parent);
        resultContent = MakeBox("ResultBox", parent, 760f, 24, new Color(0f, 0f, 0f, 0.65f));

        // strip hijau di atas
        var strip = new GameObject("HeaderStrip", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        strip.transform.SetParent(resultContent, false);
        var si = strip.GetComponent<Image>();
        si.color = new Color(0.25f, 0.9f, 0.45f); si.raycastTarget = false;
        var sle = strip.GetComponent<LayoutElement>();
        sle.minHeight = 6f; sle.preferredHeight = 6f;

        resultHeader = MakeText("ResultHeader", resultContent, "SELESAI!", 40, Color.white, FontStyles.Bold);
        resultHeader.alignment = TextAlignmentOptions.Center;

        var rowsGo = new GameObject("Stats", typeof(RectTransform), typeof(VerticalLayoutGroup));
        rowsGo.transform.SetParent(resultContent, false);
        var vg = rowsGo.GetComponent<VerticalLayoutGroup>();
        vg.spacing = 6f; vg.childControlHeight = true; vg.childControlWidth = true;
        vg.childForceExpandHeight = false; vg.childForceExpandWidth = true;
        resultRows = rowsGo.GetComponent<RectTransform>();

        csvText = MakeText("CsvText", resultContent, "", 18, new Color(0.75f, 0.78f, 0.82f), FontStyles.Normal);
        var csvBox = MakeBox("CsvBox", resultContent, 0f, 10, new Color(1f, 1f, 1f, 0.06f));
        csvText.transform.SetParent(csvBox, false);

        hintText = MakeText("HintText", resultContent, "", 20, new Color(0.85f, 0.87f, 0.9f), FontStyles.Normal);
        var hintBox = MakeBox("HintBox", resultContent, 0f, 10, new Color(1f, 1f, 1f, 0.08f));
        hintText.transform.SetParent(hintBox, false);

        resultSrc = gm != null ? gm.resultText : FindText("ResultText");
        if (resultSrc != null) resultSrc.color = Color.clear;
    }

    void RefreshResult()
    {
        if (resultSrc == null || resultRows == null) return;
        if (resultSrc.text == lastResult) return;
        lastResult = resultSrc.text;

        for (int i = resultRows.childCount - 1; i >= 0; i--)
            Destroy(resultRows.GetChild(i).gameObject);

        string csv = "", hint = "", header = "SELESAI!";
        foreach (string raw in lastResult.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("SELESAI") || line.StartsWith("GAME OVER")) { header = line; continue; }
            if (line.StartsWith("Data CSV:")) { csv = line; continue; }
            if (line.StartsWith("R / ENTER")) { hint = line; continue; }

            int c = line.IndexOf(':');
            string label = c > 0 ? line.Substring(0, c).Trim() : line;
            string value = c > 0 ? line.Substring(c + 1).Trim() : "";
            MakeStatRow(label, value);
        }

        resultHeader.text = header;
        bool over = header.StartsWith("GAME");
        resultHeader.color = over ? new Color(1f, 0.45f, 0.4f) : new Color(0.45f, 1f, 0.6f);
        csvText.text = csv;
        hintText.text = hint;
    }

    void MakeStatRow(string label, string value)
    {
        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(resultRows, false);
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 16f; h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = true; h.childForceExpandHeight = false;

        var l = MakeText("L", row.transform, label, 22, new Color(0.7f, 0.73f, 0.78f), FontStyles.Normal);
        l.alignment = TextAlignmentOptions.Left;
        var r = MakeText("V", row.transform, value, 22, Color.white, FontStyles.Bold);
        r.alignment = TextAlignmentOptions.Right;
    }

    // ------------------------------------------------------------------ HUD
    void BuildHud()
    {
        Transform root = canvas.transform;
        var boxes = new List<GameObject>();

        var a = MakeBox("HudTopLeft", root, 240f, 12, new Color(0f, 0f, 0f, 0.5f));
        Anchor(a, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f));
        AddText(a, "DistanceText"); AddText(a, "TimeText");
        boxes.Add(a.gameObject);

        var b = MakeBox("HudRight", root, 240f, 12, new Color(0f, 0f, 0f, 0.5f));
        Anchor(b, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f));
        AddText(b, "NumericAccelText"); AddText(b, "AccelText");
        boxes.Add(b.gameObject);

        var c = MakeBox("HudSpeed", root, 260f, 12, new Color(0f, 0f, 0f, 0.5f));
        Anchor(c, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f));
        var speed = AddText(c, "SpeedText");
        if (speed != null) { speed.fontSize = 40; speed.fontStyle = FontStyles.Bold; }
        boxes.Add(c.gameObject);

        if (gm != null)
        {
            var list = new List<GameObject>();
            if (gm.hudObjects != null) list.AddRange(gm.hudObjects);
            foreach (var g in boxes) if (!list.Contains(g)) list.Add(g);
            gm.hudObjects = list.ToArray();

            // Kalau UIPanels.Start jalan setelah GameManager.ShowMenu,
            // box HUD belum ikut tersembunyi. Sembunyikan saat di menu.
            bool show = gm.State != GameState.Menu;
            foreach (var g in boxes) g.SetActive(show);
        }
    }

    TMP_Text AddText(RectTransform box, string name)
    {
        TMP_Text t = FindText(name);
        if (t == null) return null;
        Move(t.transform, box);
        t.alignment = TextAlignmentOptions.Left;
        return t;
    }

    // ------------------------------------------------------------------ bantuan
    static void ClearImage(Transform t)
    {
        var img = t.GetComponent<Image>();
        if (img != null) { img.color = Color.clear; img.raycastTarget = false; }
    }

    static void Move(Transform child, Transform parent)
    {
        child.SetParent(parent, false);
        var le = child.GetComponent<LayoutElement>();
        if (le == null) le = child.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 20f;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
        rt.anchoredPosition = pos;
    }

    /// <summary>Box rounded: Image 9-slice + VerticalLayoutGroup + ContentSizeFitter.</summary>
    RectTransform MakeBox(string name, Transform parent, float width, int padding, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, 0f);

        var img = go.GetComponent<Image>();
        img.sprite = Rounded; img.type = Image.Type.Sliced;
        img.color = bg; img.raycastTarget = false;

        // border putih tipis (rounded), tidak ikut layout
        var border = new GameObject("Border", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        border.transform.SetParent(go.transform, false);
        var bimg = border.GetComponent<Image>();
        bimg.sprite = Rounded; bimg.type = Image.Type.Sliced;
        bimg.color = new Color(1f, 1f, 1f, 0.3f); bimg.raycastTarget = false;
        var brt = border.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-1.5f, -1.5f);
        brt.offsetMax = new Vector2(1.5f, 1.5f);
        border.GetComponent<LayoutElement>().ignoreLayout = true;
        border.transform.SetAsFirstSibling();

        var vg = go.GetComponent<VerticalLayoutGroup>();
        vg.padding = new RectOffset(padding, padding, padding, padding);
        vg.spacing = 10f;
        vg.childControlHeight = true; vg.childControlWidth = true;
        vg.childForceExpandHeight = false; vg.childForceExpandWidth = true;

        var fit = go.GetComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    TMP_Text MakeText(string name, Transform parent, string text, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TMP_Text>();
        var sample = FindText("ProgressText") ?? FindText("SpeedText");
        if (sample != null && sample.font != null) t.font = sample.font;
        t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.raycastTarget = false;
        t.enableWordWrapping = true;
        go.GetComponent<LayoutElement>().minHeight = 20f;
        return t;
    }

    void LateUpdate()
    {
        if (gm != null) RefreshResult();
    }

    // ------------------------------------------------------------------ sprite
    static Sprite MakeRounded()
    {
        const int size = 64, radius = 18;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - 0.5f, x - (size - radius) + 0.5f, 0f);
                float dy = Mathf.Max(radius - y - 0.5f, y - (size - radius) + 0.5f, 0f);
                float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply(false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
