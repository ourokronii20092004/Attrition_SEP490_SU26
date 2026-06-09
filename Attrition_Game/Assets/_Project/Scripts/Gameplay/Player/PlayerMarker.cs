using UnityEngine;
using TMPro;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Mũi tên + nhãn (P1/P2) nổi trên đầu player trong coop. Tự dựng runtime, không cần prefab.
    /// Mũi tên trỏ xuống đầu nhân vật; nhãn màu phân biệt local/remote.
    /// </summary>
    public class PlayerMarker : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.6f, 0f);
        private float _bob;

        public static void Attach(Transform target, string label, Color color)
        {
            var go = new GameObject("PlayerMarker");
            go.transform.SetParent(target, false);
            var marker = go.AddComponent<PlayerMarker>();
            marker.Build(label, color);
        }

        private void Build(string label, Color color)
        {
            transform.localPosition = offset;

            // Nhãn P1/P2
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(transform, false);
            var tm = textGo.AddComponent<TextMeshPro>();
            tm.text = label;
            tm.fontSize = 4.5f;
            tm.alignment = TextAlignmentOptions.Center;
            tm.color = color;
            tm.sortingOrder = 70;

            // Mũi tên trỏ xuống (tam giác bằng sprite scale)
            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(transform, false);
            arrow.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            arrow.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            arrow.transform.localScale = new Vector3(0.22f, 0.22f, 1f);
            var sr = arrow.AddComponent<SpriteRenderer>();
            sr.sprite = EnemyUISpriteCache.White();
            sr.color = color;
            sr.sortingOrder = 70;
        }

        private void Update()
        {
            // Bob nhẹ cho dễ thấy
            _bob += Time.deltaTime * 3f;
            transform.localPosition = offset + new Vector3(0f, Mathf.Sin(_bob) * 0.08f, 0f);
        }
    }

    /// <summary>Sprite trắng dùng chung cho marker/arrow (tránh tạo texture lặp).</summary>
    public static class EnemyUISpriteCache
    {
        private static Sprite _white;
        public static Sprite White()
        {
            if (_white != null) return _white;
            var tex = Texture2D.whiteTexture;
            _white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            return _white;
        }
    }
}
