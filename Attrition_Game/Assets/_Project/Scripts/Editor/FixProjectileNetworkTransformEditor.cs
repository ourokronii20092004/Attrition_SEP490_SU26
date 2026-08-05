using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Fusion;
// EnemyProjectile / SpearProjectile nằm ở namespace TOÀN CỤC (không phải Attrition.Gameplay.Enemy),
// nên chỉ EnemyAoEDamage cần using dưới đây.
using Attrition.Gameplay.Enemy;

namespace Attrition.Editor
{
    /// <summary>
    /// Gắn <see cref="NetworkTransform"/> vào MỌI prefab đạn/nổ có logic networked nhưng THIẾU nó.
    ///
    /// LỖI ĐANG SỬA: bên client không thấy skill boss bay ra, nhưng vẫn ăn damage.
    /// Vì sao: EnemyProjectile.FixedUpdateNetwork (và SpearProjectile) dời vật bằng
    /// `transform.Translate/position +=` sau guard `if (!HasStateAuthority) return;` — tức CHỈ HOST
    /// dời. Vị trí chỉ đồng bộ xuống client khi có NetworkTransform. Không có nó thì:
    ///   - HOST: đạn bay đúng, CircleCast trúng player của client → client MẤT MÁU thật.
    ///   - CLIENT: object nằm im ở vị trí gốc prefab (đa số là {0,0,0}, ngoài màn hình) → KHÔNG THẤY GÌ.
    /// Đúng triệu chứng: "không thấy bay ra nhưng vẫn ăn damage".
    ///
    /// EnemyAoEDamage cũng vậy: SnapToGround() chỉ chạy ở host, và chính comment trong file đó đã ghi
    /// "NetworkTransform sync xuống client" — nhưng thực tế chỉ FireExplosion có NT.
    ///
    /// Đây KHÔNG phải fix mới: commit 1f83a85d ("fix: correct boss skill and camera zoom") đã sửa đúng
    /// cách này cho boss Map 1 (FireBall/Firebolt_SeveredFang) bằng cách thêm NetworkTransform, nhưng
    /// chưa áp cho các boss còn lại. Tool này áp nốt.
    ///
    /// Menu: Tools/Attrition/Fix Projectile NetworkTransform
    /// </summary>
    public static class FixProjectileNetworkTransformEditor
    {
        [MenuItem("Tools/Attrition/Fix Projectile NetworkTransform")]
        public static void Fix()
        {
            var fixedPaths = new List<string>();
            var alreadyOk = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/_Project/", System.StringComparison.Ordinal)) continue;

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !NeedsNetworkTransform(asset)) continue;

                // NetworkRigidbody2D không xét: không prefab đạn/nổ nào dùng (chúng tự dời bằng
                // transform, không qua physics), và type đó nằm ở assembly Editor asmdef không tham chiếu.
                if (asset.GetComponent<NetworkTransform>() != null)
                {
                    alreadyOk.Add(System.IO.Path.GetFileName(path));
                    continue;
                }

                var go = PrefabUtility.LoadPrefabContents(path);
                if (go == null) { Debug.LogError($"[Attrition] Không mở được prefab: {path}"); continue; }

                go.AddComponent<NetworkTransform>();
                PrefabUtility.SaveAsPrefabAsset(go, path);
                PrefabUtility.UnloadPrefabContents(go);
                fixedPaths.Add(System.IO.Path.GetFileName(path));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Attrition] NetworkTransform: THÊM cho {fixedPaths.Count} prefab"
                      + (fixedPaths.Count > 0 ? " (" + string.Join(", ", fixedPaths) + ")" : "")
                      + $"; đã có sẵn: {alreadyOk.Count}.");
        }

        /// <summary>
        /// Prefab có logic networked TỰ DỜI CHỖ hoặc cần đúng vị trí ở client. Chỉ xét component trên
        /// root: Runner.Spawn tạo root, các script này đều nằm ở root (xem ProjectileInitializer).
        /// </summary>
        private static bool NeedsNetworkTransform(GameObject go)
            => go.GetComponent<NetworkObject>() != null
               && (go.GetComponent<EnemyProjectile>() != null
                   || go.GetComponent<SpearProjectile>() != null
                   || go.GetComponent<EnemyAoEDamage>() != null);
    }
}
