using BuildingBlocks.Web;
using Enemy.Service.Data;
using Enemy.Service.DTOs;
using Enemy.Service.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Enemy.Service.Repositories;

public class GameDataImportRepository(EnemyDbContext db) : IGameDataImportRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GameDataImportResult> ImportAsync(GameDataImportRequest request)
    {
        var counts = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            var itemCounts = await ImportItems(request.Items, now);
            var itemNames = request.Items.ToDictionary(x => x.ItemId, x => x.Name, StringComparer.Ordinal);
            var enemyCounts = await ImportEnemies(request.Enemies, itemNames, now);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return (itemCounts, enemyCounts);
        });
        var (enemyMax, enemyCount) = await VersionInfo(db.Enemies.Select(x => x.UpdatedAt));
        var (itemMax, itemCount) = await VersionInfo(db.Items.Select(x => x.UpdatedAt));
        return new(counts.itemCounts, counts.enemyCounts,
            Version(enemyMax, enemyCount), Version(itemMax, itemCount));
    }

    private async Task<ImportCounts> ImportItems(List<UnityItemImport> input, DateTime now)
    {
        var ids = input.Select(x => x.ItemId).ToList();
        var existing = await db.Items.Include(x => x.Modifiers).Where(x => ids.Contains(x.ItemId))
            .ToDictionaryAsync(x => x.ItemId, StringComparer.Ordinal);
        var created = 0; var updated = 0; var unchanged = 0;
        foreach (var dto in input)
        {
            var baseline = JsonSerializer.Serialize(dto, JsonOptions);
            if (!existing.TryGetValue(dto.ItemId, out var item))
            {
                item = new ItemEntity { ItemId = dto.ItemId, IconKey = dto.ItemId,
                    UnityBaselineJson = baseline, ImportedAt = now, CreatedAt = now, UpdatedAt = now };
                Apply(item, dto);
                db.Items.Add(item); created++;
            }
            else if (Signature(item) != Signature(dto, item.ImageUrl))
            {
                Apply(item, dto);
                item.UnityBaselineJson = baseline; item.ImportedAt = now; item.UpdatedAt = now; updated++;
            }
            else unchanged++;
        }
        return new(created, updated, unchanged);
    }

    private async Task<ImportCounts> ImportEnemies(List<UnityEnemyImport> input, IReadOnlyDictionary<string, string> itemNames, DateTime now)
    {
        var ids = input.Select(x => x.EnemyId).ToList();
        var existing = await db.Enemies.Include(x => x.LootTable).Where(x => ids.Contains(x.EnemyId)).ToDictionaryAsync(x => x.EnemyId, StringComparer.Ordinal);
        var created = 0; var updated = 0; var unchanged = 0;
        foreach (var dto in input)
        {
            var baseline = JsonSerializer.Serialize(dto, JsonOptions);
            if (!existing.TryGetValue(dto.EnemyId, out var enemy))
            {
                enemy = new EnemyEntity { EnemyId = dto.EnemyId, UnityBaselineJson = baseline, ImportedAt = now, CreatedAt = now, UpdatedAt = now };
                Apply(enemy, dto, itemNames);
                db.Enemies.Add(enemy); created++;
            }
            else if (Signature(enemy) != Signature(dto, enemy.ImageUrl))
            {
                Apply(enemy, dto, itemNames);
                enemy.UnityBaselineJson = baseline; enemy.ImportedAt = now; enemy.UpdatedAt = now; updated++;
            }
            else unchanged++;
        }
        return new(created, updated, unchanged);
    }

    // Unity là nguồn thật khi bấm Sync: đè hết chỉ số, kể cả field admin đã sửa trên web. Chiều
    // ngược lại (web → game) vẫn chạy qua /gameconfig, nên web sửa xong game vào phòng là áp dụng.
    // ImageUrl giữ giá trị cũ nếu Unity không có webImage — ảnh do admin upload không bị xoá.
    private static void Apply(ItemEntity item, UnityItemImport dto)
    {
        item.Name = dto.Name; item.Category = dto.Category; item.Description = Clean(dto.Description);
        item.MaxStack = dto.MaxStack; item.IsKeyItem = dto.IsKeyItem; item.ImageUrl = dto.ImageUrl ?? item.ImageUrl;
        item.Modifiers.Clear();
        item.Modifiers.AddRange(dto.Modifiers?.Select(x => new ItemModifierEntry { Stat = x.Stat, Amount = x.Amount }) ?? []);
    }

    private static void Apply(EnemyEntity enemy, UnityEnemyImport dto, IReadOnlyDictionary<string, string> itemNames)
    {
        enemy.Name = dto.Name; enemy.Tier = dto.Tier; enemy.Hp = dto.Hp; enemy.Ad = dto.Ad; enemy.Ap = dto.Ap;
        enemy.Def = dto.Def; enemy.Res = dto.Res; enemy.Poise = dto.Poise;
        enemy.PoiseRecoveryTime = dto.PoiseRecoveryTime; enemy.PatrolSpeed = dto.PatrolSpeed;
        enemy.ChaseSpeed = dto.ChaseSpeed; enemy.AttackSpeed = dto.AttackSpeed; enemy.ExpReward = dto.ExpReward;
        enemy.ImageUrl = dto.ImageUrl ?? enemy.ImageUrl;
        enemy.LootTable.Clear();
        enemy.LootTable.AddRange((dto.LootTable ?? []).Select(x => new EnemyLootEntry {
            ItemName = itemNames.GetValueOrDefault(x.ItemId, x.ItemId), IconKey = x.ItemId,
            DropChance = x.DropChance, MinQty = x.MinQty, MaxQty = x.MaxQty
        }));
    }

    // Đổi hay không thì so GIÁ TRỊ THẬT trên web với giá trị Unity sắp ghi, không so
    // UnityBaselineJson. Baseline từng bị ghi lệch (bản cũ ghi baseline nhưng không áp chỉ số), nên
    // record như axe_demon bị coi là "unchanged" vĩnh viễn dù web vẫn giữ số seed cũ.
    // Signature phải liệt kê đúng các field Apply() ghi — khác nhau ⇒ Apply() mới có tác dụng.
    private static string Signature(ItemEntity e) => JsonSerializer.Serialize(new object?[] {
        e.Name, e.Category, e.Description, e.MaxStack, e.IsKeyItem, e.ImageUrl,
        e.Modifiers.Select(x => new object?[] { x.Stat, x.Amount }) }, JsonOptions);

    private static string Signature(UnityItemImport d, string? currentImage) => JsonSerializer.Serialize(new object?[] {
        d.Name, d.Category, Clean(d.Description), d.MaxStack, d.IsKeyItem, d.ImageUrl ?? currentImage,
        (d.Modifiers ?? []).Select(x => new object?[] { x.Stat, x.Amount }) }, JsonOptions);

    private static string Signature(EnemyEntity e) => JsonSerializer.Serialize(new object?[] {
        e.Name, e.Tier, e.Hp, e.Ad, e.Ap, e.Def, e.Res, e.Poise, e.PoiseRecoveryTime,
        e.PatrolSpeed, e.ChaseSpeed, e.AttackSpeed, e.ExpReward, e.ImageUrl,
        e.LootTable.OrderBy(x => x.IconKey).Select(x => new object?[] { x.IconKey, x.DropChance, x.MinQty, x.MaxQty }) }, JsonOptions);

    private static string Signature(UnityEnemyImport d, string? currentImage) => JsonSerializer.Serialize(new object?[] {
        d.Name, d.Tier, d.Hp, d.Ad, d.Ap, d.Def, d.Res, d.Poise, d.PoiseRecoveryTime,
        d.PatrolSpeed, d.ChaseSpeed, d.AttackSpeed, d.ExpReward, d.ImageUrl ?? currentImage,
        (d.LootTable ?? []).OrderBy(x => x.ItemId).Select(x => new object?[] { x.ItemId, x.DropChance, x.MinQty, x.MaxQty }) }, JsonOptions);

    private static string? Clean(string? value) => value == null ? null : ContentSanitizer.Sanitize(value);
    private static async Task<(DateTime? max, int count)> VersionInfo(IQueryable<DateTime> query) { var count = await query.CountAsync(); return count == 0 ? (null, 0) : (await query.MaxAsync(x => (DateTime?)x), count); }
    private static string Version(DateTime? max, int count) => max is null ? "0" : $"{max:O}|{count}";
}
