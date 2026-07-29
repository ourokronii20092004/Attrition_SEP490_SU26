using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Attrition.Editor
{
    /// <summary>
    /// Đổi MÀU file PNG icon (Art/UI_Elements...) — ghi ra file MỚI, KHÔNG sửa file gốc.
    ///
    /// Cách dùng:
    ///  1. Chọn 1 hoặc nhiều file .png trong Project window.
    ///  2. Mở Tools/Attrition/Icon Recolor.
    ///  3. Chọn chế độ + màu, xem preview, bấm "Tạo file mới".
    ///
    /// 3 chế độ (pixel art nên ưu tiên Hue Shift / Colorize để GIỮ khối sáng-tối):
    ///  - HueShift : quay tông màu, giữ nguyên độ sáng + độ tươi → giáp sắt → giáp xanh/đỏ, vẫn có khối.
    ///  - Colorize : ép về 1 tông màu duy nhất, độ sáng gốc quyết định sáng/tối → đổi màu mạnh nhất.
    ///  - Multiply : nhân màu (tint) — nhanh nhưng dễ bị tối vì nhân vào cả vùng đậm.
    ///
    /// Pixel trong suốt (alpha = 0) luôn giữ nguyên; alpha KHÔNG bị đổi ở mọi chế độ.
    /// Import settings (Sprite, Point filter, PPU, alphaIsTransparency...) copy từ file gốc.
    /// </summary>
    public class IconRecolorWindow : EditorWindow
    {
        private enum Mode { HueShift, Colorize, Multiply }

        private Mode _mode = Mode.HueShift;
        private Color _target = new Color(0.35f, 0.6f, 1f, 1f);   // xanh mặc định
        private float _hueShift = 0.5f;      // 0..1 = 0..360 độ
        private float _strength = 1f;        // 0..1 pha trộn với màu gốc
        // MẶC ĐỊNH BẬT: icon trong UI_Elements có NỀN XÁM ĐỤC (#1A1A1A) phủ kín ảnh. Nếu tắt, nhuộm sẽ
        // ăn cả nền → mất màu nền (đã xảy ra với 'quần da'/'quần amethyst', phải nhuộm lại từ file gốc).
        private bool _keepGrayPixels = true;  // true = KHÔNG nhuộm pixel gần như xám (giữ nền + viền)
        private string _suffix = "_recolor";

        // ─── PHẠM VI NHUỘM: đổi 1 PHẦN ảnh, giữ nguyên nền + phần khác ───
        /// <summary>
        /// All     : nhuộm mọi pixel đục.
        /// Palette : tick từng màu — CHÍNH XÁC tuyệt đối, chỉ hợp ảnh PIXEL ART (ít màu).
        /// Range   : chọn 1 màu mẫu + sai số → nhuộm mọi màu GẦN nó. Dùng cho ảnh ANTI-ALIASED
        ///           (vd icon quần 86x117 có ~3500 màu, không thể tick tay).
        /// </summary>
        private enum Scope { All, Palette, Range }
        private Scope _scope = Scope.All;

        private readonly List<PaletteEntry> _palette = new List<PaletteEntry>();
        private readonly HashSet<int> _selectedKeys = new HashSet<int>();

        // Range mode: màu mẫu + sai số.
        private Color _pickColor = Color.white;
        private float _hueTol = 30f;    // độ (0..180)
        private float _satTol = 0.5f;   // 0..1
        private float _valTol = 1f;     // 0..1 (1 = bỏ qua độ sáng → bắt cả vùng sáng/tối cùng tông)

        /// <summary>Số màu tối đa hiện trong bảng (ảnh nhiều màu sẽ bị cắt để UI không treo).</summary>
        private const int MaxPaletteEntries = 64;
        private bool _paletteTruncated;
        /// <summary>Tổng số màu THẬT của các ảnh đang chọn (trước khi cắt) — để gợi ý chọn Palette hay Range.</summary>
        private int _paletteColorCount;

        private struct PaletteEntry
        {
            public int key;       // RGB đóng gói (bỏ alpha)
            public Color color;
            public int count;     // số pixel — sắp xếp giảm dần để màu chính lên trước
        }

        private readonly List<Texture2D> _sources = new List<Texture2D>();
        private readonly Dictionary<Texture2D, Texture2D> _previews = new Dictionary<Texture2D, Texture2D>();
        private Vector2 _scroll;

        /// <summary>Khoá màu theo RGB 0..255 (bỏ alpha) để so sánh/nhóm nhanh.</summary>
        private static int RgbKey(Color c)
            => (Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f) << 16)
             | (Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f) << 8)
             |  Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);

        [MenuItem("Tools/Attrition/Icon Recolor")]
        public static void Open()
        {
            var w = GetWindow<IconRecolorWindow>("Icon Recolor");
            w.minSize = new Vector2(430, 480);
            w.RefreshSelection();
        }

        private void OnSelectionChange() { RefreshSelection(); Repaint(); }

        private void RefreshSelection()
        {
            _sources.Clear();
            ClearPreviews();

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || !path.ToLower().EndsWith(".png")) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null && !_sources.Contains(tex)) _sources.Add(tex);
            }
            BuildPalette();   // gom bảng màu của các ảnh vừa chọn
            BuildPreviews();
        }

        private void ClearPreviews()
        {
            foreach (var kv in _previews)
                if (kv.Value != null) DestroyImmediate(kv.Value);
            _previews.Clear();
        }

        private void OnDisable() => ClearPreviews();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Chọn file .png trong Project window → chỉnh thông số → 'Tạo file mới'.\n" +
                "File GỐC không bị sửa; kết quả là file mới có hậu tố bên dưới.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();

            _mode = (Mode)EditorGUILayout.EnumPopup("Chế độ", _mode);
            switch (_mode)
            {
                case Mode.HueShift:
                    _hueShift = EditorGUILayout.Slider("Quay tông (hue)", _hueShift, 0f, 1f);
                    EditorGUILayout.LabelField(" ", $"≈ {Mathf.RoundToInt(_hueShift * 360f)}°");
                    break;
                case Mode.Colorize:
                case Mode.Multiply:
                    _target = EditorGUILayout.ColorField("Màu đích", _target);
                    break;
            }

            _strength = EditorGUILayout.Slider("Cường độ", _strength, 0f, 1f);
            _keepGrayPixels = EditorGUILayout.Toggle(
                new GUIContent("Giữ pixel xám (GIỮ NỀN)",
                    "BẬT = không nhuộm pixel gần như xám/đen/trắng → GIỮ NGUYÊN nền xám #1A1A1A của icon " +
                    "UI_Elements và các viền. Tắt sẽ nhuộm cả nền."),
                _keepGrayPixels);
            if (!_keepGrayPixels)
                EditorGUILayout.HelpBox(
                    "Đang TẮT 'Giữ pixel xám' → NỀN cũng bị nhuộm. Icon trong UI_Elements có nền xám đục " +
                    "phủ kín ảnh, nên thường phải BẬT.",
                    MessageType.Warning);
            _suffix = EditorGUILayout.TextField("Hậu tố file", _suffix);

            EditorGUILayout.Space();
            _scope = (Scope)EditorGUILayout.EnumPopup(
                new GUIContent("Phạm vi nhuộm",
                    "All = cả ảnh. Palette = tick từng màu (chỉ hợp pixel art). " +
                    "Range = chọn màu mẫu + sai số (cho ảnh anti-aliased nhiều màu)."),
                _scope);

            if (_scope == Scope.Range)
            {
                EditorGUI.indentLevel++;
                _pickColor = EditorGUILayout.ColorField("Màu mẫu (phần cần đổi)", _pickColor);
                _hueTol = EditorGUILayout.Slider("Sai số tông (độ)", _hueTol, 0f, 180f);
                _satTol = EditorGUILayout.Slider("Sai số độ tươi", _satTol, 0f, 1f);
                _valTol = EditorGUILayout.Slider("Sai số độ sáng", _valTol, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck()) BuildPreviews();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Đã chọn: {_sources.Count} file", EditorStyles.boldLabel);

            if (_sources.Count == 0)
            {
                EditorGUILayout.HelpBox("Chưa chọn file .png nào trong Project window.", MessageType.Warning);
                return;
            }

            if (_scope == Scope.Palette) DrawPaletteSection();
            if (_scope == Scope.Range) DrawRangeHelper();

            // Preview: gốc → kết quả.
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var src in _sources)
            {
                if (src == null) continue;
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField(Path.GetFileName(AssetDatabase.GetAssetPath(src)),
                                           GUILayout.Width(190));

                var r1 = GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48), GUILayout.Height(48));
                GUI.DrawTexture(r1, src, ScaleMode.ScaleToFit, true);

                EditorGUILayout.LabelField("→", GUILayout.Width(18));

                var r2 = GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48), GUILayout.Height(48));
                if (_previews.TryGetValue(src, out var prev) && prev != null)
                    GUI.DrawTexture(r2, prev, ScaleMode.ScaleToFit, true);

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_suffix)))
            {
                if (GUILayout.Button($"Tạo {_sources.Count} file mới", GUILayout.Height(32)))
                    ApplyAll();
            }
            if (string.IsNullOrWhiteSpace(_suffix))
                EditorGUILayout.HelpBox("Hậu tố không được để trống (sẽ ghi đè file gốc).", MessageType.Error);
        }

        //  PHẠM VI: pixel này có bị nhuộm không?

        /// <summary>Pixel có nằm trong phạm vi cần nhuộm không (theo Scope đang chọn).</summary>
        private bool InScope(Color c)
        {
            switch (_scope)
            {
                case Scope.Palette:
                    return _selectedKeys.Contains(RgbKey(c));

                case Scope.Range:
                {
                    Color.RGBToHSV(c, out float h, out float s, out float v);
                    Color.RGBToHSV(_pickColor, out float ph, out float ps, out float pv);

                    // Hue là vòng tròn: 0.98 và 0.02 chỉ cách nhau 0.04, không phải 0.96.
                    float dh = Mathf.Abs(Mathf.DeltaAngle(h * 360f, ph * 360f));
                    if (dh > _hueTol) return false;
                    if (Mathf.Abs(s - ps) > _satTol) return false;
                    if (Mathf.Abs(v - pv) > _valTol) return false;
                    return true;
                }

                default:
                    return true;   // All
            }
        }

        /// <summary>Gợi ý dùng Range + cho biết ảnh đang chọn có bao nhiêu màu (để chọn đúng chế độ).</summary>
        private void DrawRangeHelper()
        {
            EditorGUILayout.HelpBox(
                "Cách dùng: mở ảnh ra xem, lấy màu của PHẦN CẦN ĐỔI (vd thân cái quần) vào 'Màu mẫu'.\n" +
                "• Sai số tông nhỏ (~20-40°) = chỉ bắt đúng tông đó.\n" +
                "• Sai số độ sáng = 1 → bắt cả vùng sáng và tối cùng tông (giữ được khối shading).\n" +
                "Xem preview bên dưới rồi tăng/giảm sai số cho vừa.",
                MessageType.None);

            if (_paletteColorCount > 0)
                EditorGUILayout.LabelField($"Ảnh đang chọn có ~{_paletteColorCount} màu " +
                    (_paletteColorCount > MaxPaletteEntries
                        ? "(nhiều màu → Range là đúng lựa chọn)"
                        : "(ít màu → có thể dùng Palette để chính xác hơn)"));
            EditorGUILayout.Space();
        }

        //  PALETTE (chọn màu cần đổi)

        /// <summary>
        /// Vẽ bảng màu có trong ảnh đang chọn + ô tick cho từng màu. Chỉ màu được tick mới bị nhuộm.
        /// Sắp xếp theo số pixel giảm dần → màu chiếm nhiều (nền, thân đồ) nằm trên cùng.
        /// </summary>
        private void DrawPaletteSection()
        {
            EditorGUILayout.LabelField("Bảng màu trong ảnh — tick màu MUỐN ĐỔI", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Pixel art chỉ có ít màu. Tick các màu của phần cần đổi (vd 2-3 tông của cái quần: " +
                "sáng / giữa / tối). Màu KHÔNG tick sẽ giữ nguyên 100%.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Chọn tất cả")) { foreach (var p in _palette) _selectedKeys.Add(p.key); BuildPreviews(); }
                if (GUILayout.Button("Bỏ chọn hết")) { _selectedKeys.Clear(); BuildPreviews(); }
            }

            if (_palette.Count == 0)
            {
                EditorGUILayout.HelpBox("Không đọc được màu nào từ ảnh đã chọn.", MessageType.Warning);
                return;
            }

            int changed = -1;
            for (int i = 0; i < _palette.Count; i++)
            {
                var entry = _palette[i];
                bool on = _selectedKeys.Contains(entry.key);

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool newOn = EditorGUILayout.Toggle(on, GUILayout.Width(18));

                    // Ô màu.
                    var rect = GUILayoutUtility.GetRect(28, 16, GUILayout.Width(28), GUILayout.Height(16));
                    EditorGUI.DrawRect(rect, entry.color);

                    Color32 c32 = entry.color;
                    EditorGUILayout.LabelField($"#{c32.r:X2}{c32.g:X2}{c32.b:X2}   {entry.count} px");

                    if (newOn != on)
                    {
                        if (newOn) _selectedKeys.Add(entry.key); else _selectedKeys.Remove(entry.key);
                        changed = i;
                    }
                }
            }
            if (changed >= 0) BuildPreviews();

            EditorGUILayout.LabelField($"Đang đổi {_selectedKeys.Count}/{_palette.Count} màu");
            if (_paletteTruncated)
                EditorGUILayout.HelpBox(
                    $"Ảnh có nhiều hơn {MaxPaletteEntries} màu — chỉ hiện {MaxPaletteEntries} màu chiếm " +
                    "nhiều pixel nhất. Ảnh pixel-art 16x16 thường chỉ vài màu nên không gặp trường hợp này.",
                    MessageType.Warning);
            EditorGUILayout.Space();
        }

        /// <summary>Gom mọi màu (bỏ pixel trong suốt) của các ảnh đang chọn thành palette dùng chung.</summary>
        private void BuildPalette()
        {
            var counts = new Dictionary<int, int>();
            var colors = new Dictionary<int, Color>();

            foreach (var src in _sources)
            {
                if (src == null) continue;
                var px = ReadPixels(src, out _, out _);
                if (px == null) continue;

                foreach (var c in px)
                {
                    if (c.a <= 0.001f) continue;   // trong suốt không tính vào palette
                    int k = RgbKey(c);
                    counts.TryGetValue(k, out int n);
                    counts[k] = n + 1;
                    if (!colors.ContainsKey(k)) colors[k] = new Color(c.r, c.g, c.b, 1f);
                }
            }

            _paletteColorCount = counts.Count;   // số màu THẬT (trước khi cắt) — để gợi ý Palette vs Range

            _palette.Clear();
            foreach (var kv in counts)
                _palette.Add(new PaletteEntry { key = kv.Key, color = colors[kv.Key], count = kv.Value });

            _palette.Sort((a, b) => b.count.CompareTo(a.count));   // nhiều pixel nhất lên đầu

            // Ảnh KHÔNG phải pixel-art (ảnh chụp, gradient) có thể có hàng nghìn màu → vẽ hết sẽ treo
            // UI và cũng vô dụng để tick tay. Chỉ giữ N màu chiếm nhiều pixel nhất.
            _paletteTruncated = _palette.Count > MaxPaletteEntries;
            if (_paletteTruncated) _palette.RemoveRange(MaxPaletteEntries, _palette.Count - MaxPaletteEntries);

            // Bỏ các key đã chọn nhưng không còn trong palette (đổi file đang chọn).
            _selectedKeys.RemoveWhere(k => !counts.ContainsKey(k));
        }

        //  PREVIEW

        private void BuildPreviews()
        {
            ClearPreviews();
            foreach (var src in _sources)
            {
                if (src == null) continue;
                var pixels = ReadPixels(src, out int w, out int h);
                if (pixels == null) continue;

                Recolor(pixels);

                var prev = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                prev.SetPixels(pixels);
                prev.Apply();
                _previews[src] = prev;
            }
        }

        //  APPLY

        private void ApplyAll()
        {
            int done = 0;
            try
            {
                for (int i = 0; i < _sources.Count; i++)
                {
                    var src = _sources[i];
                    if (src == null) continue;

                    string srcPath = AssetDatabase.GetAssetPath(src);
                    EditorUtility.DisplayProgressBar("Icon Recolor", srcPath, (float)i / _sources.Count);

                    var pixels = ReadPixels(src, out int w, out int h);
                    if (pixels == null) continue;
                    Recolor(pixels);

                    var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    outTex.SetPixels(pixels);
                    outTex.Apply();

                    string dir = Path.GetDirectoryName(srcPath);
                    string name = Path.GetFileNameWithoutExtension(srcPath);
                    string outPath = $"{dir}/{name}{_suffix}.png".Replace('\\', '/');

                    File.WriteAllBytes(outPath, outTex.EncodeToPNG());
                    DestroyImmediate(outTex);

                    AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
                    CopyImportSettings(srcPath, outPath);
                    done++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[IconRecolor] Đã tạo {done} file mới (hậu tố '{_suffix}'). File gốc giữ nguyên. " +
                      "Gán sprite mới vào ItemSO.icon nếu muốn dùng trong game.");
        }

        /// <summary>Copy import settings từ file gốc để sprite mới hiển thị y hệt (Point filter, PPU...).</summary>
        private static void CopyImportSettings(string srcPath, string dstPath)
        {
            var from = AssetImporter.GetAtPath(srcPath) as TextureImporter;
            var to = AssetImporter.GetAtPath(dstPath) as TextureImporter;
            if (from == null || to == null) return;

            to.textureType = from.textureType;
            to.spriteImportMode = from.spriteImportMode;
            to.spritePixelsPerUnit = from.spritePixelsPerUnit;
            to.filterMode = from.filterMode;
            to.wrapMode = from.wrapMode;
            to.alphaIsTransparency = from.alphaIsTransparency;
            to.mipmapEnabled = from.mipmapEnabled;
            to.textureCompression = from.textureCompression;
            to.maxTextureSize = from.maxTextureSize;

            // spriteMeshType nằm trong TextureImporterSettings (không phải property trực tiếp).
            // IconImportFixer ép FullRect cho icon 16x16 → copy theo để icon mới hiển thị y hệt.
            var s = new TextureImporterSettings();
            from.ReadTextureSettings(s);
            to.SetTextureSettings(s);

            to.SaveAndReimport();
        }

        //  ĐỌC PIXEL (không cần bật Read/Write trên asset)

        /// <summary>
        /// Đọc pixel từ FILE PNG trên đĩa qua LoadImage — KHÔNG cần asset bật isReadable, và không
        /// phải sửa import settings của file gốc.
        /// </summary>
        private static Color[] ReadPixels(Texture2D src, out int w, out int h)
        {
            w = h = 0;
            string path = AssetDatabase.GetAssetPath(src);
            string full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                Debug.LogWarning($"[IconRecolor] Không đọc được file: {path}");
                return null;
            }

            var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tmp.LoadImage(File.ReadAllBytes(full)))
            {
                DestroyImmediate(tmp);
                Debug.LogWarning($"[IconRecolor] PNG không hợp lệ: {path}");
                return null;
            }

            w = tmp.width; h = tmp.height;
            var px = tmp.GetPixels();
            DestroyImmediate(tmp);
            return px;
        }

        //  THUẬT TOÁN ĐỔI MÀU

        private void Recolor(Color[] px)
        {
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                if (c.a <= 0.001f) continue;             // trong suốt → bỏ qua

                // LỌC PALETTE: chỉ nhuộm màu ĐƯỢC TICK → phần khác (nền, áo, da...) giữ nguyên 100%.
                if (!InScope(c)) continue;

                Color.RGBToHSV(c, out float h, out float s, out float v);

                // Giữ pixel gần như xám (viền đen, highlight trắng) nếu người dùng chọn.
                if (_keepGrayPixels && s < 0.12f) continue;

                Color outC;
                switch (_mode)
                {
                    case Mode.HueShift:
                        // Quay tông, GIỮ s và v → giữ nguyên khối sáng-tối của pixel art.
                        outC = Color.HSVToRGB(Mathf.Repeat(h + _hueShift, 1f), s, v);
                        break;

                    case Mode.Colorize:
                    {
                        // Lấy hue+sat của màu đích, giữ độ sáng gốc → đổi tông mạnh mà vẫn còn khối.
                        Color.RGBToHSV(_target, out float th, out float ts, out _);
                        outC = Color.HSVToRGB(th, ts, v);
                        break;
                    }

                    default: // Multiply
                        outC = new Color(c.r * _target.r, c.g * _target.g, c.b * _target.b, 1f);
                        break;
                }

                // Pha trộn theo cường độ; alpha LUÔN giữ nguyên.
                px[i] = new Color(
                    Mathf.Lerp(c.r, outC.r, _strength),
                    Mathf.Lerp(c.g, outC.g, _strength),
                    Mathf.Lerp(c.b, outC.b, _strength),
                    c.a);
            }
        }
    }
}
