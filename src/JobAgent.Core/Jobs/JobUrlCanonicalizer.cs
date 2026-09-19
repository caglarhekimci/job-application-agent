namespace JobAgent.Core.Jobs;

public static class JobUrlCanonicalizer
{
    private static readonly HashSet<string> TrackingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "fbclid", "gclid", "mc_cid", "mc_eid", "msclkid", "ref", "referrer", "source", "trk", "trackingId"
    };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Job URL must be an absolute HTTP(S) URL.", nameof(value));

        var path = uri.AbsolutePath;
        if (path.Length > 1) path = path.TrimEnd('/');
        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(Parse)
            .Where(item => !item.Key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) &&
                !TrackingKeys.Contains(item.Key))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ThenBy(item => item.Value, StringComparer.Ordinal)
            .Select(item => Uri.EscapeDataString(item.Key) +
                (item.Value.Length == 0 ? string.Empty : "=" + Uri.EscapeDataString(item.Value)))
            .ToArray();
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return $"{uri.Scheme.ToLowerInvariant()}://{uri.IdnHost.ToLowerInvariant()}{port}{path}" +
            (query.Length == 0 ? string.Empty : "?" + string.Join("&", query));
    }

    public static string Origin(string? value)
    {
        var canonical = Normalize(value);
        if (canonical.Length == 0) return string.Empty;
        return new Uri(canonical).GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static KeyValuePair<string, string> Parse(string item)
    {
        var separator = item.IndexOf('=');
        return separator < 0
            ? new(Uri.UnescapeDataString(item), string.Empty)
            : new(Uri.UnescapeDataString(item[..separator]), Uri.UnescapeDataString(item[(separator + 1)..]));
    }
}
