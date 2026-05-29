using System.Text.RegularExpressions;

namespace BuildingBlocks.Contracts;

public static class SlugHelper
{
    public static string GenerateSlug(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var slug = text.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", " ").Trim();
        slug = Regex.Replace(slug, @"\s", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        return slug;
    }
}
