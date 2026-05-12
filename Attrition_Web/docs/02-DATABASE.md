# 02 — Database Schema & Seed Data

This document covers the full PostgreSQL schema via EF Core, Redis caching patterns, and seed data.

---

## EF Core DbContext

### `api/Data/AppDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Attrition.API.Models;

namespace Attrition.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<WikiCategory> WikiCategories => Set<WikiCategory>();
    public DbSet<WikiArticle> WikiArticles => Set<WikiArticle>();
    public DbSet<WikiRevision> WikiRevisions => Set<WikiRevision>();
    public DbSet<WikiContribution> WikiContributions => Set<WikiContribution>();
    public DbSet<ForumCategory> ForumCategories => Set<ForumCategory>();
    public DbSet<ForumThread> ForumThreads => Set<ForumThread>();
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<ForumReaction> ForumReactions => Set<ForumReaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Role).HasDefaultValue("User");
            e.Property(u => u.JoinedAt).HasDefaultValueSql("NOW()");
            e.Property(u => u.IsBanned).HasDefaultValue(false);
            e.Property(u => u.MustChangePassword).HasDefaultValue(false);
        });

        // Wiki
        modelBuilder.Entity<WikiCategory>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
        });

        modelBuilder.Entity<WikiArticle>(e =>
        {
            e.HasIndex(a => a.Slug).IsUnique();
            e.HasOne<WikiCategory>().WithMany().HasForeignKey(a => a.CategoryId);
            e.HasOne<User>().WithMany().HasForeignKey(a => a.CreatedById).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<User>().WithMany().HasForeignKey(a => a.LastEditedById).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WikiRevision>(e =>
        {
            e.HasOne<WikiArticle>().WithMany().HasForeignKey(r => r.ArticleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.EditedById).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WikiContribution>(e =>
        {
            e.HasOne<WikiArticle>().WithMany().HasForeignKey(c => c.ArticleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(c => c.ContributorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(c => c.ReviewedById).OnDelete(DeleteBehavior.SetNull);
            e.Property(c => c.Status).HasDefaultValue("Pending");
        });

        // Forum
        modelBuilder.Entity<ForumCategory>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
        });

        modelBuilder.Entity<ForumThread>(e =>
        {
            e.HasOne<ForumCategory>().WithMany().HasForeignKey(t => t.CategoryId);
            e.HasOne<User>().WithMany().HasForeignKey(t => t.AuthorId).OnDelete(DeleteBehavior.Cascade);
            e.Property(t => t.IsPinned).HasDefaultValue(false);
            e.Property(t => t.IsLocked).HasDefaultValue(false);
        });

        modelBuilder.Entity<ForumPost>(e =>
        {
            e.HasOne<ForumThread>().WithMany().HasForeignKey(p => p.ThreadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(p => p.AuthorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ForumReaction>(e =>
        {
            e.HasOne<ForumPost>().WithMany().HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.PostId, r.UserId, r.ReactionType }).IsUnique();
        });
    }
}
```

---

## Entity Models

All models go in `api/Models/`. Use `Guid` for IDs on user-facing entities.

### `User.cs`
```csharp
namespace Attrition.API.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";           // "User" or "Admin"
    public string? AvatarPath { get; set; }
    public string? Bio { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public int PostCount { get; set; } = 0;
    public int ContributionCount { get; set; } = 0;
    public bool IsBanned { get; set; } = false;
    public bool MustChangePassword { get; set; } = false;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
}
```

### `WikiCategory.cs`
```csharp
namespace Attrition.API.Models;

public class WikiCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; } = 0;
}
```

### `WikiArticle.cs`
```csharp
namespace Attrition.API.Models;

public class WikiArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string Content { get; set; } = string.Empty;   // Markdown
    public Guid? CreatedById { get; set; }
    public Guid? LastEditedById { get; set; }
    public string Status { get; set; } = "Published";     // "Draft" or "Published"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### `WikiRevision.cs`
```csharp
namespace Attrition.API.Models;

public class WikiRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? EditedById { get; set; }
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
    public string? ChangeNote { get; set; }
}
```

### `WikiContribution.cs`
```csharp
namespace Attrition.API.Models;

public class WikiContribution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ArticleId { get; set; }
    public Guid ContributorId { get; set; }
    public string SuggestedContent { get; set; } = string.Empty;
    public string? ChangeNote { get; set; }
    public string Status { get; set; } = "Pending";    // Pending, Approved, Rejected
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }
}
```

### `ForumCategory.cs`
```csharp
namespace Attrition.API.Models;

public class ForumCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}
```

### `ForumThread.cs`
```csharp
namespace Attrition.API.Models;

public class ForumThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public bool IsPinned { get; set; } = false;
    public bool IsLocked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastReplyAt { get; set; } = DateTime.UtcNow;
    public int ReplyCount { get; set; } = 0;
}
```

### `ForumPost.cs`
```csharp
namespace Attrition.API.Models;

public class ForumPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;   // Markdown
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
```

### `ForumReaction.cs`
```csharp
namespace Attrition.API.Models;

public class ForumReaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public string ReactionType { get; set; } = "like";    // like, dislike
}
```

---

## Seed Data

### `api/Data/SeedData.cs`

```csharp
using Attrition.API.Models;
using BCrypt.Net;

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
```

Call from `Program.cs` after building the app:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.Initialize(db);
}
```

---

## Migrations

After creating all models and the DbContext, generate the initial migration:

```bash
cd api
dotnet ef migrations add InitialCreate
```

Migrations run automatically on startup via `db.Database.MigrateAsync()` in `Program.cs`.

---

## Redis Cache Patterns

| Key Pattern | Purpose | TTL |
|---|---|---|
| `wiki:article:{slug}` | Cached rendered wiki article | 10 min |
| `wiki:categories` | Cached category list with article counts | 30 min |
| `forum:latest:{categorySlug}` | Latest threads in a category | 2 min |
| `user:session:{userId}` | Track active refresh tokens | 7 days |
| `ratelimit:{ip}:{endpoint}` | Rate limiting counters | 1 min |

Invalidate relevant cache keys on any write operation (create/update/delete).

---

## Next Step

Proceed to `03-BACKEND-API.md` for the full API implementation.
