using Attrition.API.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace Attrition.API.Data;

public static class SeedData
{
    public static async Task Initialize(AppDbContext context)
    {
        if (context.Users.Any()) return; // Already seeded

        // Seed admin (bypasses password strength — bootstrap only)
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin123",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "Admin",
            MustChangePassword = true,
            Bio = "Site Administrator"
        };
        context.Users.Add(admin);

        // Seed wiki categories
        var wikiCategories = new List<WikiCategory>
        {
            new() { Name = "Weapons", Slug = "weapons", Description = "Swords, bows, staffs, and everything in between", SortOrder = 1 },
            new() { Name = "Enemies", Slug = "enemies", Description = "Creatures and bosses lurking in every biome", SortOrder = 2 },
            new() { Name = "Biomes", Slug = "biomes", Description = "Explore the diverse environments of Attrition", SortOrder = 3 },
            new() { Name = "Abilities", Slug = "abilities", Description = "Active and passive abilities for your build", SortOrder = 4 },
            new() { Name = "Lore", Slug = "lore", Description = "The story and history of the world", SortOrder = 5 },
            new() { Name = "Multiplayer Mechanics", Slug = "multiplayer-mechanics", Description = "How co-op and PvP systems work", SortOrder = 6 },
            new() { Name = "Skills", Slug = "skills", Description = "Character skill trees and progression", SortOrder = 7 },
            new() { Name = "Puzzles", Slug = "puzzles", Description = "Environmental puzzles and solutions", SortOrder = 8 },
        };
        context.WikiCategories.AddRange(wikiCategories);

        // Seed forum categories
        var forumCategories = new List<ForumCategory>
        {
            new() { Name = "General Discussion", Slug = "general", Description = "Talk about anything Attrition", SortOrder = 1 },
            new() { Name = "Bug Reports", Slug = "bugs", Description = "Found a bug? Report it here", SortOrder = 2 },
            new() { Name = "Build Guides", Slug = "builds", Description = "Share your best builds", SortOrder = 3 },
            new() { Name = "Fan Art", Slug = "fan-art", Description = "Show off your creative work", SortOrder = 4 },
            new() { Name = "Multiplayer / LFG", Slug = "lfg", Description = "Find other players and team up", SortOrder = 5 },
            new() { Name = "Off-Topic", Slug = "off-topic", Description = "Everything else", SortOrder = 6 },
        };
        context.ForumCategories.AddRange(forumCategories);

        // Seed Game Data
        if (!context.Levels.Any())
        {
            var levels = new List<Level>();
            for (int i = 1; i <= 50; i++)
            {
                levels.Add(new Level { LevelNumber = i, ExpRequired = i * 100, HpGrowth = 10, AdGrowth = 2 });
            }
            context.Levels.AddRange(levels);
        }

        if (!context.Items.Any())
        {
            context.Consumables.Add(new Consumable { Name = "Health Potion", HpRestore = 50, ItemType = "consumable", Description = "Restores 50 HP" });
            context.Gears.Add(new Gear { Name = "Iron Sword", EquipSlot = "weapon", BonusAd = 10, ItemType = "gear" });
        }

        if (!context.Enemies.Any())
        {
            context.Enemies.Add(new Enemy { EnemyId = "skel_01", Name = "Skeleton", Tier = "normal", Hp = 100, Ad = 10, ExpReward = 50 });
        }

        // Seed Music Data
        if (!context.MusicAlbums.Any())
        {
            var album = new MusicAlbum { Title = "Attrition: Original Soundtrack", Slug = "attrition-ost", TrackCount = 1 };
            context.MusicAlbums.Add(album);
            context.SaveChanges();

            context.MusicTracks.Add(new MusicTrack
            {
                AlbumId = album.AlbumId,
                Title = "Friday Night",
                Slug = "friday-night",
                TrackNumber = 1,
                Duration = 210, // Assuming 3:30
                FilePath = "albums/attrition-ost/01-friday-night.mp3"
            });
        }

        await context.SaveChangesAsync();
    }
}
