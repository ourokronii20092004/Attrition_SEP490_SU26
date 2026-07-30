#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Fusion;
using Attrition.Gameplay.Enemy;

namespace Attrition.Editor
{
    /// <summary>
    /// Xưởng dựng 1 prefab VFX-skill hoàn chỉnh từ 1 sprite sheet ĐÃ SLICE: tạo AnimationClip từ các frame,
    /// AnimatorController loop/one-shot, GameObject có NetworkObject + SpriteRenderer + Animator, rồi gắn
    /// component gây sát thương (EnemyProjectile cho đạn bay / EnemyAoEDamage cho nổ đứng yên).
    ///
    /// Tách riêng khỏi tool gọi nó (BossSkillPrefabSetupEditor) vì đây là phần dùng lại được cho mọi boss.
    ///
    /// LƯU Ý PPU: các sheet trong repo lẫn PPU 100 và 16 (đã kiểm: Thunder* = 100, WaterBall/Earth Wall = 16).
    /// Prefab KHÔNG chỉnh scale theo PPU — Unity tự quy đổi sprite sang world units bằng PPU của chính nó,
    /// nên hình sẽ đúng tỉ lệ. Chỉ khi thấy quá to/nhỏ trong scene thì sửa `localScale` trên prefab.
    /// </summary>
    public static class SkillVfxPrefabFactory
    {
        public const string PrefabDir = "Assets/_Project/Prefabs/Projectile";
        public const string AnimDir = "Assets/_Project/Animations/Skills";

        /// <summary>Cấu hình 1 prefab cần dựng.</summary>
        public struct Spec
        {
            public string prefabName;     // tên file prefab
            public string sheetPath;      // đường dẫn PNG đã slice
            public bool isProjectile;     // true = EnemyProjectile (bay), false = EnemyAoEDamage (đứng yên)
            public float fps;             // tốc độ animation
            public bool loop;             // đạn bay thường loop; vụ nổ thì one-shot
            public float radius;          // AoE: bán kính damage
            public float lifetime;        // thời gian sống
            public float damageDelay;     // AoE: trễ trước khi gây damage (khớp frame nổ)
            public bool snapToGround;     // AoE: có hạ xuống đất không
            public float speed;           // projectile: tốc độ mặc định
            public float hitboxRadius;    // projectile: bán kính va chạm
            public bool blockingWall;     // thêm EnemyBlockingWall + collider chặn đạn (Earth Wall)
            public float wallHeight;      // blockingWall: chiều cao tường
        }

        /// <summary>
        /// Dựng (hoặc dựng lại) prefab theo spec. Trả về prefab asset, null nếu thiếu art.
        /// Idempotent: chạy lại ghi đè prefab/clip cũ cùng tên.
        /// </summary>
        public static GameObject Build(Spec spec)
        {
            var frames = LoadFrames(spec.sheetPath);
            if (frames == null || frames.Count == 0)
            {
                Debug.LogWarning($"[SkillVfx] Bỏ qua '{spec.prefabName}': không đọc được sprite từ {spec.sheetPath}");
                return null;
            }

            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(AnimDir);

            var controller = BuildController(spec, frames);

            var go = new GameObject(spec.prefabName);
            try
            {
                go.AddComponent<NetworkObject>();

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                // Vẽ trên nền, dưới UI. Dùng sorting layer mặc định để không phụ thuộc layer tuỳ chỉnh.
                sr.sortingOrder = 10;

                var animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                int playerMask = 1 << LayerMask.NameToLayer("Player");
                int groundMask = 1 << LayerMask.NameToLayer("Ground");

                if (spec.isProjectile)
                {
                    var proj = go.AddComponent<EnemyProjectile>();
                    // Đạn nổ khi trúng Player HOẶC Ground (tường/đất) — giống cấu hình đạn boss 1.
                    proj.hitLayer = playerMask | groundMask;
                    proj.speed = spec.speed > 0f ? spec.speed : 12f;
                    proj.lifetime = spec.lifetime > 0f ? spec.lifetime : 3f;
                    proj.hitboxRadius = spec.hitboxRadius > 0f ? spec.hitboxRadius : 0.3f;
                }
                else
                {
                    var aoe = go.AddComponent<EnemyAoEDamage>();
                    aoe.hitLayer = playerMask;
                    aoe.radius = spec.radius > 0f ? spec.radius : 1.5f;
                    aoe.lifetime = spec.lifetime > 0f ? spec.lifetime : 0.8f;
                    aoe.damageDelay = spec.damageDelay;
                    aoe.snapToGround = spec.snapToGround;
                    aoe.groundLayer = groundMask;
                }

                if (spec.blockingWall)
                {
                    var wall = go.AddComponent<EnemyBlockingWall>();
                    wall.wallHeight = spec.wallHeight > 0f ? spec.wallHeight : 3f;

                    // CHẶN ĐẠN PLAYER: collider phải ở layer Ground vì đạn (EnemyProjectile dùng chung cho
                    // cả SkillProjectile của player) quét hitLayer có Ground → tự nổ khi đụng.
                    go.layer = LayerMask.NameToLayer("Ground");
                    var box = go.AddComponent<BoxCollider2D>();
                    var size = frames[0].bounds.size;
                    box.size = new Vector2(Mathf.Max(0.4f, size.x), Mathf.Max(0.4f, size.y));
                    box.isTrigger = false;   // rắn để chặn thật
                }

                string prefabPath = $"{PrefabDir}/{spec.prefabName}.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Đọc mọi Sprite con trong sheet, sắp theo hậu tố _0.._n (thứ tự AssetDatabase KHÔNG đảm bảo).</summary>
        private static List<Sprite> LoadFrames(string sheetPath)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
            if (all == null || all.Length == 0) return null;

            var sprites = new List<Sprite>();
            foreach (var o in all) if (o is Sprite s) sprites.Add(s);
            if (sprites.Count == 0) return null;

            sprites.Sort((a, b) => IndexOf(a.name).CompareTo(IndexOf(b.name)));
            return sprites;
        }

        /// <summary>Số sau dấu '_' cuối tên sprite; không có thì coi là 0.</summary>
        private static int IndexOf(string name)
        {
            int u = name.LastIndexOf('_');
            if (u < 0 || u == name.Length - 1) return 0;
            return int.TryParse(name.Substring(u + 1), out int i) ? i : 0;
        }

        /// <summary>Tạo AnimationClip (keyframe sprite) + AnimatorController 1 state chạy clip đó.</summary>
        private static AnimatorController BuildController(Spec spec, List<Sprite> frames)
        {
            float fps = spec.fps > 0f ? spec.fps : 16f;

            var clip = new AnimationClip { frameRate = fps };
            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            var keys = new ObjectReferenceKeyframe[frames.Count];
            for (int i = 0; i < frames.Count; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = spec.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string clipPath = $"{AnimDir}/{spec.prefabName}.anim";
            AssetDatabase.CreateAsset(clip, clipPath);

            string ctrlPath = $"{AnimDir}/{spec.prefabName}.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, clip);
            return controller;
        }
    }
}
#endif
