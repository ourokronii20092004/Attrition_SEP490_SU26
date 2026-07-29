#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Attrition.Editor
{
    /// <summary>
    /// Sinh TMP Font Asset từ `boldpixels.ttf` rồi đặt làm FONT MẶC ĐỊNH của TextMeshPro.
    ///
    /// VÌ SAO CẦN: UI Toolkit (GameUI.uss / MainMenuUI.uss) dùng trực tiếp file .ttf được rồi,
    /// nhưng các UI dựng RUNTIME bằng code lại dùng TextMeshPro — mà TMP KHÔNG đọc .ttf, nó cần
    /// TMP_FontAsset (có atlas ký tự). Các script đó (WorldMapController, TutorialPanel,
    /// TeamCreditsPanel, AreaNameBanner, WorldNameLabel...) KHÔNG script nào tự gán font, nên tất
    /// cả đang lấy `TMP_Settings.defaultFontAsset`. Đổi font mặc định = đổi hết một lượt, không
    /// phải sửa từng script.
    ///
    /// Atlas: 512x512, Point filter (giữ nét pixel), sampling 16px = đúng cỡ gốc của BoldPixels
    /// (font pixel phóng to bằng nội suy sẽ bị nhoè).
    ///
    /// Menu: Tools/Attrition/UI/Create BoldPixels TMP Font (+ set default)
    /// Chạy lại an toàn: có sẵn thì chỉ set lại default.
    /// </summary>
    public static class PixelFontSetupEditor
    {
        private const string TtfPath = "Assets/_Project/Art/UI_Elements/Fontstyle/boldpixels.ttf";
        private const string OutDir = "Assets/_Project/UI/Fonts";
        private const string OutPath = OutDir + "/BoldPixels SDF.asset";

        [MenuItem("Tools/Attrition/UI/Create BoldPixels TMP Font (+ set default)")]
        public static void Create()
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            if (ttf == null)
            {
                Debug.LogError($"[PixelFont] Không tìm thấy font: {TtfPath}");
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutPath);
            TMP_FontAsset fontAsset = existing;

            if (fontAsset == null)
            {
                if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

                // Bitmap (không SDF) + Point filter: font pixel nhìn nét đúng chất retro.
                // 16 = cỡ gốc BoldPixels; render mode RASTER giữ pixel vuông thay vì làm mềm cạnh.
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    ttf,
                    samplingPointSize: 16,
                    atlasPadding: 1,
                    renderMode: UnityEngine.TextCore.LowLevel.GlyphRenderMode.RASTER,
                    atlasWidth: 512,
                    atlasHeight: 512,
                    atlasPopulationMode: AtlasPopulationMode.Dynamic,
                    enableMultiAtlasSupport: true);

                if (fontAsset == null)
                {
                    Debug.LogError("[PixelFont] CreateFontAsset trả null — kiểm tra file .ttf có hợp lệ.");
                    return;
                }

                fontAsset.name = "BoldPixels SDF";
                AssetDatabase.CreateAsset(fontAsset, OutPath);

                // Atlas texture là sub-asset của font asset → phải thêm vào cùng file.
                if (fontAsset.atlasTextures != null)
                {
                    foreach (var tex in fontAsset.atlasTextures)
                    {
                        if (tex == null) continue;
                        tex.filterMode = FilterMode.Point;   // giữ nét pixel
                        if (!AssetDatabase.Contains(tex)) AssetDatabase.AddObjectToAsset(tex, fontAsset);
                    }
                }
                if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

                EditorUtility.SetDirty(fontAsset);
                Debug.Log($"[PixelFont] Đã tạo {OutPath}");
            }
            else
            {
                Debug.Log($"[PixelFont] {OutPath} đã có — chỉ set lại default.");
            }

            SetAsTmpDefault(fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Ghi fontAsset vào `m_defaultFontAsset` của TMP Settings. Mọi TextMeshPro/TextMeshProUGUI
        /// tạo runtime mà không gán font sẽ tự dùng cái này.
        /// </summary>
        private static void SetAsTmpDefault(TMP_FontAsset fontAsset)
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogWarning("[PixelFont] Không tìm thấy TMP Settings — bỏ qua bước set default. " +
                                 "Window > TextMeshPro > Project Files GUID Remapping Tool để tạo nếu thiếu.");
                return;
            }

            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null)
            {
                Debug.LogWarning("[PixelFont] TMP Settings không có field 'm_defaultFontAsset'.");
                return;
            }

            prop.objectReferenceValue = fontAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);

            Debug.Log("[PixelFont] Đã đặt BoldPixels làm font mặc định của TextMeshPro → " +
                      "World Map, Tutorial, Credits, Area banner, nhãn tên quái/NPC đều dùng font pixel. " +
                      "LƯU Ý: các TMP đã gán font TAY trong scene/prefab sẽ KHÔNG đổi theo.");
        }
    }
}
#endif
