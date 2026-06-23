using UnityEngine;

namespace Attrition.Data
{
    [CreateAssetMenu(menuName = "Attrition/Movement Config", fileName = "MovementConfig")]
    public class MovementConfigSO : ScriptableObject
    {
        [Header("---- ADVANCED MOVEMENT ----")]
        [Tooltip("Bật để Dash có I-Frames (không nhận sát thương khi đang lướt)")]
        public bool hasShadowDash = false;
        public float dashDuration = 0.2f;
        public float dashCooldownTime = 0.8f;
        public float crouchSpeedMultiplier = 0.4f;
        public float variableJumpCutMultiplier = 0.5f;
        public int maxJumps = 2;

        [Header("---- SLIDE ----")]
        public float slideDuration = 0.5f;
        public float slideCooldownTime = 1f;

        [Header("---- HITBOX RESIZING (CROUCH/SLIDE) ----")]
        public Vector2 standSize = new Vector2(1f, 2f);
        public Vector2 standOffset = new Vector2(0f, 0f);
        public Vector2 crouchSize = new Vector2(1f, 1f);
        public Vector2 crouchOffset = new Vector2(0f, -0.5f);

        [Header("---- HOLLOW KNIGHT GRAVITY ----")]
        [Tooltip("Trọng lực mặc định khi đi trên mặt đất hoặc bay lên")]
        public float normalGravity = 2f;
        [Tooltip("Trọng lực khi rơi xuống (càng cao rơi càng nhanh)")]
        public float fallGravity = 4.5f;
        [Tooltip("Tốc độ rơi tối đa")]
        public float maxFallSpeed = -25f;

        [Header("---- KNOCKBACK (KHI BỊ ĐÁNH) ----")]
        [Tooltip("Lực đẩy lùi khi bị quái đánh")]
        public float knockbackForceOverride = 6f;
        [Tooltip("Thời gian khựng sau khi bị đánh")]
        public float knockbackDuration = 0.25f;
        [Tooltip("Thời gian bất tử (I-frames)")]
        public float invincibleDuration = 0.8f;
    }
}
