#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Attrition.Gameplay.Player;

namespace Attrition.Editor
{
    /// <summary>
    /// Gắn `PlayerVfx` vào prefab player và nạp sẵn frame hiệu ứng HỒI MÁU + LÊN CẤP.
    ///
    /// VÌ SAO CẦN TOOL: `healFrames`/`levelUpFrames` là private [SerializeField] nên phải set qua
    /// SerializedObject; và sheet phải lọc/sắp đúng thứ tự frame (xem ghi chú ở LoadFrames).
    ///
    /// Menu: Tools/Attrition/Player/Setup Heal + LevelUp VFX
    /// Idempotent: chạy lại không thêm trùng component, chỉ ghi lại frame.
    /// </summary>
    public static class PlayerVfxSetupEditor
    {
        private const string ArtDir = "Assets/_Project/Art/Characters/MainCharacter";
        private const string HealSheet = ArtDir + "/Heal Effect Sprite Sheet.png";
        private const string LevelUpSheet = ArtDir + "/Level Up.png";

        private static readonly string[] PlayerPrefabs =
        {
            "Assets/_Project/Prefabs/Player/Player.prefab",
            "Assets/_Project/Prefabs/Player/Player 1.prefab",
        };

        [MenuItem("Tools/Attrition/Player/Setup Heal + LevelUp VFX")]
        public static void Setup()
        {
            var heal = LoadFrames(HealSheet);
            var levelUp = LoadFrames(LevelUpSheet);

            if (heal.Count == 0) Debug.LogWarning($"[PlayerVfx] Không đọc được sprite nào từ {HealSheet}");
            if (levelUp.Count == 0) Debug.LogWarning($"[PlayerVfx] Không đọc được sprite nào từ {LevelUpSheet}");

            int done = 0;
            foreach (var path in PlayerPrefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    Debug.LogWarning($"[PlayerVfx] Không mở được prefab: {path}");
                    continue;
                }

                try
                {
                    var vfx = root.GetComponent<PlayerVfx>() ?? root.AddComponent<PlayerVfx>();

                    var so = new SerializedObject(vfx);
                    SetSpriteArray(so, "healFrames", heal);
                    SetSpriteArray(so, "levelUpFrames", levelUp);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                    Debug.Log($"[PlayerVfx] {System.IO.Path.GetFileName(path)}: PlayerVfx + "
                              + $"{heal.Count} frame heal, {levelUp.Count} frame level-up.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PlayerVfx] Xong {done}/{PlayerPrefabs.Length} prefab.\n" +
                      "• Hiệu ứng hiện GIỮA người, chạy hết frame rồi biến mất ngay.\n" +
                      "• Hồi máu: kích từ PlayerStats.RestoreHP (bình máu, regen, lifesteal, rest).\n" +
                      "  Có chặn spam 0.4s vì Lifesteal gọi RestoreHP mỗi lần đánh trúng.\n" +
                      "• Lên cấp: kích từ PlayerProgression.OnLevelUp.\n" +
                      "• Chỉnh offset/scale trong Inspector nếu hiệu ứng lệch so với người.\n" +
                      "• Player đặt sẵn trong scene: mở scene rồi SAVE để nhận component mới.");
        }

        /// <summary>
        /// Đọc mọi Sprite trong sheet và sắp theo THỨ TỰ FRAME.
        ///
        /// ⚠ HAI BẪY ở 2 sheet này:
        ///  1. `Heal Effect Sprite Sheet` có hậu tố KHÔNG LIÊN TỤC (_0.._5, _7, _9, _11, _12, _14..._17) →
        ///     phải sort theo SỐ, không theo thứ tự chuỗi (nếu không "_11" đứng trước "_2").
        ///  2. `Level Up` có tên LẪN LỘN: 1 sprite tên "Effect 1 - Sprite Sheet_0" nằm cùng "Level Up_0.._2",
        ///     tức có HAI sprite cùng index 0 → sort theo tên/index là sai thứ tự.
        ///     Các frame nằm xếp DỌC (y giảm dần: 133 → 99 → 67 → 33) và to dần (23→27→32→34 px), nên sắp
        ///     theo Y GIẢM DẦN mới ra đúng chuỗi phóng to của hiệu ứng.
        ///
        /// Vì vậy: sắp theo `rect.y` giảm dần (trên → dưới), lấy `rect.x` rồi index làm tiêu chí phụ.
        /// Cách này đúng cho cả hai sheet mà không cần biết quy ước tên.
        /// </summary>
        private static List<Sprite> LoadFrames(string sheetPath)
        {
            var result = new List<Sprite>();
            var all = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
            if (all == null) return result;

            foreach (var o in all) if (o is Sprite s) result.Add(s);
            if (result.Count == 0) return result;

            result.Sort((a, b) =>
            {
                // Y giảm dần = từ trên xuống dưới trong ảnh (Unity rect.y tính từ ĐÁY ảnh).
                int cmp = b.rect.y.CompareTo(a.rect.y);
                if (cmp != 0) return cmp;
                cmp = a.rect.x.CompareTo(b.rect.x);      // cùng hàng → trái sang phải
                if (cmp != 0) return cmp;
                return IndexOf(a.name).CompareTo(IndexOf(b.name));
            });

            return result;
        }

        /// <summary>Số sau dấu '_' cuối tên sprite; không có thì coi là 0.</summary>
        private static int IndexOf(string name)
        {
            int u = name.LastIndexOf('_');
            if (u < 0 || u == name.Length - 1) return 0;
            return int.TryParse(name.Substring(u + 1), out int i) ? i : 0;
        }

        private static void SetSpriteArray(SerializedObject so, string field, List<Sprite> sprites)
        {
            var arr = so.FindProperty(field);
            if (arr == null)
            {
                Debug.LogWarning($"[PlayerVfx] Không thấy field '{field}'.");
                return;
            }
            arr.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }
}
#endif
