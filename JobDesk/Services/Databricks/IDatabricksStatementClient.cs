namespace JobDesk.Services.Databricks;

public interface IDatabricksStatementClient
{
    /// <summary>Runs a pre-built allowlisted SELECT. SQL must pass <see cref="AskAllowlistedSql.EnsureReadOnlySelect"/>.</summary>
    Task<DatabricksQueryResult> ExecuteSelectAsync(string sql, CancellationToken ct = default);
}
