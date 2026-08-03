using System.Text;
using TMPro;
using UnityEngine;

namespace Attrition.Gameplay
{
    /// <summary>
    /// Nhãn TÊN nổi world-space (TextMeshPro, không cần Canvas) cho quái/NPC.
    /// Tự dựng runtime; gọi Attach một lần. Tên tĩnh nên không cần update mỗi frame.
    /// Dùng chung pattern với PlayerNameTag (sprite/text world-space, quay mặt camera 2D).
    /// </summary>
    public static class WorldNameLabel
    {
        public static GameObject Attach(Transform parent, string text, Vector3 offset, Color color, float fontSize = 3f)
        {
            if (parent == null || string.IsNullOrEmpty(text)) return null;

            var go = new GameObject("NameLabel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;

            var tm = go.AddComponent<TextMeshPro>();
            tm.text = text;
            tm.fontSize = fontSize;
            tm.alignment = TextAlignmentOptions.Center;
            tm.color = color;
            tm.textWrappingMode = TextWrappingModes.NoWrap;
            SetTopSortingLayer(tm.renderer, 80);

            return go;
        }

        /// <summary>
        /// Đẩy renderer lên sorting layer TRÊN CÙNG (dùng cho mọi UI world-space tạo bằng code).
        ///
        /// VÌ SAO CẦN: renderer tạo runtime mặc định rơi vào sorting layer "Default" — trong project này
        /// Default là layer THẤP NHẤT, nằm dưới cả Ground/Decor/Foreground tilemap, nên nhãn tên và thanh
        /// máu bị tile sàn che kín (đúng lỗi "quái thường không thấy thanh máu"). Đặt sortingOrder cao
        /// KHÔNG cứu được vì order chỉ so sánh trong CÙNG một sorting layer.
        /// </summary>
        private static int _topLayerId;
        private static bool _topLayerResolved;
        public static void SetTopSortingLayer(Renderer r, int order)
        {
            if (r == null) return;
            if (!_topLayerResolved)
            {
                _topLayerResolved = true;
                var layers = SortingLayer.layers;
                _topLayerId = layers.Length > 0 ? layers[layers.Length - 1].id : 0;
            }
            r.sortingLayerID = _topLayerId;
            r.sortingOrder = order;
        }

        /// <summary>
        /// Chuyển id máy đọc thành tên hiển thị: "axe_demon" → "Axe Demon",
        /// "skeletonSword" → "Skeleton Sword". Bỏ hậu tố "(Clone)", tách _/-/camelCase, Title Case.
        /// </summary>
        public static string Prettify(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            raw = raw.Replace("(Clone)", "").Trim().Replace('_', ' ').Replace('-', ' ');

            // Tách camelCase: chèn space trước chữ hoa đứng sau chữ thường.
            var sb = new StringBuilder(raw.Length + 4);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (i > 0 && char.IsUpper(c) && raw[i - 1] != ' ' && !char.IsUpper(raw[i - 1]))
                    sb.Append(' ');
                sb.Append(c);
            }

            var words = sb.ToString().Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;
                words[i] = char.ToUpperInvariant(words[i][0])
                    + (words[i].Length > 1 ? words[i].Substring(1).ToLowerInvariant() : "");
            }
            return string.Join(" ", words).Trim();
        }
    }
}
