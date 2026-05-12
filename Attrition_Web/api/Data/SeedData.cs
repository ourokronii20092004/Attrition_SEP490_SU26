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

        await context.SaveChangesAsync();
    }
}