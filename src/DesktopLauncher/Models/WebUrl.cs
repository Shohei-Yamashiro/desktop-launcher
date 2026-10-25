namespace DesktopLauncher.Models;

/// <summary>http / https addresses, the only URLs the launcher registers.</summary>
public static class WebUrl
{
    public static bool IsWebUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Accepts an http / https URL, or a host name without a scheme ("example.com/path"), which gets "https://".
    /// </summary>
    public static bool TryNormalize(string? text, out string url)
    {
        url = "";
        text = text?.Trim() ?? "";
        if (text.Length == 0 || text.Any(char.IsWhiteSpace)) return false;

        var hasScheme = IsWebUrl(text);
        if (!hasScheme && text.Contains("://", StringComparison.Ordinal)) return false;
        var candidate = hasScheme ? text : "https://" + text;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || !LooksLikeHost(uri.Host)) return false;
        // "mailto:a@example.com" would otherwise become a URL with user info.
        if (!hasScheme && uri.UserInfo.Length > 0) return false;

        url = candidate;
        return true;
    }

    /// <summary>
    /// Copied text counts as a URL only when it starts with http:// or https://, so that a copied host
    /// name such as a mail server ("imap.example.com") is not taken for a web page.
    /// </summary>
    public static bool TryParseCopied(string? text, out string url)
    {
        url = "";
        return IsWebUrl(text?.Trim() ?? "") && TryNormalize(text, out url);
    }

    public static string DefaultName(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Length > 0 ? uri.Host : url;

    /// <summary>Rejects things like "https://memo" made from a plain word.</summary>
    private static bool LooksLikeHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.'));
}
