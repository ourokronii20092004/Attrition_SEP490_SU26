using Forum.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Forum.Service.Data;

/// <summary>
/// Seeds a default set of forum categories on startup if none exist. Idempotent:
/// no-ops once any category is present, so it is safe to run on every boot.
/// </summary>
public static class CategorySeeder
{
    private static readonly (string Name, string Slug, string Description, int SortOrder)[] Defaults =
    {
        ("Announcements", "announcements", "Official news and updates from the Attrition team.", 0),
        ("General Discussion", "general", "Talk about anything Attrition with the community.", 1),
        ("Co-op & Matchmaking", "co-op", "Find a partner, organize runs, and team up for co-op.", 2),
        ("Guides & Strategy", "guides", "Share builds, boss tactics, and walkthroughs.", 3),
        ("Lore & Story", "lore", "Theories and discussion about Ren, Iris, and the world.", 4),
        ("Bug Reports", "bug-reports", "Report issues with the game or website.", 5),
        ("Feedback & Suggestions", "feedback", "Ideas to make Attrition better.", 6),
    };

    public static async Task SeedCategoriesAsync(ForumDbContext db, ILogger logger)
    {
        if (await db.ForumCategories.AnyAsync())
            return;

        db.ForumCategories.AddRange(Defaults.Select(c => new ForumCategory
        {
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            SortOrder = c.SortOrder,
        }));
        await db.SaveChangesAsync();
        logger.LogInformation("Category seed: created {Count} default forum categories.", Defaults.Length);
    }
}
