using Wiki.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Wiki.Service.Data;

/// <summary>
/// Seeds a default set of wiki categories on startup if none exist. Idempotent:
/// no-ops once any category is present, so it is safe to run on every boot.
/// </summary>
public static class CategorySeeder
{
    private static readonly (string Name, string Slug, string Description, int SortOrder)[] Defaults =
    {
        ("Lore", "lore", "The story, history, and mythology of the Attrition world.", 0),
        ("Characters", "characters", "Ren, Iris, and everyone bound to the dying shard.", 1),
        ("Bestiary", "bestiary", "Enemies, bosses, and the Corruption that animates them.", 2),
        ("Mechanics", "mechanics", "Combat, stats, status effects, and how systems work.", 3),
        ("Items & Gear", "items", "Weapons, consumables, and loot.", 4),
        ("Co-op", "co-op", "Two-player mechanics: revive, scaling, and joint strategies.", 5),
        ("Walkthroughs", "walkthroughs", "Area guides and boss strategies.", 6),
    };

    public static async Task SeedCategoriesAsync(WikiDbContext db, ILogger logger)
    {
        if (await db.WikiCategories.AnyAsync())
            return;

        db.WikiCategories.AddRange(Defaults.Select(c => new WikiCategory
        {
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            SortOrder = c.SortOrder,
        }));
        await db.SaveChangesAsync();
        logger.LogInformation("Wiki category seed: created {Count} default categories.", Defaults.Length);
    }
}
