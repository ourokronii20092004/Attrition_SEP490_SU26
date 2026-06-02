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
                hp = Mathf.RoundToInt(hp * so.coopHpMultiplier);
                ad = Mathf.RoundToInt(ad * so.coopDamageMultiplier);
                ap = Mathf.RoundToInt(ap * so.coopDamageMultiplier);
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
    }
}
