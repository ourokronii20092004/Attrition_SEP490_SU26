using System.Net;

namespace Identity.Service.Services;

/// <summary>
/// Renders the account emails as styled HTML with a plain-text fallback.
///
/// Email clients are a hostile rendering target: no external stylesheets, patchy flexbox/grid, and
/// Outlook still uses Word for layout. So everything here is a table with inline styles, which is
/// the one approach that survives Gmail, Outlook, and Apple Mail alike. Colours are hex literals
/// rather than the site's CSS custom properties, because custom properties don't resolve in mail.
///
/// Every caller passes user-supplied values (usernames), so <see cref="Text"/> HTML-escapes them —
/// an unescaped username would let markup into the message body.
/// </summary>
public static class EmailTemplate
{
    // Site palette, hard-coded because var(--color-*) does not exist in an email client.
    private const string Bg = "#0f1115";

    private const string Panel = "#171a21";
    private const string Border = "#272b35";
    private const string Fg = "#e8e6e3";
    private const string FgMuted = "#a8a6a1";
    private const string Accent = "#ff7a45";
    private const string AccentFg = "#1a0f0a";
    private const string Danger = "#ff4365";

    /// <summary>A paragraph of body copy. Escapes its input.</summary>
    public static string Text(string content) =>
        $"""<p style="margin:0 0 16px;font-size:15px;line-height:1.6;color:{Fg};">{WebUtility.HtmlEncode(content)}</p>""";

    /// <summary>Secondary, smaller copy — for caveats and expiry notes.</summary>
    public static string Muted(string content) =>
        $"""<p style="margin:0 0 16px;font-size:13px;line-height:1.6;color:{FgMuted};">{WebUtility.HtmlEncode(content)}</p>""";

    /// <summary>
    /// A call-to-action button. Also emits the raw URL beneath it, because a fair number of clients
    /// strip or mangle anchors and the recipient still needs a way to complete the action.
    /// </summary>
    public static string Button(string label, string url, bool danger = false)
    {
        var bg = danger ? Danger : Accent;
        var fg = danger ? "#ffffff" : AccentFg;
        return $"""
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 16px;">
              <tr><td style="border-radius:6px;background:{bg};">
                <a href="{WebUtility.HtmlEncode(url)}" style="display:inline-block;padding:12px 24px;font-size:15px;font-weight:600;color:{fg};text-decoration:none;border-radius:6px;">{WebUtility.HtmlEncode(label)}</a>
              </td></tr>
            </table>
            <p style="margin:0 0 20px;font-size:12px;line-height:1.5;color:{FgMuted};word-break:break-all;">
              Or paste this into your browser:<br><span style="color:{Accent};">{WebUtility.HtmlEncode(url)}</span>
            </p>
            """;
    }

    /// <summary>A warning callout, for "if this wasn't you" guidance.</summary>
    public static string Warning(string content) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:0 0 16px;">
          <tr><td style="padding:12px 14px;border-left:3px solid {Danger};background:rgba(255,67,101,0.08);border-radius:4px;">
            <p style="margin:0;font-size:14px;line-height:1.6;color:{Fg};">{WebUtility.HtmlEncode(content)}</p>
          </td></tr>
        </table>
        """;

    /// <summary>
    /// Wraps rendered blocks in the branded shell. <paramref name="preheader"/> is the snippet
    /// shown in the inbox list next to the subject; without one, clients scrape the first visible
    /// text, which is usually the greeting and tells the reader nothing.
    /// </summary>
    public static string Wrap(string heading, string preheader, params string[] blocks) => $"""
        <!DOCTYPE html>
        <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>{WebUtility.HtmlEncode(heading)}</title></head>
        <body style="margin:0;padding:0;background:{Bg};">
          <div style="display:none;max-height:0;overflow:hidden;opacity:0;">{WebUtility.HtmlEncode(preheader)}</div>
          <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:{Bg};padding:32px 16px;">
            <tr><td align="center">
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="max-width:560px;background:{Panel};border:1px solid {Border};border-radius:10px;">
                <tr><td style="padding:28px 32px 0;">
                  <p style="margin:0 0 4px;font-size:12px;font-weight:600;letter-spacing:2px;text-transform:uppercase;color:{Accent};">ATTRITION</p>
                  <h1 style="margin:0 0 20px;font-size:21px;line-height:1.3;font-weight:700;color:{Fg};">{WebUtility.HtmlEncode(heading)}</h1>
                </td></tr>
                <tr><td style="padding:0 32px 8px;">{string.Concat(blocks)}</td></tr>
                <tr><td style="padding:8px 32px 28px;border-top:1px solid {Border};">
                  <p style="margin:16px 0 0;font-size:12px;line-height:1.5;color:{FgMuted};">
                    This message was sent by Attrition because of activity on your account.
                    If you weren't expecting it, you can ignore it — no action is taken unless you click above.
                  </p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body></html>
        """;
}