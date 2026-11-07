using System.Text.RegularExpressions;

namespace JobDesk.Services.Databricks;

/// <summary>
/// Server-side Databricks SQL warehouse settings for allowlisted read-only Ask queries.
/// Access tokens must come from environment variables only (never appsettings).
/// </summary>
public class DatabricksOptions
{
    public const string Section = "Databricks";

    /// <summary>When true, Ask tries allowlisted Databricks queries before the local SQLite snapshot.</summary>
    public bool Enabled { get; set; }

    /// <summary>Workspace host, e.g. dbc-dab8e394-216d.cloud.databricks.com (no scheme).</summary>
    public string Host { get; set; } = "";

    /// <summary>SQL warehouse ID for Statement Execution API.</summary>
    public string WarehouseId { get; set; } = "";

    /// <summary>Unity Catalog name containing schema <see cref="Schema"/>.</summary>
    public string Catalog { get; set; } = "";

    public string Schema { get; set; } = "jobdesk_synthetic";

    /// <summary>Fixed as-of date baked into allowlisted SQL (synthetic dataset).</summary>
    public string AsOfDate { get; set; } = "2026-11-06";

    public int MaxRows { get; set; } = 100;

    public int StatementTimeoutSeconds { get; set; } = 20;

    public int PollIntervalMs { get; set; } = 500;

    public int MaxPollAttempts { get; set; } = 40;

    /// <summary>Supported modes: <see cref="DatabricksAuthMode.PatBearer"/> only (no browser/cookie auth).</summary>
    public DatabricksAuthMode AuthMode { get; set; } = DatabricksAuthMode.PatBearer;

    /// <summary>Set at startup from DATABRICKS_TOKEN or Databricks__AccessToken (never appsettings).</summary>
    public string? AccessToken { get; set; }

    public bool IsConfigured =>
        Enabled
        && AuthMode == DatabricksAuthMode.PatBearer
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(WarehouseId)
        && !string.IsNullOrWhiteSpace(Catalog)
        && DatabricksPatLoader.Normalize(AccessToken) is not null;

    public static void ValidateIdentifier(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, @"^[A-Za-z0-9_]+$"))
            throw new ArgumentException($"Invalid {paramName}.", paramName);
    }

    public static void ValidateAsOfDate(string value)
    {
        if (!Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}$"))
            throw new ArgumentException("AsOfDate must be yyyy-MM-dd.");
    }
}
