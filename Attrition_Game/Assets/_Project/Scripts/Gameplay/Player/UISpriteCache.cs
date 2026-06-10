using UnityEngine;

namespace Attrition.Gameplay.Player
{
    /// <summary>Sprite trắng dùng chung cho thanh máu/nhãn world-space (tránh tạo texture lặp).</summary>
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
