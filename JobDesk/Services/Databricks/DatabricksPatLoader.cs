using Microsoft.Extensions.Configuration;

namespace JobDesk.Services.Databricks;

/// <summary>Resolves and normalizes PAT for Bearer auth (no logging of secrets).</summary>
public static class DatabricksPatLoader
{
    /// <summary>
    /// First non-empty candidate after normalization.
    /// Order: <c>DATABRICKS_TOKEN</c>, <c>Databricks__AccessToken</c> env, then <c>Databricks:AccessToken</c> configuration (same as <c>Databricks__AccessToken</c> on Lambda).
    /// Treats whitespace-only and empty strings as missing so a blank env var does not block a valid PAT.
    /// </summary>
    public static string? ResolveFromEnvironmentAndConfiguration(IConfiguration? configuration = null)
    {
        return Resolve(
            Environment.GetEnvironmentVariable("DATABRICKS_TOKEN"),
            Environment.GetEnvironmentVariable("Databricks__AccessToken"),
            configuration?["Databricks:AccessToken"]);
    }

    public static string? Resolve(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            var normalized = Normalize(c);
            if (normalized is not null)
                return normalized;
        }
        return null;
    }

    /// <summary>Trim; strip a single leading <c>Bearer</c> prefix if pasted into the token field.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var t = raw.Trim();
        if (t.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            t = t[7..].Trim();
        if (t.StartsWith("Bearer\t", StringComparison.OrdinalIgnoreCase))
            t = t[7..].Trim();
        return t.Length == 0 ? null : t;
    }
}
