#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Attrition.Core;
using Attrition.Data;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Tạo nhanh toàn bộ hệ thống item trong 1 click (menu Attrition/Setup Item System):
    ///   - 2 prefab GENERIC trong _Project/Prefabs: DroppedItem (đọc icon runtime), PickupItem.
    ///   - Bộ item SO mẫu trong _Project/Data/Items: bình HP/Mana, 4 trang bị, 2 accessory, 5 skill.
    ///   - ItemDatabase asset gom tất cả (đúng thứ tự = network index).
    /// Item KHÔNG cần prefab riêng từng món — chỉ cần icon trong SO. Gán icon sau cũng được.
    /// </summary>
    public static class ItemSystemSetup
    {
        private const string PrefabDir = "Assets/_Project/Prefabs";
        private const string DataDir = "Assets/_Project/Data/Items";
        private const string IconDir = "Assets/_Project/Art/UI_Elements/16x16";

        /// <summary>Nạp sprite icon theo tên file (không đuôi) trong IconDir. Trả null nếu không có.</summary>
        private static Sprite Icon(string fileName)
        {
            string path = $"{IconDir}/{fileName}.png";

            // Nếu file chưa import thành Sprite (vẫn là Default texture) → ép sang Sprite rồi nạp lại.
            // Nhờ vậy không phụ thuộc thứ tự chạy "Fix Icon Import" trước hay sau.
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }

            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) Debug.LogWarning($"[ItemSystemSetup] Không tìm thấy icon: {path}");
            return sp;
        }

        [MenuItem("Attrition/Setup Item System")]
        public static void Setup()
        {
            EnsureDir(PrefabDir);
            EnsureDir(DataDir);

            var droppedPrefab = CreateDroppedItemPrefab();
            CreatePickupPrefab();
            var skillProjectile = CreateSkillProjectilePrefab();

            var items = new List<ItemSO>();

            // ─── ARMOR: 4 tier (Leather/Bronze/Iron/Gold) × 3 slot (Helm/Chest/Boots) ───
            // Tên hiển thị TIẾNG ANH; tên file icon tiếng Việt khớp file bạn đã đặt.
            // Lưu ý: chưa có icon cho slot Legs (quần) nên tạm bỏ.
            items.Add(Equip("leather_helm",  "Leather Helm",  EquipmentSlot.Head,  Icon("nón da"),   (StatType.DEF, 2)));
            items.Add(Equip("leather_chest", "Leather Armor", EquipmentSlot.Chest, Icon("giáp da"),  (StatType.DEF, 4), (StatType.MaxHP, 10)));
            items.Add(Equip("leather_boots", "Leather Boots", EquipmentSlot.Boots, Icon("giày da"),  (StatType.DEF, 1), (StatType.RES, 1)));

            items.Add(Equip("bronze_helm",  "Bronze Helm",  EquipmentSlot.Head,  Icon("nón đồng"),  (StatType.DEF, 3)));
            items.Add(Equip("bronze_chest", "Bronze Armor", EquipmentSlot.Chest, Icon("giáp đồng"), (StatType.DEF, 6), (StatType.MaxHP, 15)));
            items.Add(Equip("bronze_boots", "Bronze Boots", EquipmentSlot.Boots, Icon("giày đồng"), (StatType.DEF, 2), (StatType.RES, 2)));

            items.Add(Equip("iron_helm",  "Iron Helm",  EquipmentSlot.Head,  Icon("nón sắt"),  (StatType.DEF, 4)));
            items.Add(Equip("iron_chest", "Iron Armor", EquipmentSlot.Chest, Icon("giáp sắt"), (StatType.DEF, 8), (StatType.MaxHP, 20)));
            items.Add(Equip("iron_boots", "Iron Boots", EquipmentSlot.Boots, Icon("giày sắt"), (StatType.DEF, 3), (StatType.RES, 3)));

            items.Add(Equip("gold_helm",  "Gilded Helm",  EquipmentSlot.Head,  Icon("nón vàng"),  (StatType.DEF, 6), (StatType.RES, 2)));
            items.Add(Equip("gold_chest", "Gilded Armor", EquipmentSlot.Chest, Icon("giáp vàng"), (StatType.DEF, 11), (StatType.MaxHP, 30)));
            items.Add(Equip("gold_boots", "Gilded Boots", EquipmentSlot.Boots, Icon("giày vàng"), (StatType.DEF, 4), (StatType.RES, 4)));

            // ─── ACCESSORY ───
            items.Add(AbilityAcc("acc_double_jump", "Feather Charm", GrantedAbility.DoubleJump));
            items.Add(AbilityAcc("acc_shadow_dash", "Shadow Cloak", GrantedAbility.ShadowDash));
            items.Add(DamageAcc("acc_stamina_charm", "Vigor Charm", Icon("bùa thể lực"), (StatType.MaxStamina, 20)));

            // ─── SKILL ───
            items.Add(SkillProjectile("skill_fire", "Fireball", SkillElement.Fire, 20, 0.7f, 35, skillProjectile, count: 3, spread: 30f, icon: Icon("fire_ball_skill")));
            items.Add(SkillArea("skill_wood", "Thorn Lash", SkillElement.Wood, 18, 0.6f, 30, SkillHitShape.Cone, range: 3f, angle: 100f));
            items.Add(SkillArea("skill_earth", "Stone Spike", SkillElement.Earth, 25, 0.9f, 45, SkillHitShape.Circle, range: 2.5f, angle: 360f));
            items.Add(SkillProjectile("skill_thunder", "Chain Bolt", SkillElement.Thunder, 22, 0.6f, 38, skillProjectile, count: 5, spread: 45f));
            items.Add(SkillArea("skill_thrust", "Phantom Thrust", SkillElement.Thrust, 15, 0.5f, 28, SkillHitShape.Rectangle, range: 3.5f, angle: 0f));

            CreateDatabase(items);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ItemSystemSetup] Xong. Prefab generic: {droppedPrefab}. {items.Count} item + ItemDatabase tạo tại {DataDir}.");
        }

        /// <summary>Prefab đạn cho skill PLAYER. hitLayer set qua Inspector (chọn layer Enemy) sau khi tạo.</summary>
        private static GameObject CreateSkillProjectilePrefab()
        {
            string path = PrefabDir + "/SkillProjectile.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject("SkillProjectile");
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.2f;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<Fusion.NetworkObject>();
            var proj = go.AddComponent<EnemyProjectile>();
            proj.speed = 12f;
            proj.hitboxRadius = 0.2f;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateDroppedItemPrefab()
        {
            string path = PrefabDir + "/DroppedItem.prefab";
            var go = new GameObject("DroppedItem");
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.4f;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<Fusion.NetworkObject>();
            go.AddComponent<DroppedItem>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void CreatePickupPrefab()
        {
            string path = PrefabDir + "/PickupItem.prefab";
            var go = new GameObject("PickupItem");
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.4f;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<Fusion.NetworkObject>();
            go.AddComponent<PickupItem>();
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static EquipmentSO Equip(string id, string name, EquipmentSlot slot, Sprite icon, params (StatType, int)[] mods)
        {
            var so = ScriptableObject.CreateInstance<EquipmentSO>();
            so.itemId = id; so.displayName = name; so.slot = slot; so.maxStack = 1;
            so.icon = icon;
            so.modifiers = ToMods(mods);
            return Save(so, id);
        }

        private static AccessorySO AbilityAcc(string id, string name, GrantedAbility ability, Sprite icon = null)
        {
            var so = ScriptableObject.CreateInstance<AccessorySO>();
            so.itemId = id; so.displayName = name; so.maxStack = 1;
            so.icon = icon;
            so.kind = AccessoryKind.AbilityGrant; so.grantedAbility = ability;
            return Save(so, id);
        }

        private static AccessorySO DamageAcc(string id, string name, Sprite icon, params (StatType, int)[] mods)
        {
            var so = ScriptableObject.CreateInstance<AccessorySO>();
            so.itemId = id; so.displayName = name; so.maxStack = 1;
            so.icon = icon;
            so.kind = AccessoryKind.DamageEffect; so.modifiers = ToMods(mods);
            return Save(so, id);
        }

        private static SkillSO SkillArea(string id, string name, SkillElement el, int mana, float cast, int dmg,
            SkillHitShape shape, float range, float angle, Sprite icon = null)
        {
            var so = ScriptableObject.CreateInstance<SkillSO>();
            so.itemId = id; so.displayName = name; so.maxStack = 1;
            so.icon = icon;
            so.element = el; so.manaCost = mana; so.castTime = cast; so.baseDamage = dmg;
            so.delivery = SkillDelivery.AreaInstant;
            so.hitShape = shape; so.range = range; so.angle = angle;
            return Save(so, id);
        }

        private static SkillSO SkillProjectile(string id, string name, SkillElement el, int mana, float cast, int dmg,
            GameObject projectilePrefab, int count, float spread, Sprite icon = null)
        {
            var so = ScriptableObject.CreateInstance<SkillSO>();
            so.itemId = id; so.displayName = name; so.maxStack = 1;
            so.icon = icon;
            so.element = el; so.manaCost = mana; so.castTime = cast; so.baseDamage = dmg;
            so.delivery = SkillDelivery.Projectile;
            so.projectileCount = count; so.spreadAngle = spread; so.projectileSpeed = 12f;
            // LƯU Ý: projectilePrefab (NetworkPrefabRef) phải kéo TAY vào asset trong Inspector —
            // Fusion không cho gán NetworkObject → NetworkPrefabRef bằng code editor đơn giản.
            return Save(so, id);
        }

        private static StatModifier[] ToMods((StatType, int)[] mods)
        {
            var arr = new StatModifier[mods.Length];
            for (int i = 0; i < mods.Length; i++)
                arr[i] = new StatModifier { stat = mods[i].Item1, amount = mods[i].Item2 };
            return arr;
        }

        private static void CreateDatabase(List<ItemSO> items)
        {
            string path = DataDir + "/ItemDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(path);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<ItemDatabaseSO>();
                AssetDatabase.CreateAsset(db, path);
            }
            db.EditorItems.Clear();
            db.EditorItems.AddRange(items);
            EditorUtility.SetDirty(db);
        }

        /// <summary>
        /// Ghi SO ra disk và TRẢ VỀ asset thực trên disk. Nếu đã có → copy giá trị vào asset cũ
        /// (giữ nguyên GUID/tham chiếu) thay vì để instance RAM mồ côi (gây database rỗng khi chạy lại).
        /// </summary>
        private static T Save<T>(T so, string id) where T : ScriptableObject
        {
            string path = DataDir + "/" + id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(so, existing); // ghi đè nội dung, giữ asset cũ
                Object.DestroyImmediate(so);                 // bỏ instance RAM thừa
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
    }
}
#endif
