#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Attrition.Editor
{
    /// <summary>
    /// Sinh SẴN toàn bộ prefab skill cho boss 1/3/4/5 từ các sprite sheet VFX đã slice trong
    /// `Art/Skills`, kèm animation + component gây sát thương. Sau khi chạy, việc còn lại chỉ là KÉO prefab
    /// vào ô tương ứng trên AI boss (Inspector) — đúng yêu cầu "add prefab skill vào là boss tung skill".
    ///
    /// VÌ SAO KHÔNG TỰ GÁN LUÔN VÀO Ô: các ô skill là `NetworkPrefabRef` — struct của Fusion bọc
    /// `NetworkObjectGuid` (fixed buffer 2 long) nằm trong Fusion.Runtime.dll. Gán bằng code editor phải
    /// đụng unsafe fixed-buffer + kiểu internal của DLL, dễ vỡ khi Fusion nâng cấp. Kéo tay 1 lần cho mỗi ô
    /// là việc nhỏ, nên tool in ra BẢNG ĐỐI CHIẾU prefab → ô để khỏi phải đoán.
    /// // ponytail: gán tay NetworkPrefabRef. Nếu sau này Fusion mở API public tạo NetworkPrefabRef từ
    /// // NetworkObject thì thêm bước tự-wire vào cuối tool này.
    ///
    /// Menu: Tools/Attrition/Enemy/Setup Boss Skill Prefabs
    /// Idempotent: chạy lại ghi đè prefab/clip cùng tên (đổi art rồi chạy lại là xong).
    /// </summary>
    public static class BossSkillPrefabSetupEditor
    {
        private const string Art = "Assets/_Project/Art/Skills";

        [MenuItem("Tools/Attrition/Enemy/Setup Boss Skill Prefabs")]
        public static void Setup()
        {
            var log = new StringBuilder();
            int ok = 0, fail = 0;

            foreach (var (spec, boss, slot) in AllSpecs())
            {
                var prefab = SkillVfxPrefabFactory.Build(spec);
                if (prefab != null)
                {
                    ok++;
                    log.AppendLine($"  {boss,-10} {slot,-24} ← {spec.prefabName}");
                }
                else fail++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BossSkill] Đã sinh {ok} prefab skill" + (fail > 0 ? $" ({fail} bỏ qua vì thiếu art)" : "") +
                      $".\nPrefab nằm ở {SkillVfxPrefabFactory.PrefabDir}, animation ở {SkillVfxPrefabFactory.AnimDir}.\n\n" +
                      "KÉO PREFAB VÀO Ô TƯƠNG ỨNG trên AI boss:\n" + log +
                      "\nGhi chú:\n" +
                      "• Boss 2 Druid: 5 prefab từ art Wind Effect 01/02 + Wood VFX (đều PPU 16).\n" +
                      "• ArchDemon skill 1 (Dark Orb) KHÔNG có prefab: quả cầu đã vẽ sẵn trong clip " +
                      "ArchDemon_BasicAttack (bung ra ở frame 8) → không có ô nào cần kéo.\n" +
                      "• Ô 'impactPrefab' trên prefab WaterBall: kéo WaterBallImpact vào để cầu nước nổ khi trúng.\n" +
                      "• Prefab đã có NetworkObject → Fusion 2 tự đăng ký, không cần thêm vào danh sách nào.");
        }

        /// <summary>
        /// Toàn bộ spec: (prefab, boss, tên ô trên AI). Tham số damage/lifetime đặt ở AI boss, không ở đây —
        /// prefab chỉ mang hình + cách gây sát thương.
        /// </summary>
        private static (SkillVfxPrefabFactory.Spec, string boss, string slot)[] AllSpecs()
        {
            return new (SkillVfxPrefabFactory.Spec, string, string)[]
            {
                // ═══ BOSS 1 — SEVERED FANG (ô còn thiếu) ═══
                // Vệt lửa của skill 4: trước đây phải tái dùng fireExplosionPrefab vì chưa có ô riêng.
                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "FireBreathStreak",
                    sheetPath = $"{Art}/Fire Effect 1/Fire Breath SpriteSheet.png",
                    isProjectile = false, fps = 18f, loop = false,
                    radius = 1.4f, lifetime = 0.7f, damageDelay = 0.05f, snapToGround = true,
                }, "SeveredFang", "fireBreathPrefab"),

                // ═══ BOSS 2 — DRUID (5 skill) ═══
                // Art Wind Effect 01/02 + Wood VFX — đều PPU 16, khớp chuẩn pixel art của project.
                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "WoodProjectile",
                    // Chọn bản "Repeatable" (PPU 16) chứ KHÔNG dùng "Wood VFX 01 Hit" (PPU 100):
                    // đây là viên gỗ RƠI LIÊN TỤC nên cần sprite lặp được, và PPU phải khớp phần còn lại.
                    sheetPath = $"{Art}/Wood VFX 01 - 02/Wood VFX 01/Wood VFX 01 Repeatable.png",
                    isProjectile = true, fps = 16f, loop = true,
                    speed = 12f, lifetime = 3f, hitboxRadius = 0.3f,
                }, "Druid", "woodProjectilePrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "WindBeam",
                    sheetPath = $"{Art}/Wind Effect 01/Wind Breath.png",
                    // Đốt gió của luồng WindBreath: AoE ĐỨNG YÊN, AI rải nhiều đốt thành luồng dài.
                    isProjectile = false, fps = 18f, loop = false,
                    radius = 1.3f, lifetime = 0.7f, damageDelay = 0.05f, snapToGround = true,
                }, "Druid", "windBeamPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "WindSword",
                    sheetPath = $"{Art}/Wind Effect 01/Wind Projectile.png",
                    isProjectile = true, fps = 18f, loop = true,
                    // speed 0 = giữ tốc độ prefab; DruidBossAI.windSwordSpeed cũng đang 0 nên thống nhất.
                    speed = 14f, lifetime = 3f, hitboxRadius = 0.3f,
                }, "Druid", "windSwordPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "AirBurstPullIn",
                    sheetPath = $"{Art}/Wind Effect 02/Pull in.png",
                    isProjectile = false, fps = 14f, loop = false,
                    radius = 0.1f, lifetime = 0.8f, damageDelay = 0f, snapToGround = false,
                }, "Druid", "airBurstPullInPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "AirBurst",
                    sheetPath = $"{Art}/Wind Effect 02/Air Burst.png",
                    isProjectile = false, fps = 16f, loop = false,
                    // PullIn state đã lo telegraph; damaging clip gây damage gần đầu rồi chạy hết 0.44s.
                    radius = 1.6f, lifetime = 0.55f, damageDelay = 0.08f, snapToGround = false,
                }, "Druid", "airBurstPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "AirExplosionStartup",
                    sheetPath = $"{Art}/Wind Effect 02/Explosion Startup .png",
                    isProjectile = false, fps = 14f, loop = false,
                    radius = 0.1f, lifetime = 0.45f, damageDelay = 0f, snapToGround = false,
                }, "Druid", "airExplosionStartupPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "AirExplosion",
                    sheetPath = $"{Art}/Wind Effect 02/Air Explosion.png",
                    isProjectile = false, fps = 20f, loop = false,
                    radius = 1.4f, lifetime = 0.8f, damageDelay = 0.1f, snapToGround = false,
                }, "Druid", "airExplosionPrefab"),

                // ═══ BOSS 3 — ELF (5 skill) ═══
                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "ThunderArrow",
                    sheetPath = $"{Art}/Thunder Projectile 1/Thunder projectile1 w blur.png",
                    isProjectile = true, fps = 16f, loop = true,
                    speed = 16f, lifetime = 3f, hitboxRadius = 0.3f,
                }, "Elf", "thunderArrowPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "ThunderBird",
                    sheetPath = $"{Art}/Projectile 2/Projectile 2 w blur.png",
                    // loop = false: chim sấm chỉ vỗ cánh MỘT lần rồi giữ frame cuối (trước đây loop làm nó "bay
                    // lặp lại"). fps 8 thay vì 20 để kéo dài animation ra cho nhìn rõ (16 frame ≈ 2s).
                    isProjectile = true, fps = 8f, loop = false,
                    speed = 11f, lifetime = 4f, hitboxRadius = 0.6f,
                }, "Elf", "thunderBirdPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "ThunderHit",
                    sheetPath = $"{Art}/Thunder Hit/Thunder hit w blur.png",
                    // fps 9 (was 18): 5 frame trong 0.22s là quá nhanh không nhìn rõ → giãn ra ~0.55s.
                    // lifetime phải >= độ dài clip, nếu không prefab bị Despawn giữa animation.
                    isProjectile = false, fps = 9f, loop = false,
                    radius = 1.3f, lifetime = 0.75f, damageDelay = 0.12f, snapToGround = false,
                }, "Elf", "thunderHitPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "ThunderSplash",
                    sheetPath = $"{Art}/Thunder Splash/Thunder splash w blur.png",
                    // fps 14 (was 20): sprite splash chỉ ~1.3-2.4 unit, chạy trong 0.5s thì gần như không thấy.
                    // Giãn ra ~0.79s + scale 2 (ở Elf.prefab spawn) cho player nhận ra boss vừa dịch chuyển.
                    isProjectile = false, fps = 14f, loop = false,
                    radius = 1.2f, lifetime = 1f, damageDelay = 0.08f, snapToGround = false,
                    scale = 2f,
                }, "Elf", "thunderSplashPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "ThunderStrike",
                    sheetPath = $"{Art}/Thunder Strike/Thunderstrike w blur.png",
                    isProjectile = false, fps = 22f, loop = false,
                    // Cột sét được kéo DÀI theo Y; vẫn snap cả visual + damage xuống cùng mặt đất.
                    radius = 1.1f, lifetime = 1.8f, damageDelay = 0.75f, snapToGround = true,
                    scaleY = 2.5f, groundOffset = 0.1f,
                }, "Elf", "thunderStrikePrefab"),

                // ═══ BOSS 4 — DEMONKIN (4 skill) ═══
                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "EarthProjectile",
                    sheetPath = $"{Art}/Earth Effect 01/Earth projectile Spritesheet .png",
                    isProjectile = true, fps = 16f, loop = true,
                    speed = 13f, lifetime = 3f, hitboxRadius = 0.35f,
                }, "DemonKin", "earthProjectilePrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "IrregularRock",
                    sheetPath = $"{Art}/Earth Effect 01/Irregular rock Spritesheet.png",
                    isProjectile = false, fps = 20f, loop = false,
                    // damageDelay ≈ encloseTime(0.85) + explodeDelay(0.9) để hình nổ khớp lúc gây damage.
                    radius = 2.2f, lifetime = 2.2f, damageDelay = 1.75f, snapToGround = false,
                }, "DemonKin", "irregularRockPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "EarthBump",
                    sheetPath = $"{Art}/Earth Effect 02/Earth Bump.png",
                    isProjectile = false, fps = 16f, loop = false,
                    radius = 1.5f, lifetime = 0.9f, damageDelay = 0.1f, snapToGround = true,
                }, "DemonKin", "earthBumpPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "EarthWall",
                    sheetPath = $"{Art}/Earth Effect 02/Earth Wall.png",
                    isProjectile = false, fps = 16f, loop = false,
                    radius = 1.2f, lifetime = 4.5f, damageDelay = 0.15f, snapToGround = true,
                    blockingWall = true, wallHeight = 3f,
                }, "DemonKin", "earthWallPrefab"),

                // ═══ BOSS 5 — ARCH DEMON (skill 2-5) ═══
                // SKILL 1 (Dark Orb) KHÔNG sinh prefab: quả cầu bóng tối đã vẽ SẴN trong clip
                // ArchDemon_BasicAttack (bung ra ở frame 8). AD_DarkOrbState chỉ chạy animation rồi quét
                // hộp sát thương đúng frame đó — không có NetworkObject nào để gán.
                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "WaterBall",
                    sheetPath = $"{Art}/Water Ball - Spritesheet/WaterBall - Startup and Infinite.png",
                    isProjectile = true, fps = 20f, loop = true,
                    speed = 18f, lifetime = 3f, hitboxRadius = 0.35f,
                }, "ArchDemon", "waterBallPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "WaterBallImpact",
                    sheetPath = $"{Art}/Water Ball - Spritesheet/WaterBall - Impact.png",
                    isProjectile = false, fps = 22f, loop = false,
                    radius = 1.1f, lifetime = 0.7f, damageDelay = 0.05f, snapToGround = false,
                }, "ArchDemon", "waterBallImpactPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "WaterBlast",
                    sheetPath = $"{Art}/Water Blast - Spritesheet/Water Blast - Startup and Infinite.png",
                    // Lốc do AI tự lái đi-về → lifetime phải dài hơn hành trình, và loop để hình chạy liên tục.
                    isProjectile = false, fps = 20f, loop = true,
                    radius = 1.2f, lifetime = 8f, damageDelay = 0.05f, snapToGround = false,
                }, "ArchDemon", "waterBlastPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "WaterBlastEnd",
                    sheetPath = $"{Art}/Water Blast - Spritesheet/Water Blast - End.png",
                    isProjectile = false, fps = 24f, loop = false,
                    radius = 1f, lifetime = 1.2f, damageDelay = 0.05f, snapToGround = false,
                }, "ArchDemon", "waterBlastEndPrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "WaterStartup1",
                    sheetPath = $"{Art}/Start Up/Water StartUp 1 SpriteSheet.png",
                    // Dấu báo trước: KHÔNG gây damage (AI truyền damage 0) nhưng vẫn là AoE để dùng chung đường spawn.
                    isProjectile = false, fps = 24f, loop = false,
                    radius = 0.8f, lifetime = 0.6f, damageDelay = 99f, snapToGround = true,
                }, "ArchDemon", "waterStartup1Prefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "WaterSpike",
                    sheetPath = $"{Art}/Water Effect SpriteSheet/Water Spike 01 - SpriteSheet.png",
                    isProjectile = false, fps = 24f, loop = false,
                    radius = 1.2f, lifetime = 1.1f, damageDelay = 0.15f, snapToGround = true,
                    groundOffset = 1.1f,
                }, "ArchDemon", "waterSpikePrefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "WaterStartup2",
                    sheetPath = $"{Art}/Start Up/Water StartUp 2 SpriteSheet.png",
                    isProjectile = false, fps = 24f, loop = false,
                    radius = 0.8f, lifetime = 0.7f, damageDelay = 99f, snapToGround = true,
                }, "ArchDemon", "waterStartup2Prefab"),

                (new SkillVfxPrefabFactory.Spec {
                    prefabName = "WaterSplash",
                    sheetPath = $"{Art}/Water Effect SpriteSheet/Water Splash 01 - Spritesheet.png",
                    isProjectile = false, fps = 22f, loop = false,
                    radius = 1.5f, lifetime = 1f, damageDelay = 0.1f, snapToGround = true,
                    groundOffset = 1.5f,
                }, "ArchDemon", "waterSplashPrefab"),
            };
        }
    }
}
#endif
