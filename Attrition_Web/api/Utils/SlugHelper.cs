using System.Text.RegularExpressions;

namespace Attrition.API.Utils;

public static class SlugHelper
{
    public static string GenerateSlug(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // Convert to lowercase
        string slug = text.ToLowerInvariant();

        // Remove invalid characters
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");

        // Collapse multiple spaces into one and trim
        slug = Regex.Replace(slug, @"\s+", " ").Trim();

        // Hyphenate spaces
        slug = Regex.Replace(slug, @"\s", "-");

        // Trim duplicate hyphens
        slug = Regex.Replace(slug, @"-+", "-");

        return slug;
    }
}
