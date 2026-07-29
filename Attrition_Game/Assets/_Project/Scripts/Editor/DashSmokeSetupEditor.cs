#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Attrition.Gameplay.Player;

namespace Attrition.Editor
{
    /// <summary>
    /// Gắn `ShadowDashEffect` (khói dash + afterimage) vào prefab player và nạp sẵn 4 frame khói.
    ///
    /// VÌ SAO CẦN: khói/afterimage nằm trong `ShadowDashEffect`, nhưng component này CHƯA có trên
    /// prefab nào (đã kiểm: 0 tham chiếu tới guid script trong Player.prefab / Player 1.prefab) → dash
    /// hiện không ra hiệu ứng gì. Mảng `smokeFrames` lại là private [SerializeField] nên phải set qua
    /// SerializedObject.
    ///
    /// FRAME KHÓI: lấy 4 sprite `Free Smoke Fx  Pixel 04_0.._3` từ sheet đã slice (21x9, 27x8, 30x7,
    /// 20x6 — các cụm khói bốc ngang, đúng loại cho dash). Sheet 04 đã đúng chuẩn pixel art của player:
    /// PPU 16 + filterMode Point, khớp `_Idle.png` (PPU 16, Point) nên khói KHÔNG bị lệch tỉ lệ.
    /// (3 sheet 05/06/07 là PPU 100 + Bilinear → không dùng, sẽ nhỏ xíu và mờ.)
    ///
    /// KHÔNG dùng `Animations/Dash/DashSmoke.anim` + controller có sẵn: chúng cần thêm 1 GameObject có
    /// Animator cho mỗi cụm khói, mà `DashSmokeAnim` đổi 4 frame bằng tay là xong — ít asset hơn, và
    /// anim đó cũng chỉ đang được scene test Enemy_Axe_Demon dùng.
    ///
    /// Menu: Tools/Attrition/Player/Setup Dash Smoke
    /// Idempotent: chạy lại không thêm trùng component, chỉ ghi lại frame.
    /// </summary>
    public static class DashSmokeSetupEditor
    {
        private const string SmokeSheetPath =
            "Assets/_Project/Art/Characters/MainCharacter/Free Smoke Fx Pixel 2/Free Smoke Fx  Pixel 04.png";

        private static readonly string[] PlayerPrefabs =
        {
            "Assets/_Project/Prefabs/Player/Player.prefab",
            "Assets/_Project/Prefabs/Player/Player 1.prefab",
        };

        [MenuItem("Tools/Attrition/Player/Setup Dash Smoke")]
        public static void Setup()
        {
            var frames = LoadSmokeFrames();
            if (frames == null) return;

            int done = 0;
            foreach (var path in PlayerPrefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    Debug.LogWarning($"[DashSmoke] Không mở được prefab: {path} — bỏ qua.");
                    continue;
                }

                try
                {
                    var fx = root.GetComponent<ShadowDashEffect>();
                    if (fx == null) fx = root.AddComponent<ShadowDashEffect>();

                    // smokeFrames + sourceSprite đều private [SerializeField] → set qua SerializedObject.
                    var so = new SerializedObject(fx);

                    var arr = so.FindProperty("smokeFrames");
                    if (arr != null)
                    {
                        arr.arraySize = frames.Length;
                        for (int i = 0; i < frames.Length; i++)
                            arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                    }

                    // Gán luôn SpriteRenderer con ("Visual") để khói/ghost lấy đúng sorting layer.
                    var srcProp = so.FindProperty("sourceSprite");
                    if (srcProp != null && srcProp.objectReferenceValue == null)
                    {
                        var sr = root.GetComponentInChildren<SpriteRenderer>();
                        if (sr != null) srcProp.objectReferenceValue = sr;
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                    Debug.Log($"[DashSmoke] {System.IO.Path.GetFileName(path)}: ShadowDashEffect + {frames.Length} frame khói.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DashSmoke] Xong {done}/{PlayerPrefabs.Length} prefab.\n" +
                      "• Khói LUÔN hiện khi dash (không cần mở khoá shadow dash).\n" +
                      "• Chỉnh trong Inspector nếu khói lệch chân: smokeOffset (mặc định -0.35, -1.15) / smokeScale.\n" +
                      "• Player đặt sẵn trong scene: mở scene rồi SAVE để nhận component mới.");
        }

        /// <summary>
        /// Lấy 4 sprite `..._0.._3` từ sheet. Sheet auto-slice ra 195 sprite (phần lớn là rác nhiễu),
        /// nên phải lọc THEO TÊN chứ không lấy 4 cái đầu theo thứ tự AssetDatabase.
        /// </summary>
        private static Sprite[] LoadSmokeFrames()
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(SmokeSheetPath);
            if (all == null || all.Length == 0)
            {
                Debug.LogError($"[DashSmoke] Không thấy sheet khói: {SmokeSheetPath}");
                return null;
            }

            var byName = new Dictionary<string, Sprite>();
            foreach (var o in all)
                if (o is Sprite s) byName[s.name] = s;

            var frames = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                string want = $"Free Smoke Fx  Pixel 04_{i}";
                if (!byName.TryGetValue(want, out var s))
                {
                    Debug.LogError($"[DashSmoke] Sheet thiếu sprite '{want}'. " +
                                   "Kiểm tra lại slice của Free Smoke Fx  Pixel 04.png.");
                    return null;
                }
                frames[i] = s;
            }
            return frames;
        }
    }
}
#endif
