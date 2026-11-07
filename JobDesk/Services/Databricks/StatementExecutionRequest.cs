using System.Text.Json.Serialization;

namespace JobDesk.Services.Databricks;

/// <summary>POST /api/2.0/sql/statements/ body — property names match Databricks API (snake_case).</summary>
public sealed class StatementExecutionRequest
{
    [JsonPropertyName("warehouse_id")]
    public string WarehouseId { get; init; } = "";

    [JsonPropertyName("statement")]
    public string Statement { get; init; } = "";

    [JsonPropertyName("wait_timeout")]
    public string WaitTimeout { get; init; } = "20s";

    [JsonPropertyName("on_wait_timeout")]
    public string OnWaitTimeout { get; init; } = "CONTINUE";

    [JsonPropertyName("disposition")]
    public string Disposition { get; init; } = "INLINE";

    [JsonPropertyName("format")]
    public string Format { get; init; } = "JSON_ARRAY";
}
