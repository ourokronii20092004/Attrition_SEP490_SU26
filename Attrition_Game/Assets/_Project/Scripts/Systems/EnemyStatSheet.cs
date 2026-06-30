using UnityEngine;
using Attrition.Core;
using Attrition.Data;

namespace Attrition.Systems
{
    /// <summary>
    /// Chỉ số quái lúc runtime = EnemyStatsSO (STATIC default)
    ///   ⊕ override từ backend (admin sửa trên web — DYNAMIC)
    ///   ⊕ coop scaling (nhân lên khi 2 người chơi).
    /// Host build sheet này khi spawn rồi đồng bộ con số đã chốt xuống client qua [Networked].
    /// </summary>
    public class EnemyStatSheet
    {
        public int MaxHP { get; private set; }
        public int AD { get; private set; }
        public int AP { get; private set; }
        public int DEF { get; private set; }
        public int RES { get; private set; }
        public int Poise { get; private set; }

        public static EnemyStatSheet Build(EnemyStatsSO so, EnemyStatOverride ovr, bool isCoop)
        {
            int hp  = ovr?.maxHP ?? so.maxHP;
            int ad  = ovr?.ad   ?? so.ad;
            int ap  = ovr?.ap   ?? so.ap;
            int def = ovr?.def  ?? so.def;
            int res = ovr?.res  ?? so.res;
            int poise = ovr?.poise ?? so.poise;

            if (isCoop)
            {
                // BR-20: Max HP is scaled up by exactly 50%
                hp = Mathf.RoundToInt(hp * 1.5f);

                // BR-22: Enemy stagger resistance (Poise) is increased by exactly 50%
                poise = Mathf.RoundToInt(poise * 1.5f);
            }

            return new EnemyStatSheet
            {
                MaxHP = Mathf.Max(1, hp),
                AD = Mathf.Max(0, ad),
                AP = Mathf.Max(0, ap),
                DEF = Mathf.Max(0, def),
                RES = Mathf.Max(0, res),
                Poise = Mathf.Max(0, poise),
            };
        }
    }

    /// <summary>
    /// Giá trị override do admin chỉnh trên web (nullable = không override, dùng default SO).
    /// Persistence layer điền từ JSON backend trả về.
    /// </summary>
    public class EnemyStatOverride
    {
        public int? maxHP;
        public int? ad;
        public int? ap;
        public int? def;
        public int? res;
        public int? poise;

        /// <summary>
        /// Bảng rơi đồ admin cấu hình trên web (null/rỗng = không override, dùng lootItemIds trong SO).
        /// Mỗi rule: itemId (= ItemName trên web, khớp ItemSO.itemId) + tỉ lệ + số lượng min/max.
        /// EnemyController đọc khi quái chết để rơi/thưởng theo cấu hình web.
        /// </summary>
        public System.Collections.Generic.List<LootRule> loot;
    }

    /// <summary>1 dòng loot từ web đã chuẩn hoá cho game dùng (itemId khớp ItemDatabase).</summary>
    public struct LootRule
    {
        public string itemId;     // = LootEntryDto.ItemName (khớp ItemSO.itemId)
        public float dropChance;  // 0..1
        public int minQty;
        public int maxQty;
    }
}
