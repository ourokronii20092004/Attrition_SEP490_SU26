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

            // ─────────────────────────────────────────────────────────────────────────────
            // ⚠ THỨ TỰ TRONG DANH SÁCH = INDEX MẠNG (xem ItemDatabaseSO). Item ĐÃ CÓ phải giữ
            // NGUYÊN vị trí, item mới CHỈ được APPEND vào cuối. Vì vậy 12 món helm/chest/boots của
            // 4 bậc đầu giữ đúng chỗ cũ (index 0..11); quần của 4 bậc đó + 6 bậc mới nằm ở CUỐI hàm
            // (xem phần "TRANG BỊ BỔ SUNG"). Nếu chèn giữa, inventory đã lưu sẽ trỏ sai item.
            // ─────────────────────────────────────────────────────────────────────────────

            // Tên hiển thị TIẾNG ANH; tên file icon tiếng Việt khớp file bạn đã đặt.
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

            items.Add(AbilityAcc("acc_double_jump", "Feather Charm", GrantedAbility.DoubleJump));
            items.Add(AbilityAcc("acc_shadow_dash", "Shadow Cloak", GrantedAbility.ShadowDash));
            items.Add(DamageAcc("acc_stamina_charm", "Vigor Charm", Icon("bùa thể lực"), (StatType.MaxStamina, 20)));

            // ─── ACCESSORY HIỆU ỨNG (chưa có art — icon null, chỉnh tham số trong Inspector nếu cần) ───
            items.Add(EffectAcc("acc_burn",       "Ember Charm",    DamageEffectType.Burn,          magnitude: 30, duration: 3f));
            items.Add(EffectAcc("acc_slow",       "Frost Charm",    DamageEffectType.Slow,          magnitude: 0.5f, duration: 2.5f));
            items.Add(EffectAcc("acc_lifesteal",  "Vampiric Charm", DamageEffectType.Lifesteal,     magnitude: 0.2f));
            items.Add(EffectAcc("acc_regen",      "Renewal Charm",  DamageEffectType.HealthRegen,   magnitude: 5f, threshold: 0.5f, thresholdStop: 0.8f));
            items.Add(EffectAcc("acc_potion",     "Alchemist Charm",DamageEffectType.PotionBoost,   magnitude: 0.3f));
            items.Add(EffectAcc("acc_shield",     "Aegis Charm",    DamageEffectType.DamageShield,  magnitude: 40, duration: 4f, cooldown: 8f));
            items.Add(EffectAcc("acc_postskill",  "Focus Charm",    DamageEffectType.PostSkillDamage,magnitude: 1.5f));

            // ─── SKILL ───
            items.Add(SkillProjectile("skill_fire", "Fireball", SkillElement.Fire, 20, 0.7f, 35, skillProjectile, count: 3, spread: 30f, icon: Icon("fire_ball_skill")));
            items.Add(SkillArea("skill_wood", "Thorn Lash", SkillElement.Wood, 18, 0.6f, 30, SkillHitShape.Cone, range: 3f, angle: 100f));
            items.Add(SkillArea("skill_earth", "Stone Spike", SkillElement.Earth, 25, 0.9f, 45, SkillHitShape.Circle, range: 2.5f, angle: 360f));
            items.Add(SkillProjectile("skill_thunder", "Chain Bolt", SkillElement.Thunder, 22, 0.6f, 38, skillProjectile, count: 5, spread: 45f));
            items.Add(SkillArea("skill_thrust", "Phantom Thrust", SkillElement.Thrust, 15, 0.5f, 28, SkillHitShape.Rectangle, range: 3.5f, angle: 0f));

            // ─── TRANG BỊ BỔ SUNG (APPEND CUỐI để không lệch index mạng của item cũ) ───
            AddExtraEquipment(items);

            CreateDatabase(items);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ItemSystemSetup] Xong. Prefab generic: {droppedPrefab}. {items.Count} item + ItemDatabase tạo tại {DataDir}.");
        }

        /// <summary>
        /// Một BẬC trang bị: id/tên tiếng Anh + hậu tố tên file icon tiếng Việt + chỉ số 4 món.
        /// Thứ tự sức mạnh: leather → bronze → iron → gold → amethyst → emerald → diamond →
        /// obsidian → ancient → mythic.
        /// </summary>
        private struct Tier
        {
            public string id;        // tiền tố itemId, vd "amethyst" → amethyst_helm
            public string name;      // tiền tố tên hiển thị, vd "Amethyst" → "Amethyst Helm"
            public string iconVi;    // hậu tố tên file icon, vd "amethyst" → "nón amethyst"

            public int helmDef, helmRes;
            public int chestDef, chestHp;
            public int legsDef, legsHp, legsRes;
            public int bootsDef, bootsRes, bootsMoveSpeed;
        }

        /// <summary>
        /// Bảng chỉ số 6 bậc MỚI + quần cho CẢ 10 bậc.
        ///
        /// Nguyên tắc cân bằng (nối tiếp 4 bậc đã có: helm DEF 2/3/4/6, chest DEF 4/6/8/11 + HP 10/15/20/30,
        /// boots DEF+RES 1/2/3/4):
        ///  - Tăng dần, bậc càng cao bước nhảy càng lớn (tạo cảm giác "vượt cấp" cuối game).
        ///  - Quần (Legs) nằm GIỮA giáp và mũ về sức mạnh: def cao hơn mũ, thấp hơn giáp; có thêm ít HP+RES.
        ///  - Từ bậc diamond trở lên boots mới cộng MoveSpeed (phần thưởng cuối game).
        ///  - 4 bậc đầu: chỉ điền cột LEGS (mũ/giáp/giày giữ nguyên như cũ, không tạo lại).
        /// </summary>
        private static readonly Tier[] NewTiers =
        {
            // 6 BẬC MỚI — tạo đủ 4 món.
            new Tier { id = "amethyst", name = "Amethyst", iconVi = "amethyst",
                       helmDef =  8, helmRes =  3, chestDef = 14, chestHp =  40,
                       legsDef = 10, legsHp = 20, legsRes = 3, bootsDef =  5, bootsRes =  5, bootsMoveSpeed = 0 },
            new Tier { id = "emerald", name = "Emerald", iconVi = "lục bảo",
                       helmDef = 10, helmRes =  4, chestDef = 18, chestHp =  52,
                       legsDef = 13, legsHp = 26, legsRes = 4, bootsDef =  6, bootsRes =  6, bootsMoveSpeed = 0 },
            new Tier { id = "diamond", name = "Diamond", iconVi = "kim cương",
                       helmDef = 13, helmRes =  6, chestDef = 22, chestHp =  66,
                       legsDef = 16, legsHp = 33, legsRes = 6, bootsDef =  8, bootsRes =  8, bootsMoveSpeed = 1 },
            new Tier { id = "obsidian", name = "Obsidian", iconVi = "obsidian",
                       helmDef = 16, helmRes =  8, chestDef = 27, chestHp =  82,
                       legsDef = 20, legsHp = 41, legsRes = 8, bootsDef = 10, bootsRes = 10, bootsMoveSpeed = 1 },
            new Tier { id = "ancient", name = "Ancient", iconVi = "cổ đại",
                       helmDef = 20, helmRes = 10, chestDef = 32, chestHp = 100,
                       legsDef = 24, legsHp = 50, legsRes = 10, bootsDef = 12, bootsRes = 12, bootsMoveSpeed = 2 },
            new Tier { id = "mythic", name = "Mythic", iconVi = "huyền thoại",
                       helmDef = 24, helmRes = 12, chestDef = 38, chestHp = 120,
                       legsDef = 29, legsHp = 60, legsRes = 12, bootsDef = 14, bootsRes = 14, bootsMoveSpeed = 2 },
        };

        /// <summary>4 bậc CŨ — chỉ thêm QUẦN (mũ/giáp/giày đã tạo ở trên, giữ nguyên index mạng).</summary>
        private static readonly Tier[] LegsOnlyTiers =
        {
            new Tier { id = "leather", name = "Leather", iconVi = "da",    legsDef = 3, legsHp =  5, legsRes = 0 },
            new Tier { id = "bronze",  name = "Bronze",  iconVi = "đồng",  legsDef = 4, legsHp =  8, legsRes = 1 },
            new Tier { id = "iron",    name = "Iron",    iconVi = "sắt",   legsDef = 6, legsHp = 10, legsRes = 2 },
            new Tier { id = "gold",    name = "Gilded",  iconVi = "vàng",  legsDef = 8, legsHp = 15, legsRes = 3 },
        };

        /// <summary>
        /// Thêm: QUẦN cho 4 bậc cũ, rồi ĐỦ 4 MÓN cho 6 bậc mới. Append cuối danh sách → không lệch
        /// index mạng của item đã lưu trong save/inventory cũ.
        /// </summary>
        private static void AddExtraEquipment(List<ItemSO> items)
        {
            // 1) Quần cho 4 bậc đã có sẵn 3 món.
            foreach (var t in LegsOnlyTiers)
                items.Add(Legs(t));

            // 2) 6 bậc mới: mũ → giáp → quần → giày.
            foreach (var t in NewTiers)
            {
                items.Add(Equip($"{t.id}_helm", $"{t.name} Helm", EquipmentSlot.Head,
                    Icon($"nón {t.iconVi}"), (StatType.DEF, t.helmDef), (StatType.RES, t.helmRes)));

                items.Add(Equip($"{t.id}_chest", $"{t.name} Armor", EquipmentSlot.Chest,
                    Icon($"giáp {t.iconVi}"), (StatType.DEF, t.chestDef), (StatType.MaxHP, t.chestHp)));

                items.Add(Legs(t));

                if (t.bootsMoveSpeed > 0)
                    items.Add(Equip($"{t.id}_boots", $"{t.name} Boots", EquipmentSlot.Boots,
                        Icon($"giày {t.iconVi}"), (StatType.DEF, t.bootsDef), (StatType.RES, t.bootsRes),
                        (StatType.MoveSpeed, t.bootsMoveSpeed)));
                else
                    items.Add(Equip($"{t.id}_boots", $"{t.name} Boots", EquipmentSlot.Boots,
                        Icon($"giày {t.iconVi}"), (StatType.DEF, t.bootsDef), (StatType.RES, t.bootsRes)));
            }
        }

        /// <summary>Tạo món QUẦN của 1 bậc. Icon quần nằm ở UI_Elements (gốc), user thêm sau → có thể null.</summary>
        private static EquipmentSO Legs(Tier t)
        {
            var mods = t.legsRes > 0
                ? new[] { (StatType.DEF, t.legsDef), (StatType.MaxHP, t.legsHp), (StatType.RES, t.legsRes) }
                : new[] { (StatType.DEF, t.legsDef), (StatType.MaxHP, t.legsHp) };

            return Equip($"{t.id}_legs", $"{t.name} Legguards", EquipmentSlot.Legs,
                         IconLegs(t.iconVi), mods);
        }

        /// <summary>
        /// Nạp icon QUẦN. Khác các món khác: file quần nằm ở `UI_Elements` (gốc), KHÔNG phải `16x16`.
        /// User sẽ bổ sung/đổi icon sau → thiếu file thì trả null (item vẫn tạo được, chỉ chưa có hình).
        /// KHÔNG đổi import settings vì các file này đang là sprite sheet (spriteMode Multiple).
        /// </summary>
        private static Sprite IconLegs(string tierVi)
        {
            string path = $"Assets/_Project/Art/UI_Elements/quần {tierVi}.png";

            // File chưa tồn tại (user chưa thêm icon) → null, item vẫn tạo bình thường.
            if (AssetImporter.GetAtPath(path) == null)
            {
                Debug.Log($"[ItemSystemSetup] Chưa có icon quần: {path} — item vẫn tạo, gán icon sau.");
                return null;
            }

            // Các file quần đang là spriteMode MULTIPLE (sprite sheet) → main asset là Texture2D,
            // LoadAssetAtPath<Sprite> trả NULL. Phải quét sub-asset để lấy Sprite đầu tiên.
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;

            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub is Sprite s) return s;

            Debug.LogWarning($"[ItemSystemSetup] '{path}' không có Sprite nào (texture chưa slice?) — " +
                             "item vẫn tạo, gán icon sau.");
            return null;
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

        /// <summary>Accessory DamageEffect có HIỆU ỨNG đặc biệt (burn/slow/lifesteal...). Chưa có asset art
        /// nên icon = null; chỉnh magnitude/duration/threshold sau trong Inspector nếu cần.</summary>
        private static AccessorySO EffectAcc(string id, string name, DamageEffectType effect,
            float magnitude, float duration = 3f, float threshold = 0.5f, float thresholdStop = 0.8f, float cooldown = 8f)
        {
            var so = ScriptableObject.CreateInstance<AccessorySO>();
            so.itemId = id; so.displayName = name; so.maxStack = 1;
            so.kind = AccessoryKind.DamageEffect;
            so.effect = effect;
            so.effectMagnitude = magnitude;
            so.effectDuration = duration;
            so.effectThreshold = threshold;
            so.effectThresholdStop = thresholdStop;
            so.effectCooldown = cooldown;
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
