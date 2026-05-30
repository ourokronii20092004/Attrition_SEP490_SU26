using Ganss.Xss;

namespace Forum.Service.Services;

/// <summary>
/// Server-side HTML sanitization for user-authored content (post/thread bodies). Defense-in-depth:
/// even if the frontend renders markdown/HTML, stored content can never carry script/event-handler
/// payloads. Strips dangerous tags/attributes while keeping basic formatting.
/// </summary>
public static class ContentSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    private static HtmlSanitizer BuildSanitizer()
    {
        var s = new HtmlSanitizer();
        s.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "strong", "em", "b", "i", "u", "s", "del", "ins",
            "h1", "h2", "h3", "h4", "h5", "h6",
            "ul", "ol", "li", "blockquote", "code", "pre", "hr",
            "a", "img", "table", "thead", "tbody", "tr", "th", "td", "span"
        }) s.AllowedTags.Add(tag);

        s.AllowedAttributes.Clear();
        foreach (var attr in new[] { "href", "src", "alt", "title", "class" })
            s.AllowedAttributes.Add(attr);

        s.AllowedSchemes.Clear();
        s.AllowedSchemes.Add("http");
        s.AllowedSchemes.Add("https");
        return s;
    }

    public static string Sanitize(string? content) =>
        string.IsNullOrWhiteSpace(content) ? content ?? string.Empty : Sanitizer.Sanitize(content);
}
