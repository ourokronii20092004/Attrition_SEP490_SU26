using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BuildingBlocks.Contracts;

public static class SlugHelper
{
    public static string GenerateSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return FallbackSlug(text);

        // Normalize accented Latin (e.g. Vietnamese "Nhân vật" → "Nhan vat") by decomposing
        // to base char + combining marks, then dropping the marks.
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        // Vietnamese đ/Đ have no NFD decomposition, so map them explicitly.
        var slug = sb.ToString().Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd').Replace('Đ', 'd')
            .ToLowerInvariant();

        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", " ").Trim();
        slug = Regex.Replace(slug, @"\s", "-");
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');

        // Non-Latin scripts (CJK, Cyrillic, …) get stripped to empty above — fall back to a
        // stable hash so the slug is never empty and stays unique-ish per distinct title.
        return string.IsNullOrEmpty(slug) ? FallbackSlug(text) : slug;
    }

    private static string FallbackSlug(string text)
    {
        var basis = text ?? string.Empty;
        uint hash = 2166136261u;
        foreach (var ch in basis)
        {
            hash ^= ch;
            hash *= 16777619u;
        }
        return $"item-{hash:x8}";
    }

    /// <summary>
    /// Generates a slug and appends "-2", "-3", … until <paramref name="slugExists"/> reports
    /// the candidate is free. Closes the common (non-concurrent) duplicate-slug case; a DB unique
    /// index remains the authority for the concurrent race.
    /// </summary>
    public static async Task<string> GenerateUniqueSlugAsync(string text, Func<string, Task<bool>> slugExists)
    {
        var baseSlug = GenerateSlug(text);
        var candidate = baseSlug;
        var n = 2;
        while (await slugExists(candidate))
            candidate = $"{baseSlug}-{n++}";
        return candidate;
    }
}
