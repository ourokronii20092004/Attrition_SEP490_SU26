using Microsoft.EntityFrameworkCore;

namespace Enemy.Service.Data;

/// <summary>
/// Seed dữ liệu enemy + item gốc lấy từ ScriptableObject trong game (Assets/_Project/Data).
/// Idempotent theo từng bản ghi: chỉ thêm id chưa có, KHÔNG sửa/đè bản ghi admin đã chỉnh trên web.
/// An toàn chạy mỗi lần boot. Tier/Stat đã map sang chuỗi khớp enum game (Normal/Elite/Boss, AD/DEF...).
/// </summary>
public static class EnemyItemSeeder
{
    public static async Task SeedAsync(EnemyDbContext db, ILogger logger)
    {
        await SeedEnemiesAsync(db, logger);
        await SeedItemsAsync(db, logger);
    }

    private static async Task SeedEnemiesAsync(EnemyDbContext db, ILogger logger)
    {
        var existing = await db.Enemies.Select(e => e.EnemyId).ToHashSetAsync();
        var toAdd = SeedData.Enemies().Where(e => !existing.Contains(e.EnemyId)).ToList();
        if (toAdd.Count == 0) return;
        db.Enemies.AddRange(toAdd);
        await db.SaveChangesAsync();
        logger.LogInformation("EnemyItemSeeder: added {Count} enemies.", toAdd.Count);
    }

    private static async Task SeedItemsAsync(EnemyDbContext db, ILogger logger)
    {
        var existing = await db.Items.Select(i => i.ItemId).ToHashSetAsync();
        var toAdd = SeedData.Items().Where(i => !existing.Contains(i.ItemId)).ToList();
        if (toAdd.Count == 0) return;
        db.Items.AddRange(toAdd);
        await db.SaveChangesAsync();
        logger.LogInformation("EnemyItemSeeder: added {Count} items.", toAdd.Count);
    }
}