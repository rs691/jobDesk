namespace JobDesk.Services.Databricks;

public sealed record DatabricksQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows);
