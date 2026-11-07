namespace JobDesk.Services.Databricks;

public static class DatabricksApiUrls
{
    public static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Databricks Host is not configured.");
        var h = host.Trim().TrimEnd('/');
        if (h.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            h = h[8..];
        if (h.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            h = h[7..];
        h = h.TrimEnd('/');
        if (h.Length == 0)
            throw new InvalidOperationException("Databricks Host is not configured.");
        return h;
    }

    public static Uri StatementExecution(string? host, string relativePath)
    {
        var h = NormalizeHost(host);
        var path = relativePath.TrimStart('/');
        return new Uri($"https://{h}/api/2.0/{path}", UriKind.Absolute);
    }

    public static string TryNormalizeHost(string? host)
    {
        try { return NormalizeHost(host); }
        catch { return "(invalid)"; }
    }
}
