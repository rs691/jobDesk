using System.Text.Json;
using JobDesk.Services.Databricks;
using Xunit;

namespace JobDesk.Tests;

public class DatabricksStatementClientTests
{
    [Fact]
    public void ParseResult_reads_columns_and_rows()
    {
        const string json = """
            {
              "status": { "state": "SUCCEEDED" },
              "manifest": { "schema": { "columns": [
                { "name": "status" },
                { "name": "orders" }
              ]}},
              "result": { "data_array": [
                ["open", 3],
                ["completed", 10]
              ]}
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var result = DatabricksStatementClient.ParseResult(doc.RootElement);

        Assert.Equal(["status", "orders"], result.Columns);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("open", result.Rows[0][0]);
    }
}
