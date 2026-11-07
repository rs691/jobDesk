using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JobDesk.Services.Databricks;

public class DatabricksStatementClient : IDatabricksStatementClient
{
    private readonly HttpClient _http;
    private readonly DatabricksOptions _opts;
    private readonly ILogger<DatabricksStatementClient> _log;

    public DatabricksStatementClient(HttpClient http, DatabricksOptions opts, ILogger<DatabricksStatementClient> log)
    {
        _http = http;
        _opts = opts;
        _log = log;
    }

    public async Task<DatabricksQueryResult> ExecuteSelectAsync(string sql, CancellationToken ct = default)
    {
        AskAllowlistedSql.EnsureReadOnlySelect(sql);

        if (_opts.AuthMode != DatabricksAuthMode.PatBearer)
            throw new InvalidOperationException("Databricks auth mode is not supported for unattended API calls.");
        if (string.IsNullOrWhiteSpace(_opts.AccessToken))
            throw new InvalidOperationException("Databricks PAT (Bearer token) is not configured.");

        var waitTimeout = Math.Clamp(_opts.StatementTimeoutSeconds, 5, 50);
        var body = new
        {
            warehouse_id = _opts.WarehouseId,
            statement = sql,
            wait_timeout = $"{waitTimeout}s",
            on_wait_timeout = "CONTINUE",
            disposition = "INLINE",
            format = "JSON_ARRAY"
        };

        using var create = new HttpRequestMessage(HttpMethod.Post, "sql/statements/")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.AccessToken);

        using var createResp = await _http.SendAsync(create, ct);
        var createJson = await createResp.Content.ReadAsStringAsync(ct);
        if (!createResp.IsSuccessStatusCode)
        {
            _log.LogWarning("Databricks statement create failed with {Status}", createResp.StatusCode);
            throw new InvalidOperationException($"Databricks statement failed ({(int)createResp.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(createJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("statement_id", out var idProp))
            throw new InvalidOperationException("Databricks response missing statement_id.");

        var statementId = idProp.GetString() ?? throw new InvalidOperationException("Invalid statement_id.");

        var state = root.TryGetProperty("status", out var st) && st.TryGetProperty("state", out var stName)
            ? stName.GetString()
            : "PENDING";

        var attempts = 0;
        JsonElement resultRoot = root;
        while (state is "PENDING" or "RUNNING" && attempts < _opts.MaxPollAttempts)
        {
            await Task.Delay(_opts.PollIntervalMs, ct);
            using var poll = new HttpRequestMessage(HttpMethod.Get, $"sql/statements/{statementId}");
            poll.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.AccessToken);
            using var pollResp = await _http.SendAsync(poll, ct);
            var pollJson = await pollResp.Content.ReadAsStringAsync(ct);
            if (!pollResp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Databricks poll failed ({(int)pollResp.StatusCode}).");

            using var pollDoc = JsonDocument.Parse(pollJson);
            resultRoot = pollDoc.RootElement.Clone();
            state = resultRoot.TryGetProperty("status", out var pst) && pst.TryGetProperty("state", out var psn)
                ? psn.GetString()
                : null;
            attempts++;
        }

        if (state is not "SUCCEEDED")
        {
            var msg = resultRoot.TryGetProperty("status", out var statusEl) && statusEl.TryGetProperty("error", out var err)
                ? err.GetRawText()
                : state ?? "unknown";
            throw new InvalidOperationException($"Databricks query did not succeed ({msg}).");
        }

        return ParseResult(resultRoot);
    }

    public static DatabricksQueryResult ParseResult(JsonElement root)
    {
        var columns = new List<string>();
        if (root.TryGetProperty("manifest", out var manifest)
            && manifest.TryGetProperty("schema", out var schema)
            && schema.TryGetProperty("columns", out var cols)
            && cols.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cols.EnumerateArray())
            {
                if (c.TryGetProperty("name", out var name))
                    columns.Add(name.GetString() ?? "?");
            }
        }

        var rows = new List<IReadOnlyList<string?>>();
        if (root.TryGetProperty("result", out var result))
        {
            if (result.TryGetProperty("data_array", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in data.EnumerateArray())
                {
                    var cells = new List<string?>();
                    foreach (var cell in row.EnumerateArray())
                        cells.Add(cell.ValueKind == JsonValueKind.Null ? null : cell.ToString());
                    rows.Add(cells);
                }
            }
        }

        return new DatabricksQueryResult(columns, rows);
    }
}
