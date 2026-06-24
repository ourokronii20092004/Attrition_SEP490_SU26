using UnityEditor;
using UnityEngine;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool thêm vùng GÂY SÁT THƯƠNG KHI CHẠM (ContactDamageZone) vào boss SeveredFang — thứ còn
    /// thiếu khiến "player chạm boss không mất HP". Quái thường có child này; boss thì chưa.
    ///
    /// Tạo 1 child "ContactDamageZone": BoxCollider2D (IsTrigger) phủ thân boss + script
    /// EnemyContactDamage. Để layer 0 (Default) — KHÔNG để layer Enemy (tránh IgnoreLayerCollision).
    /// Menu: Tools/Attrition/Add Boss Contact Damage Zone
    /// </summary>
    public static class BossContactDamageSetupEditor
    {
        private const string BossPrefabPath = "Assets/_Project/Prefabs/Enemy/SeveredFang.prefab";

        [MenuItem("Tools/Attrition/Add Boss Contact Damage Zone")]
        public static void AddContactZone()
        {
            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            if (root == null) { Debug.LogError($"[Attrition] Không mở được boss prefab: {BossPrefabPath}"); return; }

            // Đã có ContactDamageZone chưa? (tránh tạo trùng)
            Transform existing = root.transform.Find("ContactDamageZone");
            if (existing != null)
            {
                Debug.Log("[Attrition] Boss đã có ContactDamageZone — bỏ qua.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            var zone = new GameObject("ContactDamageZone");
            zone.layer = 0; // Default — KHÔNG để Enemy
            zone.transform.SetParent(root.transform, false);
            zone.transform.localPosition = Vector3.zero;

            // Collider phủ thân boss (collider chính capsule 2.25 x 2.875). Phủ rộng hơn chút.
            var col = zone.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.4f, 3.0f);
            col.offset = Vector2.zero;

            // Gắn script EnemyContactDamage (type nằm ở global namespace).
            var contact = zone.AddComponent<EnemyContactDamage>();
            // Boss có EnemyStats → contactDamage là HỆ SỐ × AD. Để 1 (= 1×AD mỗi lần chạm).
            var so = new SerializedObject(contact);
            SetIfExists(so, "contactDamage", 1);
            SetIfExists(so, "contactKnockbackForce", 6f);
            SetIfExists(so, "contactCooldown", 0.6f);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log("[Attrition] Đã thêm ContactDamageZone vào boss SeveredFang (BoxCollider trigger 2.4x3.0 + " +
                      "EnemyContactDamage, layer Default). Player chạm boss giờ sẽ mất HP. Chỉnh size/offset nếu cần.");
        }

        private static void SetIfExists(SerializedObject so, string field, object value)
        {
            var p = so.FindProperty(field);
            if (p == null) return;
            if (value is int i) p.intValue = i;
            else if (value is float f) p.floatValue = f;
        }
    }
}
