using UnityEditor;
using UnityEngine;
using Fusion;
using Attrition.Gameplay.World;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo "cổng sau Boss → sang Map kế tiếp": 1 Door (đóng) + 1 RoomTransitionZone (tắt,
    /// load scene Map 2) + 1 BossGateController nối chúng lại. Đánh boss xong → cửa mở + vùng bật.
    /// Menu: Tools/Attrition/Create Boss Exit Door + Scene Transition
    /// Sau khi chạy: KÉO EnemyController của Boss vào ô 'boss' của BossGateController,
    /// chỉnh 'nextSceneName' (mặc định 'Forest - Map 2'), đặt vị trí cửa/vùng, gán sprite.
    /// </summary>
    public static class BossExitDoorSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Boss Exit Door + Scene Transition")]
        public static void CreateBossExit()
        {
            var root = new GameObject("BossExitGate");
            Undo.RegisterCreatedObjectUndo(root, "Create Boss Exit Gate");

            var sv = SceneView.lastActiveSceneView;
            if (sv != null) root.transform.position = sv.pivot;

            // ── Exit Door (đóng) ──
            var doorGo = new GameObject("ExitDoor");
            doorGo.transform.SetParent(root.transform);
            doorGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            doorGo.AddComponent<NetworkObject>();
            var blockingCol = doorGo.AddComponent<BoxCollider2D>();
            blockingCol.size = new Vector2(1f, 3f);
            blockingCol.isTrigger = false;
            var doorVisual = CreateVisualChild(doorGo, "DoorVisual", new Vector2(1f, 3f), new Color(0.35f, 0.2f, 0.1f));
            var door = doorGo.AddComponent<Door>();
            SetPrivate(door, "blockingCollider", blockingCol);
            SetPrivate(door, "doorVisual", doorVisual);
            SetPrivate(door, "startOpen", false);

            // ── Transition Zone (tắt) — đặt ngay sau cửa ──
            var zoneGo = new GameObject("SceneTransitionZone");
            zoneGo.transform.SetParent(root.transform);
            zoneGo.transform.localPosition = new Vector3(2f, 1.5f, 0f);
            zoneGo.AddComponent<NetworkObject>();
            var zoneCol = zoneGo.AddComponent<BoxCollider2D>();
            zoneCol.size = new Vector2(2f, 3f);
            zoneCol.isTrigger = true;
            var zone = zoneGo.AddComponent<RoomTransitionZone>();
            SetPrivate(zone, "nextSceneName", "Forest - Map 2");
            SetPrivate(zone, "startActive", false);

            // ── Entry Door (mở sẵn; sẽ ĐÓNG khi vào trận để chặn quay lại) — đặt phía lối vào ──
            var entryGo = new GameObject("EntryDoor");
            entryGo.transform.SetParent(root.transform);
            entryGo.transform.localPosition = new Vector3(-12f, 1.5f, 0f);
            entryGo.AddComponent<NetworkObject>();
            var entryCol = entryGo.AddComponent<BoxCollider2D>();
            entryCol.size = new Vector2(1f, 3f);
            entryCol.isTrigger = false;
            var entryVisual = CreateVisualChild(entryGo, "DoorVisual", new Vector2(1f, 3f), new Color(0.3f, 0.15f, 0.3f));
            var entryDoor = entryGo.AddComponent<Door>();
            SetPrivate(entryDoor, "blockingCollider", entryCol);
            SetPrivate(entryDoor, "doorVisual", entryVisual);
            SetPrivate(entryDoor, "startOpen", true); // mở sẵn cho player đi vào; đóng khi đánh nhau

            // ── BossGateController ──
            var gateGo = new GameObject("BossGateController");
            gateGo.transform.SetParent(root.transform);
            gateGo.transform.localPosition = Vector3.zero;
            gateGo.AddComponent<NetworkObject>();
            var gate = gateGo.AddComponent<BossGateController>();
            SetPrivate(gate, "entryDoor", entryDoor);
            SetPrivate(gate, "exitDoor", door);
            SetPrivate(gate, "exitZone", zone);
            // 'boss' (EnemyController) + 'bossAI' (SeveredFangAI) để trống — người dùng tự kéo boss vào.

            Selection.activeGameObject = gateGo;
            EditorGUIUtility.PingObject(gateGo);
            Debug.Log("[Attrition] Đã tạo BossExitGate (gồm EntryDoor + ExitDoor + Zone). BẮT BUỘC: kéo Boss SeveredFang " +
                      "vào CẢ ô 'boss' (EnemyController) lẫn 'bossAI' (SeveredFangAI) trên BossGateController. " +
                      "Đặt EntryDoor ở lối vào phòng, ExitDoor + Zone ở lối ra. Kiểm tra nextSceneName='Forest - Map 2' " +
                      "đã nằm trong Build Settings. SAVE scene để Fusion bake NetworkObject.");
        }

        private static GameObject CreateVisualChild(GameObject parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = Vector3.zero;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeQuadSprite();
            sr.color = color;
            // Simple mode + scale transform: Sliced cần sprite có border, sprite 1x1 trắng KHÔNG
            // có border → Sliced render rỗng (cửa "không hiện"). Dùng Simple + scale cho chắc.
            sr.drawMode = SpriteDrawMode.Simple;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.sortingOrder = 5;
            return go;
        }

        // Sprite 1x1 trắng (1 unit = 1 pixel) để scale transform ra đúng kích thước units.
        private static Sprite _quad;
        private static Sprite MakeQuadSprite()
        {
            if (_quad != null) return _quad;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _quad = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _quad;
        }

        private static void SetPrivate(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[Attrition] Field '{field}' không tìm thấy trên {target.GetType().Name}"); return; }

            switch (value)
            {
                case bool b: prop.boolValue = b; break;
                case string s: prop.stringValue = s; break;
                case Object o: prop.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
