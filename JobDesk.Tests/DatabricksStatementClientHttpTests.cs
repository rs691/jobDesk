using System.Net;
using JobDesk.Services.Databricks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobDesk.Tests;

public class DatabricksStatementClientHttpTests
{
    [Fact]
    public async Task ExecuteSelectAsync_sends_Bearer_pat_to_workspace_api()
    {
        var capture = new CaptureHandler();
        var http = new HttpClient(capture);
        var opts = new DatabricksOptions
        {
            Host = "dbc-dab8e394-216d.cloud.databricks.com",
            WarehouseId = "2f980fb501d6a119",
            AccessToken = "Bearer dapi-sample",
            AuthMode = DatabricksAuthMode.PatBearer,
            StatementTimeoutSeconds = 5,
            MaxPollAttempts = 1,
            PollIntervalMs = 1
        };
        var config = new ConfigurationBuilder().Build();
        var client = new DatabricksStatementClient(http, opts, config, NullLogger<DatabricksStatementClient>.Instance);
        var sql = AskAllowlistedSql.Build(AskIntent.WorkOrdersByStatus, new DatabricksOptions
        {
            Catalog = "workspace",
            Schema = "jobdesk_synthetic",
            AsOfDate = "2026-11-06"
        });

        await client.ExecuteSelectAsync(sql);

        Assert.NotNull(capture.Last);
        Assert.Equal(
            "https://dbc-dab8e394-216d.cloud.databricks.com/api/2.0/sql/statements/",
            capture.Last!.RequestUri!.ToString());
        Assert.Contains("Bearer dapi-sample", capture.Last.Headers.GetValues("Authorization"), StringComparer.Ordinal);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            const string json = """
                {
                  "statement_id": "stub",
                  "status": { "state": "SUCCEEDED" },
                  "manifest": { "schema": { "columns": [{ "name": "status" }, { "name": "orders" }] } },
                  "result": { "data_array": [["scheduled", "1"]] }
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
