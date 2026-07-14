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
            tm.sortingOrder = 80;
            tm.textWrappingMode = TextWrappingModes.NoWrap;
            
            return go;
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
