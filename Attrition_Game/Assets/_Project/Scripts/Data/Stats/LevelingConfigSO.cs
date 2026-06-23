using UnityEngine;

namespace Attrition.Data
{
    /// <summary>
    /// SO chứa cấu hình Leveling và chỉ số nhân vật khi thăng cấp.
    /// Tách rời khỏi Player Prefab để dễ chỉnh sửa balance.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Leveling Config", fileName = "LevelingConfig")]
    public class LevelingConfigSO : ScriptableObject
    {
        [Header("---- EXP CURVE ----")]
        [Tooltip("EXP cần để lên level 2 (mốc đầu).")]
        public int baseExp = 100;
        [Tooltip("EXP cần tăng thêm mỗi level (về sau cày lâu hơn).")]
        public int perLevelExp = 50;

        [Header("---- LEVELING (Option 2 — tự phân bổ) ----")]
        [Tooltip("Cấp tối đa.")]
        public int maxLevel = 21;
        [Tooltip("Số điểm chỉ số nhận mỗi lần lên cấp, người chơi tự cộng.")]
        public int statPointsPerLevel = 5;

        [Header("---- ĐỘ LỚN MỖI ĐIỂM CỘNG ----")]
        [Tooltip("1 điểm cộng vào HP = +bao nhiêu HP tối đa.")]
        public int hpPerPoint = 20;
        public int manaPerPoint = 10;
        public int staminaPerPoint = 5;
        public int adPerPoint = 2;
        public int apPerPoint = 2;
        public int defPerPoint = 1;
        public int resPerPoint = 1;
    }
}
