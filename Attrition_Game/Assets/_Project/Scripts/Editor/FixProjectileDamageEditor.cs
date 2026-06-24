using UnityEditor;
using UnityEngine;
using Fusion;
using Attrition.Gameplay.Enemy;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool gắn logic GÂY SÁT THƯƠNG vào các prefab đạn của boss (Firebolt, FireExplosion, FireBall).
    /// Lý do: các prefab này trước đó CHỈ có hình (SpriteRenderer + Animator + NetworkObject), KHÔNG có
    /// EnemyProjectile/EnemyAoEDamage nên bay xuyên player vô hại.
    ///
    /// - Firebolt / FireBall (đạn bay) → thêm EnemyProjectile, hitLayer = Player + Ground.
    /// - FireExplosion (nổ đứng yên)   → thêm EnemyAoEDamage, hitLayer = Player.
    ///
    /// Menu: Tools/Attrition/Fix Boss Projectile Damage
    /// </summary>
    public static class FixProjectileDamageEditor
    {
        private const string FireboltPath = "Assets/_Project/Prefabs/Projectile/Firebolt_SeveredFang.prefab";
        private const string FireballPath = "Assets/_Project/Prefabs/Projectile/FireBall_SeveredFang.prefab";
        private const string ExplosionPath = "Assets/_Project/Prefabs/Projectile/FireExplosion.prefab";

        [MenuItem("Tools/Attrition/Fix Boss Projectile Damage")]
        public static void Fix()
        {
            int playerMask = 1 << LayerMask.NameToLayer("Player");
            int groundMask = 1 << LayerMask.NameToLayer("Ground");

            FixProjectile(FireboltPath, playerMask | groundMask);
            FixProjectile(FireballPath, playerMask | groundMask);
            FixExplosion(ExplosionPath, playerMask);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Attrition] Đã gắn logic damage cho đạn boss. KIỂM TRA: prefab nổ/đạn đã đăng ký trong " +
                      "Fusion NetworkProjectConfig (chúng vốn đã là NetworkObject nên thường OK).");
        }

        private static void FixProjectile(string path, int hitMask)
        {
            var go = PrefabUtility.LoadPrefabContents(path);
            if (go == null) { Debug.LogError($"[Attrition] Không mở được prefab: {path}"); return; }

            var proj = go.GetComponent<EnemyProjectile>();
            if (proj == null) proj = go.AddComponent<EnemyProjectile>();
            proj.hitLayer = hitMask;
            if (proj.speed <= 0f) proj.speed = 10f;
            if (proj.lifetime <= 0f) proj.lifetime = 3f;
            if (proj.hitboxRadius <= 0f) proj.hitboxRadius = 0.3f;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            PrefabUtility.UnloadPrefabContents(go);
            Debug.Log($"[Attrition] Firebolt/Ball OK: {path} (EnemyProjectile, hitLayer=Player+Ground).");
        }

        private static void FixExplosion(string path, int hitMask)
        {
            var go = PrefabUtility.LoadPrefabContents(path);
            if (go == null) { Debug.LogError($"[Attrition] Không mở được prefab: {path}"); return; }

            var aoe = go.GetComponent<EnemyAoEDamage>();
            if (aoe == null) aoe = go.AddComponent<EnemyAoEDamage>();
            aoe.hitLayer = hitMask;
            if (aoe.radius <= 0f) aoe.radius = 1.5f;
            if (aoe.lifetime <= 0f) aoe.lifetime = 0.6f;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            PrefabUtility.UnloadPrefabContents(go);
            Debug.Log($"[Attrition] FireExplosion OK: {path} (EnemyAoEDamage, hitLayer=Player).");
        }
    }
}
