using System.Text.Json;
using JobDesk.Services.Databricks;
using Xunit;

namespace JobDesk.Tests;

public class StatementExecutionRequestTests
{
    [Fact]
    public void Serializes_snake_case_warehouse_id()
    {
        var json = JsonSerializer.Serialize(new StatementExecutionRequest
        {
            WarehouseId = "2f980fb501d6a119",
            Statement = "SELECT 1",
            WaitTimeout = "20s"
        }, StatementJson.Options);
        Assert.Contains("\"warehouse_id\":\"2f980fb501d6a119\"", json);
        Assert.Contains("\"statement\":\"SELECT 1\"", json);
    }
}
