using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Attrition.Gameplay.Enemy;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool dựng RIG TEST BOSS trong scene Enemy_Axe_Demon: thêm 1 object "BossTestRig" mang
    /// <see cref="BossTestSpawner"/> đã gán sẵn 4 prefab boss (Druid/Elf/DemonKin/ArchDemon) + điểm spawn
    /// đặt cách chỗ player đứng vài mét.
    ///
    /// Menu: Tools/Attrition/Enemy/Setup Boss Test Rig (Enemy_Axe_Demon)
    /// Dùng: Play scene này rồi bấm 2/3/4/5 để gọi boss map 2/3/4/5, bấm 0 để despawn.
    /// Idempotent: chạy lại chỉ gán lại prefab, không tạo trùng object.
    /// </summary>
    public static class BossTestRigSetupEditor
    {
        private const string RigName = "BossTestRig";
        private const string ArenaName = "BossTestArena";
        private const string PrefabDir = "Assets/_Project/Prefabs/Enemy";
        private const float FloorY = 47f;

        [MenuItem("Tools/Attrition/Enemy/Setup Boss Test Rig (Enemy_Axe_Demon)")]
        public static void SetupRig()
        {
            var rig = GameObject.Find(RigName);
            if (rig == null)
            {
                rig = new GameObject(RigName);
                Undo.RegisterCreatedObjectUndo(rig, "Create Boss Test Rig");
            }

            var spawner = rig.GetComponent<BossTestSpawner>() ?? rig.AddComponent<BossTestSpawner>();
            var bossPoint = EnsureArena(out var playerPoints);
            rig.transform.position = bossPoint.position;

            var sceneSpawner = Object.FindFirstObjectByType<NetworkSpawner>();
            if (sceneSpawner == null)
            {
                Debug.LogError("[BossTest] Scene không có NetworkSpawner — không thể gán điểm spawn player.");
                return;
            }

            var sceneSpawnerSo = new SerializedObject(sceneSpawner);
            var spawnPoints = sceneSpawnerSo.FindProperty("spawnPoints");
            spawnPoints.arraySize = playerPoints.Length;
            for (int i = 0; i < playerPoints.Length; i++)
                spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue = playerPoints[i];
            sceneSpawnerSo.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(spawner);
            so.FindProperty("spawnPoint").objectReferenceValue = bossPoint;
            var safePoints = so.FindProperty("safePlayerPoints");
            safePoints.arraySize = playerPoints.Length;
            for (int i = 0; i < playerPoints.Length; i++)
                safePoints.GetArrayElementAtIndex(i).objectReferenceValue = playerPoints[i];
            so.FindProperty("rescueBelowY").floatValue = FloorY - 2f;
            int ok = 0;
            ok += Assign(so, "druidPrefab", "Druid") ? 1 : 0;
            ok += Assign(so, "elfPrefab", "Elf") ? 1 : 0;
            ok += Assign(so, "demonKinPrefab", "DemonKin") ? 1 : 0;
            ok += Assign(so, "archDemonPrefab", "ArchDemon") ? 1 : 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(rig.scene);
            Selection.activeGameObject = rig;
            EditorGUIUtility.PingObject(rig);

            Debug.Log($"[BossTest] Đã dựng arena kín + '{RigName}' với {ok}/4 boss. " +
                      "Play scene rồi bấm 2=Druid, 3=Elf, 4=DemonKin, 5=ArchDemon, 0=despawn. SAVE scene.");
        }

        private static Transform EnsureArena(out Transform[] playerPoints)
        {
            var arena = GameObject.Find(ArenaName) ?? new GameObject(ArenaName);
            int ground = LayerMask.NameToLayer("Ground");

            EnsureWall(arena.transform, "Floor", new Vector3(0f, FloorY, 0f), new Vector2(36f, 1f), ground);
            EnsureWall(arena.transform, "LeftWall", new Vector3(-18f, FloorY + 6f, 0f), new Vector2(1f, 13f), ground);
            EnsureWall(arena.transform, "RightWall", new Vector3(18f, FloorY + 6f, 0f), new Vector2(1f, 13f), ground);

            playerPoints = new[]
            {
                EnsurePoint(arena.transform, "PlayerSpawn_1", new Vector3(-8f, FloorY + 1.5f, 0f)),
                EnsurePoint(arena.transform, "PlayerSpawn_2", new Vector3(-5f, FloorY + 1.5f, 0f)),
            };
            return EnsurePoint(arena.transform, "BossSpawn", new Vector3(7f, FloorY + 1.5f, 0f));
        }

        private static void EnsureWall(Transform parent, string name, Vector3 position, Vector2 size, int layer)
        {
            var child = parent.Find(name);
            var go = child != null ? child.gameObject : new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;
            if (layer >= 0) go.layer = layer;

            var col = go.GetComponent<BoxCollider2D>() ?? go.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = size;
        }

        private static Transform EnsurePoint(Transform parent, string name, Vector3 position)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent);
                child = go.transform;
            }
            child.position = position;
            return child;
        }

        /// <summary>Gán NetworkObject trên prefab qua object reference chuẩn của Unity.</summary>
        private static bool Assign(SerializedObject so, string field, string prefabName)
        {
            string path = $"{PrefabDir}/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var networkObject = prefab != null ? prefab.GetComponent<Fusion.NetworkObject>() : null;
            if (networkObject == null)
            {
                Debug.LogWarning($"[BossTest] Không thấy NetworkObject trên prefab {path} → bỏ qua {field}.");
                return false;
            }

            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[BossTest] Không thấy field {field} trên BossTestSpawner.");
                return false;
            }

            prop.objectReferenceValue = networkObject;
            return true;
        }
    }
}
