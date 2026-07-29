#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Dựng CỬA NỐI HAI CHIỀU cho TOÀN CHUỖI map: Map1 ↔ Map2 ↔ Map3 ↔ Map4 ↔ Map5.
    /// Tổng quát hoá `Map1Map2LinkSetupEditor` (tool cũ chỉ làm 1 cặp + hardcode toạ độ) — mọi cặp map
    /// giờ dùng CÙNG một quy tắc và đọc vị trí từ scene. Tool cũ vẫn còn nhưng KHÔNG cần dùng nữa;
    /// tool này bao trùm cả cặp Map1↔Map2 và idempotent nên chạy đè lên kết quả cũ vẫn an toàn.
    ///
    /// Với mỗi map N trong chuỗi:
    ///  - Có map TRƯỚC  → tạo `SceneTransitionZone_BackToMap{N-1}` (startActive = TRUE: quay về map cũ
    ///    không cần điều kiện) + `SceneEntryPoint` id "from_map{N-1}" (chỗ hiện ra khi ĐI TỚI map này).
    ///  - Có map SAU    → gán `entryPointId = "from_map{N}"` cho zone đi-tiếp có sẵn + tạo
    ///    `SceneEntryPoint` id "from_map{N+1}" (chỗ hiện ra khi QUAY VỀ map này).
    ///
    /// Quy ước ID: "from_mapX" = điểm player xuất hiện TRONG scene này khi đến từ map X.
    ///
    /// Vị trí lấy TỪ SCENE (không hardcode) để tự khớp khi designer dịch chuyển object:
    ///  - Cửa về + điểm vào-từ-map-trước: quanh `Player_SpawnPoint` (rìa vào map).
    ///  - Điểm vào-từ-map-sau: LÙI VÀO TRONG 8 units so với zone đi-tiếp, để player quay về không
    ///    đứng đè lên zone rồi bị hút đi lại ngay.
    ///
    /// Menu: Tools/Attrition/Scene Link/... (một scene, hoặc TẤT CẢ).
    /// Idempotent. Sau khi chạy: kiểm tra vị trí trong Scene view rồi SAVE (Fusion bake NetworkObject).
    /// </summary>
    public static class SceneLinkChainSetupEditor
    {
        /// <summary>Chuỗi map theo đúng thứ tự tiến trình. Thêm map mới = thêm vào cuối.</summary>
        private static readonly string[] Chain =
        {
            "The Darkest Path - Map 1",
            "Forest - Map 2",
            "Elf Valley -Map 3",
            "Dark Forest - Map 4",
            "Castle - Map 5",
        };

        private const string SpawnPointName = "Player_SpawnPoint";

        /// <summary>Khoảng lùi vào trong map của điểm vào phía cửa đi-tiếp (tránh đè zone).</summary>
        private const float InwardOffset = 8f;

        private static string EntryId(int mapNumber) => $"from_map{mapNumber}";

        [MenuItem("Tools/Attrition/Scene Link/Setup Back Doors (current scene)")]
        public static void SetupCurrent()
        {
            var scene = SceneManager.GetActiveScene();
            int idx = System.Array.IndexOf(Chain, scene.name);
            if (idx < 0)
            {
                EditorUtility.DisplayDialog("Scene Link",
                    $"Scene '{scene.name}' không nằm trong chuỗi map.\n\nMở một trong:\n- "
                    + string.Join("\n- ", Chain), "OK");
                return;
            }

            var log = new List<string>();
            SetupScene(idx, log);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[SceneLink] '{scene.name}':\n  " + string.Join("\n  ", log)
                      + "\nSAVE scene để Fusion bake NetworkObject của zone mới.");
        }

        [MenuItem("Tools/Attrition/Scene Link/Setup Back Doors (ALL maps)")]
        public static void SetupAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string original = SceneManager.GetActiveScene().path;

            for (int i = 0; i < Chain.Length; i++)
            {
                string path = $"Assets/_Project/Scenes/{Chain[i]}.unity";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning($"[SceneLink] Không thấy scene: {path} — bỏ qua.");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var log = new List<string>();
                SetupScene(i, log);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);   // lưu ngay, tránh mất khi mở scene kế
                Debug.Log($"[SceneLink] {Chain[i]}:\n  " + string.Join("\n  ", log) + "\n  → đã lưu.");
            }

            if (!string.IsNullOrEmpty(original) && System.IO.File.Exists(original))
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);

            Debug.Log("[SceneLink] XONG toàn chuỗi. Kiểm tra vị trí cửa/điểm vào trong từng scene "
                      + "(phải nằm trên mặt đất đứng được).");
        }

        //  CORE

        private static void SetupScene(int idx, List<string> log)
        {
            int mapNo = idx + 1;
            bool hasPrev = idx > 0;
            bool hasNext = idx < Chain.Length - 1;

            Vector3 spawnPos = FindSpawnPos(log);
            var forwardZone = hasNext ? FindZoneTo(Chain[idx + 1]) : null;

            // ── Phía VÀO map (có map trước) ──
            if (hasPrev)
            {
                // Cửa quay về map trước — mở sẵn (không cần đánh boss).
                EnsureBackZone(Chain[idx - 1], EntryId(mapNo), spawnPos + new Vector3(-3f, 0f, 0f), log);

                // Chỗ player hiện ra khi ĐI TỚI map này từ map trước (lệch phải để không đè cửa về).
                EnsureEntryPoint(EntryId(mapNo - 1), spawnPos + new Vector3(2f, 0f, 0f), log);
            }

            // ── Phía RA map (có map sau) ──
            if (hasNext)
            {
                if (forwardZone != null)
                {
                    SetZoneEntryId(forwardZone, EntryId(mapNo), log);

                    // Chỗ player hiện ra khi QUAY VỀ map này từ map sau — LÙI VÀO TRONG để không bị
                    // hút sang map sau ngay lập tức.
                    EnsureEntryPoint(EntryId(mapNo + 1),
                        forwardZone.transform.position + new Vector3(-InwardOffset, 0f, 0f), log);
                }
                else
                {
                    log.Add($"⚠ KHÔNG thấy zone đi tới '{Chain[idx + 1]}' → chưa gán entryPointId và chưa "
                            + $"tạo điểm vào '{EntryId(mapNo + 1)}'. Tạo zone đi-tiếp trước rồi chạy lại.");
                }
            }
        }

        //  TÌM / TẠO

        private static Vector3 FindSpawnPos(List<string> log)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name == SpawnPointName) return t.position;
            }
            log.Add($"⚠ Không thấy '{SpawnPointName}' → dùng gốc (0,0). PHẢI chỉnh vị trí cửa về bằng tay.");
            return Vector3.zero;
        }

        /// <summary>Tìm zone có nextSceneName == tên scene đích.</summary>
        private static RoomTransitionZone FindZoneTo(string sceneName)
        {
            foreach (var z in Object.FindObjectsByType<RoomTransitionZone>(FindObjectsSortMode.None))
            {
                if (z == null) continue;
                var so = new SerializedObject(z);
                var p = so.FindProperty("nextSceneName");
                if (p != null && p.stringValue == sceneName) return z;
            }
            return null;
        }

        private static void SetZoneEntryId(RoomTransitionZone zone, string entryId, List<string> log)
        {
            var so = new SerializedObject(zone);
            var p = so.FindProperty("entryPointId");
            if (p == null) { log.Add("⚠ RoomTransitionZone thiếu field 'entryPointId'."); return; }

            if (p.stringValue == entryId) { log.Add($"zone đi-tiếp: entryPointId đã là '{entryId}'."); return; }

            p.stringValue = entryId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(zone);
            log.Add($"zone đi-tiếp: gán entryPointId = '{entryId}'.");
        }

        /// <summary>Tạo cửa quay về map trước. Idempotent theo nextSceneName.</summary>
        private static void EnsureBackZone(string prevScene, string entryId, Vector3 pos, List<string> log)
        {
            var existing = FindZoneTo(prevScene);
            if (existing != null)
            {
                // Đã có → chỉ bảo đảm entryPointId + startActive đúng, KHÔNG dịch vị trí (designer có
                // thể đã chỉnh tay).
                SetZoneEntryId(existing, entryId, log);
                var so = new SerializedObject(existing);
                var sa = so.FindProperty("startActive");
                if (sa != null && !sa.boolValue)
                {
                    sa.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(existing);
                    log.Add("cửa về: bật startActive = true.");
                }
                log.Add($"cửa về '{prevScene}' đã có — giữ vị trí hiện tại.");
                return;
            }

            var go = new GameObject($"SceneTransitionZone_BackTo_{Sanitize(prevScene)}");
            Undo.RegisterCreatedObjectUndo(go, "Create Back Transition Zone");
            go.transform.position = pos;

            go.AddComponent<NetworkObject>();

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2f, 4f);
            col.isTrigger = true;

            var zone = go.AddComponent<RoomTransitionZone>();
            var zso = new SerializedObject(zone);
            zso.FindProperty("nextSceneName").stringValue = prevScene;
            zso.FindProperty("entryPointId").stringValue = entryId;
            zso.FindProperty("startActive").boolValue = true;   // về map cũ: mở sẵn
            zso.ApplyModifiedPropertiesWithoutUndo();

            log.Add($"TẠO cửa về '{prevScene}' tại {pos} (entryPointId='{entryId}', startActive=true).");
        }

        /// <summary>Tạo SceneEntryPoint theo id. Idempotent — đã có thì giữ nguyên vị trí.</summary>
        private static void EnsureEntryPoint(string id, Vector3 pos, List<string> log)
        {
            foreach (var ep in Object.FindObjectsByType<SceneEntryPoint>(FindObjectsSortMode.None))
            {
                if (ep != null && ep.EntryId == id)
                {
                    log.Add($"điểm vào '{id}' đã có — giữ vị trí hiện tại.");
                    return;
                }
            }

            var go = new GameObject($"SceneEntryPoint_{id}");
            Undo.RegisterCreatedObjectUndo(go, "Create Scene Entry Point");
            go.transform.position = pos;

            var comp = go.AddComponent<SceneEntryPoint>();
            var so = new SerializedObject(comp);
            so.FindProperty("entryId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();

            log.Add($"TẠO điểm vào '{id}' tại {pos}.");
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }
    }
}
#endif
