#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Attrition.Editor
{
    /// <summary>
    /// Set import settings hàng loạt cho icon pixel-art 16×16 trong UI_Elements:
    ///   - Texture Type = Sprite (2D and UI)
    ///   - Filter Mode = Point (giữ nét pixel, không mờ)
    ///   - Compression = None (không vỡ màu)
    ///   - Pixels Per Unit = 16, Mip = off
    /// Menu: Attrition → Fix Icon Import (16x16).
    /// </summary>
    public static class IconImportFixer
    {
        private const string IconDir = "Assets/_Project/Art/UI_Elements/16x16";

        [MenuItem("Attrition/Fix Icon Import (16x16)")]
        public static void FixAll()
        {
            if (!Directory.Exists(IconDir))
            {
                Debug.LogError($"[IconImportFixer] Không thấy thư mục: {IconDir}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { IconDir });
            int changed = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null) continue;

                    bool dirty = false;
                    if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
                    if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                    if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; dirty = true; }
                    if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                    if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
                    if (ti.spritePixelsPerUnit != 16f) { ti.spritePixelsPerUnit = 16f; dirty = true; }

                    var settings = new TextureImporterSettings();
                    ti.ReadTextureSettings(settings);
                    if (settings.spriteMeshType != SpriteMeshType.FullRect)
                    {
                        settings.spriteMeshType = SpriteMeshType.FullRect;
                        ti.SetTextureSettings(settings);
                        dirty = true;
                    }

                    if (dirty)
                    {
                        EditorUtility.SetDirty(ti);
                        ti.SaveAndReimport();
                        changed++;
                    }

                    if (i % 100 == 0)
                        EditorUtility.DisplayProgressBar("Fix Icon Import", $"{i}/{guids.Length}", (float)i / guids.Length);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[IconImportFixer] Xong. Quét {guids.Length} texture, sửa {changed} file.");
        }
    }
}
#endif
