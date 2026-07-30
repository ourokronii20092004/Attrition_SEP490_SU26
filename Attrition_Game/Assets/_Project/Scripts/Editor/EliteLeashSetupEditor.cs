#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Attrition.Editor
{
    /// <summary>
    /// Bật LEASH (giới hạn đuổi theo trong phòng) cho toàn bộ prefab quái ELITE.
    ///
    /// VÌ SAO CẦN: user báo "player ra khỏi phòng thì elite vẫn đuổi theo". `EnemyAI` trước đây chỉ dựa vào
    /// `viewRadius`, không có khái niệm ranh giới phòng. Nay có `leashToRoom` (đọc vùng `CameraBoundsZone`
    /// của căn phòng) nhưng mặc định TẮT để quái thường giữ nguyên hành vi cũ — nên phải bật riêng cho elite.
    ///
    /// 6 elite lấy từ `Data/Enemies/*.asset` có `tier: 1` (Elite): Crab, Cultist, Frogger, Gollux,
    /// NightBorne, Undead.
    ///
    /// Menu: Tools/Attrition/Enemy/Setup Elite Leash (thoi duoi khi player ra khoi phong)
    /// Idempotent: chạy lại không đổi gì nếu đã bật.
    /// </summary>
    public static class EliteLeashSetupEditor
    {
        private const string PrefabDir = "Assets/_Project/Prefabs/Enemy";

        /// <summary>Prefab của quái tier Elite (khớp tier:1 trong Data/Enemies).</summary>
        private static readonly string[] ElitePrefabs =
        {
            "Crab", "Cultist", "Frogger", "Gollux", "NightBorne", "Undead",
        };

        /// <summary>Nới ngoài mép phòng bao nhiêu unit mới nhả target — chống nhả/bắt liên tục ở mép cửa.</summary>
        private const float LeashPadding = 1.5f;

        [MenuItem("Tools/Attrition/Enemy/Setup Elite Leash (thoi duoi khi player ra khoi phong)")]
        public static void Setup()
        {
            int done = 0, skipped = 0;

            foreach (var name in ElitePrefabs)
            {
                string path = $"{PrefabDir}/{name}.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    Debug.LogWarning($"[EliteLeash] Không mở được prefab: {path}");
                    skipped++;
                    continue;
                }

                try
                {
                    // EnemyAI ở global namespace (không nằm trong Attrition.*) — xem EnemyAI.cs.
                    var ai = root.GetComponent<EnemyAI>();
                    if (ai == null)
                    {
                        Debug.LogWarning($"[EliteLeash] {name}: không có EnemyAI → bỏ qua.");
                        skipped++;
                        continue;
                    }

                    // leashToRoom/leashPadding là public field nên set trực tiếp được, nhưng vẫn qua
                    // SerializedObject cho nhất quán với các tool khác (và để Undo/dirty đúng cách).
                    var so = new SerializedObject(ai);
                    var leash = so.FindProperty("leashToRoom");
                    var pad = so.FindProperty("leashPadding");

                    bool changed = false;
                    if (leash != null && !leash.boolValue) { leash.boolValue = true; changed = true; }
                    if (pad != null && !Mathf.Approximately(pad.floatValue, LeashPadding))
                    {
                        pad.floatValue = LeashPadding;
                        changed = true;
                    }

                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        Debug.Log($"[EliteLeash] {name}: leashToRoom = true, padding = {LeashPadding}.");
                    }
                    done++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EliteLeash] Xong {done}/{ElitePrefabs.Length} elite" +
                      (skipped > 0 ? $" ({skipped} bỏ qua)" : "") + ".\n" +
                      "YÊU CẦU: phòng của elite phải có CameraBoundsZone (collider phủ phòng) — leash đọc\n" +
                      "vùng đó để biết ranh giới. Phòng CHƯA đặt zone thì elite đuổi như cũ (không vỡ gì).\n" +
                      "Elite đã đặt sẵn trong scene: mở scene rồi SAVE để nhận giá trị mới.");
        }
    }
}
#endif
