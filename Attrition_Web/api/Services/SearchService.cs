using Attrition.API.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Attrition.API.Services;

public class SearchService
{
    private readonly AppDbContext _db;

    public SearchService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<object> GlobalSearchAsync(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return new { success = true, data = new { } };

        q = q.Trim();
        var isWiki = false;
        var isUser = false;
        var isPost = false;

        // Parse tags like wiki:"enemy" or wiki:enemy
        var tagMatch = Regex.Match(q, @"^(wiki|user|post):\s*""?([^""]+)""?", RegexOptions.IgnoreCase);
        if (tagMatch.Success)
        {
            var tag = tagMatch.Groups[1].Value.ToLower();
            q = tagMatch.Groups[2].Value;
            if (tag == "wiki") isWiki = true;
            else if (tag == "user") isUser = true;
            else if (tag == "post") isPost = true;
        }

        var searchAll = !isWiki && !isUser && !isPost;

        var result = new Dictionary<string, object>();

        if (searchAll || isWiki)
        {
            result["wiki"] = await _db.WikiArticles
                .Where(a => a.Title.Contains(q) || a.Slug.Contains(q))
                .Take(5)
                .Select(a => new { a.Id, a.Title, a.Slug })
                .ToListAsync();
        }

        if (searchAll || isUser)
        {
            result["users"] = await _db.Users
                .Where(u => u.Username.Contains(q) || u.DisplayName!.Contains(q))
                .Take(5)
                .Select(u => new { u.Id, u.Username, u.DisplayName, u.AvatarPath, u.GoogleAvatarUrl, u.Role })
                .ToListAsync();
        }

        if (searchAll || isPost)
        {
            result["posts"] = await _db.ForumPosts
                .Where(p => !p.IsRemoved)
                .Join(_db.ForumThreads, p => p.ThreadId, t => t.Id, (p, t) => new { Post = p, Thread = t })
                .Where(x => x.Post.Content.Contains(q) || x.Thread.Title.Contains(q))
                .Take(5)
                .Select(x => new { x.Post.Id, x.Post.ThreadId, x.Post.Content, x.Thread.Title, x.Post.CreatedAt })
                .ToListAsync();
        }

        return new { success = true, data = result };
    }
}
